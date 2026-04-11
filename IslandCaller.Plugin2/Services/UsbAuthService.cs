using IslandCaller.Models;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services;

public readonly record struct UsbAuthSnapshot(bool IsRequired, bool IsVerified, string Summary, string Detail);

public sealed class UsbAuthService
{
    private const string DefaultAuthFileName = "IslandCaller.auth";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(1);

    private readonly ILogger<UsbAuthService> _logger;
    private UsbAuthSnapshot _cachedSnapshot = new(false, true, "未启用", "U 盘身份验证未启用，保底模式不受 U 盘限制。");
    private DateTimeOffset _lastRefreshAt = DateTimeOffset.MinValue;

    public UsbAuthService(ILogger<UsbAuthService> logger)
    {
        _logger = logger;
    }

    public UsbAuthSnapshot RefreshStatus(bool forceRefresh = false)
    {
        if (!forceRefresh && DateTimeOffset.UtcNow - _lastRefreshAt <= CacheDuration)
        {
            return _cachedSnapshot;
        }

        _cachedSnapshot = Evaluate();
        _lastRefreshAt = DateTimeOffset.UtcNow;
        return _cachedSnapshot;
    }

    public bool CanUseGachaMode()
    {
        var snapshot = RefreshStatus();
        return !snapshot.IsRequired || snapshot.IsVerified;
    }

    public static string NormalizeAuthFileName(string? rawFileName)
    {
        string fileName = Path.GetFileName((rawFileName ?? string.Empty).Trim());
        return string.IsNullOrWhiteSpace(fileName) ? DefaultAuthFileName : fileName;
    }

    private UsbAuthSnapshot Evaluate()
    {
        bool isRequired = Settings.Instance.Gacha.RequireUsbAuth;
        string fileName = NormalizeAuthFileName(Settings.Instance.Gacha.UsbAuthFileName);
        string token = (Settings.Instance.Gacha.UsbAuthToken ?? string.Empty).Trim();

        if (!isRequired)
        {
            return new UsbAuthSnapshot(false, true, "未启用", "U 盘身份验证未启用，保底模式不受 U 盘限制。");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new UsbAuthSnapshot(true, false, "未通过", $"请先填写授权密钥，并在 U 盘根目录创建文件 {fileName}。");
        }

        bool hasReadyRemovableDrive = false;
        bool foundAuthFile = false;
        bool foundMismatchedAuthFile = false;

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Removable || !drive.IsReady)
                {
                    continue;
                }

                hasReadyRemovableDrive = true;
                string authFilePath = Path.Combine(drive.RootDirectory.FullName, fileName);
                if (!File.Exists(authFilePath))
                {
                    continue;
                }

                foundAuthFile = true;
                string fileContent = File.ReadAllText(authFilePath).Trim();
                if (string.Equals(fileContent, token, StringComparison.Ordinal))
                {
                    return new UsbAuthSnapshot(true, true, "已通过", $"已通过 U 盘验证：{drive.Name}{fileName}");
                }

                foundMismatchedAuthFile = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "扫描 U 盘身份验证文件时出现异常");
            return new UsbAuthSnapshot(true, false, "验证异常", "扫描 U 盘授权文件时出现异常，请重新插拔 U 盘后重试。");
        }

        if (!hasReadyRemovableDrive)
        {
            return new UsbAuthSnapshot(true, false, "未检测到 U 盘", "未检测到已就绪的可移动磁盘，保底模式当前不可启用。");
        }

        if (foundMismatchedAuthFile)
        {
            return new UsbAuthSnapshot(true, false, "密钥不匹配", $"已找到授权文件 {fileName}，但其中的密钥与设置页配置不一致。");
        }

        if (foundAuthFile)
        {
            return new UsbAuthSnapshot(true, false, "未通过", $"已找到授权文件 {fileName}，但当前验证未通过。");
        }

        return new UsbAuthSnapshot(true, false, "未找到授权文件", $"请在 U 盘根目录放置 {fileName}，并将文件内容填写为授权密钥。");
    }
}
