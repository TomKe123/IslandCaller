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

    public async Task RandomCall(int stunum)
    {
        if (stunum <= 0)
        {
            return;
        }

        await ShowRollingAnimation();

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

    private async Task ShowRollingAnimation()
    {
        var names = coreService.StudentNames.ToList();
        if (names.Count == 0) return;

        var random = new Random();
        var startTime = DateTime.Now;
        var duration = TimeSpan.FromSeconds(1.5);
        var interval = 80;

        while (DateTime.Now - startTime < duration)
        {
            var randomInd = random.Next(names.Count);
            var rollingName = names[randomInd];

            var request = BuildRollingRequest(rollingName);
            ShowNotification(request);

            await Task.Delay(interval);
        }
    }

    private static NotificationRequest BuildRollingRequest(string name)
    {
        var promptColor = Brushes.Gray;
        var overlayRoot = new Border
        {
            MinWidth = 240,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            BorderBrush = promptColor,
            Background = CreateOverlayBackground(promptColor),
            Child = new TextBlock
            {
                Text = name,
                Foreground = promptColor,
                FontSize = 24,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center
            }
        };

        return new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask("正在抽取...", factory: x =>
            {
                x.Duration = TimeSpan.FromMilliseconds(150);
                x.IsSpeechEnabled = false;
                x.Color = promptColor;
            }),
            OverlayContent = new NotificationContent(overlayRoot)
            {
                Duration = TimeSpan.FromMilliseconds(150),
                Color = promptColor
            },
            RequestNotificationSettings =
            {
                IsSettingsEnabled = true,
                IsNotificationEnabled = true,
                IsNotificationEffectEnabled = true,
                IsSpeechEnabled = false
            }
        };
    }

    private static NotificationRequest BuildSingleNameRequest(string name, IBrush promptColor)
    {
        var overlayRoot = new Border
        {
            MinWidth = 240,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            BorderBrush = promptColor,
            Background = CreateOverlayBackground(promptColor),
            Child = new TextBlock
            {
                Text = name,
                Foreground = promptColor,
                FontSize = 24,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center
            }
        };

        return new NotificationRequest
        {
            // 仅使用 ClassIsland 原生模板，避免自定义额外遮罩控件。
            MaskContent = NotificationContent.CreateTwoIconsMask(name, factory: x =>
            {
                x.Duration = TimeSpan.FromSeconds(2);
                x.IsSpeechEnabled = true;
                x.SpeechContent = name;
                x.Color = promptColor;
            }),
            // 为正文补充同色卡片，保证提示文本和视觉主体也使用对应颜色。
            OverlayContent = new NotificationContent(overlayRoot)
            {
                Duration = TimeSpan.FromSeconds(2),
                Color = promptColor
            },
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

    private static IBrush CreateOverlayBackground(IBrush promptColor)
    {
        if (promptColor is ISolidColorBrush solid)
        {
            var c = solid.Color;
            return new SolidColorBrush(Color.FromArgb(48, c.R, c.G, c.B));
        }

        return Brushes.Transparent;
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
