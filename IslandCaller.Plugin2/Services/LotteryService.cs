using IslandCaller.Models;
using IslandCaller.Services.NotificationProvidersNew;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IslandCaller.Services;

public sealed class LotteryService
{
    private readonly CoreService _coreService;
    private readonly IslandCallerNotificationProviderNew _notificationProvider;
    private readonly Status _status;
    private readonly ILogger<LotteryService> _logger;

    public LotteryService(
        CoreService coreService,
        IslandCallerNotificationProviderNew notificationProvider,
        Status status,
        ILogger<LotteryService> logger)
    {
        _coreService = coreService;
        _notificationProvider = notificationProvider;
        _status = status;
        _logger = logger;
    }

    public IReadOnlyList<LotteryPrizeItem> GetConfiguredPrizes()
    {
        return NormalizePrizeItems(ParseConfiguredPrizes());
    }

    public IReadOnlyList<string> DrawLottery(string prizeName, int winnerCount, bool showNotification = true)
    {
        if (!_status.IsPluginReady || winnerCount <= 0)
        {
            return [];
        }

        _status.OccupationDisable = false;
        try
        {
            var winners = _coreService.DrawLotteryWinners(winnerCount);
            if (showNotification && winners.Count > 0)
            {
                _notificationProvider.ShowLotteryResult(prizeName, winners);
            }

            _logger.LogInformation("抽奖完成: Prize={Prize}, WinnerCount={WinnerCount}, Winners={Winners}",
                prizeName,
                winners.Count,
                string.Join(",", winners));
            return winners;
        }
        finally
        {
            _status.OccupationDisable = true;
        }
    }

    public LotteryPrizeItem ResolvePrize(string? prizeName, int? winnerCount)
    {
        var configuredPrizes = GetConfiguredPrizes();
        var matched = configuredPrizes.FirstOrDefault(x => string.Equals(x.Name, prizeName?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (matched != null)
        {
            return new LotteryPrizeItem
            {
                Name = matched.Name,
                WinnerCount = Math.Max(1, winnerCount ?? matched.WinnerCount)
            };
        }

        if (configuredPrizes.Count > 0)
        {
            var fallback = configuredPrizes[0];
            return new LotteryPrizeItem
            {
                Name = string.IsNullOrWhiteSpace(prizeName) ? fallback.Name : prizeName.Trim(),
                WinnerCount = Math.Max(1, winnerCount ?? fallback.WinnerCount)
            };
        }

        return new LotteryPrizeItem
        {
            Name = string.IsNullOrWhiteSpace(prizeName) ? "抽奖结果" : prizeName.Trim(),
            WinnerCount = Math.Max(1, winnerCount ?? 1)
        };
    }

    public static IReadOnlyList<LotteryPrizeItem> NormalizePrizeItems(IEnumerable<LotteryPrizeItem>? source)
    {
        return (source ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new LotteryPrizeItem
            {
                Name = x.Name.Trim(),
                WinnerCount = Math.Max(1, x.WinnerCount)
            })
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<LotteryPrizeItem> ParseConfiguredPrizes()
    {
        try
        {
            return JsonSerializer.Deserialize<List<LotteryPrizeItem>>(Settings.Instance.General.LotteryPrizeListJson ?? "[]") ?? [];
        }
        catch
        {
            return [];
        }
    }
}
