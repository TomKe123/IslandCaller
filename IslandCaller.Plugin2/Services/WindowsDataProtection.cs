using System.ComponentModel;
using System.Runtime.InteropServices;

namespace IslandCaller.Services;

internal static class WindowsDataProtection
{
    private const int CryptProtectUiForbidden = 0x1;

    public static byte[] Protect(byte[] plainBytes, byte[]? entropy = null)
    {
        return Transform(plainBytes, entropy, protect: true);
    }

    public static byte[] Unprotect(byte[] cipherBytes, byte[]? entropy = null)
    {
        return Transform(cipherBytes, entropy, protect: false);
    }

    private static byte[] Transform(byte[] input, byte[]? entropy, bool protect)
    {
        DATA_BLOB inputBlob = CreateBlob(input);
        DATA_BLOB entropyBlob = CreateBlob(entropy);
        DATA_BLOB outputBlob = default;

        try
        {
            bool ok = protect
                ? CryptProtectData(ref inputBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref outputBlob)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref outputBlob);

            if (!ok)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            byte[] result = new byte[outputBlob.cbData];
            if (outputBlob.cbData > 0)
            {
                Marshal.Copy(outputBlob.pbData, result, 0, outputBlob.cbData);
            }

            return result;
        }
        finally
        {
            FreeInputBlob(inputBlob);
            FreeInputBlob(entropyBlob);
            FreeOutputBlob(outputBlob);
        }
    }

    private static DATA_BLOB CreateBlob(byte[]? data)
    {
        if (data == null || data.Length == 0)
        {
            return new DATA_BLOB();
        }

        IntPtr pointer = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, pointer, data.Length);
        return new DATA_BLOB
        {
            cbData = data.Length,
            pbData = pointer
        };
    }

    private static void FreeInputBlob(DATA_BLOB blob)
    {
        if (blob.pbData == IntPtr.Zero)
        {
            return;
        }

        if (blob.cbData > 0)
        {
            Span<byte> zero = stackalloc byte[Math.Min(blob.cbData, 256)];
            int remaining = blob.cbData;
            int offset = 0;
            while (remaining > 0)
            {
                int blockLength = Math.Min(remaining, zero.Length);
                zero[..blockLength].Clear();
                Marshal.Copy(zero[..blockLength].ToArray(), 0, blob.pbData + offset, blockLength);
                remaining -= blockLength;
                offset += blockLength;
            }
        }

        Marshal.FreeHGlobal(blob.pbData);
    }

    private static void FreeOutputBlob(DATA_BLOB blob)
    {
        if (blob.pbData == IntPtr.Zero)
        {
            return;
        }

        LocalFree(blob.pbData);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
