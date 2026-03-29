using IslandCaller.Services.NotificationProvidersNew;
using ClassIsland.Core.Abstractions.Services;
using IslandCaller.Views;
using ClassIsland.Shared.Enums;
using IslandCaller.Models;

namespace IslandCaller.Services.IslandCallerService
{
    public class IslandCallerService
    {
        private ILessonsService LessonsService { get; }
        private CoreService CoreService {  get; }
        private IslandCallerNotificationProviderNew NotificationProvider { get; }
        private Plugin Plugin { get; }
        public Status Status { get; set; }
        public IslandCallerService(Plugin plugin, 
                                    IUriNavigationService uriNavigationService, 
                                    ILessonsService lessonsService,
                                    HistoryService historyService,
                                    CoreService coreService,
                                    IslandCallerNotificationProviderNew notificationProvider,
                                    Status status
            )
        {
            
            LessonsService = lessonsService;
            CoreService = coreService;
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
                    NotificationProvider.RandomCall(1);
                }
            );
            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Advanced/GUI",
                args =>
                {
                    new PersonalCall().Show();
                }
            );
            status.IslandCallerServiceInitialized = true;
        }

        public async void ShowRandomStudent(int stunum)
        {
            if(Status.IsPluginReady == false) return;
            Status.OccupationDisable = false;
            NotificationProvider.RandomCall(stunum);
            await Task.Delay(stunum * 2000 + 1000);
            Status.OccupationDisable = true;
        }
    }
}
