using System.Threading;
using ClassIsland.Shared;
using IslandCaller.Models;

namespace IslandCaller.Services;

public static class SettingsWriteGate
{
    private static int _bypassDepth;

    public static IDisposable Bypass()
    {
        Interlocked.Increment(ref _bypassDepth);
        return new BypassScope();
    }

    public static bool IsProtectionActive()
    {
        return Settings.Instance.UsbAuth.Enabled
            && !string.IsNullOrWhiteSpace(Settings.Instance.UsbAuth.PublicKey);
    }

    public static bool CanModifyProtectedSettings()
    {
        if (Volatile.Read(ref _bypassDepth) > 0)
        {
            return true;
        }

        if (!IsProtectionActive())
        {
            return true;
        }

        var usbAuthService = IAppHost.TryGetService<UsbAuthService>();
        return usbAuthService?.CanModifyProtectedSettings() ?? false;
    }

    private sealed class BypassScope : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            Interlocked.Decrement(ref _bypassDepth);
        }
    }
}
