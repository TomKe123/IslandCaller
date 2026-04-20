using System.ComponentModel;
using IslandCaller.Services;
using static System.Guid;

namespace IslandCaller.Models
{
    public class SettingsModel
    {
        public GeneralSetting General { get; set; } = new();
        public GachaSetting Gacha { get; set; } = new();
        public UsbAuthSetting UsbAuth { get; set; } = new();
        public ProfileSetting Profile { get; set; } = new();
        public HoverSetting Hover { get; set; } = new();
    }

    public enum DrawSelectionScope
    {
        All = 0,
        Male = 1,
        Female = 2
    }

    public enum DrawSelectionAlgorithm
    {
        Balanced = 0,
        PureRandom = 1
    }

    public enum LessonDrawScopeOption
    {
        FollowMain = -1,
        All = 0,
        Male = 1,
        Female = 2
    }

    public enum LessonDrawAlgorithmOption
    {
        FollowMain = -1,
        Balanced = 0,
        PureRandom = 1
    }

    public abstract class ProtectedSettingsObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProtectedField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            if (!SettingsWriteGate.CanModifyProtectedSettings())
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class GeneralSetting : ProtectedSettingsObject
    {
        public GeneralSetting()
        {
            _version = new Version(2, 0, 0, 0);
            _breakDisable = true;
            _enableGlobalHotkeys = true;
            _quickCallHotkey = "Ctrl+Alt+R";
            _advancedCallHotkey = "Ctrl+Alt+G";
            _enableGuarantee = false;
            _guaranteeThreshold = 40;
            _guaranteeListText = string.Empty;
            _guaranteeWeightListJson = "[]";
            _lotteryPrizeListJson = "[]";
            _pacerListJson = "[]";
            _pacerListDate = string.Empty;
            _pacerThreshold = 50;
            _defaultDrawScope = DrawSelectionScope.All;
            _defaultDrawAlgorithm = DrawSelectionAlgorithm.Balanced;
        }

        private readonly Version _version;
        public Version Version => _version;

        private bool _breakDisable;
        public bool BreakDisable
        {
            get => _breakDisable;
            set => SetProtectedField(ref _breakDisable, value, nameof(BreakDisable));
        }

        private bool _enableGlobalHotkeys;
        public bool EnableGlobalHotkeys
        {
            get => _enableGlobalHotkeys;
            set => SetProtectedField(ref _enableGlobalHotkeys, value, nameof(EnableGlobalHotkeys));
        }

        private string _quickCallHotkey;
        public string QuickCallHotkey
        {
            get => _quickCallHotkey;
            set => SetProtectedField(ref _quickCallHotkey, value, nameof(QuickCallHotkey));
        }

        private string _advancedCallHotkey;
        public string AdvancedCallHotkey
        {
            get => _advancedCallHotkey;
            set => SetProtectedField(ref _advancedCallHotkey, value, nameof(AdvancedCallHotkey));
        }

        private bool _enableGuarantee;
        public bool EnableGuarantee
        {
            get => _enableGuarantee;
            set => SetProtectedField(ref _enableGuarantee, value, nameof(EnableGuarantee));
        }

        private int _guaranteeThreshold;
        public int GuaranteeThreshold
        {
            get => _guaranteeThreshold;
            set => SetProtectedField(ref _guaranteeThreshold, value, nameof(GuaranteeThreshold));
        }

        private string _guaranteeListText;
        public string GuaranteeListText
        {
            get => _guaranteeListText;
            set => SetProtectedField(ref _guaranteeListText, value, nameof(GuaranteeListText));
        }

        private string _guaranteeWeightListJson;
        public string GuaranteeWeightListJson
        {
            get => _guaranteeWeightListJson;
            set => SetProtectedField(ref _guaranteeWeightListJson, value, nameof(GuaranteeWeightListJson));
        }

        private string _lotteryPrizeListJson;
        public string LotteryPrizeListJson
        {
            get => _lotteryPrizeListJson;
            set => SetProtectedField(ref _lotteryPrizeListJson, value, nameof(LotteryPrizeListJson));
        }

        private string _pacerListJson;
        public string PacerListJson
        {
            get => _pacerListJson;
            set => SetProtectedField(ref _pacerListJson, value, nameof(PacerListJson));
        }

        private string _pacerListDate;
        public string PacerListDate
        {
            get => _pacerListDate;
            set => SetProtectedField(ref _pacerListDate, value, nameof(PacerListDate));
        }

        private int _pacerThreshold;
        public int PacerThreshold
        {
            get => _pacerThreshold;
            set => SetProtectedField(ref _pacerThreshold, value, nameof(PacerThreshold));
        }

        private DrawSelectionScope _defaultDrawScope;
        public DrawSelectionScope DefaultDrawScope
        {
            get => _defaultDrawScope;
            set => SetProtectedField(ref _defaultDrawScope, value, nameof(DefaultDrawScope));
        }

        private DrawSelectionAlgorithm _defaultDrawAlgorithm;
        public DrawSelectionAlgorithm DefaultDrawAlgorithm
        {
            get => _defaultDrawAlgorithm;
            set => SetProtectedField(ref _defaultDrawAlgorithm, value, nameof(DefaultDrawAlgorithm));
        }
    }

    public class GachaSetting : ProtectedSettingsObject
    {
        public GachaSetting()
        {
            _enabled = false;
            _requireUsbAuth = false;
            _usbAuthFileName = "IslandCaller.auth";
            _usbAuthToken = string.Empty;
            _fiveStarBaseRate = 0.006;
            _fiveStarSoftPityStart = 74;
            _fiveStarHardPity = 90;
            _fiveStarSoftPityStep = 0.06;
            _fourStarBaseRate = 0.051;
            _fourStarSoftPityStart = 9;
            _fourStarHardPity = 10;
            _fourStarSoftPityStep = 0.225;
            _fiveStarFeaturedRate = 0.5;
            _fourStarFeaturedRate = 0.5;
        }

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set => SetProtectedField(ref _enabled, value, nameof(Enabled));
        }

        // Legacy fields kept for migration from the token-based USB auth scheme.
        private bool _requireUsbAuth;
        public bool RequireUsbAuth
        {
            get => _requireUsbAuth;
            set => SetProtectedField(ref _requireUsbAuth, value, nameof(RequireUsbAuth));
        }

        private string _usbAuthFileName;
        public string UsbAuthFileName
        {
            get => _usbAuthFileName;
            set => SetProtectedField(ref _usbAuthFileName, value, nameof(UsbAuthFileName));
        }

        private string _usbAuthToken;
        public string UsbAuthToken
        {
            get => _usbAuthToken;
            set => SetProtectedField(ref _usbAuthToken, value, nameof(UsbAuthToken));
        }

        private double _fiveStarBaseRate;
        public double FiveStarBaseRate
        {
            get => _fiveStarBaseRate;
            set => SetProtectedField(ref _fiveStarBaseRate, value, nameof(FiveStarBaseRate));
        }

        private int _fiveStarSoftPityStart;
        public int FiveStarSoftPityStart
        {
            get => _fiveStarSoftPityStart;
            set => SetProtectedField(ref _fiveStarSoftPityStart, value, nameof(FiveStarSoftPityStart));
        }

        private int _fiveStarHardPity;
        public int FiveStarHardPity
        {
            get => _fiveStarHardPity;
            set => SetProtectedField(ref _fiveStarHardPity, value, nameof(FiveStarHardPity));
        }

        private double _fiveStarSoftPityStep;
        public double FiveStarSoftPityStep
        {
            get => _fiveStarSoftPityStep;
            set => SetProtectedField(ref _fiveStarSoftPityStep, value, nameof(FiveStarSoftPityStep));
        }

        private double _fourStarBaseRate;
        public double FourStarBaseRate
        {
            get => _fourStarBaseRate;
            set => SetProtectedField(ref _fourStarBaseRate, value, nameof(FourStarBaseRate));
        }

        private int _fourStarSoftPityStart;
        public int FourStarSoftPityStart
        {
            get => _fourStarSoftPityStart;
            set => SetProtectedField(ref _fourStarSoftPityStart, value, nameof(FourStarSoftPityStart));
        }

        private int _fourStarHardPity;
        public int FourStarHardPity
        {
            get => _fourStarHardPity;
            set => SetProtectedField(ref _fourStarHardPity, value, nameof(FourStarHardPity));
        }

        private double _fourStarSoftPityStep;
        public double FourStarSoftPityStep
        {
            get => _fourStarSoftPityStep;
            set => SetProtectedField(ref _fourStarSoftPityStep, value, nameof(FourStarSoftPityStep));
        }

        private double _fiveStarFeaturedRate;
        public double FiveStarFeaturedRate
        {
            get => _fiveStarFeaturedRate;
            set => SetProtectedField(ref _fiveStarFeaturedRate, value, nameof(FiveStarFeaturedRate));
        }

        private double _fourStarFeaturedRate;
        public double FourStarFeaturedRate
        {
            get => _fourStarFeaturedRate;
            set => SetProtectedField(ref _fourStarFeaturedRate, value, nameof(FourStarFeaturedRate));
        }
    }

    public class UsbAuthSetting : ProtectedSettingsObject
    {
        public UsbAuthSetting()
        {
            _enabled = false;
            _authFileName = "IslandCaller.auth";
            _publicKey = string.Empty;
            _protectedPrivateKey = string.Empty;
        }

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set => SetProtectedField(ref _enabled, value, nameof(Enabled));
        }

        private string _authFileName;
        public string AuthFileName
        {
            get => _authFileName;
            set => SetProtectedField(ref _authFileName, value, nameof(AuthFileName));
        }

        private string _publicKey;
        public string PublicKey
        {
            get => _publicKey;
            set => SetProtectedField(ref _publicKey, value, nameof(PublicKey));
        }

        private string _protectedPrivateKey;
        public string ProtectedPrivateKey
        {
            get => _protectedPrivateKey;
            set => SetProtectedField(ref _protectedPrivateKey, value, nameof(ProtectedPrivateKey));
        }
    }

    public class ProfileSetting : ProtectedSettingsObject
    {
        public ProfileSetting()
        {
            _profileNum = 1;
            _defaultProfile = NewGuid();
            _profileList.Add(_defaultProfile, "Default");
            _isPreferProfile = false;
        }

        private int _profileNum;
        public int ProfileNum
        {
            get => _profileNum;
            set => SetProtectedField(ref _profileNum, value, nameof(ProfileNum));
        }

        private Guid _defaultProfile;
        public Guid DefaultProfile
        {
            get => _defaultProfile;
            set => SetProtectedField(ref _defaultProfile, value, nameof(DefaultProfile));
        }

        private Dictionary<Guid, string> _profileList = new();
        public Dictionary<Guid, string> ProfileList
        {
            get => _profileList;
            set => SetProtectedField(ref _profileList, value, nameof(ProfileList));
        }

        private Dictionary<Guid, string> _profilePrefer = new();
        public Dictionary<Guid, string> ProfilePrefer
        {
            get => _profilePrefer;
            set => SetProtectedField(ref _profilePrefer, value, nameof(ProfilePrefer));
        }

        private bool _isPreferProfile;
        public bool IsPreferProfile
        {
            get => _isPreferProfile;
            set => SetProtectedField(ref _isPreferProfile, value, nameof(IsPreferProfile));
        }
    }

    public class HoverSetting : ProtectedSettingsObject
    {
        public HoverSetting()
        {
            _isEnable = true;
            _scalingFactor = 1.0;
        }

        private bool _isEnable;
        public bool IsEnable
        {
            get => _isEnable;
            set => SetProtectedField(ref _isEnable, value, nameof(IsEnable));
        }

        private double _scalingFactor;
        public double ScalingFactor
        {
            get => _scalingFactor;
            set
            {
                var normalized = Math.Clamp(value, 0.5, 2.0);
                SetProtectedField(ref _scalingFactor, normalized, nameof(ScalingFactor));
            }
        }

        public PositionSetting Position { get; set; } = new();
    }

    public class PositionSetting : ProtectedSettingsObject
    {
        public PositionSetting()
        {
            _x = 200.0;
            _y = 200.0;
        }

        private double _x;
        public double X
        {
            get => _x;
            set => SetProtectedField(ref _x, value, nameof(X));
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProtectedField(ref _y, value, nameof(Y));
        }
    }
}
