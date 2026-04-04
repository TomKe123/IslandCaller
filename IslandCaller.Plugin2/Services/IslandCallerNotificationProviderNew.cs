using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Shared.Enums;
using Avalonia;
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
        var requests = selectedStudents
            .Select(x => BuildSingleNameRequest(x.Name, promptColor))
            .ToArray();

        if (requests.Length == 1)
        {
            ShowNotification(requests[0]);
            return;
        }

        // 多人抽取按顺序逐条弹出姓名。
        ShowChainedNotifications(requests);
    }

    private static NotificationRequest BuildSingleNameRequest(string name, IBrush promptColor)
    {
        var overlayRoot = new Border
        {
            MinWidth = 220,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            BorderBrush = promptColor,
            Background = CreateOverlayBackground(promptColor),
            Child = new TextBlock
            {
                Text = name,
                Foreground = promptColor,
                FontSize = 22,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center
            }
        };

        return new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(name, factory: x =>
            {
                x.Duration = TimeSpan.FromSeconds(2);
                x.IsSpeechEnabled = true;
                x.SpeechContent = name;
                x.Color = promptColor;
            }),
            OverlayContent = new NotificationContent(overlayRoot)
            {
                Duration = TimeSpan.FromSeconds(2),
                Color = promptColor
            }
        };
    }

    private static IBrush CreateOverlayBackground(IBrush promptColor)
    {
        if (promptColor is ISolidColorBrush solid)
        {
            var c = solid.Color;
            return new SolidColorBrush(Color.FromArgb(40, c.R, c.G, c.B));
        }

        return Brushes.Transparent;
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
