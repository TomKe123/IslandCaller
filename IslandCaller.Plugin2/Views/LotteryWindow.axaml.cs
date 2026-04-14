using Avalonia.Controls;
using ClassIsland.Shared;
using IslandCaller.Models;
using IslandCaller.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace IslandCaller.Views;

public partial class LotteryWindow : Window, INotifyPropertyChanged
{
    private readonly LotteryService _lotteryService;
    private readonly Status _status;
    private readonly CoreService _coreService;
    private readonly bool _autoDrawOnOpen;

    public ObservableCollection<LotteryPrizeItem> PrizeOptions { get; } = [];

    private LotteryPrizeItem? _selectedPrize;
    public LotteryPrizeItem? SelectedPrize
    {
        get => _selectedPrize;
        set
        {
            if (_selectedPrize == value)
            {
                return;
            }

            _selectedPrize = value;
            if (value != null)
            {
                PrizeName = value.Name;
                WinnerCount = value.WinnerCount;
            }

            RaisePropertyChanged(nameof(SelectedPrize));
        }
    }

    private string _prizeName = "抽奖结果";
    public string PrizeName
    {
        get => _prizeName;
        set
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "抽奖结果" : value.Trim();
            if (_prizeName == normalized)
            {
                return;
            }

            _prizeName = normalized;
            RaisePropertyChanged(nameof(PrizeName));
        }
    }

    private int _winnerCount = 1;
    public int WinnerCount
    {
        get => _winnerCount;
        set
        {
            int normalized = Math.Clamp(value, 1, WinnerCountMaximum);
            if (_winnerCount == normalized)
            {
                return;
            }

            _winnerCount = normalized;
            RaisePropertyChanged(nameof(WinnerCount));
            RaisePropertyChanged(nameof(SelectionSummaryText));
        }
    }

    private int _winnerCountMaximum = 20;
    public int WinnerCountMaximum
    {
        get => _winnerCountMaximum;
        private set
        {
            int normalized = Math.Max(1, value);
            if (_winnerCountMaximum == normalized)
            {
                return;
            }

            _winnerCountMaximum = normalized;
            RaisePropertyChanged(nameof(WinnerCountMaximum));
            RaisePropertyChanged(nameof(SelectionSummaryText));
        }
    }

    private bool _canDraw;
    public bool CanDraw
    {
        get => _canDraw;
        private set
        {
            if (_canDraw == value)
            {
                return;
            }

            _canDraw = value;
            RaisePropertyChanged(nameof(CanDraw));
            RaisePropertyChanged(nameof(SelectionSummaryText));
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

    private string _lotteryResultText = "还没有抽奖结果，完成抽奖后会显示在这里。";
    public string LotteryResultText
    {
        get => _lotteryResultText;
        set
        {
            if (_lotteryResultText == value)
            {
                return;
            }

            _lotteryResultText = value;
            RaisePropertyChanged(nameof(LotteryResultText));
        }
    }

    public string SelectionSummaryText => CanDraw
        ? $"当前将从 {GetAvailableMemberCount()} 名可参与成员中抽取 {WinnerCount} 人。"
        : AvailabilityText;

    public LotteryWindow() : this(null, null, false)
    {
    }

    public LotteryWindow(string? presetPrizeName, int? presetWinnerCount, bool autoDraw)
    {
        _lotteryService = IAppHost.GetService<LotteryService>();
        _status = IAppHost.GetService<Status>();
        _coreService = IAppHost.GetService<CoreService>();
        _autoDrawOnOpen = autoDraw;

        InitializeComponent();
        DataContext = this;

        foreach (var prize in _lotteryService.GetConfiguredPrizes())
        {
            PrizeOptions.Add(prize);
        }

        var resolvedPrize = _lotteryService.ResolvePrize(presetPrizeName, presetWinnerCount);
        PrizeName = resolvedPrize.Name;

        RefreshAvailability();
        WinnerCount = resolvedPrize.WinnerCount;
        SelectedPrize = PrizeOptions.FirstOrDefault(x => string.Equals(x.Name, resolvedPrize.Name, StringComparison.OrdinalIgnoreCase));

        _status.PropertyChanged += StatusOnPropertyChanged;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_autoDrawOnOpen && CanDraw)
        {
            ExecuteLottery();
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

    private void RefreshAvailability()
    {
        int memberCount = GetAvailableMemberCount();
        WinnerCountMaximum = memberCount > 0 ? Math.Min(20, memberCount) : 20;
        WinnerCount = Math.Min(Math.Max(1, WinnerCount), WinnerCountMaximum);

        string? blockedReason = GetBlockedReason(memberCount);
        CanDraw = string.IsNullOrWhiteSpace(blockedReason);
        AvailabilityText = blockedReason ?? $"当前可参与抽奖的成员共有 {memberCount} 人，本次最多可抽取 {WinnerCountMaximum} 人。";
    }

    private string? GetBlockedReason(int memberCount)
    {
        if (memberCount <= 0)
        {
            return "当前名单为空，请先添加成员或导入名单。";
        }

        if (!_status.ProfileServiceInitialized || !_status.HistoryServiceInitialized || !_status.CoreServiceInitialized || !_status.IslandCallerServiceInitialized)
        {
            return "插件仍在初始化，请稍后再试。";
        }

        if (!_status.IsTimeStatusAvailable)
        {
            return "当前为非上课时段，抽奖已按设置暂停。";
        }

        if (!_status.OccupationDisable)
        {
            return "当前已有点名或抽奖流程正在进行，请稍后再试。";
        }

        return null;
    }

    private int GetAvailableMemberCount()
    {
        return _coreService.StudentNames.Count(name => !string.IsNullOrWhiteSpace(name));
    }

    private void DrawButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ExecuteLottery();
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void ExecuteLottery()
    {
        RefreshAvailability();
        if (!CanDraw)
        {
            LotteryResultText = AvailabilityText;
            return;
        }

        var winners = _lotteryService.DrawLottery(PrizeName, WinnerCount);
        LotteryResultText = winners.Count == 0
            ? "本次没有抽到可用成员，请检查名单或当前状态。"
            : $"{PrizeName}：{string.Join("、", winners)}";
    }

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
}
