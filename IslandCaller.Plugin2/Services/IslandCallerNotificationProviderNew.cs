using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Shared.Enums;
using Avalonia.Media;
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

        var selectedStudents = new List<CoreService.DrawResult>(stunum);
        for (int i = 0; i < stunum; i++)
        {
            var student = coreService.GetRandomStudentResult();
            if (string.IsNullOrWhiteSpace(student.Name) || string.Equals(student.Name, "Error", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            selectedStudents.Add(student);
        }

        if (selectedStudents.Count == 0)
        {
            return;
        }

        var requests = selectedStudents
            .Select(result => new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask(result.Name, factory: x =>
                {
                    x.Duration = TimeSpan.FromSeconds(2);
                    x.IsSpeechEnabled = true;
                    x.SpeechContent = result.Name;
                    // 使用 ClassIsland 的通知强调动画颜色字段，而不是文本 emoji。
                    x.Color = ToNotificationColor(result.Type);
                })
            })
            .ToArray();

        if (requests.Length == 1)
        {
            ShowNotification(requests[0]);
            return;
        }

        ShowChainedNotifications(requests);
    }

    private static IBrush ToNotificationColor(CoreService.DrawType type)
    {
        return type switch
        {
            CoreService.DrawType.Guarantee => Brushes.Gold,
            CoreService.DrawType.Pacer => Brushes.MediumPurple,
            _ => Brushes.DodgerBlue
        };
    }
}
