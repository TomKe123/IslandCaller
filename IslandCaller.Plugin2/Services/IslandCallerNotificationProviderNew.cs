using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Shared.Enums;
using IslandCaller.Models;

namespace IslandCaller.Services.NotificationProvidersNew;

[NotificationProviderInfo(
    "9B570BF1-9A32-40C0-9D5D-4FFA69E03A37",
    "IslandCallerServices",
    "\uECEE",
    "用于为IslandCaller提供通知接口")]
public class IslandCallerNotificationProviderNew(ILessonsService lessonsService,CoreService coreService) : NotificationProviderBase
{
    private readonly ILessonsService lessonsService = lessonsService;

    public void RandomCall(int stunum)
    {
        if (stunum <= 0)
        {
            return;
        }

        var selectedStudents = new List<string>(stunum);
        for (int i = 0; i < stunum; i++)
        {
            var student = coreService.GetRandomStudent();
            if (string.IsNullOrWhiteSpace(student))
            {
                continue;
            }

            selectedStudents.Add(student);
        }

        if (selectedStudents.Count == 0)
        {
            return;
        }

        var output = string.Join("  ", selectedStudents);
        int maskduration = selectedStudents.Count * 2 + 1; // 计算持续时间
        ShowNotification(new NotificationRequest()
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(output, factory: x =>
            {
                x.Duration = new TimeSpan(0, 0, maskduration);
                x.IsSpeechEnabled = true;
                x.SpeechContent = output;
            })
        });
    }
}
