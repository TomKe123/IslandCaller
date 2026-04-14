using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared.Enums;
using IslandCaller.Models;
using IslandCaller.Services.NotificationProvidersNew;
using IslandCaller.Views;

namespace IslandCaller.Services.IslandCallerService
{
    public class IslandCallerService
    {
        private LotteryService LotteryService { get; }
        private IslandCallerNotificationProviderNew NotificationProvider { get; }
        public Status Status { get; }

        public IslandCallerService(
            IUriNavigationService uriNavigationService,
            ILessonsService lessonsService,
            HistoryService historyService,
            LotteryService lotteryService,
            IslandCallerNotificationProviderNew notificationProvider,
            Status status)
        {
            LotteryService = lotteryService;
            NotificationProvider = notificationProvider;
            Status = status;

            status.IslandCallerServiceInitialized = false;
            UpdateTimeStatusAvailability(lessonsService);

            lessonsService.CurrentTimeStateChanged += (_, _) =>
            {
                historyService.ClearThisLessonHistory();
                UpdateTimeStatusAvailability(lessonsService);
            };

            Settings.Instance.General.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Settings.Instance.General.BreakDisable))
                {
                    UpdateTimeStatusAvailability(lessonsService);
                }
            };

            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Simple",
                args =>
                {
                    var request = ParseUriRequest(args.Uri, args.ChildrenPathPatterns);
                    TriggerUriSimpleCall(request.ResolveSimpleCallStudentCount());
                });

            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Advanced/GUI",
                _ => TriggerUriAdvancedCall());

            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Lottery/GUI",
                args =>
                {
                    var request = ParseUriRequest(args.Uri, args.ChildrenPathPatterns);
                    TriggerUriLotteryGuiCall(request.PrizeName, request.WinnerCount, request.AutoDraw);
                });

            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Lottery/Draw",
                args =>
                {
                    var request = ParseUriRequest(args.Uri, args.ChildrenPathPatterns);
                    TriggerUriLotteryDrawCall(request.PrizeName, request.WinnerCount);
                });

            status.IslandCallerServiceInitialized = true;
        }

        public void TriggerUriSimpleCall(int stunum)
        {
            if (stunum <= 0)
            {
                return;
            }

            RunOnUiThread(() => _ = ShowRandomStudentAsync(stunum));
        }

        public void TriggerUriAdvancedCall()
        {
            RunOnUiThread(static () => new PersonalCall().Show());
        }

        public void TriggerUriLotteryGuiCall(string? prizeName = null, int? winnerCount = null, bool autoDraw = false)
        {
            RunOnUiThread(() =>
            {
                var resolvedPrize = LotteryService.ResolvePrize(prizeName, winnerCount);
                new LotteryWindow(resolvedPrize.Name, resolvedPrize.WinnerCount, autoDraw).Show();
            });
        }

        public void TriggerUriLotteryDrawCall(string? prizeName = null, int? winnerCount = null)
        {
            RunOnUiThread(() =>
            {
                var resolvedPrize = LotteryService.ResolvePrize(prizeName, winnerCount);
                LotteryService.DrawLottery(resolvedPrize.Name, resolvedPrize.WinnerCount);
            });
        }

        public void ShowRandomStudent(int stunum)
        {
            _ = ShowRandomStudentAsync(stunum);
        }

        private async Task ShowRandomStudentAsync(int stunum)
        {
            if (Status.IsPluginReady == false)
            {
                return;
            }

            Status.OccupationDisable = false;
            try
            {
                await NotificationProvider.RandomCall(stunum);
                await Task.Delay(stunum * 2000 + 1000);
            }
            finally
            {
                Status.OccupationDisable = true;
            }
        }

        private void UpdateTimeStatusAvailability(ILessonsService lessonsService)
        {
            Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & lessonsService.CurrentState == TimeState.Breaking);
        }

        private static void RunOnUiThread(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
                return;
            }

            Dispatcher.UIThread.Post(action, DispatcherPriority.Send);
        }

        private static UriRequest ParseUriRequest(Uri uri, IEnumerable<string>? childrenPathPatterns)
        {
            string? prizeName = null;
            int? winnerCount = null;
            bool autoDraw = false;

            ReadOnlySpan<char> query = uri.Query.AsSpan();
            if (!query.IsEmpty && query[0] == '?')
            {
                query = query[1..];
            }

            while (!query.IsEmpty)
            {
                int segmentSeparatorIndex = query.IndexOf('&');
                ReadOnlySpan<char> segment = segmentSeparatorIndex >= 0 ? query[..segmentSeparatorIndex] : query;
                query = segmentSeparatorIndex >= 0 ? query[(segmentSeparatorIndex + 1)..] : ReadOnlySpan<char>.Empty;

                if (segment.IsEmpty)
                {
                    continue;
                }

                int valueSeparatorIndex = segment.IndexOf('=');
                ReadOnlySpan<char> rawKey = valueSeparatorIndex >= 0 ? segment[..valueSeparatorIndex] : segment;
                ReadOnlySpan<char> rawValue = valueSeparatorIndex >= 0 ? segment[(valueSeparatorIndex + 1)..] : ReadOnlySpan<char>.Empty;

                string key = Uri.UnescapeDataString(rawKey.ToString());
                string value = Uri.UnescapeDataString(rawValue.ToString());

                if (string.Equals(key, "prize", StringComparison.OrdinalIgnoreCase))
                {
                    prizeName = value;
                    continue;
                }

                if (string.Equals(key, "count", StringComparison.OrdinalIgnoreCase))
                {
                    winnerCount = TryParsePositiveInt(value, out int parsedCount) ? parsedCount : null;
                    continue;
                }

                if (string.Equals(key, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    autoDraw = IsTruthyValue(value);
                }
            }

            return new UriRequest(childrenPathPatterns?.FirstOrDefault(), prizeName, winnerCount, autoDraw);
        }

        private static bool TryParsePositiveInt(string? rawValue, out int value)
        {
            if (int.TryParse(rawValue, out value) && value > 0)
            {
                return true;
            }

            value = 0;
            return false;
        }

        private static bool IsTruthyValue(string? rawValue)
        {
            return string.Equals(rawValue, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private readonly record struct UriRequest(
            string? FirstChildPathPattern,
            string? PrizeName,
            int? WinnerCount,
            bool AutoDraw)
        {
            public int ResolveSimpleCallStudentCount()
            {
                if (TryParsePositiveInt(FirstChildPathPattern, out int childPathCount))
                {
                    return childPathCount;
                }

                return WinnerCount.GetValueOrDefault(1);
            }
        }
    }
}
