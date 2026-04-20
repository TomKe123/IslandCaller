using Avalonia;
using Avalonia.Controls;
using ClassIsland.Shared;
using IslandCaller.Models;
using IslandCaller.Services;
using IslandCaller.Services.IslandCallerService;
using System.ComponentModel;
using System.Linq;

namespace IslandCaller.Views;

public partial class PersonalCall : Window, INotifyPropertyChanged
{
    public sealed record ScopeOption(LessonDrawScopeOption Value, string Label);
    public sealed record AlgorithmOption(LessonDrawAlgorithmOption Value, string Label);

    private readonly IslandCallerService _islandCallerService;
    private readonly Status _status;
    private readonly CoreService _coreService;
    private readonly LessonDrawSettingsService _lessonDrawSettingsService;
    private const int OwnerGapPx = 12;

    public IReadOnlyList<ScopeOption> ScopeOptions { get; } =
    [
        new(LessonDrawScopeOption.FollowMain, "跟随主设置"),
        new(LessonDrawScopeOption.All, "全班"),
        new(LessonDrawScopeOption.Male, "仅男生"),
        new(LessonDrawScopeOption.Female, "仅女生")
    ];

    public IReadOnlyList<AlgorithmOption> AlgorithmOptions { get; } =
    [
        new(LessonDrawAlgorithmOption.FollowMain, "跟随主设置"),
        new(LessonDrawAlgorithmOption.Balanced, "平衡抽选"),
        new(LessonDrawAlgorithmOption.PureRandom, "完全公平" )
    ];

    private double _num = 1;
    public double Num
    {
        get => _num;
        set
        {
            double normalized = Math.Clamp(Math.Round(value), 1, 10);
            if (Math.Abs(_num - normalized) < double.Epsilon)
            {
                return;
            }

            _num = normalized;
            RaisePropertyChanged(nameof(Num));
            RaisePropertyChanged(nameof(SelectionSummaryText));
        }
    }

    private ScopeOption? _selectedLessonScopeOption;
    public ScopeOption? SelectedLessonScopeOption
    {
        get => _selectedLessonScopeOption;
        set
        {
            if (_selectedLessonScopeOption == value || value == null)
            {
                return;
            }

            _selectedLessonScopeOption = value;
            ApplyLessonQuickSettings();
            RaisePropertyChanged(nameof(SelectedLessonScopeOption));
            RaisePropertyChanged(nameof(EffectiveScopeText));
            RaisePropertyChanged(nameof(QuickSettingsSummaryText));
            RefreshAvailability();
        }
    }

    private AlgorithmOption? _selectedLessonAlgorithmOption;
    public AlgorithmOption? SelectedLessonAlgorithmOption
    {
        get => _selectedLessonAlgorithmOption;
        set
        {
            if (_selectedLessonAlgorithmOption == value || value == null)
            {
                return;
            }

            _selectedLessonAlgorithmOption = value;
            ApplyLessonQuickSettings();
            RaisePropertyChanged(nameof(SelectedLessonAlgorithmOption));
            RaisePropertyChanged(nameof(EffectiveAlgorithmText));
            RaisePropertyChanged(nameof(QuickSettingsSummaryText));
            RefreshAvailability();
        }
    }

    private bool _canStartCall;
    public bool CanStartCall
    {
        get => _canStartCall;
        private set
        {
            if (_canStartCall == value)
            {
                return;
            }

            _canStartCall = value;
            RaisePropertyChanged(nameof(CanStartCall));
        }
    }

    private string _availabilityText = string.Empty;
    public string AvailabilityText
    {
        get => _availabilityText;
        private set
        {
            if (_availabilityText == value)
            {
                return;
            }

            _availabilityText = value;
            RaisePropertyChanged(nameof(AvailabilityText));
            RaisePropertyChanged(nameof(SelectionSummaryText));
        }
    }

    public string EffectiveScopeText => ResolveScopeLabel(_lessonDrawSettingsService.EffectiveScope);
    public string EffectiveAlgorithmText => ResolveAlgorithmLabel(_lessonDrawSettingsService.EffectiveAlgorithm);

    public string QuickSettingsSummaryText => _lessonDrawSettingsService.HasLessonOverride
        ? $"当前已对本节课临时生效：{EffectiveScopeText} / {EffectiveAlgorithmText}。到下一节课会自动恢复主设置。"
        : $"当前跟随主设置：{EffectiveScopeText} / {EffectiveAlgorithmText}。";

    public string SelectionSummaryText => CanStartCall
        ? $"将按顺序点名 {(int)Num} 人；当前模式：{EffectiveScopeText} / {EffectiveAlgorithmText}。连续抽取时可能出现重复。"
        : AvailabilityText;

    public PersonalCall()
    {
        _islandCallerService = IAppHost.GetService<IslandCallerService>();
        _status = IAppHost.GetService<Status>();
        _coreService = IAppHost.GetService<CoreService>();
        _lessonDrawSettingsService = IAppHost.GetService<LessonDrawSettingsService>();

        InitializeComponent();
        DataContext = this;

        SelectedLessonScopeOption = ScopeOptions.First(x => x.Value == _lessonDrawSettingsService.GetLessonScopeOption());
        SelectedLessonAlgorithmOption = AlgorithmOptions.First(x => x.Value == _lessonDrawSettingsService.GetLessonAlgorithmOption());
        RefreshAvailability();
        _status.PropertyChanged += StatusOnPropertyChanged;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (Owner != null)
        {
            PositionNearOwner(Owner);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _status.PropertyChanged -= StatusOnPropertyChanged;
        base.OnClosed(e);
    }

    private void StatusOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Status.ProfileServiceInitialized)
            or nameof(Status.HistoryServiceInitialized)
            or nameof(Status.CoreServiceInitialized)
            or nameof(Status.IslandCallerServiceInitialized)
            or nameof(Status.IsTimeStatusAvailable)
            or nameof(Status.OccupationDisable)
            or nameof(Status.IsPluginReady))
        {
            RefreshAvailability();
        }
    }

    private void ApplyLessonQuickSettings()
    {
        _lessonDrawSettingsService.ApplyLessonOverride(
            SelectedLessonScopeOption?.Value ?? LessonDrawScopeOption.FollowMain,
            SelectedLessonAlgorithmOption?.Value ?? LessonDrawAlgorithmOption.FollowMain);
    }

    private void RefreshAvailability()
    {
        string? blockedReason = GetBlockedReason();
        CanStartCall = string.IsNullOrWhiteSpace(blockedReason);
        AvailabilityText = blockedReason ?? $"当前可用成员 {GetAvailableMemberCount()} 人。";
        RaisePropertyChanged(nameof(QuickSettingsSummaryText));
        RaisePropertyChanged(nameof(SelectionSummaryText));
    }

    private string? GetBlockedReason()
    {
        int memberCount = GetAvailableMemberCount();
        if (memberCount <= 0)
        {
            return "当前筛选结果为空，请调整快速设置或补充名单。";
        }

        if (!_status.ProfileServiceInitialized || !_status.HistoryServiceInitialized || !_status.CoreServiceInitialized || !_status.IslandCallerServiceInitialized)
        {
            return "插件仍在初始化，请稍后再试。";
        }

        if (!_status.IsTimeStatusAvailable)
        {
            return "当前为非上课时段，点名已按设置暂停。";
        }

        if (!_status.OccupationDisable)
        {
            return "当前已有点名流程正在进行，请稍后再试。";
        }

        return null;
    }

    private int GetAvailableMemberCount()
    {
        var names = _coreService.StudentNames.Where(name => !string.IsNullOrWhiteSpace(name));
        return _lessonDrawSettingsService.EffectiveScope switch
        {
            DrawSelectionScope.Male => _coreService.StudentEntries.Count(p => !string.IsNullOrWhiteSpace(p.Name) && p.Gender == 0),
            DrawSelectionScope.Female => _coreService.StudentEntries.Count(p => !string.IsNullOrWhiteSpace(p.Name) && p.Gender == 1),
            _ => names.Count()
        };
    }

    private static string ResolveScopeLabel(DrawSelectionScope scope)
    {
        return scope switch
        {
            DrawSelectionScope.Male => "仅男生",
            DrawSelectionScope.Female => "仅女生",
            _ => "全班"
        };
    }

    private static string ResolveAlgorithmLabel(DrawSelectionAlgorithm algorithm)
    {
        return algorithm switch
        {
            DrawSelectionAlgorithm.PureRandom => "完全公平",
            _ => "平衡抽选"
        };
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void SureButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!CanStartCall)
        {
            RefreshAvailability();
            return;
        }

        _islandCallerService.ShowRandomStudent((int)Num);
        Close();
    }

    private void PositionNearOwner(WindowBase owner)
    {
        if (owner is not Window ownerWindow)
        {
            return;
        }

        var screen = owner.Screens.ScreenFromWindow(ownerWindow) ?? owner.Screens.Primary ?? throw new Exception("No primary screen found");
        var screenBounds = screen.Bounds;

        var ownerScaling = owner.RenderScaling;
        var thisScaling = RenderScaling;

        var ownerLeft = ownerWindow.Position.X;
        var ownerTop = ownerWindow.Position.Y;
        var ownerWidth = (int)Math.Round(owner.Bounds.Width * ownerScaling);
        var ownerHeight = (int)Math.Round(owner.Bounds.Height * ownerScaling);
        var ownerRight = ownerLeft + ownerWidth;
        var ownerBottom = ownerTop + ownerHeight;
        var ownerCenterX = ownerLeft + ownerWidth / 2.0;

        var thisWidth = (int)Math.Round(Bounds.Width * thisScaling);
        var thisHeight = (int)Math.Round(Bounds.Height * thisScaling);

        if (thisWidth <= 0) thisWidth = (int)Math.Round(Width * thisScaling);
        if (thisHeight <= 0) thisHeight = (int)Math.Round(Height * thisScaling);

        var thirdWidth = screenBounds.Width / 3.0;
        var align = ownerCenterX < screenBounds.X + thirdWidth
            ? "left"
            : ownerCenterX > screenBounds.X + 2 * thirdWidth
                ? "right"
                : "center";

        int targetX = align switch
        {
            "left" => ownerLeft,
            "right" => ownerRight - thisWidth,
            _ => (int)Math.Round(ownerCenterX - thisWidth / 2.0)
        };

        int targetY = ownerTop - OwnerGapPx - thisHeight;
        if (targetY < screenBounds.Y)
        {
            targetY = ownerBottom + OwnerGapPx;
        }

        targetX = Clamp(targetX, screenBounds.X, screenBounds.X + screenBounds.Width - thisWidth);
        targetY = Clamp(targetY, screenBounds.Y, screenBounds.Y + screenBounds.Height - thisHeight);

        Position = new PixelPoint(targetX, targetY);
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
}
