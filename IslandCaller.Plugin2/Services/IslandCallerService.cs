using IslandCaller.Services.NotificationProvidersNew;
using ClassIsland.Core.Abstractions.Services;
using IslandCaller.Views;
using ClassIsland.Shared.Enums;
using IslandCaller.Models;
using Avalonia.Threading;
using System.Collections.Specialized;

namespace IslandCaller.Services.IslandCallerService
{
    public class IslandCallerService
    {
        private ILessonsService LessonsService { get; }
        private CoreService CoreService {  get; }
        private LotteryService LotteryService { get; }
        private IslandCallerNotificationProviderNew NotificationProvider { get; }
        private Plugin Plugin { get; }
        public Status Status { get; set; }
        public IslandCallerService(Plugin plugin, 
                                    IUriNavigationService uriNavigationService, 
                                    ILessonsService lessonsService,
                                    HistoryService historyService,
                                    CoreService coreService,
                                    LotteryService lotteryService,
                                    IslandCallerNotificationProviderNew notificationProvider,
                                    Status status
            )
        {
            
            LessonsService = lessonsService;
            CoreService = coreService;
            LotteryService = lotteryService;
            NotificationProvider = notificationProvider;
            Plugin = plugin;
            Status = status;
            status.IslandCallerServiceInitialized = false;
            Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & lessonsService.CurrentState == TimeState.Breaking);
            lessonsService.CurrentTimeStateChanged += (s, e) =>
            {
                historyService.ClearThisLessonHistory();
                Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & lessonsService.CurrentState == TimeState.Breaking);
            };
            Settings.Instance.General.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.Instance.General.BreakDisable))
                {
                    Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & lessonsService.CurrentState == TimeState.Breaking);
                }
            };
            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Simple",
                args =>
                {
                    TriggerUriSimpleCall(1);
                }
            );
            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Advanced/GUI",
                args =>
                {
                    TriggerUriAdvancedCall();
                }
            );
            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Lottery/GUI",
                args =>
                {
                    string? prizeName = GetUriQueryValue(args.Uri, "prize");
                    int? winnerCount = TryGetPositiveInt(args.Uri, "count");
                    bool autoDraw = TryGetBoolean(args.Uri, "auto");
                    TriggerUriLotteryGuiCall(prizeName, winnerCount, autoDraw);
                }
            );
            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Lottery/Draw",
                args =>
                {
                    string? prizeName = GetUriQueryValue(args.Uri, "prize");
                    int? winnerCount = TryGetPositiveInt(args.Uri, "count");
                    TriggerUriLotteryDrawCall(prizeName, winnerCount);
                }
            );
            status.IslandCallerServiceInitialized = true;
        }

        public void TriggerUriSimpleCall(int stunum)
        {
            if (stunum <= 0)
            {
                return;
            }

            Dispatcher.UIThread.Post(() => _ = ShowRandomStudentAsync(stunum), DispatcherPriority.Send);
        }

        public void TriggerUriAdvancedCall()
        {
            Dispatcher.UIThread.Post(static () => new PersonalCall().Show(), DispatcherPriority.Send);
        }

        public void TriggerUriLotteryGuiCall(string? prizeName = null, int? winnerCount = null, bool autoDraw = false)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var resolvedPrize = LotteryService.ResolvePrize(prizeName, winnerCount);
                new LotteryWindow(resolvedPrize.Name, resolvedPrize.WinnerCount, autoDraw).Show();
            }, DispatcherPriority.Send);
        }

        public void TriggerUriLotteryDrawCall(string? prizeName = null, int? winnerCount = null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var resolvedPrize = LotteryService.ResolvePrize(prizeName, winnerCount);
                LotteryService.DrawLottery(resolvedPrize.Name, resolvedPrize.WinnerCount);
            }, DispatcherPriority.Send);
        }

        public void ShowRandomStudent(int stunum)
        {
            _ = ShowRandomStudentAsync(stunum);
        }

        private async Task ShowRandomStudentAsync(int stunum)
        {
            if(Status.IsPluginReady == false) return;
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

        private static string? GetUriQueryValue(Uri uri, string key)
        {
            var values = ParseQuery(uri);
            return values[key];
        }

        private static int? TryGetPositiveInt(Uri uri, string key)
        {
            string? raw = GetUriQueryValue(uri, key);
            return int.TryParse(raw, out int value) && value > 0 ? value : null;
        }

        private static bool TryGetBoolean(Uri uri, string key)
        {
            string? raw = GetUriQueryValue(uri, key);
            return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static NameValueCollection ParseQuery(Uri uri)
        {
            NameValueCollection values = new();
            string rawQuery = uri.Query;
            if (string.IsNullOrWhiteSpace(rawQuery))
            {
                return values;
            }

            string query = rawQuery.TrimStart('?');
            foreach (string segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = segment.Split('=', 2);
                string key = Uri.UnescapeDataString(pair[0]);
                string value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
                values[key] = value;
            }

            return values;
        }
    }
}
