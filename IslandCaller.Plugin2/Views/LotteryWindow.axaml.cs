using Avalonia.Controls;
using ClassIsland.Shared;
using IslandCaller.Models;
using IslandCaller.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace IslandCaller.Views;

public partial class LotteryWindow : Window, INotifyPropertyChanged
{
    private readonly LotteryService _lotteryService;
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

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPrize)));
        }
    }

    private string _prizeName = "Lottery";
    public string PrizeName
    {
        get => _prizeName;
        set
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "Lottery" : value.Trim();
            if (_prizeName == normalized)
            {
                return;
            }

            _prizeName = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PrizeName)));
        }
    }

    private int _winnerCount = 1;
    public int WinnerCount
    {
        get => _winnerCount;
        set
        {
            int normalized = Math.Clamp(value, 1, 20);
            if (_winnerCount == normalized)
            {
                return;
            }

            _winnerCount = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WinnerCount)));
        }
    }

    private string _lotteryResultText = "No lottery result yet.";
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LotteryResultText)));
        }
    }

    public LotteryWindow() : this(null, null, false)
    {
    }

    public LotteryWindow(string? presetPrizeName, int? presetWinnerCount, bool autoDraw)
    {
        _lotteryService = IAppHost.GetService<LotteryService>();
        _autoDrawOnOpen = autoDraw;
        InitializeComponent();
        DataContext = this;

        foreach (var prize in _lotteryService.GetConfiguredPrizes())
        {
            PrizeOptions.Add(prize);
        }

        var resolvedPrize = _lotteryService.ResolvePrize(presetPrizeName, presetWinnerCount);
        PrizeName = resolvedPrize.Name;
        WinnerCount = resolvedPrize.WinnerCount;
        SelectedPrize = PrizeOptions.FirstOrDefault(x => string.Equals(x.Name, resolvedPrize.Name, StringComparison.OrdinalIgnoreCase));
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_autoDrawOnOpen)
        {
            ExecuteLottery();
        }
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
        var winners = _lotteryService.DrawLottery(PrizeName, WinnerCount);
        LotteryResultText = winners.Count == 0
            ? "No available winners. Check the roster or current status."
            : $"{PrizeName}: {string.Join(", ", winners)}";
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
}
