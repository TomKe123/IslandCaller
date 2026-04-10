using ClassIsland.Shared;
using CommunityToolkit.Mvvm.Input;
using IslandCaller.Models;
using IslandCaller.Services;
using IslandCaller.Views;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Input;
using static IslandCaller.Services.ProfileService;

namespace IslandCaller.ViewModels
{
    public class SettingPageViewModel : ReactiveObject
    {
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

        private bool _isGachaEnabled;
        public bool IsGachaEnabled
        {
            get => _isGachaEnabled;
            set => this.RaiseAndSetIfChanged(ref _isGachaEnabled, value);
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
            set => this.RaiseAndSetIfChanged(ref _guaranteeWeightList, value);
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
        private readonly Dictionary<StudentModel, PropertyChangedEventHandler> _handlers = new();
        private ObservableCollection<StudentModel> _profileList = new();
        public ObservableCollection<StudentModel> ProfileList
        {
            get => _profileList;
            set => this.RaiseAndSetIfChanged(ref _profileList, value);
        }
        public IReadOnlyList<int> RarityOptions { get; } = [3, 4, 5];
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
            ProfileService profileService = IAppHost.GetService<ProfileService>();
            HistoryService historyService = IAppHost.GetService<HistoryService>();
            CoreService coreService = IAppHost.GetService<CoreService>();
            Plugin plugin = IAppHost.GetService<Plugin>();

            // 初始化默认值
            IsBreakDisable = Settings.Instance.General.BreakDisable;
            IsGlobalHotkeyEnabled = Settings.Instance.General.EnableGlobalHotkeys;
            QuickCallHotkey = Settings.Instance.General.QuickCallHotkey;
            AdvancedCallHotkey = Settings.Instance.General.AdvancedCallHotkey;
            IsGuaranteeEnabled = Settings.Instance.General.EnableGuarantee;
            GuaranteeThreshold = Settings.Instance.General.GuaranteeThreshold;
            PacerThreshold = Settings.Instance.General.PacerThreshold;
            IsGachaEnabled = Settings.Instance.Gacha.Enabled;
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
            GuaranteeWeightList = BuildGuaranteeWeightList(profileService);
            UpdateGuaranteeMemberOptions(profileService);
            RefreshPacerList(profileService);
            IsHoverEnable = Settings.Instance.Hover.IsEnable;
            HoverScalingFactor = Settings.Instance.Hover.ScalingFactor;
            var profile = profileService.GetMembers(CurrentProfile)
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
                    UpdateGuaranteeSummary(profileService);
                    RefreshHistoryAndStatistics(historyService);
                }
                else if (args.PropertyName == nameof(GuaranteeThreshold))
                {
                    Settings.Instance.General.GuaranteeThreshold = Math.Max(1, GuaranteeThreshold);
                    if (GuaranteeThreshold < 1)
                    {
                        GuaranteeThreshold = 1;
                    }
                    UpdateGuaranteeSummary(profileService);
                    RefreshHistoryAndStatistics(historyService);
                }
                else if (args.PropertyName == nameof(PacerThreshold))
                {
                    Settings.Instance.General.PacerThreshold = Math.Max(1, PacerThreshold);
                    if (PacerThreshold < 1)
                    {
                        PacerThreshold = 1;
                    }

                    RefreshPacerList(profileService);
                    RefreshHistoryAndStatistics(historyService);
                }
                else if (args.PropertyName == nameof(IsGachaEnabled))
                {
                    Settings.Instance.Gacha.Enabled = IsGachaEnabled;
                    RefreshHistoryAndStatistics(historyService);
                    UpdateGachaSummary(historyService);
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
                    UpdateGuaranteeSummary(profileService);
                    RefreshPacerList(profileService);
                }
                else if (args.PropertyName == nameof(GuaranteeWeightList))
                {
                    SaveGuaranteeWeights();
                    UpdateGuaranteeSummary(profileService);
                    UpdateGuaranteeMemberOptions(profileService);
                    RefreshPacerList(profileService);
                }
                else if (args.PropertyName == nameof(IsHoverEnable))
                {
                    Settings.Instance.Hover.IsEnable = IsHoverEnable;
                    if (IsHoverEnable)
                    {
                        plugin.HoverWindow = new HoverFluent();
                        plugin.HoverWindow.DataContext = IAppHost.GetService<HoverFluentViewModel>();
                        plugin.HoverWindow.Show();
                    }
                    else plugin.HoverWindow?.Close();
                }
                else if (args.PropertyName == nameof(HoverScalingFactor))
                {
                    Settings.Instance.Hover.ScalingFactor = HoverScalingFactor;
                }
                else if (args.PropertyName == nameof(ProfileList))
                {
                    List<Person> list = BuildProfilePersons();
                    profileService.Members = list;
                    profileService.SaveProfile(CurrentProfile, list);
                    historyService.Load(CurrentProfile);
                    coreService.InitializeCore();
                    GuaranteeWeightList = BuildGuaranteeWeightList(profileService);
                    UpdateGuaranteeSummary(profileService);
                    UpdateGuaranteeMemberOptions(profileService);
                    RefreshPacerList(profileService);
                    UpdateGachaSummary(historyService);
                    RefreshHistoryAndStatistics(historyService);
                }
            };

            GuaranteeWeightList.CollectionChanged += (_, __) =>
            {
                SaveGuaranteeWeights();
                UpdateGuaranteeListTextFromRows();
                UpdateGuaranteeSummary(profileService);
                UpdateGuaranteeMemberOptions(profileService);
                RefreshPacerList(profileService);
                UpdateGachaSummary(historyService);
            };

            ProfileList.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (StudentModel student in e.NewItems)
                    {
                        PropertyChangedEventHandler handler = (_, _) =>
                        {
                            List<Person> list = BuildProfilePersons();

                            profileService.Members = list;
                            profileService.SaveProfile(CurrentProfile, list);
                            historyService.Load(CurrentProfile);
                            coreService.InitializeCore();
                            GuaranteeWeightList = BuildGuaranteeWeightList(profileService);
                            UpdateGuaranteeSummary(profileService);
                            UpdateGuaranteeMemberOptions(profileService);
                            RefreshPacerList(profileService);
                            UpdateGachaSummary(historyService);
                            RefreshHistoryAndStatistics(historyService);
                        };

                        _handlers[student] = handler;
                        student.PropertyChanged += handler;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (StudentModel student in e.OldItems)
                    {
                        if (_handlers.TryGetValue(student, out var handler))
                        {
                            student.PropertyChanged -= handler;
                            _handlers.Remove(student);
                        }
                    }
                }

            };
            foreach (var student in ProfileList)
            {
                PropertyChangedEventHandler handler = (_, _) =>
                {
                    List<Person> list = BuildProfilePersons();

                    profileService.Members = list;
                    profileService.SaveProfile(CurrentProfile, list);
                    historyService.Load(CurrentProfile);
                    coreService.InitializeCore();
                    GuaranteeWeightList = BuildGuaranteeWeightList(profileService);
                    UpdateGuaranteeSummary(profileService);
                    UpdateGuaranteeMemberOptions(profileService);
                    RefreshPacerList(profileService);
                    UpdateGachaSummary(historyService);
                    RefreshHistoryAndStatistics(historyService);
                };

                _handlers[student] = handler;
                student.PropertyChanged += handler;
            }

            UpdateGuaranteeSummary(profileService);
            UpdateGachaSummary(historyService);
            RefreshHistoryAndStatistics(historyService);
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
            StatisticsList.Add(new StatisticsItem { Metric = "保底状态", Value = IsGuaranteeEnabled ? "开启" : "关闭" });
            StatisticsList.Add(new StatisticsItem { Metric = "保底阈值", Value = Math.Max(1, GuaranteeThreshold).ToString() });
            StatisticsList.Add(new StatisticsItem { Metric = "陪跑保底阈值", Value = Math.Max(1, PacerThreshold).ToString() });
            StatisticsList.Add(new StatisticsItem { Metric = "角色池模式", Value = IsGachaEnabled ? "开启" : "关闭" });
            StatisticsList.Add(new StatisticsItem { Metric = "五星水位", Value = pityState.FiveStarPity.ToString() });
            StatisticsList.Add(new StatisticsItem { Metric = "四星水位", Value = pityState.FourStarPity.ToString() });
            StatisticsList.Add(new StatisticsItem { Metric = "五星大保底", Value = pityState.IsFiveStarFeaturedGuaranteed ? "是" : "否" });
            StatisticsList.Add(new StatisticsItem { Metric = "四星大保底", Value = pityState.IsFourStarFeaturedGuaranteed ? "是" : "否" });
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
            int fourStarCount = ProfileList.Count(x => x.Rarity == 4);
            int threeStarCount = ProfileList.Count(x => x.Rarity == 3);
            int featuredFiveStarCount = ProfileList.Count(x => x.Rarity == 5 && x.IsFeatured);
            int featuredFourStarCount = ProfileList.Count(x => x.Rarity == 4 && x.IsFeatured);

            GachaSummaryText =
                $"角色池模式：{(IsGachaEnabled ? "开启" : "关闭")}；" +
                $"3星={threeStarCount}，4星={fourStarCount}（UP {featuredFourStarCount}），5星={fiveStarCount}（UP {featuredFiveStarCount}）；" +
                $"当前水位：五星 {pityState.FiveStarPity}/{Math.Max(1, FiveStarHardPity)}，四星 {pityState.FourStarPity}/{Math.Max(1, FourStarHardPity)}；" +
                $"五星大保底={(pityState.IsFiveStarFeaturedGuaranteed ? "是" : "否")}，四星大保底={(pityState.IsFourStarFeaturedGuaranteed ? "是" : "否")}。";
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

            foreach (var item in cleaned)
            {
                item.PropertyChanged += (_, __) =>
                {
                    if (item.Weight < 0.01)
                    {
                        item.Weight = 0.01;
                    }

                    SaveGuaranteeWeights();
                    UpdateGuaranteeListTextFromRows();
                };
            }

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
                    Name = s.Name,
                    Gender = s.Gender,
                    ManualWeight = s.ManualWeight,
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
