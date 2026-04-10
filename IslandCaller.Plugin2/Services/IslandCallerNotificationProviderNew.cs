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

    public Task RandomCall(int stunum)
    {
        if (stunum <= 0)
        {
            return Task.CompletedTask;
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
            return Task.CompletedTask;
        }

        var requests = selectedStudents
            .Select(x => BuildSingleNameRequest(x.Name, ToNotificationColor(x.Type)))
            .ToArray();

        if (requests.Length == 1)
        {
            ShowNotification(requests[0]);
            return Task.CompletedTask;
        }

        // 多人抽取按顺序逐条弹出姓名。
        ShowChainedNotifications(requests);
        return Task.CompletedTask;
    }

    private static NotificationRequest BuildSingleNameRequest(string name, IBrush promptColor)
    {
        var maskContent = NotificationContent.CreateTwoIconsMask(name, factory: x =>
        {
            x.Duration = TimeSpan.FromSeconds(2);
            x.IsSpeechEnabled = true;
            x.SpeechContent = name;
            x.Color = promptColor;
        });
        maskContent.Color = promptColor;

        return new NotificationRequest
        {
            // 仅保留遮罩通知，不显示提醒正文。
            MaskContent = maskContent,
            // 强制启用此次提醒的原生特效，确保 Color 能在主屏幕遮罩效果上生效。
            RequestNotificationSettings =
            {
                IsSettingsEnabled = true,
                IsNotificationEnabled = true,
                IsNotificationEffectEnabled = true,
                IsSpeechEnabled = true
            }
        };
    }

    private static IBrush ToNotificationColor(CoreService.DrawType type)
    {
        return type switch
        {
            CoreService.DrawType.Guarantee => Brushes.Gold,
            CoreService.DrawType.Pacer => Brushes.MediumPurple,
            CoreService.DrawType.ThreeStar => Brushes.SteelBlue,
            CoreService.DrawType.FourStar => Brushes.MediumPurple,
            CoreService.DrawType.FeaturedFourStar => Brushes.Orchid,
            CoreService.DrawType.FiveStar => Brushes.DarkOrange,
            CoreService.DrawType.FeaturedFiveStar => Brushes.Gold,
            CoreService.DrawType.CapturedRadiance => Brushes.HotPink,
            _ => Brushes.DodgerBlue
        };
    }
}
