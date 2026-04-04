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

        await ShowCountdownShuffleAnimation();

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

    private async Task ShowCountdownShuffleAnimation()
    {
        var names = coreService.StudentNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0)
        {
            return;
        }

        var random = new Random();

        // 第一阶段：倒计时，营造即将揭晓的节奏感。
        var countdownColors = new IBrush[]
        {
            Brushes.OrangeRed,
            Brushes.Orange,
            Brushes.Gold
        };
        for (int i = 3; i >= 1; i--)
        {
            var previewName = names[random.Next(names.Count)];
            var request = BuildCountdownRequest(i, previewName, countdownColors[3 - i]);
            ShowNotification(request);
            await Task.Delay(260);
        }

        // 第二阶段：姓名快速跳动并逐步减速，再交给最终抽取结果。
        var intervals = new[] { 70, 80, 95, 115, 140, 170, 210 };
        for (int i = 0; i < intervals.Length; i++)
        {
            var previewName = names[random.Next(names.Count)];
            var color = i < intervals.Length / 2 ? Brushes.SlateBlue : Brushes.DodgerBlue;
            var request = BuildShuffleRequest(previewName, color, intervals[i]);
            ShowNotification(request);
            await Task.Delay(intervals[i]);
        }
    }

    private static NotificationRequest BuildCountdownRequest(int countdown, string previewName, IBrush promptColor)
    {
        var overlayRoot = new Border
        {
            MinWidth = 260,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            BorderBrush = promptColor,
            Background = CreateOverlayBackground(promptColor),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = "即将抽取",
                        Foreground = promptColor,
                        FontSize = 13,
                        TextAlignment = TextAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = countdown.ToString(),
                        Foreground = promptColor,
                        FontSize = 34,
                        FontWeight = FontWeight.Bold,
                        TextAlignment = TextAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = previewName,
                        Foreground = promptColor,
                        FontSize = 18,
                        FontWeight = FontWeight.Medium,
                        TextAlignment = TextAlignment.Center
                    }
                }
            }
        };

        return BuildAnimationRequest($"倒计时 {countdown}", overlayRoot, promptColor, TimeSpan.FromMilliseconds(260));
    }

    private static NotificationRequest BuildShuffleRequest(string previewName, IBrush promptColor, int intervalMs)
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
                Text = previewName,
                Foreground = promptColor,
                FontSize = 26,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center
            }
        };

        var duration = TimeSpan.FromMilliseconds(Math.Max(120, intervalMs + 30));
        return BuildAnimationRequest("抽取中...", overlayRoot, promptColor, duration);
    }

    private static NotificationRequest BuildAnimationRequest(string maskText, Control overlayRoot, IBrush promptColor, TimeSpan duration)
    {
        var maskContent = NotificationContent.CreateTwoIconsMask(maskText, factory: x =>
        {
            x.Duration = duration;
            x.IsSpeechEnabled = false;
            x.Color = promptColor;
        });
        maskContent.Color = promptColor;

        var overlayContent = new NotificationContent(overlayRoot)
        {
            Duration = duration,
            Color = promptColor
        };

        return new NotificationRequest
        {
            MaskContent = maskContent,
            OverlayContent = overlayContent,
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

        var maskContent = NotificationContent.CreateTwoIconsMask(name, factory: x =>
        {
            x.Duration = TimeSpan.FromSeconds(2);
            x.IsSpeechEnabled = true;
            x.SpeechContent = name;
            x.Color = promptColor;
        });
        maskContent.Color = promptColor;

        var overlayContent = new NotificationContent(overlayRoot)
        {
            Duration = TimeSpan.FromSeconds(2),
            Color = promptColor
        };

        return new NotificationRequest
        {
            // 仅使用 ClassIsland 原生模板，避免自定义额外遮罩控件。
            MaskContent = maskContent,
            // 为正文补充同色卡片，保证提示文本和视觉主体也使用对应颜色。
            OverlayContent = overlayContent,
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
