using ClassIsland.Shared;
using CommunityToolkit.Mvvm.Input;
using IslandCaller.Models;
using IslandCaller.Services;
using IslandCaller.Views;
using ReactiveUI;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Input;
using static IslandCaller.Services.ProfileService;

namespace IslandCaller.ViewModels
{
    public class SettingPageViewModel : ReactiveObject
    {
        private readonly ProfileService _profileService;
        private readonly HistoryService _historyService;
        private readonly CoreService _coreService;
        private readonly LotteryService _lotteryService;
        private readonly UsbAuthService _usbAuthService;
        private readonly UsbAuthProvisioningService _usbAuthProvisioningService;
        private readonly Plugin _plugin;

        // 基本设置
        private bool _isBreakDisable;
        public bool IsBreakDisable
        {
            get => _isBreakDisable;
            set => this.RaiseAndSetIfChanged(ref _isBreakDisable, value);
        }

        private bool _isGlobalHotkeyEnabled;
        public bool IsGlobalHotkeyEnabled
        {
            get => _isGlobalHotkeyEnabled;
            set => this.RaiseAndSetIfChanged(ref _isGlobalHotkeyEnabled, value);
        }

        private string _quickCallHotkey = string.Empty;
        public string QuickCallHotkey
        {
            get => _quickCallHotkey;
            set => this.RaiseAndSetIfChanged(ref _quickCallHotkey, value);
        }

        private string _advancedCallHotkey = string.Empty;
        public string AdvancedCallHotkey
        {
            get => _advancedCallHotkey;
            set => this.RaiseAndSetIfChanged(ref _advancedCallHotkey, value);
        }

        private bool _isGuaranteeEnabled;
        public bool IsGuaranteeEnabled
        {
            get => _isGuaranteeEnabled;
            set => this.RaiseAndSetIfChanged(ref _isGuaranteeEnabled, value);
        }

        private int _guaranteeThreshold;
        public int GuaranteeThreshold
        {
            get => _guaranteeThreshold;
            set => this.RaiseAndSetIfChanged(ref _guaranteeThreshold, value);
        }

        private int _pacerThreshold;
        public int PacerThreshold
        {
            get => _pacerThreshold;
            set => this.RaiseAndSetIfChanged(ref _pacerThreshold, value);
        }

        private DrawSelectionScope _defaultDrawScope;
        public DrawSelectionScope DefaultDrawScope
        {
            get => _defaultDrawScope;
            set => this.RaiseAndSetIfChanged(ref _defaultDrawScope, value);
        }

        private DrawSelectionAlgorithm _defaultDrawAlgorithm;
        public DrawSelectionAlgorithm DefaultDrawAlgorithm
        {
            get => _defaultDrawAlgorithm;
            set => this.RaiseAndSetIfChanged(ref _defaultDrawAlgorithm, value);
        }

        private DrawScopeOptionItem? _selectedDrawScopeOption;
        public DrawScopeOptionItem? SelectedDrawScopeOption
        {
            get => _selectedDrawScopeOption;
            set
            {
                if (_selectedDrawScopeOption == value)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _selectedDrawScopeOption, value);
                if (value != null)
                {
                    DefaultDrawScope = value.Value;
                }
            }
        }

        private DrawAlgorithmOptionItem? _selectedDrawAlgorithmOption;
        public DrawAlgorithmOptionItem? SelectedDrawAlgorithmOption
        {
            get => _selectedDrawAlgorithmOption;
            set
            {
                if (_selectedDrawAlgorithmOption == value)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _selectedDrawAlgorithmOption, value);
                if (value != null)
                {
                    DefaultDrawAlgorithm = value.Value;
                }
            }
        }

        public sealed class DrawScopeOptionItem
        {
            public DrawSelectionScope Value { get; init; }
            public string Label { get; init; } = string.Empty;
        }

        public sealed class DrawAlgorithmOptionItem
        {
            public DrawSelectionAlgorithm Value { get; init; }
            public string Label { get; init; } = string.Empty;
        }

        private bool _isGachaEnabled;
        public bool IsGachaEnabled
        {
            get => _isGachaEnabled;
            set => this.RaiseAndSetIfChanged(ref _isGachaEnabled, value);
        }

        public sealed class UsbDriveItemModel : ReactiveObject
        {
            public UsbDriveItemModel(UsbDriveInfo driveInfo)
            {
                DriveInfo = driveInfo;
            }

            public UsbDriveInfo DriveInfo { get; }
            public string DisplayName => DriveInfo.DisplayName;
            public string RootPath => DriveInfo.RootPath;
            public bool HasAuthorizationFile => DriveInfo.HasAuthorizationFile;
            public string StateText => HasAuthorizationFile ? "已存在授权文件" : "未写入授权文件";
        }

        private bool _isUsbAuthEnabled;
        public bool IsUsbAuthEnabled
        {
            get => _isUsbAuthEnabled;
            set => this.RaiseAndSetIfChanged(ref _isUsbAuthEnabled, value);
        }

        private ObservableCollection<UsbDriveItemModel> _removableDrives = [];
        public ObservableCollection<UsbDriveItemModel> RemovableDrives
        {
            get => _removableDrives;
            set => this.RaiseAndSetIfChanged(ref _removableDrives, value);
        }

        private UsbDriveItemModel? _selectedDrive;
        public UsbDriveItemModel? SelectedDrive
        {
            get => _selectedDrive;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedDrive, value);
                RefreshUsbAuthComputedState();
            }
        }

        private string _authFileName = string.Empty;
        public string AuthFileName
        {
            get => _authFileName;
            set
            {
                string normalized = UsbAuthService.NormalizeAuthFileName(value);
                if (_authFileName == normalized)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _authFileName, normalized);
                if (CanModifyUsbAuthConfiguration)
                {
                    Settings.Instance.UsbAuth.AuthFileName = normalized;
                    RefreshUsbAuthSection();
                    return;
                }

                LoadAuthFileNameFromSettings();
            }
        }

        private string _usbAuthStatusSummary = string.Empty;
        public string UsbAuthStatusSummary
        {
            get => _usbAuthStatusSummary;
            set => this.RaiseAndSetIfChanged(ref _usbAuthStatusSummary, value);
        }

        private string _usbAuthStatusText = string.Empty;
        public string UsbAuthStatusText
        {
            get => _usbAuthStatusText;
            set => this.RaiseAndSetIfChanged(ref _usbAuthStatusText, value);
        }

        private bool _isUsbAuthVerified;
        public bool IsUsbAuthVerified
        {
            get => _isUsbAuthVerified;
            set => this.RaiseAndSetIfChanged(ref _isUsbAuthVerified, value);
        }

        private bool _canEditProtectedSettings = true;
        public bool CanEditProtectedSettings
        {
            get => _canEditProtectedSettings;
            set => this.RaiseAndSetIfChanged(ref _canEditProtectedSettings, value);
        }

        private bool _isProtectedSettingsLocked;
        public bool IsProtectedSettingsLocked
        {
            get => _isProtectedSettingsLocked;
            set => this.RaiseAndSetIfChanged(ref _isProtectedSettingsLocked, value);
        }

        private string _protectedSettingsLockText = string.Empty;
        public string ProtectedSettingsLockText
        {
            get => _protectedSettingsLockText;
            set => this.RaiseAndSetIfChanged(ref _protectedSettingsLockText, value);
        }

        private string _keyFingerprint = string.Empty;
        public string KeyFingerprint
        {
            get => _keyFingerprint;
            set => this.RaiseAndSetIfChanged(ref _keyFingerprint, value);
        }

        private bool _canModifyUsbAuthConfiguration;
        public bool CanModifyUsbAuthConfiguration
        {
            get => _canModifyUsbAuthConfiguration;
            set => this.RaiseAndSetIfChanged(ref _canModifyUsbAuthConfiguration, value);
        }

        private bool _canWriteAuthorization;
        public bool CanWriteAuthorization
        {
            get => _canWriteAuthorization;
            set => this.RaiseAndSetIfChanged(ref _canWriteAuthorization, value);
        }

        private bool _canDisableProtection;
        public bool CanDisableProtection
        {
            get => _canDisableProtection;
            set => this.RaiseAndSetIfChanged(ref _canDisableProtection, value);
        }

        private bool _canRegenerateKeyPair;
        public bool CanRegenerateKeyPair
        {
            get => _canRegenerateKeyPair;
            set => this.RaiseAndSetIfChanged(ref _canRegenerateKeyPair, value);
        }

        private string _writeButtonText = string.Empty;
        public string WriteButtonText
        {
            get => _writeButtonText;
            set => this.RaiseAndSetIfChanged(ref _writeButtonText, value);
        }

        private string _selectedDriveHint = string.Empty;
        public string SelectedDriveHint
        {
            get => _selectedDriveHint;
            set => this.RaiseAndSetIfChanged(ref _selectedDriveHint, value);
        }

        private string _usbAuthGuideText = string.Empty;
        public string UsbAuthGuideText
        {
            get => _usbAuthGuideText;
            set => this.RaiseAndSetIfChanged(ref _usbAuthGuideText, value);
        }

        private string _usbAuthActionMessage = string.Empty;
        public string UsbAuthActionMessage
        {
            get => _usbAuthActionMessage;
            set => this.RaiseAndSetIfChanged(ref _usbAuthActionMessage, value);
        }

        private double _fiveStarBaseRate;
        public double FiveStarBaseRate
        {
            get => _fiveStarBaseRate;
            set => this.RaiseAndSetIfChanged(ref _fiveStarBaseRate, value);
        }

        private int _fiveStarSoftPityStart;
        public int FiveStarSoftPityStart
        {
            get => _fiveStarSoftPityStart;
            set => this.RaiseAndSetIfChanged(ref _fiveStarSoftPityStart, value);
        }

        private int _fiveStarHardPity;
        public int FiveStarHardPity
        {
            get => _fiveStarHardPity;
            set => this.RaiseAndSetIfChanged(ref _fiveStarHardPity, value);
        }

        private double _fiveStarSoftPityStep;
        public double FiveStarSoftPityStep
        {
            get => _fiveStarSoftPityStep;
            set => this.RaiseAndSetIfChanged(ref _fiveStarSoftPityStep, value);
        }

        private double _fourStarBaseRate;
        public double FourStarBaseRate
        {
            get => _fourStarBaseRate;
            set => this.RaiseAndSetIfChanged(ref _fourStarBaseRate, value);
        }

        private int _fourStarSoftPityStart;
        public int FourStarSoftPityStart
        {
            get => _fourStarSoftPityStart;
            set => this.RaiseAndSetIfChanged(ref _fourStarSoftPityStart, value);
        }

        private int _fourStarHardPity;
        public int FourStarHardPity
        {
            get => _fourStarHardPity;
            set => this.RaiseAndSetIfChanged(ref _fourStarHardPity, value);
        }

        private double _fourStarSoftPityStep;
        public double FourStarSoftPityStep
        {
            get => _fourStarSoftPityStep;
            set => this.RaiseAndSetIfChanged(ref _fourStarSoftPityStep, value);
        }

        private double _fiveStarFeaturedRate;
        public double FiveStarFeaturedRate
        {
            get => _fiveStarFeaturedRate;
            set => this.RaiseAndSetIfChanged(ref _fiveStarFeaturedRate, value);
        }

        private double _fourStarFeaturedRate;
        public double FourStarFeaturedRate
        {
            get => _fourStarFeaturedRate;
            set => this.RaiseAndSetIfChanged(ref _fourStarFeaturedRate, value);
        }

        private string _guaranteeListText = string.Empty;
        public string GuaranteeListText
        {
            get => _guaranteeListText;
            set => this.RaiseAndSetIfChanged(ref _guaranteeListText, value);
        }

        public class GuaranteeWeightModel : ReactiveObject
        {
            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set => this.RaiseAndSetIfChanged(ref _name, value);
            }

            private double _weight = 1.0;
            public double Weight
            {
                get => _weight;
                set => this.RaiseAndSetIfChanged(ref _weight, value);
            }
        }

        private ObservableCollection<GuaranteeWeightModel> _guaranteeWeightList = [];
        public ObservableCollection<GuaranteeWeightModel> GuaranteeWeightList
        {
            get => _guaranteeWeightList;
            set
            {
                if (ReferenceEquals(_guaranteeWeightList, value))
                {
                    return;
                }

                DetachGuaranteeWeightCollection(_guaranteeWeightList);
                this.RaiseAndSetIfChanged(ref _guaranteeWeightList, value);
                AttachGuaranteeWeightCollection(_guaranteeWeightList);
            }
        }

        private ObservableCollection<string> _guaranteeMemberOptions = [];
        public ObservableCollection<string> GuaranteeMemberOptions
        {
            get => _guaranteeMemberOptions;
            set => this.RaiseAndSetIfChanged(ref _guaranteeMemberOptions, value);
        }

        private string? _selectedGuaranteeMember;
        public string? SelectedGuaranteeMember
        {
            get => _selectedGuaranteeMember;
            set => this.RaiseAndSetIfChanged(ref _selectedGuaranteeMember, value);
        }

        private ObservableCollection<string> _pacerList = [];
        public ObservableCollection<string> PacerList
        {
            get => _pacerList;
            set => this.RaiseAndSetIfChanged(ref _pacerList, value);
        }

        private string _pacerSummaryText = string.Empty;
        public string PacerSummaryText
        {
            get => _pacerSummaryText;
            set => this.RaiseAndSetIfChanged(ref _pacerSummaryText, value);
        }

        public class LotteryPrizeModel : ReactiveObject
        {
            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set => this.RaiseAndSetIfChanged(ref _name, value);
            }

            private int _winnerCount = 1;
            public int WinnerCount
            {
                get => _winnerCount;
                set => this.RaiseAndSetIfChanged(ref _winnerCount, value);
            }
        }

        private ObservableCollection<LotteryPrizeModel> _lotteryPrizeList = [];
        public ObservableCollection<LotteryPrizeModel> LotteryPrizeList
        {
            get => _lotteryPrizeList;
            set
            {
                if (ReferenceEquals(_lotteryPrizeList, value))
                {
                    return;
                }

                DetachLotteryPrizeCollection(_lotteryPrizeList);
                this.RaiseAndSetIfChanged(ref _lotteryPrizeList, value);
                AttachLotteryPrizeCollection(_lotteryPrizeList);
            }
        }

        private string _lotterySummaryText = string.Empty;
        public string LotterySummaryText
        {
            get => _lotterySummaryText;
            set => this.RaiseAndSetIfChanged(ref _lotterySummaryText, value);
        }

        private string _gachaSummaryText = string.Empty;
        public string GachaSummaryText
        {
            get => _gachaSummaryText;
            set => this.RaiseAndSetIfChanged(ref _gachaSummaryText, value);
        }

        public ICommand RemoveGuaranteeRowCommand => new RelayCommand<GuaranteeWeightModel>(row =>
        {
            if (row == null)
            {
                return;
            }

            GuaranteeWeightList.Remove(row);
        });

        public ICommand RefreshUsbDrivesCommand => new RelayCommand(() =>
        {
            RefreshUsbAuthSection();
        });

        public ICommand WriteAuthorizationFileCommand => new RelayCommand(() =>
        {
            if (SelectedDrive == null)
            {
                return;
            }

            var result = _usbAuthProvisioningService.WriteAuthorizationAndEnable(SelectedDrive.DriveInfo);
            UsbAuthActionMessage = result.Message;
            RefreshUsbAuthSection();
        });

        public ICommand DisableUsbAuthCommand => new RelayCommand(() =>
        {
            var result = _usbAuthProvisioningService.DisableProtection();
            UsbAuthActionMessage = result.Message;
            RefreshUsbAuthSection();
        });

        public ICommand RegenerateKeyPairCommand => new RelayCommand(() =>
        {
            var result = _usbAuthProvisioningService.RegenerateKeyPair();
            UsbAuthActionMessage = result.Message;
            RefreshUsbAuthSection();
        });

        public ICommand AddLotteryPrizeCommand => new RelayCommand(() =>
        {
            LotteryPrizeList.Add(new LotteryPrizeModel
            {
                Name = $"奖项{LotteryPrizeList.Count + 1}",
                WinnerCount = 1
            });
        });

        public ICommand RemoveLotteryPrizeRowCommand => new RelayCommand<LotteryPrizeModel>(row =>
        {
            if (row == null)
            {
                return;
            }

            LotteryPrizeList.Remove(row);
        });

        public ICommand OpenLotteryWindowCommand => new RelayCommand(() =>
        {
            IAppHost.GetService<IslandCaller.Services.IslandCallerService.IslandCallerService>().TriggerUriLotteryGuiCall();
        });

        public class StatisticsItem : ReactiveObject
        {
            private string _metric = string.Empty;
            public string Metric
            {
                get => _metric;
                set => this.RaiseAndSetIfChanged(ref _metric, value);
            }

            private string _value = string.Empty;
            public string Value
            {
                get => _value;
                set => this.RaiseAndSetIfChanged(ref _value, value);
            }
        }

        public class HistoryItem : ReactiveObject
        {
            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set => this.RaiseAndSetIfChanged(ref _name, value);
            }

            private int _longTermCount;
            public int LongTermCount
            {
                get => _longTermCount;
                set => this.RaiseAndSetIfChanged(ref _longTermCount, value);
            }

            private int _sessionMissCount;
            public int SessionMissCount
            {
                get => _sessionMissCount;
                set => this.RaiseAndSetIfChanged(ref _sessionMissCount, value);
            }

            private string _lastCallText = string.Empty;
            public string LastCallText
            {
                get => _lastCallText;
                set => this.RaiseAndSetIfChanged(ref _lastCallText, value);
            }
        }

        public class RecentCallItem : ReactiveObject
        {
            private int _index;
            public int Index
            {
                get => _index;
                set => this.RaiseAndSetIfChanged(ref _index, value);
            }

            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set => this.RaiseAndSetIfChanged(ref _name, value);
            }
        }

        public ObservableCollection<StatisticsItem> StatisticsList { get; } = [];
        public ObservableCollection<HistoryItem> HistoryList { get; } = [];
        public ObservableCollection<RecentCallItem> RecentCallList { get; } = [];

        private string _guaranteeSummaryText = string.Empty;
        public string GuaranteeSummaryText
        {
            get => _guaranteeSummaryText;
            set => this.RaiseAndSetIfChanged(ref _guaranteeSummaryText, value);
        }

        //悬浮窗设置
        private bool _isHoverEnable;
        public bool IsHoverEnable
        {
            get => _isHoverEnable;
            set => this.RaiseAndSetIfChanged(ref _isHoverEnable, value);
        }
        private double _hoverScalingFactor;
        public double HoverScalingFactor
        {
            get => _hoverScalingFactor;
            set => this.RaiseAndSetIfChanged(ref _hoverScalingFactor, value);
        }

        // 档案设置
        private Guid _currentProfile = Settings.Instance.Profile.DefaultProfile;
        public Guid CurrentProfile { get => _currentProfile; }
        public class StudentModel : ReactiveObject
        {
            private int _id;
            public int ID
            {
                get => _id;
                set => this.RaiseAndSetIfChanged(ref _id, value);
            }
            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set => this.RaiseAndSetIfChanged(ref _name, value);
            }
            private int _gender;
            public int Gender
            {
                get => _gender;
                set => this.RaiseAndSetIfChanged(ref _gender, value);
            }
            private double _manualWeight;
            public double ManualWeight
            {
                get => _manualWeight;
                set => this.RaiseAndSetIfChanged(ref _manualWeight, value);
            }

            private int _rarity = 3;
            public int Rarity
            {
                get => _rarity;
                set => this.RaiseAndSetIfChanged(ref _rarity, value);
            }

            private bool _isFeatured;
            public bool IsFeatured
            {
                get => _isFeatured;
                set => this.RaiseAndSetIfChanged(ref _isFeatured, value);
            }
        }
        private ObservableCollection<StudentModel> _profileList = new();
        public ObservableCollection<StudentModel> ProfileList
        {
            get => _profileList;
            set
            {
                if (ReferenceEquals(_profileList, value))
                {
                    return;
                }

                DetachProfileCollection(_profileList);
                this.RaiseAndSetIfChanged(ref _profileList, value);
                AttachProfileCollection(_profileList);
                UpdateProfileSummary();
            }
        }

        private string _profileSummaryText = string.Empty;
        public string ProfileSummaryText
        {
            get => _profileSummaryText;
            set => this.RaiseAndSetIfChanged(ref _profileSummaryText, value);
        }

        public IReadOnlyList<int> RarityOptions { get; } = [3, 5];
        public IReadOnlyList<DrawScopeOptionItem> DrawScopeOptions { get; } =
        [
            new() { Value = DrawSelectionScope.All, Label = "完全班级" },
            new() { Value = DrawSelectionScope.Male, Label = "仅男生" },
            new() { Value = DrawSelectionScope.Female, Label = "仅女生" }
        ];

        public IReadOnlyList<DrawAlgorithmOptionItem> DrawAlgorithmOptions { get; } =
        [
            new() { Value = DrawSelectionAlgorithm.Balanced, Label = "平衡抽选" },
            new() { Value = DrawSelectionAlgorithm.PureRandom, Label = "完全公平" }
        ];

        public ICommand RowCommand => new RelayCommand<StudentModel>(row =>
        {
            if (row == null) return;
            var firstColumnValue = row.ID;
            var item = ProfileList.FirstOrDefault(p => p.ID == firstColumnValue);
            if (item != null)
            {
                ProfileList.Remove(item);
            }
        });


        // 构造函数
        public SettingPageViewModel()
        {
            _profileService = IAppHost.GetService<ProfileService>();
            _historyService = IAppHost.GetService<HistoryService>();
            _coreService = IAppHost.GetService<CoreService>();
            _lotteryService = IAppHost.GetService<LotteryService>();
            _usbAuthService = IAppHost.GetService<UsbAuthService>();
            _usbAuthProvisioningService = IAppHost.GetService<UsbAuthProvisioningService>();
            _plugin = IAppHost.GetService<Plugin>();

            // 初始化默认值
            IsBreakDisable = Settings.Instance.General.BreakDisable;
            IsGlobalHotkeyEnabled = Settings.Instance.General.EnableGlobalHotkeys;
            QuickCallHotkey = Settings.Instance.General.QuickCallHotkey;
            AdvancedCallHotkey = Settings.Instance.General.AdvancedCallHotkey;
            IsGuaranteeEnabled = Settings.Instance.General.EnableGuarantee;
            GuaranteeThreshold = Settings.Instance.General.GuaranteeThreshold;
            PacerThreshold = Settings.Instance.General.PacerThreshold;
            DefaultDrawScope = Settings.Instance.General.DefaultDrawScope;
            DefaultDrawAlgorithm = Settings.Instance.General.DefaultDrawAlgorithm;
            SelectedDrawScopeOption = DrawScopeOptions.FirstOrDefault(x => x.Value == DefaultDrawScope);
            SelectedDrawAlgorithmOption = DrawAlgorithmOptions.FirstOrDefault(x => x.Value == DefaultDrawAlgorithm);
            IsGachaEnabled = Settings.Instance.Gacha.Enabled;
            IsUsbAuthEnabled = Settings.Instance.UsbAuth.Enabled;
            LoadAuthFileNameFromSettings();
            FiveStarBaseRate = Settings.Instance.Gacha.FiveStarBaseRate;
            FiveStarSoftPityStart = Settings.Instance.Gacha.FiveStarSoftPityStart;
            FiveStarHardPity = Settings.Instance.Gacha.FiveStarHardPity;
            FiveStarSoftPityStep = Settings.Instance.Gacha.FiveStarSoftPityStep;
            FourStarBaseRate = Settings.Instance.Gacha.FourStarBaseRate;
            FourStarSoftPityStart = Settings.Instance.Gacha.FourStarSoftPityStart;
            FourStarHardPity = Settings.Instance.Gacha.FourStarHardPity;
            FourStarSoftPityStep = Settings.Instance.Gacha.FourStarSoftPityStep;
            FiveStarFeaturedRate = Settings.Instance.Gacha.FiveStarFeaturedRate;
            FourStarFeaturedRate = Settings.Instance.Gacha.FourStarFeaturedRate;
            GuaranteeListText = Settings.Instance.General.GuaranteeListText;
            GuaranteeWeightList = BuildGuaranteeWeightList(_profileService);
            LotteryPrizeList = BuildLotteryPrizeList(_lotteryService);
            UpdateGuaranteeMemberOptions(_profileService);
            RefreshPacerList(_profileService);
            IsHoverEnable = Settings.Instance.Hover.IsEnable;
            HoverScalingFactor = Settings.Instance.Hover.ScalingFactor;
            var profile = _profileService.GetMembers(CurrentProfile)
            .OrderBy(m => m.Id)
            .Select(m => new StudentModel
            {
                ID = m.Id,
                Name = m.Name,
                Gender = m.Gender,
                ManualWeight = m.ManualWeight,
                Rarity = (int)m.Rarity,
                IsFeatured = m.IsFeatured
            });
            ProfileList = new ObservableCollection<StudentModel>(profile);
            RefreshUsbAuthSection(refreshHistory: false);
            UpdateLotterySummary();
            UpdateProfileSummary();
            RefreshProtectedSettingsLockState();
            _usbAuthService.StatusChanged += (_, snapshot) => Avalonia.Threading.Dispatcher.UIThread.Post(() => OnUsbAuthStatusChanged(snapshot));
            _usbAuthService.DrivesChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => RefreshUsbAuthSection(refreshHistory: false));
            Settings.Instance.UsbAuth.PropertyChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => RefreshUsbAuthSection(refreshHistory: false));

            this.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(IsBreakDisable))
                {
                    Settings.Instance.General.BreakDisable = IsBreakDisable;
                }
                else if (args.PropertyName == nameof(IsGlobalHotkeyEnabled))
                {
                    Settings.Instance.General.EnableGlobalHotkeys = IsGlobalHotkeyEnabled;
                }
                else if (args.PropertyName == nameof(QuickCallHotkey))
                {
                    Settings.Instance.General.QuickCallHotkey = QuickCallHotkey;
                }
                else if (args.PropertyName == nameof(AdvancedCallHotkey))
                {
                    Settings.Instance.General.AdvancedCallHotkey = AdvancedCallHotkey;
                }
                else if (args.PropertyName == nameof(IsGuaranteeEnabled))
                {
                    Settings.Instance.General.EnableGuarantee = IsGuaranteeEnabled;
                    UpdateGuaranteeSummary(_profileService);
                    RefreshHistoryAndStatistics(_historyService);
                }
                else if (args.PropertyName == nameof(GuaranteeThreshold))
                {
                    Settings.Instance.General.GuaranteeThreshold = Math.Max(1, GuaranteeThreshold);
                    if (GuaranteeThreshold < 1)
                    {
                        GuaranteeThreshold = 1;
                    }
                    UpdateGuaranteeSummary(_profileService);
                    RefreshHistoryAndStatistics(_historyService);
                }
                else if (args.PropertyName == nameof(PacerThreshold))
                {
                    Settings.Instance.General.PacerThreshold = Math.Max(1, PacerThreshold);
                    if (PacerThreshold < 1)
                    {
                        PacerThreshold = 1;
                    }

                    RefreshPacerList(_profileService);
                    RefreshHistoryAndStatistics(_historyService);
                }
                else if (args.PropertyName == nameof(DefaultDrawScope))
                {
                    Settings.Instance.General.DefaultDrawScope = DefaultDrawScope;
                    if (SelectedDrawScopeOption?.Value != DefaultDrawScope)
                    {
                        SelectedDrawScopeOption = DrawScopeOptions.FirstOrDefault(x => x.Value == DefaultDrawScope);
                    }
                    RefreshHistoryAndStatistics(_historyService);
                }
                else if (args.PropertyName == nameof(DefaultDrawAlgorithm))
                {
                    Settings.Instance.General.DefaultDrawAlgorithm = DefaultDrawAlgorithm;
                    if (SelectedDrawAlgorithmOption?.Value != DefaultDrawAlgorithm)
                    {
                        SelectedDrawAlgorithmOption = DrawAlgorithmOptions.FirstOrDefault(x => x.Value == DefaultDrawAlgorithm);
                    }
                    RefreshHistoryAndStatistics(_historyService);
                }
                else if (args.PropertyName == nameof(IsGachaEnabled))
                {
                    if (IsGachaEnabled)
                    {
                        var authSnapshot = _usbAuthService.RefreshStatus(forceRefresh: true);
                        ApplyUsbAuthSnapshot(authSnapshot);
                        if (Settings.Instance.UsbAuth.Enabled && !authSnapshot.IsVerified)
                        {
                            IsGachaEnabled = false;
                            return;
                        }
                    }

                    Settings.Instance.Gacha.Enabled = IsGachaEnabled;
                    RefreshHistoryAndStatistics(_historyService);
                    UpdateGachaSummary(_historyService);
                }
                else if (args.PropertyName == nameof(FiveStarBaseRate))
                {
                    Settings.Instance.Gacha.FiveStarBaseRate = Math.Clamp(FiveStarBaseRate, 0.0, 1.0);
                }
                else if (args.PropertyName == nameof(FiveStarSoftPityStart))
                {
                    Settings.Instance.Gacha.FiveStarSoftPityStart = Math.Max(1, FiveStarSoftPityStart);
                }
                else if (args.PropertyName == nameof(FiveStarHardPity))
                {
                    Settings.Instance.Gacha.FiveStarHardPity = Math.Max(1, FiveStarHardPity);
                }
                else if (args.PropertyName == nameof(FiveStarSoftPityStep))
                {
                    Settings.Instance.Gacha.FiveStarSoftPityStep = Math.Max(0.0, FiveStarSoftPityStep);
                }
                else if (args.PropertyName == nameof(FourStarBaseRate))
                {
                    Settings.Instance.Gacha.FourStarBaseRate = Math.Clamp(FourStarBaseRate, 0.0, 1.0);
                }
                else if (args.PropertyName == nameof(FourStarSoftPityStart))
                {
                    Settings.Instance.Gacha.FourStarSoftPityStart = Math.Max(1, FourStarSoftPityStart);
                }
                else if (args.PropertyName == nameof(FourStarHardPity))
                {
                    Settings.Instance.Gacha.FourStarHardPity = Math.Max(1, FourStarHardPity);
                }
                else if (args.PropertyName == nameof(FourStarSoftPityStep))
                {
                    Settings.Instance.Gacha.FourStarSoftPityStep = Math.Max(0.0, FourStarSoftPityStep);
                }
                else if (args.PropertyName == nameof(FiveStarFeaturedRate))
                {
                    Settings.Instance.Gacha.FiveStarFeaturedRate = Math.Clamp(FiveStarFeaturedRate, 0.0, 1.0);
                }
                else if (args.PropertyName == nameof(FourStarFeaturedRate))
                {
                    Settings.Instance.Gacha.FourStarFeaturedRate = Math.Clamp(FourStarFeaturedRate, 0.0, 1.0);
                }
                else if (args.PropertyName == nameof(GuaranteeListText))
                {
                    Settings.Instance.General.GuaranteeListText = GuaranteeListText;
                    UpdateGuaranteeSummary(_profileService);
                    RefreshPacerList(_profileService);
                }
                else if (args.PropertyName == nameof(GuaranteeWeightList))
                {
                    SaveGuaranteeWeights();
                    UpdateGuaranteeListTextFromRows();
                    UpdateGuaranteeSummary(_profileService);
                    UpdateGuaranteeMemberOptions(_profileService);
                    RefreshPacerList(_profileService);
                }
                else if (args.PropertyName == nameof(LotteryPrizeList))
                {
                    SaveLotteryPrizes();
                    UpdateLotterySummary();
                }
                else if (args.PropertyName == nameof(IsHoverEnable))
                {
                    Settings.Instance.Hover.IsEnable = IsHoverEnable;
                    if (IsHoverEnable)
                    {
                        _plugin.HoverWindow = new HoverFluent();
                        _plugin.HoverWindow.DataContext = IAppHost.GetService<HoverFluentViewModel>();
                        _plugin.HoverWindow.Show();
                    }
                    else _plugin.HoverWindow?.Close();
                }
                else if (args.PropertyName == nameof(HoverScalingFactor))
                {
                    Settings.Instance.Hover.ScalingFactor = HoverScalingFactor;
                }
                else if (args.PropertyName == nameof(ProfileList))
                {
                    ApplyProfileChanges();
                }
            };
            UpdateGuaranteeSummary(_profileService);
            UpdateLotterySummary();
            UpdateGachaSummary(_historyService);
            RefreshHistoryAndStatistics(_historyService);
        }

        private void AttachGuaranteeWeightCollection(ObservableCollection<GuaranteeWeightModel>? collection)
        {
            if (collection == null)
            {
                return;
            }

            collection.CollectionChanged += OnGuaranteeWeightCollectionChanged;
            foreach (var item in collection)
            {
                item.PropertyChanged += OnGuaranteeWeightPropertyChanged;
            }
        }

        private void DetachGuaranteeWeightCollection(ObservableCollection<GuaranteeWeightModel>? collection)
        {
            if (collection == null)
            {
                return;
            }

            collection.CollectionChanged -= OnGuaranteeWeightCollectionChanged;
            foreach (var item in collection)
            {
                item.PropertyChanged -= OnGuaranteeWeightPropertyChanged;
            }
        }

        private void OnGuaranteeWeightCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (GuaranteeWeightModel item in e.NewItems)
                {
                    item.PropertyChanged += OnGuaranteeWeightPropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (GuaranteeWeightModel item in e.OldItems)
                {
                    item.PropertyChanged -= OnGuaranteeWeightPropertyChanged;
                }
            }

            SaveGuaranteeWeights();
            UpdateGuaranteeListTextFromRows();
            UpdateGuaranteeSummary(_profileService);
            UpdateGuaranteeMemberOptions(_profileService);
            RefreshPacerList(_profileService);
            UpdateGachaSummary(_historyService);
        }

        private void OnGuaranteeWeightPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not GuaranteeWeightModel item)
            {
                return;
            }

            if (item.Weight < 0.01)
            {
                item.Weight = 0.01;
                return;
            }

            SaveGuaranteeWeights();
            UpdateGuaranteeListTextFromRows();
            UpdateGuaranteeSummary(_profileService);
            UpdateGuaranteeMemberOptions(_profileService);
            RefreshPacerList(_profileService);
            UpdateGachaSummary(_historyService);
        }

        private void AttachLotteryPrizeCollection(ObservableCollection<LotteryPrizeModel>? collection)
        {
            if (collection == null)
            {
                return;
            }

            collection.CollectionChanged += OnLotteryPrizeCollectionChanged;
            foreach (var item in collection)
            {
                item.PropertyChanged += LotteryPrize_PropertyChanged;
            }
        }

        private void DetachLotteryPrizeCollection(ObservableCollection<LotteryPrizeModel>? collection)
        {
            if (collection == null)
            {
                return;
            }

            collection.CollectionChanged -= OnLotteryPrizeCollectionChanged;
            foreach (var item in collection)
            {
                item.PropertyChanged -= LotteryPrize_PropertyChanged;
            }
        }

        private void OnLotteryPrizeCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (LotteryPrizeModel item in e.NewItems)
                {
                    item.PropertyChanged += LotteryPrize_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (LotteryPrizeModel item in e.OldItems)
                {
                    item.PropertyChanged -= LotteryPrize_PropertyChanged;
                }
            }

            SaveLotteryPrizes();
            UpdateLotterySummary();
        }

        private void AttachProfileCollection(ObservableCollection<StudentModel>? collection)
        {
            if (collection == null)
            {
                return;
            }

            collection.CollectionChanged += OnProfileCollectionChanged;
            foreach (var student in collection)
            {
                student.PropertyChanged += OnProfileStudentChanged;
            }
        }

        private void DetachProfileCollection(ObservableCollection<StudentModel>? collection)
        {
            if (collection == null)
            {
                return;
            }

            collection.CollectionChanged -= OnProfileCollectionChanged;
            foreach (var student in collection)
            {
                student.PropertyChanged -= OnProfileStudentChanged;
            }
        }

        private void OnProfileCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (StudentModel student in e.NewItems)
                {
                    student.PropertyChanged += OnProfileStudentChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (StudentModel student in e.OldItems)
                {
                    student.PropertyChanged -= OnProfileStudentChanged;
                }
            }

            ApplyProfileChanges();
        }

        private void OnProfileStudentChanged(object? sender, PropertyChangedEventArgs e)
        {
            ApplyProfileChanges();
        }

        private void ApplyProfileChanges()
        {
            List<Person> list = BuildProfilePersons();

            _profileService.Members = list;
            _profileService.SaveProfile(CurrentProfile, list);
            _historyService.Load(CurrentProfile);
            _coreService.InitializeCore();
            GuaranteeWeightList = BuildGuaranteeWeightList(_profileService);
            UpdateGuaranteeSummary(_profileService);
            UpdateGuaranteeMemberOptions(_profileService);
            RefreshPacerList(_profileService);
            UpdateGachaSummary(_historyService);
            RefreshHistoryAndStatistics(_historyService);
            UpdateProfileSummary();
        }

        private void UpdateProfileSummary()
        {
            if (ProfileList.Count == 0)
            {
                ProfileSummaryText = "当前名单为空。可以手动添加成员，或通过导入快速开始。";
                return;
            }

            int blankCount = ProfileList.Count(x => string.IsNullOrWhiteSpace(x.Name));
            int duplicateCount = ProfileList
                .Select(x => x.Name?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Sum(g => Math.Max(0, g.Count() - 1));
            int validCount = ProfileList.Count - blankCount;
            int weightedCount = ProfileList.Count(x => x.ManualWeight > 1.0);
            int fiveStarCount = ProfileList.Count(x => x.Rarity == 5);

            List<string> parts =
            [
                $"当前名单共 {ProfileList.Count} 人，可正常参与 {Math.Max(0, validCount)} 人",
                $"五星候选 {fiveStarCount} 人",
                $"手动加权 {weightedCount} 人"
            ];

            if (blankCount > 0)
            {
                parts.Add($"有 {blankCount} 个空白姓名待补全");
            }

            if (duplicateCount > 0)
            {
                parts.Add($"有 {duplicateCount} 条重复姓名，建议合并或改名");
            }

            ProfileSummaryText = string.Join("；", parts) + "。";
        }

        public void AddGuaranteeMember(ProfileService profileService)
        {
            string selected = SelectedGuaranteeMember?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selected))
            {
                return;
            }

            bool exists = GuaranteeWeightList.Any(x => string.Equals(x.Name?.Trim(), selected, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                SelectedGuaranteeMember = null;
                UpdateGuaranteeMemberOptions(profileService);
                return;
            }

            GuaranteeWeightList.Add(new GuaranteeWeightModel
            {
                Name = selected,
                Weight = 1.0
            });

            SelectedGuaranteeMember = null;
            UpdateGuaranteeMemberOptions(profileService);
        }

        public void RefreshHistoryAndStatistics(HistoryService historyService)
        {
            ApplyUsbAuthSnapshot(_usbAuthService.RefreshStatus());
            var snapshot = historyService.GetHistorySnapshot().OrderByDescending(x => x.LongTermCount).ThenBy(x => x.Name).ToList();
            var totalLongTerm = historyService.GetTotalLongTermCallCount();
            var average = historyService.GetAverageLongTermCount();
            var recent = historyService.GetRecentCalls(20);
            var pityState = historyService.GetGachaPityState();

            HistoryList.Clear();
            foreach (var item in snapshot)
            {
                HistoryList.Add(new HistoryItem
                {
                    Name = item.Name,
                    LongTermCount = item.LongTermCount,
                    SessionMissCount = item.SessionMissCount,
                    LastCallText = item.LastCallIndex < 0 ? "未出现" : (item.LastCallIndex + 1).ToString()
                });
            }

            StatisticsList.Clear();
            StatisticsList.Add(new StatisticsItem { Metric = "总点名次数（长期）", Value = totalLongTerm.ToString() });
            StatisticsList.Add(new StatisticsItem { Metric = "全班长期平均点名次数", Value = average.ToString("F2") });
            StatisticsList.Add(new StatisticsItem { Metric = "保底模式", Value = GetGachaModeStatusText() });
            StatisticsList.Add(new StatisticsItem { Metric = "U盘验证", Value = UsbAuthStatusSummary });
            StatisticsList.Add(new StatisticsItem { Metric = "五星水位", Value = pityState.FiveStarPity.ToString() });
            StatisticsList.Add(new StatisticsItem { Metric = "四星水位", Value = pityState.FourStarPity.ToString() });
            StatisticsList.Add(new StatisticsItem { Metric = "五星大保底", Value = pityState.IsFiveStarFeaturedGuaranteed ? "是" : "否" });
            StatisticsList.Add(new StatisticsItem { Metric = "四星大保底", Value = pityState.IsFourStarFeaturedGuaranteed ? "是" : "否" });
            StatisticsList.Add(new StatisticsItem { Metric = "捕获明光次数", Value = pityState.CapturedRadianceCount.ToString() });
            StatisticsList.Add(new StatisticsItem { Metric = "当前名单人数", Value = (ProfileList?.Count ?? 0).ToString() });

            RecentCallList.Clear();
            int index = 1;
            foreach (var name in recent)
            {
                RecentCallList.Add(new RecentCallItem
                {
                    Index = index++,
                    Name = name
                });
            }
        }

        private void UpdateGachaSummary(HistoryService historyService)
        {
            var pityState = historyService.GetGachaPityState();
            int fiveStarCount = ProfileList.Count(x => x.Rarity == 5);
            int nonFiveStarCount = ProfileList.Count - fiveStarCount;
            string featuredFiveStar = string.IsNullOrWhiteSpace(pityState.FeaturedFiveStarName) ? "未生成" : pityState.FeaturedFiveStarName;
            string featuredFourStars = pityState.FeaturedFourStarNames.Count == 0 ? "未生成" : string.Join("、", pityState.FeaturedFourStarNames);
            string gachaModeStatus = GetGachaModeStatusText();

            GachaSummaryText =
                $"保底模式：{gachaModeStatus}；" +
                $"U盘验证：{UsbAuthStatusSummary}；" +
                $"五星候选 {fiveStarCount} 人，非五星 {nonFiveStarCount} 人；" +
                $"今日五星 UP：{featuredFiveStar}；今日四星 UP：{featuredFourStars}；" +
                $"当前水位：五星 {pityState.FiveStarPity}/{Math.Max(1, FiveStarHardPity)}，四星 {pityState.FourStarPity}/{Math.Max(1, FourStarHardPity)}；" +
                $"五星大保底={(pityState.IsFiveStarFeaturedGuaranteed ? "是" : "否")}，四星大保底={(pityState.IsFourStarFeaturedGuaranteed ? "是" : "否")}；" +
                $"五星小保底歪后额外有 {0.1:P0} 概率触发捕获明光，累计触发 {pityState.CapturedRadianceCount} 次。";
        }

        private void RefreshUsbAuthSection(bool refreshHistory = true)
        {
            var drives = _usbAuthService.GetRemovableDrives(forceRefresh: true)
                .Select(x => new UsbDriveItemModel(x))
                .ToList();
            var snapshot = _usbAuthService.RefreshStatus(forceRefresh: true);
            string? selectedRoot = SelectedDrive?.RootPath;

            RemovableDrives = new ObservableCollection<UsbDriveItemModel>(drives);
            SelectedDrive = RemovableDrives.FirstOrDefault(x => string.Equals(x.RootPath, selectedRoot, StringComparison.OrdinalIgnoreCase))
                ?? RemovableDrives.FirstOrDefault();

            LoadAuthFileNameFromSettings();
            ApplyUsbAuthSnapshot(snapshot);
            KeyFingerprint = _usbAuthProvisioningService.GetKeyFingerprint();
            RefreshUsbAuthComputedState(snapshot);
            UsbAuthActionMessage = string.IsNullOrWhiteSpace(UsbAuthActionMessage) ? snapshot.Detail : UsbAuthActionMessage;

            if (!refreshHistory)
            {
                return;
            }

            UpdateGachaSummary(_historyService);
            RefreshHistoryAndStatistics(_historyService);
        }

        private void OnUsbAuthStatusChanged(UsbAuthSnapshot snapshot)
        {
            ApplyUsbAuthSnapshot(snapshot);
            RefreshUsbAuthComputedState(snapshot);
            UpdateGachaSummary(_historyService);
        }

        private void ApplyUsbAuthSnapshot(UsbAuthSnapshot snapshot)
        {
            IsUsbAuthEnabled = Settings.Instance.UsbAuth.Enabled;
            IsUsbAuthVerified = snapshot.IsVerified;
            UsbAuthStatusSummary = snapshot.Summary;
            UsbAuthStatusText = snapshot.Detail;
            UpdateUsbAuthGuide();
            RefreshProtectedSettingsLockState(snapshot);
        }

        private void RefreshProtectedSettingsLockState(UsbAuthSnapshot? snapshot = null)
        {
            var currentSnapshot = snapshot ?? _usbAuthService.RefreshStatus();
            bool locked = SettingsWriteGate.IsProtectionActive() && !currentSnapshot.IsVerified;
            CanEditProtectedSettings = !locked;
            IsProtectedSettingsLocked = locked;
            ProtectedSettingsLockText = locked
                ? "已启用U盘验证，当前未检测到已授权U盘。除当前页中的“U盘验证”区域外，其它配置现已锁定为只读。"
                : string.Empty;
        }

        private void LoadAuthFileNameFromSettings()
        {
            string normalized = UsbAuthService.NormalizeAuthFileName(Settings.Instance.UsbAuth.AuthFileName);
            this.RaiseAndSetIfChanged(ref _authFileName, normalized, nameof(AuthFileName));
        }

        private void RefreshUsbAuthComputedState(UsbAuthSnapshot? snapshot = null)
        {
            var currentSnapshot = snapshot ?? _usbAuthService.RefreshStatus();
            CanModifyUsbAuthConfiguration = _usbAuthProvisioningService.CanManageProvisioning(currentSnapshot);
            CanWriteAuthorization = SelectedDrive != null && CanModifyUsbAuthConfiguration;
            CanDisableProtection = !SettingsWriteGate.IsProtectionActive() || currentSnapshot.IsVerified;
            CanRegenerateKeyPair = CanModifyUsbAuthConfiguration;
            WriteButtonText = Settings.Instance.UsbAuth.Enabled ? "重写授权文件" : "写入授权并启用";
            SelectedDriveHint = SelectedDrive == null
                ? "请先插入U盘，检测到可移动磁盘后写入按钮会自动解锁。"
                : $"当前目标：{SelectedDrive.DisplayName}，授权文件将写入 {SelectedDrive.DriveInfo.AuthFilePath}";
        }

        private void UpdateUsbAuthGuide()
        {
            List<string> steps =
            [
                "使用说明：",
                "1. 插入需要授权的U盘。",
                $"2. 确认目标U盘后，点击“{WriteButtonText}”。",
                "3. 插件会自动生成密钥对，使用私钥签发授权文件，并将加密后的授权文件写入U盘根目录。",
                "4. 运行期插件只保留公钥进行验签；当U盘拔出后，其它设置区会自动切换为只读。"
            ];

            if (Settings.Instance.UsbAuth.Enabled)
            {
                steps.Add("5. 当前已启用保护。若要关闭或重写授权文件，请先插入已授权U盘。");
            }
            else
            {
                steps.Add("5. 当前尚未启用保护。首次写入成功后会自动开启U盘验证。");
            }

            UsbAuthGuideText = string.Join(Environment.NewLine, steps);
        }

        private string GetGachaModeStatusText()
        {
            if (!IsGachaEnabled)
            {
                return "关闭";
            }

            if (IsUsbAuthEnabled && !IsUsbAuthVerified)
            {
                return "未生效（等待验证）";
            }

            return "开启";
        }

        private void UpdateGuaranteeSummary(ProfileService profileService)
        {
            var tokens = GuaranteeWeightList
                .Select(x => x.Name?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tokens.Count == 0)
            {
                GuaranteeSummaryText = "保底名单为空，当前不会触发保底命中。";
                return;
            }

            var memberSet = profileService.Members
                .Select(x => x.Name.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int matched = tokens.Count(x => memberSet.Contains(x));
            var missing = tokens.Where(x => !memberSet.Contains(x)).ToList();

            if (missing.Count == 0)
            {
                GuaranteeSummaryText = $"保底名单共 {tokens.Count} 人，全部已匹配当前档案。";
                return;
            }

            GuaranteeSummaryText = $"保底名单共 {tokens.Count} 人，已匹配 {matched} 人，未匹配 {missing.Count} 人：{string.Join("、", missing)}";
        }

        private ObservableCollection<GuaranteeWeightModel> BuildGuaranteeWeightList(ProfileService profileService)
        {
            var currentMembers = profileService.Members.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fromSettings = ParseGuaranteeWeightJson(Settings.Instance.General.GuaranteeWeightListJson);

            if (fromSettings.Count == 0)
            {
                fromSettings = ParseNameTokens(Settings.Instance.General.GuaranteeListText)
                    .Select(x => new GuaranteeWeightModel { Name = x, Weight = 1.0 })
                    .ToList();
            }

            var cleaned = fromSettings
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new GuaranteeWeightModel
                {
                    Name = g.First().Name.Trim(),
                    Weight = Math.Max(0.01, g.Last().Weight)
                })
                .OrderByDescending(x => currentMembers.Contains(x.Name))
                .ThenBy(x => x.Name)
                .ToList();

            SaveGuaranteeWeights(cleaned);
            return new ObservableCollection<GuaranteeWeightModel>(cleaned);
        }

        private static List<GuaranteeWeightModel> ParseGuaranteeWeightJson(string raw)
        {
            try
            {
                return JsonSerializer.Deserialize<List<GuaranteeWeightModel>>(raw ?? "[]") ?? [];
            }
            catch
            {
                return [];
            }
        }

        private void SaveGuaranteeWeights(List<GuaranteeWeightModel>? source = null)
        {
            var list = (source ?? GuaranteeWeightList.ToList())
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new GuaranteeWeightModel
                {
                    Name = x.Name.Trim(),
                    Weight = Math.Max(0.01, x.Weight)
                })
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToList();

            Settings.Instance.General.GuaranteeWeightListJson = JsonSerializer.Serialize(list);
        }

        private ObservableCollection<LotteryPrizeModel> BuildLotteryPrizeList(LotteryService lotteryService)
        {
            var items = lotteryService.GetConfiguredPrizes()
                .Select(x => new LotteryPrizeModel
                {
                    Name = x.Name,
                    WinnerCount = x.WinnerCount
                })
                .ToList();

            return new ObservableCollection<LotteryPrizeModel>(items);
        }

        private void SaveLotteryPrizes()
        {
            var list = LotteryService.NormalizePrizeItems(LotteryPrizeList.Select(x => new LotteryPrizeItem
            {
                Name = x.Name,
                WinnerCount = x.WinnerCount
            }));

            Settings.Instance.General.LotteryPrizeListJson = JsonSerializer.Serialize(list);
        }

        private void UpdateLotterySummary()
        {
            var list = LotteryPrizeList
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => $"{x.Name.Trim()} x{x.WinnerCount}")
                .ToList();

            LotterySummaryText = list.Count == 0
                ? "抽奖预设为空。请先添加奖项和人数，或通过 URI 传入 prize/count。"
                : $"当前已配置 {list.Count} 个奖项：{string.Join("；", list)}。";
        }

        private void LotteryPrize_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not LotteryPrizeModel prize)
            {
                return;
            }

            if (prize.WinnerCount < 1)
            {
                prize.WinnerCount = 1;
                return;
            }

            SaveLotteryPrizes();
            UpdateLotterySummary();
        }

        private void UpdateGuaranteeMemberOptions(ProfileService profileService)
        {
            var selectedNames = GuaranteeWeightList
                .Select(x => x.Name?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var options = profileService.Members
                .Select(x => x.Name?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(x => !selectedNames.Contains(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            GuaranteeMemberOptions = new ObservableCollection<string>(options);

            if (!string.IsNullOrWhiteSpace(SelectedGuaranteeMember) &&
                !GuaranteeMemberOptions.Any(x => string.Equals(x, SelectedGuaranteeMember, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedGuaranteeMember = null;
            }
        }

        private void RefreshPacerList(ProfileService profileService)
        {
            var guaranteeNames = GuaranteeWeightList
                .Select(x => x.Name?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int targetCount = Math.Max(2, (int)Math.Floor(guaranteeNames.Count * 1.5));

            var candidateNames = profileService.Members
                .Select(x => x.Name?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(x => !guaranteeNames.Contains(x, StringComparer.OrdinalIgnoreCase))
                .ToList();

            targetCount = Math.Min(targetCount, candidateNames.Count);
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            var pacerListFromSettings = ParsePacerListJson(Settings.Instance.General.PacerListJson);
            bool needRegenerate = Settings.Instance.General.PacerListDate != today
                || pacerListFromSettings.Count != targetCount
                || pacerListFromSettings.Any(x => guaranteeNames.Contains(x, StringComparer.OrdinalIgnoreCase))
                || pacerListFromSettings.Any(x => !candidateNames.Contains(x, StringComparer.OrdinalIgnoreCase));

            List<string> pacerList;
            if (needRegenerate)
            {
                pacerList = PickRandomNames(candidateNames, targetCount);
                Settings.Instance.General.PacerListJson = JsonSerializer.Serialize(pacerList);
                Settings.Instance.General.PacerListDate = today;
            }
            else
            {
                pacerList = pacerListFromSettings;
            }

            PacerList = new ObservableCollection<string>(pacerList);

            if (candidateNames.Count == 0)
            {
                PacerSummaryText = "陪跑名单：当前无可选成员（已全部在保底名单或无成员）。";
            }
            else
            {
                PacerSummaryText = $"陪跑名单：每日随机生成，共 {pacerList.Count} 人（规则：floor(保底人数×1.5)，最少2人）；陪跑成员有小幅权重提升，陪跑保底阈值={Math.Max(1, PacerThreshold)}。";
            }
        }

        private static List<string> ParsePacerListJson(string raw)
        {
            try
            {
                return (JsonSerializer.Deserialize<List<string>>(raw ?? "[]") ?? [])
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        private static List<string> PickRandomNames(List<string> source, int count)
        {
            if (count <= 0 || source.Count == 0)
            {
                return [];
            }

            var list = source
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            return list.Take(Math.Min(count, list.Count)).ToList();
        }

        private List<Person> BuildProfilePersons()
        {
            return ProfileList
                .Select(s => new Person
                {
                    Id = s.ID,
                    Name = s.Name?.Trim() ?? string.Empty,
                    Gender = s.Gender,
                    ManualWeight = Math.Max(0.01, s.ManualWeight),
                    Rarity = Enum.IsDefined(typeof(GachaRarity), s.Rarity) ? (GachaRarity)s.Rarity : GachaRarity.ThreeStar,
                    IsFeatured = s.IsFeatured
                })
                .ToList();
        }

        private void UpdateGuaranteeListTextFromRows()
        {
            GuaranteeListText = string.Join(",", GuaranteeWeightList
                .Select(x => x.Name?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static List<string> ParseNameTokens(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            return raw
                .Split([',', '，', '\n', '\r', ' ', '\t', ';', '；', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

    }
}
