using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Shared.Enums;
using Avalonia.Controls;
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

        var dominantType = ResolveDominantType(selectedStudents);
        var promptColor = ToNotificationColor(dominantType);
        var output = string.Join("  ", selectedStudents.Select(x => x.Name));
        int durationSeconds = selectedStudents.Count * 2 + 1;

        var request = new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(output, factory: x =>
            {
                x.Duration = TimeSpan.FromSeconds(durationSeconds);
                x.IsSpeechEnabled = true;
                x.SpeechContent = output;
                // 文档说明此字段用于通知强调动画颜色。
                x.Color = promptColor;
            }),
            OverlayContent = new NotificationContent(new TextBlock
            {
                Text = output,
                Foreground = promptColor
            })
            {
                Duration = TimeSpan.FromSeconds(durationSeconds),
                Color = promptColor
            }
        };

        ShowNotification(request);
    }

    private static CoreService.DrawType ResolveDominantType(IEnumerable<CoreService.DrawResult> results)
    {
        if (results.Any(x => x.Type == CoreService.DrawType.Guarantee))
        {
            return CoreService.DrawType.Guarantee;
        }

        if (results.Any(x => x.Type == CoreService.DrawType.Pacer))
        {
            return CoreService.DrawType.Pacer;
        }

        return CoreService.DrawType.Normal;
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
