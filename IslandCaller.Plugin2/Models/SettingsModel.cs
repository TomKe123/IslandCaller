using static System.Guid;
using System.ComponentModel;

namespace IslandCaller.Models
{
    public class SettingsModel
    {
        public GeneralSetting General { get; set; } = new GeneralSetting();
        public GachaSetting Gacha { get; set; } = new GachaSetting();
        public ProfileSetting Profile { get; set; } = new ProfileSetting();
        public HoverSetting Hover { get; set; } = new HoverSetting();
    }

    public class GeneralSetting : INotifyPropertyChanged
    {
        public GeneralSetting()
        {
            _version = new Version(2, 0, 0, 0);
            _breakdisable = true;
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
        }

        private Version _version;
        public Version Version
        {
            get => _version;
        }

        private bool _breakdisable;
        public bool BreakDisable
        {
            get => _breakdisable;
            set { if (_breakdisable != value) { _breakdisable = value; OnPropertyChanged(nameof(BreakDisable)); } }
        }

        private bool _enableGlobalHotkeys;
        public bool EnableGlobalHotkeys
        {
            get => _enableGlobalHotkeys;
            set { if (_enableGlobalHotkeys != value) { _enableGlobalHotkeys = value; OnPropertyChanged(nameof(EnableGlobalHotkeys)); } }
        }

        private string _quickCallHotkey;
        public string QuickCallHotkey
        {
            get => _quickCallHotkey;
            set { if (_quickCallHotkey != value) { _quickCallHotkey = value; OnPropertyChanged(nameof(QuickCallHotkey)); } }
        }

        private string _advancedCallHotkey;
        public string AdvancedCallHotkey
        {
            get => _advancedCallHotkey;
            set { if (_advancedCallHotkey != value) { _advancedCallHotkey = value; OnPropertyChanged(nameof(AdvancedCallHotkey)); } }
        }

        private bool _enableGuarantee;
        public bool EnableGuarantee
        {
            get => _enableGuarantee;
            set { if (_enableGuarantee != value) { _enableGuarantee = value; OnPropertyChanged(nameof(EnableGuarantee)); } }
        }

        private int _guaranteeThreshold;
        public int GuaranteeThreshold
        {
            get => _guaranteeThreshold;
            set { if (_guaranteeThreshold != value) { _guaranteeThreshold = value; OnPropertyChanged(nameof(GuaranteeThreshold)); } }
        }

        private string _guaranteeListText;
        public string GuaranteeListText
        {
            get => _guaranteeListText;
            set { if (_guaranteeListText != value) { _guaranteeListText = value; OnPropertyChanged(nameof(GuaranteeListText)); } }
        }

        private string _guaranteeWeightListJson;
        public string GuaranteeWeightListJson
        {
            get => _guaranteeWeightListJson;
            set { if (_guaranteeWeightListJson != value) { _guaranteeWeightListJson = value; OnPropertyChanged(nameof(GuaranteeWeightListJson)); } }
        }

        private string _lotteryPrizeListJson;
        public string LotteryPrizeListJson
        {
            get => _lotteryPrizeListJson;
            set { if (_lotteryPrizeListJson != value) { _lotteryPrizeListJson = value; OnPropertyChanged(nameof(LotteryPrizeListJson)); } }
        }

        private string _pacerListJson;
        public string PacerListJson
        {
            get => _pacerListJson;
            set { if (_pacerListJson != value) { _pacerListJson = value; OnPropertyChanged(nameof(PacerListJson)); } }
        }

        private string _pacerListDate;
        public string PacerListDate
        {
            get => _pacerListDate;
            set { if (_pacerListDate != value) { _pacerListDate = value; OnPropertyChanged(nameof(PacerListDate)); } }
        }

        private int _pacerThreshold;
        public int PacerThreshold
        {
            get => _pacerThreshold;
            set { if (_pacerThreshold != value) { _pacerThreshold = value; OnPropertyChanged(nameof(PacerThreshold)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class GachaSetting : INotifyPropertyChanged
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
            set { if (_enabled != value) { _enabled = value; OnPropertyChanged(nameof(Enabled)); } }
        }

        private bool _requireUsbAuth;
        public bool RequireUsbAuth
        {
            get => _requireUsbAuth;
            set { if (_requireUsbAuth != value) { _requireUsbAuth = value; OnPropertyChanged(nameof(RequireUsbAuth)); } }
        }

        private string _usbAuthFileName;
        public string UsbAuthFileName
        {
            get => _usbAuthFileName;
            set { if (_usbAuthFileName != value) { _usbAuthFileName = value; OnPropertyChanged(nameof(UsbAuthFileName)); } }
        }

        private string _usbAuthToken;
        public string UsbAuthToken
        {
            get => _usbAuthToken;
            set { if (_usbAuthToken != value) { _usbAuthToken = value; OnPropertyChanged(nameof(UsbAuthToken)); } }
        }

        private double _fiveStarBaseRate;
        public double FiveStarBaseRate
        {
            get => _fiveStarBaseRate;
            set { if (_fiveStarBaseRate != value) { _fiveStarBaseRate = value; OnPropertyChanged(nameof(FiveStarBaseRate)); } }
        }

        private int _fiveStarSoftPityStart;
        public int FiveStarSoftPityStart
        {
            get => _fiveStarSoftPityStart;
            set { if (_fiveStarSoftPityStart != value) { _fiveStarSoftPityStart = value; OnPropertyChanged(nameof(FiveStarSoftPityStart)); } }
        }

        private int _fiveStarHardPity;
        public int FiveStarHardPity
        {
            get => _fiveStarHardPity;
            set { if (_fiveStarHardPity != value) { _fiveStarHardPity = value; OnPropertyChanged(nameof(FiveStarHardPity)); } }
        }

        private double _fiveStarSoftPityStep;
        public double FiveStarSoftPityStep
        {
            get => _fiveStarSoftPityStep;
            set { if (_fiveStarSoftPityStep != value) { _fiveStarSoftPityStep = value; OnPropertyChanged(nameof(FiveStarSoftPityStep)); } }
        }

        private double _fourStarBaseRate;
        public double FourStarBaseRate
        {
            get => _fourStarBaseRate;
            set { if (_fourStarBaseRate != value) { _fourStarBaseRate = value; OnPropertyChanged(nameof(FourStarBaseRate)); } }
        }

        private int _fourStarSoftPityStart;
        public int FourStarSoftPityStart
        {
            get => _fourStarSoftPityStart;
            set { if (_fourStarSoftPityStart != value) { _fourStarSoftPityStart = value; OnPropertyChanged(nameof(FourStarSoftPityStart)); } }
        }

        private int _fourStarHardPity;
        public int FourStarHardPity
        {
            get => _fourStarHardPity;
            set { if (_fourStarHardPity != value) { _fourStarHardPity = value; OnPropertyChanged(nameof(FourStarHardPity)); } }
        }

        private double _fourStarSoftPityStep;
        public double FourStarSoftPityStep
        {
            get => _fourStarSoftPityStep;
            set { if (_fourStarSoftPityStep != value) { _fourStarSoftPityStep = value; OnPropertyChanged(nameof(FourStarSoftPityStep)); } }
        }

        private double _fiveStarFeaturedRate;
        public double FiveStarFeaturedRate
        {
            get => _fiveStarFeaturedRate;
            set { if (_fiveStarFeaturedRate != value) { _fiveStarFeaturedRate = value; OnPropertyChanged(nameof(FiveStarFeaturedRate)); } }
        }

        private double _fourStarFeaturedRate;
        public double FourStarFeaturedRate
        {
            get => _fourStarFeaturedRate;
            set { if (_fourStarFeaturedRate != value) { _fourStarFeaturedRate = value; OnPropertyChanged(nameof(FourStarFeaturedRate)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ProfileSetting : INotifyPropertyChanged
    {
        public ProfileSetting()
        {
            _profilenum = 1;
            _defaultprofile = NewGuid();
            _profilelist.Add(_defaultprofile, "Default");
            _ispreferprofile = false;
        }

        private int _profilenum;
        public int ProfileNum
        {
            get => _profilenum;
            set { if (_profilenum != value) { _profilenum = value; OnPropertyChanged(nameof(ProfileNum)); } }
        }

        private Guid _defaultprofile;
        public Guid DefaultProfile
        {
            get => _defaultprofile;
            set { if (_defaultprofile != value) { _defaultprofile = value; OnPropertyChanged(nameof(DefaultProfile)); } }
        }

        private Dictionary<Guid, string> _profilelist = new Dictionary<Guid, string>();
        public Dictionary<Guid, string> ProfileList
        {
            get => _profilelist;
            set { if (_profilelist != value) { _profilelist = value; OnPropertyChanged(nameof(ProfileList)); } }
        }
        private Dictionary<Guid, string> _profileprefer = new Dictionary<Guid, string>();

        private bool _ispreferprofile;
        public bool IsPreferProfile
        {
            get => _ispreferprofile;
            set { if (_ispreferprofile != value) { _ispreferprofile = value; OnPropertyChanged(nameof(IsPreferProfile)); } }
        }
        public Dictionary<Guid, string> ProfilePrefer
        {
            get => _profileprefer;
            set { if (_profileprefer != value) { _profileprefer = value; OnPropertyChanged(nameof(ProfilePrefer)); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class HoverSetting : INotifyPropertyChanged
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
            set { if (_isEnable != value) { _isEnable = value; OnPropertyChanged(nameof(IsEnable)); } }
        }

        private double _scalingFactor;

        public double ScalingFactor
        {
            get => _scalingFactor;
            set
            {
                var normalized = Math.Clamp(value, 0.5, 2.0);
                if (_scalingFactor != normalized)
                {
                    _scalingFactor = normalized;
                    OnPropertyChanged(nameof(ScalingFactor));
                }
            }
        }

        public PositionSetting Position { get; set; } = new PositionSetting();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PositionSetting : INotifyPropertyChanged
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
            set { if (_x != value) { _x = value; OnPropertyChanged(nameof(X)); } }
        }

        private double _y;
        public double Y
        {
            get => _y;
            set { if (_y != value) { _y = value; OnPropertyChanged(nameof(Y)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
