using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services;

public readonly record struct UsbAuthSnapshot(
    bool IsEnabled,
    bool ProtectionActive,
    bool IsVerified,
    string Summary,
    string Detail,
    string VerifiedDriveRoot);

public readonly record struct UsbDriveInfo(
    string RootPath,
    string DisplayName,
    bool HasAuthorizationFile)
{
    public string AuthFilePath => Path.Combine(RootPath, UsbAuthService.NormalizeAuthFileName(Settings.Instance.UsbAuth.AuthFileName));
}

internal sealed record UsbAuthPayload(
    int Version,
    string PluginId,
    string KeyFingerprint,
    string VolumeSerialNumber,
    string FileName,
    string IssuedAtUtc);

internal sealed record UsbAuthEnvelope(
    int Version,
    string PayloadBase64,
    string SignatureBase64);

public sealed class UsbAuthService
{
    private const string DefaultAuthFileName = "IslandCaller.auth";
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<UsbAuthService> _logger;
    private readonly object _sync = new();
    private UsbAuthSnapshot _snapshot = new(false, false, true, "未启用", "U盘验证未启用，当前配置可直接编辑。", string.Empty);
    private IReadOnlyList<UsbDriveInfo> _drives = [];

    public event EventHandler<UsbAuthSnapshot>? StatusChanged;
    public event EventHandler<IReadOnlyList<UsbDriveInfo>>? DrivesChanged;

    public UsbAuthService(ILogger<UsbAuthService> logger)
    {
        _logger = logger;
        try
        {
            RefreshState();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "初始化U盘验证状态时发生异常");
        }

        _ = Task.Run(MonitorAsync);
    }

    public UsbAuthSnapshot RefreshStatus(bool forceRefresh = false)
    {
        if (forceRefresh)
        {
            RefreshState();
        }

        lock (_sync)
        {
            return _snapshot;
        }
    }

    public IReadOnlyList<UsbDriveInfo> GetRemovableDrives(bool forceRefresh = false)
    {
        if (forceRefresh)
        {
            RefreshState();
        }

        lock (_sync)
        {
            return _drives;
        }
    }

    public bool CanUseGachaMode()
    {
        if (!Settings.Instance.UsbAuth.Enabled)
        {
            return true;
        }

        if (!SettingsWriteGate.IsProtectionActive())
        {
            return false;
        }

        return RefreshStatus().IsVerified;
    }

    public bool CanModifyProtectedSettings()
    {
        if (!SettingsWriteGate.IsProtectionActive())
        {
            return true;
        }

        return RefreshStatus().IsVerified;
    }

    public static string NormalizeAuthFileName(string? rawFileName)
    {
        string fileName = Path.GetFileName((rawFileName ?? string.Empty).Trim());
        return string.IsNullOrWhiteSpace(fileName) ? DefaultAuthFileName : fileName;
    }

    public static string GetPublicKeyFingerprint(string? publicKey)
    {
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            return "未生成";
        }

        byte[] publicKeyBytes = Convert.FromBase64String(publicKey);
        byte[] hash = SHA256.HashData(publicKeyBytes);
        return Convert.ToHexString(hash[..8]);
    }

    internal static bool TryGetVolumeSerialNumber(string rootPath, out string serialNumber)
    {
        serialNumber = string.Empty;
        string normalizedRoot = Path.GetPathRoot(rootPath) ?? rootPath;
        if (!GetVolumeInformation(
                normalizedRoot,
                null,
                0,
                out uint volumeSerialNumber,
                out _,
                out _,
                null,
                0))
        {
            return false;
        }

        serialNumber = volumeSerialNumber.ToString("X8");
        return true;
    }

    private async Task MonitorAsync()
    {
        while (true)
        {
            try
            {
                RefreshState();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "后台刷新U盘验证状态时发生异常");
            }

            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
    }

    private void RefreshState()
    {
        var drives = EnumerateDrives();
        var snapshot = Evaluate(drives);

        UsbAuthSnapshot? snapshotChanged = null;
        IReadOnlyList<UsbDriveInfo>? drivesChanged = null;

        lock (_sync)
        {
            if (!_drives.SequenceEqual(drives))
            {
                _drives = drives;
                drivesChanged = _drives;
            }

            if (!_snapshot.Equals(snapshot))
            {
                _snapshot = snapshot;
                snapshotChanged = _snapshot;
            }
        }

        if (drivesChanged != null)
        {
            _logger.LogInformation("U盘列表已更新，共检测到 {DriveCount} 个候选盘: {Drives}",
                drivesChanged.Count,
                drivesChanged.Count == 0 ? "无" : string.Join(", ", drivesChanged.Select(x => $"{x.DisplayName} [{x.RootPath}]")));
            DrivesChanged?.Invoke(this, drivesChanged);
        }

        if (snapshotChanged.HasValue)
        {
            _logger.LogInformation("U盘验证状态变化: Summary={Summary}, Verified={Verified}, Drive={Drive}, Detail={Detail}",
                snapshotChanged.Value.Summary,
                snapshotChanged.Value.IsVerified,
                string.IsNullOrWhiteSpace(snapshotChanged.Value.VerifiedDriveRoot) ? "无" : snapshotChanged.Value.VerifiedDriveRoot,
                snapshotChanged.Value.Detail);
            StatusChanged?.Invoke(this, snapshotChanged.Value);
        }
    }

    private IReadOnlyList<UsbDriveInfo> EnumerateDrives()
    {
        List<UsbDriveInfo> drives = [];
        HashSet<string> usbDriveRoots = GetUsbDriveRoots();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                _logger.LogDebug("跳过未就绪盘符: Name={Name}, Type={Type}", drive.Name, drive.DriveType);
                continue;
            }

            string rootPath = drive.RootDirectory.FullName;
            bool isSupportedDriveType = drive.DriveType == DriveType.Removable
                || (drive.DriveType == DriveType.Fixed && usbDriveRoots.Contains(rootPath));
            if (!isSupportedDriveType)
            {
                _logger.LogDebug("跳过非候选盘符: Name={Name}, Root={Root}, Type={Type}", drive.Name, rootPath, drive.DriveType);
                continue;
            }

            string displayName = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                ? drive.Name
                : $"{drive.Name} ({drive.VolumeLabel})";
            string authFilePath = Path.Combine(rootPath, NormalizeAuthFileName(Settings.Instance.UsbAuth.AuthFileName));

            drives.Add(new UsbDriveInfo(
                rootPath,
                displayName,
                File.Exists(authFilePath)));

            _logger.LogDebug("识别到U盘候选: Name={Name}, Root={Root}, Type={Type}, HasAuthFile={HasAuthFile}",
                displayName,
                rootPath,
                drive.DriveType,
                File.Exists(authFilePath));
        }

        return drives
            .OrderBy(x => x.RootPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private HashSet<string> GetUsbDriveRoots()
    {
        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows())
        {
            return roots;
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
            {
                continue;
            }

            if (IsUsbFixedDrive(drive.RootDirectory.FullName))
            {
                roots.Add(drive.RootDirectory.FullName);
                _logger.LogDebug("固定盘识别为USB设备: Root={Root}", drive.RootDirectory.FullName);
            }
        }

        return roots;
    }

    private bool IsUsbFixedDrive(string rootPath)
    {
        string volumePath = BuildVolumeDevicePath(rootPath);
        SafeFileHandle? handle = null;

        try
        {
            handle = CreateFile(
                volumePath,
                0,
                FileShare.ReadWrite | FileShare.Delete,
                IntPtr.Zero,
                FileMode.Open,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                _logger.LogDebug("打开卷句柄失败，无法判断是否为USB固定盘: Root={Root}, Error={Error}", rootPath, Marshal.GetLastWin32Error());
                return false;
            }

            STORAGE_PROPERTY_QUERY query = new()
            {
                PropertyId = 0,
                QueryType = 0,
                AdditionalParameters = 0
            };

            int querySize = Marshal.SizeOf<STORAGE_PROPERTY_QUERY>();
            int outputSize = Marshal.SizeOf<STORAGE_DEVICE_DESCRIPTOR>();
            IntPtr queryBuffer = Marshal.AllocHGlobal(querySize);
            IntPtr outputBuffer = Marshal.AllocHGlobal(outputSize);

            try
            {
                Marshal.StructureToPtr(query, queryBuffer, false);

                bool success = DeviceIoControl(
                    handle,
                    IoctlStorageQueryProperty,
                    queryBuffer,
                    querySize,
                    outputBuffer,
                    outputSize,
                    out int bytesReturned,
                    IntPtr.Zero);

                if (!success || bytesReturned < Marshal.SizeOf<STORAGE_DEVICE_DESCRIPTOR>())
                {
                    _logger.LogDebug("查询存储总线类型失败: Root={Root}, Success={Success}, BytesReturned={BytesReturned}, Error={Error}",
                        rootPath, success, bytesReturned, Marshal.GetLastWin32Error());
                    return false;
                }

                STORAGE_DEVICE_DESCRIPTOR descriptor = Marshal.PtrToStructure<STORAGE_DEVICE_DESCRIPTOR>(outputBuffer);
                _logger.LogDebug("固定盘总线类型查询结果: Root={Root}, BusType={BusType}", rootPath, descriptor.BusType);
                return descriptor.BusType == StorageBusType.BusTypeUsb;
            }
            finally
            {
                Marshal.FreeHGlobal(queryBuffer);
                Marshal.FreeHGlobal(outputBuffer);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "判断固定盘是否为USB设备时发生异常: Root={Root}", rootPath);
            return false;
        }
        finally
        {
            handle?.Dispose();
        }
    }

    private static string BuildVolumeDevicePath(string rootPath)
    {
        string normalizedRoot = Path.GetPathRoot(rootPath) ?? rootPath;
        string driveLetter = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return $@"\\.\\{driveLetter}";
    }

    private UsbAuthSnapshot Evaluate(IReadOnlyList<UsbDriveInfo> drives)
    {
        bool enabled = Settings.Instance.UsbAuth.Enabled;
        string publicKey = Settings.Instance.UsbAuth.PublicKey ?? string.Empty;
        string normalizedFileName = NormalizeAuthFileName(Settings.Instance.UsbAuth.AuthFileName);
        bool hasKeyMaterial = !string.IsNullOrWhiteSpace(publicKey);
        bool legacyConfigured = Settings.Instance.Gacha.RequireUsbAuth || !string.IsNullOrWhiteSpace(Settings.Instance.Gacha.UsbAuthToken);

        if (!enabled)
        {
            if (!hasKeyMaterial)
            {
                if (legacyConfigured)
                {
                    return new UsbAuthSnapshot(
                        false,
                        false,
                        true,
                        "待迁移",
                        "检测到旧版口令式U盘验证配置。请在“U盘验证”页面重新写入授权U盘后再启用新方案。",
                        string.Empty);
                }

                return new UsbAuthSnapshot(
                    false,
                    false,
                    true,
                    "未启用",
                    "U盘验证未启用。插入U盘后可在独立设置页写入授权文件并开启保护。",
                    string.Empty);
            }

            return new UsbAuthSnapshot(
                false,
                false,
                true,
                "未启用",
                $"密钥对已准备就绪。插入U盘后可写入 {normalizedFileName} 并启用验证。",
                string.Empty);
        }

        if (!hasKeyMaterial)
        {
            return new UsbAuthSnapshot(
                true,
                false,
                false,
                "未初始化",
                "U盘验证已打开，但尚未生成公钥。请到“U盘验证”页面重新写入授权U盘。",
                string.Empty);
        }

        if (drives.Count == 0)
        {
            return new UsbAuthSnapshot(
                true,
                true,
                false,
                "未检测到U盘",
                $"未检测到可移动磁盘。请插入已授权U盘，且根目录包含 {normalizedFileName}。",
                string.Empty);
        }

        string firstFailureDetail = $"未找到授权文件 {normalizedFileName}。";
        foreach (var drive in drives)
        {
            if (!drive.HasAuthorizationFile)
            {
                continue;
            }

            if (TryVerifyDrive(drive, publicKey, out string failureDetail))
            {
                return new UsbAuthSnapshot(
                    true,
                    true,
                    true,
                    "已通过",
                    $"已通过U盘验证：{drive.DisplayName}",
                    drive.RootPath);
            }

            firstFailureDetail = failureDetail;
        }

        return new UsbAuthSnapshot(
            true,
            true,
            false,
            "未通过",
            firstFailureDetail,
            string.Empty);
    }

    private bool TryVerifyDrive(UsbDriveInfo drive, string publicKey, out string failureDetail)
    {
        failureDetail = $"未找到授权文件：{drive.AuthFilePath}";
        try
        {
            if (!File.Exists(drive.AuthFilePath))
            {
                return false;
            }

            byte[] protectedBytes = File.ReadAllBytes(drive.AuthFilePath);
            byte[] envelopeBytes = WindowsDataProtection.Unprotect(
                protectedBytes,
                UsbAuthCrypto.FileEntropy);

            var envelope = JsonSerializer.Deserialize<UsbAuthEnvelope>(envelopeBytes, JsonSerializerOptions);
            if (envelope == null)
            {
                failureDetail = "授权文件无法解析，请重新写入授权文件。";
                return false;
            }

            byte[] payloadBytes = Convert.FromBase64String(envelope.PayloadBase64);
            byte[] signatureBytes = Convert.FromBase64String(envelope.SignatureBase64);

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            if (!ecdsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256))
            {
                failureDetail = $"检测到 {drive.DisplayName} 上存在授权文件，但签名校验失败。";
                return false;
            }

            var payload = JsonSerializer.Deserialize<UsbAuthPayload>(payloadBytes, JsonSerializerOptions);
            if (payload == null)
            {
                failureDetail = $"检测到 {drive.DisplayName} 上存在授权文件，但载荷已损坏。";
                return false;
            }

            if (!string.Equals(payload.PluginId, UsbAuthCrypto.PluginId, StringComparison.Ordinal))
            {
                failureDetail = $"检测到 {drive.DisplayName} 上存在授权文件，但它不属于当前插件。";
                return false;
            }

            string currentFingerprint = GetPublicKeyFingerprint(publicKey);
            if (!string.Equals(payload.KeyFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                failureDetail = $"检测到 {drive.DisplayName} 上存在授权文件，但它对应的是另一组密钥。";
                return false;
            }

            if (!TryGetVolumeSerialNumber(drive.RootPath, out string currentSerialNumber))
            {
                failureDetail = $"无法读取 {drive.DisplayName} 的卷序列号，请重新插拔U盘后重试。";
                return false;
            }

            if (!string.Equals(payload.VolumeSerialNumber, currentSerialNumber, StringComparison.OrdinalIgnoreCase))
            {
                failureDetail = $"检测到 {drive.DisplayName} 上存在授权文件，但它与当前U盘并不匹配。";
                return false;
            }

            if (!string.Equals(payload.FileName, NormalizeAuthFileName(Settings.Instance.UsbAuth.AuthFileName), StringComparison.OrdinalIgnoreCase))
            {
                failureDetail = $"授权文件已找到，但当前配置的授权文件名与签发时不一致。";
                return false;
            }

            return true;
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "解密或校验U盘授权文件时发生异常");
            failureDetail = $"检测到 {drive.DisplayName} 上存在授权文件，但无法解密或校验。";
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取U盘授权文件时发生异常");
            failureDetail = $"读取 {drive.DisplayName} 上的授权文件时发生异常。";
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder? fileSystemNameBuffer,
        int nFileSystemNameSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public int PropertyId;
        public int QueryType;
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_DEVICE_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        public byte DeviceType;
        public byte DeviceTypeModifier;
        [MarshalAs(UnmanagedType.U1)] public bool RemovableMedia;
        [MarshalAs(UnmanagedType.U1)] public bool CommandQueueing;
        public uint VendorIdOffset;
        public uint ProductIdOffset;
        public uint ProductRevisionOffset;
        public uint SerialNumberOffset;
        public StorageBusType BusType;
        public uint RawPropertiesLength;
    }

    private enum StorageBusType : uint
    {
        BusTypeUnknown = 0x00,
        BusTypeScsi = 0x1,
        BusTypeAtapi = 0x2,
        BusTypeAta = 0x3,
        BusType1394 = 0x4,
        BusTypeSsa = 0x5,
        BusTypeFibre = 0x6,
        BusTypeUsb = 0x7
    }
}

internal static class UsbAuthCrypto
{
    internal const string PluginId = "IslandCaller.Plugin2";
    internal static readonly byte[] FileEntropy = Encoding.UTF8.GetBytes("IslandCaller.UsbAuth.File.v2");
    internal static readonly byte[] PrivateKeyEntropy = Encoding.UTF8.GetBytes("IslandCaller.UsbAuth.PrivateKey.v2");
}
