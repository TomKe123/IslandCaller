using Microsoft.Win32;
using System.Text.Json;
using IslandCaller.Services;

namespace IslandCaller.Models
{
    public class Settings(ProfileService profileService)
    {
        public static SettingsModel Instance { get; } = new SettingsModel();
        public ProfileService ProfileService { get; } = profileService;

        private static string GetAppDataRootPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IslandCaller"
            );
        }

        private static bool HasLegacyDefaultProfileFile()
        {
            string profilePath = Path.Combine(GetAppDataRootPath(), "Profile");
            return File.Exists(Path.Combine(profilePath, "Default.csv")) ||
                   File.Exists(Path.Combine(profilePath, "default.csv"));
        }

        private static void CleanupLegacyInstall()
        {
            string appDataRootPath = GetAppDataRootPath();

            if (Directory.Exists(appDataRootPath))
            {
                Directory.Delete(appDataRootPath, recursive: true);
            }

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\IslandCaller", throwOnMissingSubKey: false);
        }

        private void InitializeNewInstall()
        {
            RegistryKey? IsC_RootKey = Registry.CurrentUser.CreateSubKey(@"Software\IslandCaller", writable: true);
            RegistryKey? IsC_GeneralKey = IsC_RootKey?.CreateSubKey("General", writable: true);
            RegistryKey? IsC_GachaKey = IsC_RootKey?.CreateSubKey("Gacha", writable: true);
            RegistryKey? IsC_ProfileKey = IsC_RootKey?.CreateSubKey("Profile", writable: true);
            RegistryKey? IsC_HoverKey = IsC_RootKey?.CreateSubKey("Hover", writable: true);
            RegistryKey? IsC_HoverKey_Position = IsC_HoverKey?.CreateSubKey("Position", writable: true);

            IsC_GeneralKey?.SetValue("BreakDisable", Instance.General.BreakDisable);
            IsC_GeneralKey?.SetValue("EnableGlobalHotkeys", Instance.General.EnableGlobalHotkeys);
            IsC_GeneralKey?.SetValue("QuickCallHotkey", Instance.General.QuickCallHotkey);
            IsC_GeneralKey?.SetValue("AdvancedCallHotkey", Instance.General.AdvancedCallHotkey);
            IsC_GeneralKey?.SetValue("EnableGuarantee", Instance.General.EnableGuarantee);
            IsC_GeneralKey?.SetValue("GuaranteeThreshold", Instance.General.GuaranteeThreshold);
            IsC_GeneralKey?.SetValue("GuaranteeListText", Instance.General.GuaranteeListText);
            IsC_GeneralKey?.SetValue("GuaranteeWeightListJson", Instance.General.GuaranteeWeightListJson);
            IsC_GeneralKey?.SetValue("PacerListJson", Instance.General.PacerListJson);
            IsC_GeneralKey?.SetValue("PacerListDate", Instance.General.PacerListDate);
            IsC_GeneralKey?.SetValue("PacerThreshold", Instance.General.PacerThreshold);
            SaveGachaSettings(IsC_GachaKey);
            IsC_ProfileKey?.SetValue("ProfileNum", Instance.Profile.ProfileNum);
            IsC_ProfileKey?.SetValue("DefaultProfileName", Instance.Profile.DefaultProfile.ToString());
            IsC_ProfileKey?.SetValue("IsPreferProfile", Instance.Profile.IsPreferProfile);
            IsC_ProfileKey?.SetValue("ProfileList", JsonSerializer.Serialize(Instance.Profile.ProfileList));
            IsC_ProfileKey?.SetValue("PreferProfile", JsonSerializer.Serialize(Instance.Profile.ProfilePrefer));
            IsC_HoverKey?.SetValue("IsEnable", Instance.Hover.IsEnable);
            IsC_HoverKey?.SetValue("ScalingFactor", Instance.Hover.ScalingFactor);
            IsC_HoverKey_Position?.SetValue("X", Instance.Hover.Position.X);
            IsC_HoverKey_Position?.SetValue("Y", Instance.Hover.Position.Y);

            ProfileService.CreateDemoProfile(Instance.Profile.DefaultProfile);
            ClassIsland.Core.Controls.CommonTaskDialogs.ShowDialog("Welcome", "欢迎使用IslandCaller2.0");
        }

        public void Load()
        {
            RegistryKey? IsC_RootKey = Registry.CurrentUser.OpenSubKey(@"Software\IslandCaller", writable: true);
            RegistryKey? IsC_GeneralKey;
            RegistryKey? IsC_GachaKey;
            RegistryKey? IsC_ProfileKey;
            RegistryKey? IsC_HoverKey;
            RegistryKey? IsC_HoverKey_Position;

            if (IsC_RootKey == null)
            {
                InitializeNewInstall();
            }
            else
            {
                if (HasLegacyDefaultProfileFile())
                {
                    CleanupLegacyInstall();
                    InitializeNewInstall();
                    SettingsBinder.Bind(Instance, Save);
                    return;
                }

                IsC_GeneralKey = IsC_RootKey?.OpenSubKey("General", writable: true);
                IsC_GachaKey = IsC_RootKey?.OpenSubKey("Gacha", writable: true) ?? IsC_RootKey?.CreateSubKey("Gacha", writable: true);
                IsC_ProfileKey = IsC_RootKey?.OpenSubKey("Profile", writable: true);
                IsC_HoverKey = IsC_RootKey?.OpenSubKey("Hover", writable: true);
                IsC_HoverKey_Position = IsC_HoverKey?.OpenSubKey("Position", writable: true);

                Instance.General.BreakDisable = Convert.ToBoolean(IsC_GeneralKey?.GetValue("BreakDisable") ?? true);
                Instance.General.EnableGlobalHotkeys = Convert.ToBoolean(IsC_GeneralKey?.GetValue("EnableGlobalHotkeys") ?? true);
                Instance.General.QuickCallHotkey = (IsC_GeneralKey?.GetValue("QuickCallHotkey") as string) ?? "Ctrl+Alt+R";
                Instance.General.AdvancedCallHotkey = (IsC_GeneralKey?.GetValue("AdvancedCallHotkey") as string) ?? "Ctrl+Alt+G";
                Instance.General.EnableGuarantee = Convert.ToBoolean(IsC_GeneralKey?.GetValue("EnableGuarantee") ?? false);
                Instance.General.GuaranteeThreshold = Convert.ToInt32(IsC_GeneralKey?.GetValue("GuaranteeThreshold") ?? 40);
                Instance.General.GuaranteeListText = (IsC_GeneralKey?.GetValue("GuaranteeListText") as string) ?? string.Empty;
                Instance.General.GuaranteeWeightListJson = (IsC_GeneralKey?.GetValue("GuaranteeWeightListJson") as string) ?? "[]";
                Instance.General.PacerListJson = (IsC_GeneralKey?.GetValue("PacerListJson") as string) ?? "[]";
                Instance.General.PacerListDate = (IsC_GeneralKey?.GetValue("PacerListDate") as string) ?? string.Empty;
                Instance.General.PacerThreshold = Convert.ToInt32(IsC_GeneralKey?.GetValue("PacerThreshold") ?? 50);
                LoadGachaSettings(IsC_GachaKey);
                Instance.Profile.ProfileNum = Convert.ToInt32(IsC_ProfileKey?.GetValue("ProfileNum"));
                Instance.Profile.DefaultProfile = Guid.Parse((IsC_ProfileKey?.GetValue("DefaultProfileName") as string) ?? Guid.Empty.ToString());
                Instance.Profile.IsPreferProfile = Convert.ToBoolean(IsC_ProfileKey?.GetValue("IsPreferProfile") ?? false);
                Instance.Profile.ProfileList = JsonSerializer.Deserialize<Dictionary<Guid, string>>(((IsC_ProfileKey?.GetValue("ProfileList") as string) ?? "{}")) ?? new();
                Instance.Profile.ProfilePrefer = JsonSerializer.Deserialize<Dictionary<Guid, string>>(((IsC_ProfileKey?.GetValue("PreferProfile") as string) ?? "{}")) ?? new();
                Instance.Hover.IsEnable = Convert.ToBoolean(IsC_HoverKey?.GetValue("IsEnable") ?? true);
                Instance.Hover.ScalingFactor = Convert.ToDouble(IsC_HoverKey?.GetValue("ScalingFactor") ?? 1.0);
                Instance.Hover.Position.X = Convert.ToDouble(IsC_HoverKey_Position?.GetValue("X") ?? 200.0);
                Instance.Hover.Position.Y = Convert.ToDouble(IsC_HoverKey_Position?.GetValue("Y") ?? 200.0);
                Save();
            }

            SettingsBinder.Bind(Instance, Save);
        }

        public void Save()
        {
            RegistryKey? IsC_RootKey = Registry.CurrentUser.OpenSubKey(@"Software\IslandCaller", writable: true);
            RegistryKey? IsC_GeneralKey = IsC_RootKey?.OpenSubKey("General", writable: true);
            RegistryKey? IsC_GachaKey = IsC_RootKey?.OpenSubKey("Gacha", writable: true) ?? IsC_RootKey?.CreateSubKey("Gacha", writable: true);
            RegistryKey? IsC_ProfileKey = IsC_RootKey?.OpenSubKey("Profile", writable: true);
            RegistryKey? IsC_HoverKey = IsC_RootKey?.OpenSubKey("Hover", writable: true);
            RegistryKey? IsC_HoverKey_Position = IsC_HoverKey?.OpenSubKey("Position", writable: true);

            IsC_GeneralKey?.SetValue("BreakDisable", Instance.General.BreakDisable);
            IsC_GeneralKey?.SetValue("EnableGlobalHotkeys", Instance.General.EnableGlobalHotkeys);
            IsC_GeneralKey?.SetValue("QuickCallHotkey", Instance.General.QuickCallHotkey);
            IsC_GeneralKey?.SetValue("AdvancedCallHotkey", Instance.General.AdvancedCallHotkey);
            IsC_GeneralKey?.SetValue("EnableGuarantee", Instance.General.EnableGuarantee);
            IsC_GeneralKey?.SetValue("GuaranteeThreshold", Instance.General.GuaranteeThreshold);
            IsC_GeneralKey?.SetValue("GuaranteeListText", Instance.General.GuaranteeListText);
            IsC_GeneralKey?.SetValue("GuaranteeWeightListJson", Instance.General.GuaranteeWeightListJson);
            IsC_GeneralKey?.SetValue("PacerListJson", Instance.General.PacerListJson);
            IsC_GeneralKey?.SetValue("PacerListDate", Instance.General.PacerListDate);
            IsC_GeneralKey?.SetValue("PacerThreshold", Instance.General.PacerThreshold);
            SaveGachaSettings(IsC_GachaKey);
            IsC_ProfileKey?.SetValue("ProfileNum", Instance.Profile.ProfileNum);
            IsC_ProfileKey?.SetValue("DefaultProfileName", Instance.Profile.DefaultProfile.ToString());
            IsC_ProfileKey?.SetValue("IsPreferProfile", Instance.Profile.IsPreferProfile);
            IsC_ProfileKey?.SetValue("ProfileList", JsonSerializer.Serialize(Instance.Profile.ProfileList));
            IsC_ProfileKey?.SetValue("PreferProfile", JsonSerializer.Serialize(Instance.Profile.ProfilePrefer));
            IsC_HoverKey?.SetValue("IsEnable", Instance.Hover.IsEnable);
            IsC_HoverKey?.SetValue("ScalingFactor", Instance.Hover.ScalingFactor);
            IsC_HoverKey_Position?.SetValue("X", Instance.Hover.Position.X);
            IsC_HoverKey_Position?.SetValue("Y", Instance.Hover.Position.Y);
        }

        private static void SaveGachaSettings(RegistryKey? gachaKey)
        {
            gachaKey?.SetValue("Enabled", Instance.Gacha.Enabled);
            gachaKey?.SetValue("FiveStarBaseRate", Instance.Gacha.FiveStarBaseRate);
            gachaKey?.SetValue("FiveStarSoftPityStart", Instance.Gacha.FiveStarSoftPityStart);
            gachaKey?.SetValue("FiveStarHardPity", Instance.Gacha.FiveStarHardPity);
            gachaKey?.SetValue("FiveStarSoftPityStep", Instance.Gacha.FiveStarSoftPityStep);
            gachaKey?.SetValue("FourStarBaseRate", Instance.Gacha.FourStarBaseRate);
            gachaKey?.SetValue("FourStarSoftPityStart", Instance.Gacha.FourStarSoftPityStart);
            gachaKey?.SetValue("FourStarHardPity", Instance.Gacha.FourStarHardPity);
            gachaKey?.SetValue("FourStarSoftPityStep", Instance.Gacha.FourStarSoftPityStep);
            gachaKey?.SetValue("FiveStarFeaturedRate", Instance.Gacha.FiveStarFeaturedRate);
            gachaKey?.SetValue("FourStarFeaturedRate", Instance.Gacha.FourStarFeaturedRate);
        }

        private static void LoadGachaSettings(RegistryKey? gachaKey)
        {
            Instance.Gacha.Enabled = Convert.ToBoolean(gachaKey?.GetValue("Enabled") ?? false);
            Instance.Gacha.FiveStarBaseRate = Convert.ToDouble(gachaKey?.GetValue("FiveStarBaseRate") ?? 0.006);
            Instance.Gacha.FiveStarSoftPityStart = Convert.ToInt32(gachaKey?.GetValue("FiveStarSoftPityStart") ?? 74);
            Instance.Gacha.FiveStarHardPity = Convert.ToInt32(gachaKey?.GetValue("FiveStarHardPity") ?? 90);
            Instance.Gacha.FiveStarSoftPityStep = Convert.ToDouble(gachaKey?.GetValue("FiveStarSoftPityStep") ?? 0.06);
            Instance.Gacha.FourStarBaseRate = Convert.ToDouble(gachaKey?.GetValue("FourStarBaseRate") ?? 0.051);
            Instance.Gacha.FourStarSoftPityStart = Convert.ToInt32(gachaKey?.GetValue("FourStarSoftPityStart") ?? 9);
            Instance.Gacha.FourStarHardPity = Convert.ToInt32(gachaKey?.GetValue("FourStarHardPity") ?? 10);
            Instance.Gacha.FourStarSoftPityStep = Convert.ToDouble(gachaKey?.GetValue("FourStarSoftPityStep") ?? 0.225);
            Instance.Gacha.FiveStarFeaturedRate = Convert.ToDouble(gachaKey?.GetValue("FiveStarFeaturedRate") ?? 0.5);
            Instance.Gacha.FourStarFeaturedRate = Convert.ToDouble(gachaKey?.GetValue("FourStarFeaturedRate") ?? 0.5);
        }
    }
    public static class SettingsBinder
    {
        public static void Bind(SettingsModel model, Action onChange)
        {
            // General
            model.General.PropertyChanged += (_, _) => onChange();
            model.Gacha.PropertyChanged += (_, _) => onChange();

            // Hover
            model.Hover.PropertyChanged += (_, _) => onChange();
            model.Hover.Position.PropertyChanged += (_, _) => onChange();
        }
    }

}
