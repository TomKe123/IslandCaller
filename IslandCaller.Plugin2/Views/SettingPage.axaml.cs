using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Shared;
using IslandCaller.Helpers;
using IslandCaller.Services;
using IslandCaller.ViewModels;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Threading;
using static IslandCaller.Services.ProfileService;
using static IslandCaller.ViewModels.SettingPageViewModel;

namespace IslandCaller.Views;

[SettingsPageInfo("plugins.IslandCaller", "IslandCaller 设置", "\uED39", "\uECF8", SettingsPageCategory.External)]
public partial class SettingPage : SettingsPageBase
{
    private enum HotkeyBindingTarget
    {
        None,
        Quick,
        Advanced
    }

    private SettingPageViewModel vm;
    private HistoryService HistoryService;
    private ILogger<SettingPage> logger;
    private HotkeyBindingTarget _bindingTarget = HotkeyBindingTarget.None;
    private const int HotkeyBindingTimeoutMs = 2500;
    private const int HotkeySequenceCollectMs = 500;
    private CancellationTokenSource? _bindingTimeoutCts;
    private CancellationTokenSource? _mouseChordCts;
    private string? _bindingHintOverride;
    private readonly HashSet<string> _capturedMouseKeys = [];

    public SettingPage()
    {
        InitializeComponent();
        vm = this.DataContext as SettingPageViewModel;
        HistoryService = IAppHost.GetService<HistoryService>();
        logger = IAppHost.GetService<ILogger<SettingPage>>();
        AddHandler(InputElement.KeyDownEvent, HotkeyCapture_OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyUpEvent, HotkeyCapture_OnKeyUp, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerPressedEvent, HotkeyCapture_OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, HotkeyCapture_OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerMovedEvent, HotkeyCapture_OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerWheelChangedEvent, HotkeyCapture_OnPointerWheelChanged, RoutingStrategies.Tunnel);
        logger.LogInformation("SettingPage 初始化完成");
    }

    private void AddButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int nextId = vm.ProfileList.Any() ? vm.ProfileList.Max(s => s.ID) + 1 : 1;
        vm.ProfileList.Add(new SettingPageViewModel.StudentModel
        {
            ID = nextId,
            Name = "",
            Gender = 0,
            ManualWeight = 1.0
        });
        logger.LogInformation("手动新增名单项，ID: {Id}", nextId);
    }

    private async void ImportButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        List<Person> newlist = new List<Person>();
        logger.LogInformation("开始导入名单流程");

        await CommonTaskDialogs.ShowDialog("导入提示", "导入的名单仅支持下列格式: \n\n" +
            "文本名单 (*.txt): 名单仅包含姓名，使用空格，逗号，或换行分隔\n\n" +
            "SecRandom 名单 (\\list\\rool_call_list\\*.json)\n\n" +
            "CSV 名单 (*.csv): 名单包含姓名,性别可选，不能含有标题");

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            logger.LogError("导入名单失败：无法获取 TopLevel");
            await CommonTaskDialogs.ShowDialog("导入失败", "无法获取窗口上下文，请重试。");
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择要导入的名单",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("文本名单")
                {
                    Patterns = new[] { "*.txt" }
                },
                new FilePickerFileType("SecRandom 名单")
                {
                    Patterns = new[] { "*.json" }
                },
                new FilePickerFileType("CSV 名单")
                {
                    Patterns = new[] { "*.csv" }
                }
            }
        });

        if (files.Count == 0)
        {
            logger.LogInformation("用户取消了名单导入");
            return;
        }

        IStorageFile file = files[0];
        string extension = Path.GetExtension(file.Name).ToLowerInvariant();
        logger.LogInformation("已选择导入文件：{FileName}，扩展名：{Extension}", file.Name, extension);

        try
        {
            switch (extension)
            {
                case ".txt":
                    newlist = await new TextFilePraseHelper().ParseTextFileAsync(file);
                    break;
                case ".json":
                    var resultJson = await new SecRandomImport().ShowDialog<(bool isGender, string male, string female)>(this.GetVisualRoot() as Window);
                    logger.LogDebug("SecRandom 导入参数：isGender={IsGender}", resultJson.isGender);
                    newlist = await new SecRandomParseHelper().ParseSecRandomProfileAsync(file, resultJson.isGender, resultJson.male, resultJson.female);
                    break;
                case ".csv":
                    var resultCsv = await new CsvImport().ShowDialog<(int nameRow, int genderRow, bool isGender, string male, string female)>(this.GetVisualRoot() as Window);
                    resultCsv.nameRow -= 1;
                    resultCsv.genderRow -= 1;
                    if (!resultCsv.isGender) resultCsv.genderRow = -1;
                    logger.LogDebug("CSV 导入参数：nameRow={NameRow}, genderRow={GenderRow}, isGender={IsGender}", resultCsv.nameRow, resultCsv.genderRow, resultCsv.isGender);
                    newlist = await new CsvParseHelper().ParseCsvFileAsync(file, resultCsv.nameRow, resultCsv.genderRow, resultCsv.male, resultCsv.female);
                    break;
                default:
                    logger.LogError("导入名单失败：不支持的文件类型 {Extension}", extension);
                    await CommonTaskDialogs.ShowDialog("导入失败", $"不支持的文件类型：{extension}");
                    return;
            }

            var orderedProfile = newlist
                .OrderBy(m => m.Id)
                .Select(m => new StudentModel
                {
                    ID = m.Id,
                    Name = m.Name,
                    Gender = m.Gender,
                    ManualWeight = m.ManualWeight
                });

            vm.ProfileList = new ObservableCollection<StudentModel>(orderedProfile);
            logger.LogInformation("名单导入成功，共导入 {Count} 人", vm.ProfileList.Count);
            await CommonTaskDialogs.ShowDialog("导入完成", $"成功导入 {vm.ProfileList.Count} 条名单。");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "导入名单过程中发生异常，文件：{FileName}", file.Name);
            await CommonTaskDialogs.ShowDialog("导入失败", "导入名单时发生错误，请检查文件格式后重试。");
        }
    }

    private void ClearButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        logger.LogInformation("清空点名历史记录");
        HistoryService.ClearThisLessonHistory();
        HistoryService.ClearLongTermHistory();
    }

    private void StartQuickHotkeyBindingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        BeginHotkeyBinding(HotkeyBindingTarget.Quick);
    }

    private void StartAdvancedHotkeyBindingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        BeginHotkeyBinding(HotkeyBindingTarget.Advanced);
    }

    private void ResetQuickHotkeyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (vm == null)
        {
            return;
        }

        vm.QuickCallHotkey = "Ctrl+Alt+R";
        _bindingHintOverride = "快速点名快捷键已重置为 Ctrl+Alt+R。";
        EndHotkeyBinding();
        logger.LogInformation("已单独重置快速点名快捷键为默认值");
    }

    private void ResetAdvancedHotkeyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (vm == null)
        {
            return;
        }

        vm.AdvancedCallHotkey = "Ctrl+Alt+G";
        _bindingHintOverride = "高级点名快捷键已重置为 Ctrl+Alt+G。";
        EndHotkeyBinding();
        logger.LogInformation("已单独重置高级点名快捷键为默认值");
    }

    private async void ResetHotkeysButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (vm == null)
        {
            return;
        }

        vm.QuickCallHotkey = "Ctrl+Alt+R";
        vm.AdvancedCallHotkey = "Ctrl+Alt+G";
        _bindingHintOverride = null;
        EndHotkeyBinding();
        logger.LogInformation("快捷键已重置为默认值：Quick={Quick}, Advanced={Advanced}", vm.QuickCallHotkey, vm.AdvancedCallHotkey);
        await CommonTaskDialogs.ShowDialog("快捷键已重置", "快速点名：Ctrl+Alt+R\n高级点名：Ctrl+Alt+G");
    }

    private void HotkeyCapture_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_bindingTarget == HotkeyBindingTarget.None)
        {
            return;
        }

        if (e.Source is PopupRoot)
        {
            return;
        }

        SetHotkeyFromInput(e, _bindingTarget);
    }

    private void HotkeyCapture_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (_bindingTarget == HotkeyBindingTarget.None)
        {
            return;
        }

        if (e.Source is PopupRoot)
        {
            return;
        }

        e.Handled = true;
    }

    private void HotkeyCapture_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_bindingTarget == HotkeyBindingTarget.None)
        {
            return;
        }

        if (e.Source is PopupRoot)
        {
            return;
        }

        CaptureMouseHotkeyInput(e, _bindingTarget);
        e.Handled = true;
    }

    private void HotkeyCapture_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_bindingTarget == HotkeyBindingTarget.None)
        {
            return;
        }

        if (e.Source is PopupRoot)
        {
            return;
        }

        e.Handled = true;
    }

    private void HotkeyCapture_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_bindingTarget == HotkeyBindingTarget.None)
        {
            return;
        }

        if (e.Source is PopupRoot)
        {
            return;
        }

        e.Handled = true;
    }

    private void HotkeyCapture_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_bindingTarget == HotkeyBindingTarget.None)
        {
            return;
        }

        if (e.Source is PopupRoot)
        {
            return;
        }

        e.Handled = true;
    }

    private void SetHotkeyFromInput(KeyEventArgs e, HotkeyBindingTarget target)
    {
        if (vm == null)
        {
            return;
        }

        if (_bindingTarget != target)
        {
            return;
        }

        if (e.Key is Key.Back or Key.Delete or Key.Escape)
        {
            CancelMouseChordCapture();
            _capturedMouseKeys.Clear();
            ApplyHotkeyBinding(target, string.Empty);

            e.Handled = true;
            EndHotkeyBinding();
            return;
        }

        if (!TryCaptureKeyToken(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            return;
        }

        StartOrRestartHotkeyCommit(target);

        e.Handled = true;
    }

    private void CaptureMouseHotkeyInput(PointerPressedEventArgs e, HotkeyBindingTarget target)
    {
        if (vm == null)
        {
            return;
        }

        var pointerPoint = e.GetCurrentPoint(this);
        string mainKey = GetMouseKeyText(pointerPoint.Properties.PointerUpdateKind);
        if (string.IsNullOrWhiteSpace(mainKey))
        {
            return;
        }

        if (_bindingTarget != target)
        {
            BeginHotkeyBinding(target);
        }

        if (!TryCapturePointerToken(mainKey, e.KeyModifiers))
        {
            return;
        }

        StartOrRestartHotkeyCommit(target);
    }

    private void StartOrRestartHotkeyCommit(HotkeyBindingTarget target)
    {
        CancelMouseChordCapture();
        _mouseChordCts = new CancellationTokenSource();
        var token = _mouseChordCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(HotkeySequenceCollectMs, token);
                if (token.IsCancellationRequested || _bindingTarget != target)
                {
                    return;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (_bindingTarget != target || _capturedMouseKeys.Count == 0)
                    {
                        return;
                    }

                    string hotkeyText = BuildHotkeyTextFromCapturedTokens(_capturedMouseKeys);
                    bool updated = ApplyHotkeyBinding(target, hotkeyText);
                    _capturedMouseKeys.Clear();

                    if (updated)
                    {
                        EndHotkeyBinding();
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }, token);
    }

    private void CancelMouseChordCapture()
    {
        _mouseChordCts?.Cancel();
        _mouseChordCts?.Dispose();
        _mouseChordCts = null;
    }

    private bool TryCaptureKeyToken(Key key, KeyModifiers modifiers)
    {
        AddModifierTokens(modifiers);

        string token = GetSequentialKeyToken(key);
        if (string.IsNullOrWhiteSpace(token))
        {
            return _capturedMouseKeys.Count > 0;
        }

        return TryAddHotkeyToken(token);
    }

    private bool TryCapturePointerToken(string pointerToken, KeyModifiers modifiers)
    {
        AddModifierTokens(modifiers);
        return TryAddHotkeyToken(pointerToken);
    }

    private void AddModifierTokens(KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Control)) _capturedMouseKeys.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift)) _capturedMouseKeys.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Alt)) _capturedMouseKeys.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Meta)) _capturedMouseKeys.Add("Win");
    }

    private bool TryAddHotkeyToken(string token)
    {
        if (IsModifierToken(token))
        {
            _capturedMouseKeys.Add(token);
            return true;
        }

        int mainCount = _capturedMouseKeys.Count(x => !IsModifierToken(x));
        if (!_capturedMouseKeys.Contains(token) && mainCount >= 2)
        {
            _bindingHintOverride = "绑定无效：最多支持两个主键（可配合 Ctrl/Shift/Alt/Win）。";
            UpdateHotkeyBindingHint();
            return false;
        }

        _capturedMouseKeys.Add(token);
        return true;
    }

    private static bool IsModifierToken(string token)
    {
        return token is "Ctrl" or "Shift" or "Alt" or "Win";
    }

    private static string GetSequentialKeyToken(Key key)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => "Ctrl",
            Key.LeftShift or Key.RightShift => "Shift",
            Key.LeftAlt or Key.RightAlt => "Alt",
            Key.LWin or Key.RWin => "Win",
            _ => GetMainKeyText(key)
        };
    }

    private bool ApplyHotkeyBinding(HotkeyBindingTarget target, string hotkeyText)
    {
        if (!string.IsNullOrWhiteSpace(hotkeyText))
        {
            if (IsBareLeftOrRightMouseHotkey(hotkeyText))
            {
                _bindingHintOverride = "绑定无效：不允许仅使用鼠标左键或右键，请添加组合键。";
                UpdateHotkeyBindingHint();
                logger.LogWarning("快捷键绑定无效（单左键/右键）：Target={Target}, Hotkey={Hotkey}", target, hotkeyText);
                return false;
            }

            string otherHotkey = target == HotkeyBindingTarget.Quick
                ? vm.AdvancedCallHotkey
                : vm.QuickCallHotkey;

            if (AreHotkeysEquivalent(hotkeyText, otherHotkey))
            {
                _bindingHintOverride = "绑定冲突：快速点名和高级点名不能使用同一个按键，请重新绑定。";
                UpdateHotkeyBindingHint();
                logger.LogWarning("快捷键绑定冲突，Target={Target}, Hotkey={Hotkey}", target, hotkeyText);
                return false;
            }
        }

        _bindingHintOverride = null;
        if (target == HotkeyBindingTarget.Quick)
        {
            vm.QuickCallHotkey = hotkeyText;
            logger.LogInformation("快速点名快捷键已设置为 {Hotkey}", hotkeyText);
            return true;
        }

        if (target == HotkeyBindingTarget.Advanced)
        {
            vm.AdvancedCallHotkey = hotkeyText;
            logger.LogInformation("高级点名快捷键已设置为 {Hotkey}", hotkeyText);
            return true;
        }

        return false;
    }

    private static bool IsBareLeftOrRightMouseHotkey(string hotkeyText)
    {
        string normalized = hotkeyText.Trim();
        return normalized.Equals("MOUSELEFT", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("LBUTTON", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("MOUSERIGHT", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("RBUTTON", StringComparison.OrdinalIgnoreCase)
            || normalized == "鼠标左键"
            || normalized == "左键"
            || normalized == "鼠标右键"
            || normalized == "右键";
    }

    private void BeginHotkeyBinding(HotkeyBindingTarget target)
    {
        if (_bindingTarget == target)
        {
            return;
        }

        _bindingTarget = target;
        _bindingHintOverride = null;
        _capturedMouseKeys.Clear();
        StartHotkeyBindingTimeout(target);
        Focus();
        UpdateHotkeyBindingHint();
    }

    private void EndHotkeyBinding()
    {
        CancelMouseChordCapture();
        _capturedMouseKeys.Clear();
        _bindingTimeoutCts?.Cancel();
        _bindingTimeoutCts?.Dispose();
        _bindingTimeoutCts = null;
        _bindingTarget = HotkeyBindingTarget.None;
        UpdateHotkeyBindingHint();
    }

    private void StartHotkeyBindingTimeout(HotkeyBindingTarget target)
    {
        _bindingTimeoutCts?.Cancel();
        _bindingTimeoutCts?.Dispose();
        _bindingTimeoutCts = new CancellationTokenSource();
        var token = _bindingTimeoutCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(HotkeyBindingTimeoutMs, token);
                if (token.IsCancellationRequested || _bindingTarget != target)
                {
                    return;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (_bindingTarget != target)
                    {
                        return;
                    }

                    _bindingHintOverride = "监听超时：请重新点击“开始绑定”后按键。";
                    EndHotkeyBinding();
                });
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }, token);
    }

    private static bool AreHotkeysEquivalent(string left, string right)
    {
        static string Normalize(string text)
        {
            string CanonicalToken(string token)
            {
                return token.ToUpperInvariant() switch
                {
                    "CONTROL" => "CTRL",
                    "WINDOWS" => "WIN",
                    "ESCAPE" => "ESC",
                    "RETURN" => "ENTER",
                    "LBUTTON" => "MOUSELEFT",
                    "RBUTTON" => "MOUSERIGHT",
                    "MBUTTON" => "MOUSEMIDDLE",
                    "鼠标左键" => "MOUSELEFT",
                    "左键" => "MOUSELEFT",
                    "鼠标右键" => "MOUSERIGHT",
                    "右键" => "MOUSERIGHT",
                    "鼠标中键" => "MOUSEMIDDLE",
                    "中键" => "MOUSEMIDDLE",
                    "XBUTTON1" => "MOUSEX1",
                    "XBUTTON2" => "MOUSEX2",
                    var t => t
                };
            }

            var tokens = text
                .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(CanonicalToken)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            string[] modifierOrder = ["CTRL", "SHIFT", "ALT", "WIN"];
            var modifiers = modifierOrder.Where(tokens.Contains).ToList();
            var mains = tokens.Where(t => !modifierOrder.Contains(t)).OrderBy(t => t, StringComparer.Ordinal).ToList();

            return string.Join("+", modifiers.Concat(mains));
        }

        return Normalize(left) == Normalize(right);
    }

    private void UpdateHotkeyBindingHint()
    {
        if (HotkeyCaptureHint == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_bindingHintOverride))
        {
            HotkeyCaptureHint.Text = _bindingHintOverride;
            return;
        }

        HotkeyCaptureHint.Text = _bindingTarget switch
        {
            HotkeyBindingTarget.Quick => "正在监听：快速点名。键鼠操作已屏蔽，请按下用于绑定的按键...",
            HotkeyBindingTarget.Advanced => "正在监听：高级点名。键鼠操作已屏蔽，请按下用于绑定的按键...",
            _ => "按键绑定模式：点击开始绑定，可依次按下组合键完成绑定（仅禁止单独鼠标左键/鼠标右键；Esc/Backspace/Delete 清空）"
        };
    }

    private static string BuildHotkeyText(KeyEventArgs e)
    {
        string mainKey = GetMainKeyText(e.Key);
        if (string.IsNullOrWhiteSpace(mainKey))
        {
            return string.Empty;
        }

        return BuildHotkeyTextFromParts(e.KeyModifiers, mainKey);
    }

    private static string BuildHotkeyTextFromParts(KeyModifiers keyModifiers, string mainKey)
    {
        return BuildHotkeyTextFromParts(keyModifiers, [mainKey]);
    }

    private static string BuildHotkeyTextFromCapturedTokens(IEnumerable<string> tokens)
    {
        string[] modifierOrder = ["Ctrl", "Shift", "Alt", "Win"];
        var list = tokens.Distinct(StringComparer.Ordinal).ToList();

        var modifiers = modifierOrder.Where(list.Contains).ToList();
        var mains = list.Where(t => !modifierOrder.Contains(t)).OrderBy(t => t, StringComparer.Ordinal).ToList();

        return string.Join("+", modifiers.Concat(mains));
    }

    private static string BuildHotkeyTextFromParts(KeyModifiers keyModifiers, IEnumerable<string> mainKeys)
    {
        var parts = new List<string>(5);

        if (keyModifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (keyModifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (keyModifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (keyModifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Win");
        }

        foreach (var mainKey in mainKeys
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(x => x, StringComparer.Ordinal))
        {
            parts.Add(mainKey);
        }

        return string.Join("+", parts);
    }

    private static string GetMouseKeyText(PointerUpdateKind updateKind)
    {
        return updateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => "鼠标左键",
            PointerUpdateKind.RightButtonPressed => "鼠标右键",
            PointerUpdateKind.MiddleButtonPressed => "鼠标中键",
            PointerUpdateKind.XButton1Pressed => "MouseX1",
            PointerUpdateKind.XButton2Pressed => "MouseX2",
            _ => string.Empty
        };
    }

    private static string GetMainKeyText(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            return key.ToString();
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return key.ToString()[1..];
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return key.ToString()[^1..];
        }

        if (key >= Key.F1 && key <= Key.F24)
        {
            return key.ToString();
        }

        return key switch
        {
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Escape => "Esc",
            Key.LeftCtrl => string.Empty,
            Key.RightCtrl => string.Empty,
            Key.LeftShift => string.Empty,
            Key.RightShift => string.Empty,
            Key.LeftAlt => string.Empty,
            Key.RightAlt => string.Empty,
            Key.LWin => string.Empty,
            Key.RWin => string.Empty,
            _ => string.Empty
        };
    }
}
