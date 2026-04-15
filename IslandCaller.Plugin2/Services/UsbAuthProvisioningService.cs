using System.Security.Cryptography;
using System.Text.Json;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services;

public readonly record struct UsbAuthProvisioningResult(
    bool Success,
    string Message,
    UsbAuthSnapshot Snapshot);

public sealed class UsbAuthProvisioningService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly UsbAuthService _usbAuthService;
    private readonly ILogger<UsbAuthProvisioningService> _logger;

    public UsbAuthProvisioningService(UsbAuthService usbAuthService, ILogger<UsbAuthProvisioningService> logger)
    {
        _usbAuthService = usbAuthService;
        _logger = logger;
    }

    public string GetKeyFingerprint()
    {
        return UsbAuthService.GetPublicKeyFingerprint(Settings.Instance.UsbAuth.PublicKey);
    }

    public bool CanManageProvisioning(UsbAuthSnapshot? snapshot = null)
    {
        return !SettingsWriteGate.IsProtectionActive() || (snapshot ?? _usbAuthService.RefreshStatus()).IsVerified;
    }

    public UsbAuthProvisioningResult WriteAuthorizationAndEnable(UsbDriveInfo drive)
    {
        if (string.IsNullOrWhiteSpace(drive.RootPath) || !Directory.Exists(drive.RootPath))
        {
            return new UsbAuthProvisioningResult(false, "未找到可写入的U盘。", _usbAuthService.RefreshStatus(forceRefresh: true));
        }

        if (!CanManageProvisioning())
        {
            return new UsbAuthProvisioningResult(false, "当前已启用U盘验证，只有插入已授权U盘后才能重写授权文件。", _usbAuthService.RefreshStatus(forceRefresh: true));
        }

        try
        {
            using var ecdsa = LoadOrCreatePrivateKey();
            string authFileName = UsbAuthService.NormalizeAuthFileName(Settings.Instance.UsbAuth.AuthFileName);
            if (!UsbAuthService.TryGetVolumeSerialNumber(drive.RootPath, out string serialNumber))
            {
                return new UsbAuthProvisioningResult(false, "无法读取目标U盘的卷序列号，请重新插拔后重试。", _usbAuthService.RefreshStatus(forceRefresh: true));
            }

            var payload = new UsbAuthPayload(
                1,
                UsbAuthCrypto.PluginId,
                GetKeyFingerprint(),
                serialNumber,
                authFileName,
                DateTimeOffset.UtcNow.ToString("O"));

            byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonSerializerOptions);
            byte[] signatureBytes = ecdsa.SignData(payloadBytes, HashAlgorithmName.SHA256);

            var envelope = new UsbAuthEnvelope(
                1,
                Convert.ToBase64String(payloadBytes),
                Convert.ToBase64String(signatureBytes));

            byte[] envelopeBytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonSerializerOptions);
            byte[] protectedBytes = WindowsDataProtection.Protect(
                envelopeBytes,
                UsbAuthCrypto.FileEntropy);

            File.WriteAllBytes(drive.AuthFilePath, protectedBytes);

            using (SettingsWriteGate.Bypass())
            {
                Settings.Instance.UsbAuth.Enabled = true;
            }

            var snapshot = _usbAuthService.RefreshStatus(forceRefresh: true);
            if (!snapshot.IsVerified)
            {
                using (SettingsWriteGate.Bypass())
                {
                    Settings.Instance.UsbAuth.Enabled = false;
                }

                snapshot = _usbAuthService.RefreshStatus(forceRefresh: true);
                return new UsbAuthProvisioningResult(false, "授权文件已写入，但回读校验未通过，请重新尝试。", snapshot);
            }

            return new UsbAuthProvisioningResult(true, $"已向 {drive.DisplayName} 写入授权文件并启用U盘验证。", snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入U盘授权文件时发生异常");
            return new UsbAuthProvisioningResult(false, "写入授权文件失败，请确认U盘可写后重试。", _usbAuthService.RefreshStatus(forceRefresh: true));
        }
    }

    public UsbAuthProvisioningResult DisableProtection()
    {
        if (SettingsWriteGate.IsProtectionActive() && !_usbAuthService.RefreshStatus().IsVerified)
        {
            return new UsbAuthProvisioningResult(false, "请先插入已授权U盘，再关闭U盘验证。", _usbAuthService.RefreshStatus(forceRefresh: true));
        }

        Settings.Instance.UsbAuth.Enabled = false;
        var snapshot = _usbAuthService.RefreshStatus(forceRefresh: true);
        return new UsbAuthProvisioningResult(true, "已关闭U盘验证。", snapshot);
    }

    public UsbAuthProvisioningResult RegenerateKeyPair()
    {
        if (!CanManageProvisioning())
        {
            return new UsbAuthProvisioningResult(false, "当前未通过U盘验证，不能重置密钥对。", _usbAuthService.RefreshStatus(forceRefresh: true));
        }

        try
        {
            using (SettingsWriteGate.Bypass())
            {
                CreateAndPersistKeyPair();
                Settings.Instance.UsbAuth.Enabled = false;
            }

            var snapshot = _usbAuthService.RefreshStatus(forceRefresh: true);
            return new UsbAuthProvisioningResult(true, "已重置密钥对。请重新向目标U盘写入授权文件。", snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "重置U盘验证密钥对时发生异常");
            return new UsbAuthProvisioningResult(false, "重置密钥对失败，请重试。", _usbAuthService.RefreshStatus(forceRefresh: true));
        }
    }

    private ECDsa LoadOrCreatePrivateKey()
    {
        string protectedPrivateKey = Settings.Instance.UsbAuth.ProtectedPrivateKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(protectedPrivateKey) || string.IsNullOrWhiteSpace(Settings.Instance.UsbAuth.PublicKey))
        {
            return CreateAndPersistKeyPair();
        }

        byte[] protectedBytes = Convert.FromBase64String(protectedPrivateKey);
        byte[] privateKeyBytes = WindowsDataProtection.Unprotect(
            protectedBytes,
            UsbAuthCrypto.PrivateKeyEntropy);

        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
        return ecdsa;
    }

    private ECDsa CreateAndPersistKeyPair()
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] privateKeyBytes = ecdsa.ExportPkcs8PrivateKey();
        byte[] publicKeyBytes = ecdsa.ExportSubjectPublicKeyInfo();
        byte[] protectedPrivateKey = WindowsDataProtection.Protect(
            privateKeyBytes,
            UsbAuthCrypto.PrivateKeyEntropy);

        Settings.Instance.UsbAuth.PublicKey = Convert.ToBase64String(publicKeyBytes);
        Settings.Instance.UsbAuth.ProtectedPrivateKey = Convert.ToBase64String(protectedPrivateKey);
        return ecdsa;
    }
}
