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

        var requests = selectedStudents
            .Select(x => BuildSingleNameRequest(x.Name, ToNotificationColor(x.Type)))
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
        var titleRoot = new Border
        {
            MinWidth = 220,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = promptColor,
            Background = promptColor,
            Child = new TextBlock
            {
                Text = name,
                Foreground = GetTitleForeground(promptColor),
                FontSize = 22,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center
            }
        };

        return new NotificationRequest
        {
            // 仅显示标题（Mask），不显示正文（Overlay）。
            MaskContent = new NotificationContent(titleRoot)
            {
                Duration = TimeSpan.FromSeconds(2),
                IsSpeechEnabled = true,
                SpeechContent = name,
                Color = promptColor
            }
        };
    }

    private static IBrush GetTitleForeground(IBrush background)
    {
        if (background is not ISolidColorBrush solid)
        {
            return Brushes.White;
        }

        var c = solid.Color;
        double luminance = (0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B);
        return luminance > 150 ? Brushes.Black : Brushes.White;
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
