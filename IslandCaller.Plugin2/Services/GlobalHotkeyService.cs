using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using IslandCaller.Models;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace IslandCaller.Services;

public sealed class GlobalHotkeyService
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;

    private const int VkControl = 0x11;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkShift = 0x10;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;
    private const int VkMenu = 0x12;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const int VkLButton = 0x01;
    private const int VkRButton = 0x02;
    private const int VkMButton = 0x04;
    private const int VkXButton1 = 0x05;
    private const int VkXButton2 = 0x06;

    private readonly global::IslandCaller.Services.IslandCallerService.IslandCallerService _islandCallerService;
    private readonly ILogger<GlobalHotkeyService> _logger;
    private readonly HashSet<int> _pressedKeys = [];
    private readonly HashSet<int> _suppressedKeys = [];

    private nint _keyboardHookHandle;
    private nint _mouseHookHandle;
    private HookProc? _keyboardHookProc;
    private HookProc? _mouseHookProc;
    private bool _initialized;

    private HotkeyDefinition _quickHotkey;
    private HotkeyDefinition _advancedHotkey;
    private bool _quickTriggered;
    private bool _advancedTriggered;

    public GlobalHotkeyService(global::IslandCaller.Services.IslandCallerService.IslandCallerService islandCallerService, ILogger<GlobalHotkeyService> logger)
    {
        _islandCallerService = islandCallerService;
        _logger = logger;
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _keyboardHookProc = KeyboardHookCallback;
        _mouseHookProc = MouseHookCallback;

        // Low-level hooks should use a null module handle when callback is inside current process.
        _keyboardHookHandle = SetWindowsHookEx(WhKeyboardLl, _keyboardHookProc, 0, 0);
        _mouseHookHandle = SetWindowsHookEx(WhMouseLl, _mouseHookProc, 0, 0);

        if (_keyboardHookHandle == 0)
        {
            _logger.LogError("全局快捷键初始化失败，无法注册键盘钩子，Win32Error={Error}", Marshal.GetLastWin32Error());
            return;
        }

        if (_mouseHookHandle == 0)
        {
            _logger.LogWarning("鼠标快捷键监听初始化失败，无法注册鼠标钩子，Win32Error={Error}", Marshal.GetLastWin32Error());
        }
        else
        {
            _logger.LogInformation("鼠标快捷键监听已启动");
        }

        Settings.Instance.General.PropertyChanged += OnGeneralSettingChanged;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TryUnhook();
        ReloadHotkeysFromSettings();
        _initialized = true;
        _logger.LogInformation("全局快捷键服务已启动");
    }

    private void TryUnhook()
    {
        if (_keyboardHookHandle != 0)
        {
            if (!UnhookWindowsHookEx(_keyboardHookHandle))
            {
                _logger.LogWarning("键盘快捷键钩子释放失败，Win32Error={Error}", Marshal.GetLastWin32Error());
            }

            _keyboardHookHandle = 0;
        }

        if (_mouseHookHandle != 0)
        {
            if (!UnhookWindowsHookEx(_mouseHookHandle))
            {
                _logger.LogWarning("鼠标快捷键钩子释放失败，Win32Error={Error}", Marshal.GetLastWin32Error());
            }

            _mouseHookHandle = 0;
        }
    }

    private void OnGeneralSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GeneralSetting.EnableGlobalHotkeys)
            || e.PropertyName == nameof(GeneralSetting.QuickCallHotkey)
            || e.PropertyName == nameof(GeneralSetting.AdvancedCallHotkey))
        {
            ReloadHotkeysFromSettings();
        }
    }

    private void ReloadHotkeysFromSettings()
    {
        _quickTriggered = false;
        _advancedTriggered = false;

        if (!Settings.Instance.General.EnableGlobalHotkeys)
        {
            _quickHotkey = default;
            _advancedHotkey = default;
            _logger.LogInformation("全局快捷键已禁用");
            return;
        }

        if (!TryParseHotkey(Settings.Instance.General.QuickCallHotkey, out _quickHotkey))
        {
            _logger.LogWarning("快速点名快捷键格式无效: {Hotkey}", Settings.Instance.General.QuickCallHotkey);
            _quickHotkey = default;
        }

        if (!TryParseHotkey(Settings.Instance.General.AdvancedCallHotkey, out _advancedHotkey))
        {
            _logger.LogWarning("高级点名快捷键格式无效: {Hotkey}", Settings.Instance.General.AdvancedCallHotkey);
            _advancedHotkey = default;
        }

        _logger.LogInformation("全局快捷键已更新: Quick={Quick}, Advanced={Advanced}",
            Settings.Instance.General.QuickCallHotkey,
            Settings.Instance.General.AdvancedCallHotkey);
    }

    private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var message = unchecked((int)wParam);
            var vkCode = Marshal.ReadInt32(lParam);
            PruneReleasedKeys(vkCode);

            if (message == WmKeyDown || message == WmSysKeyDown)
            {
                _pressedKeys.Add(vkCode);
                if (_suppressedKeys.Contains(vkCode) || TryTriggerHotkeys())
                {
                    return 1;
                }
            }
            else if (message == WmKeyUp || message == WmSysKeyUp)
            {
                bool shouldSuppress = _suppressedKeys.Contains(vkCode);
                _pressedKeys.Remove(vkCode);
                _suppressedKeys.Remove(vkCode);
                ResetTriggerFlagsIfNeeded();
                if (shouldSuppress)
                {
                    return 1;
                }
            }
        }

        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var message = unchecked((int)wParam);
            int mouseVkCode = GetMouseVirtualKey(message, lParam);

            if (mouseVkCode != 0)
            {
                if (message is WmLButtonDown or WmRButtonDown or WmMButtonDown or WmXButtonDown)
                {
                    _pressedKeys.Add(mouseVkCode);
                    if (_suppressedKeys.Contains(mouseVkCode) || TryTriggerHotkeys())
                    {
                        return 1;
                    }
                }
                else if (message is WmLButtonUp or WmRButtonUp or WmMButtonUp or WmXButtonUp)
                {
                    bool shouldSuppress = _suppressedKeys.Contains(mouseVkCode);
                    _pressedKeys.Remove(mouseVkCode);
                    _suppressedKeys.Remove(mouseVkCode);
                    ResetTriggerFlagsIfNeeded();
                    if (shouldSuppress)
                    {
                        return 1;
                    }
                }
            }
        }

        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private static int GetMouseVirtualKey(int message, nint lParam)
    {
        return message switch
        {
            WmLButtonDown or WmLButtonUp => VkLButton,
            WmRButtonDown or WmRButtonUp => VkRButton,
            WmMButtonDown or WmMButtonUp => VkMButton,
            WmXButtonDown or WmXButtonUp => ResolveXButtonVirtualKey(lParam),
            _ => 0
        };
    }

    private static int ResolveXButtonVirtualKey(nint lParam)
    {
        var data = Marshal.PtrToStructure<MouseLowLevelHookData>(lParam);
        int xButton = (data.MouseData >> 16) & 0xFFFF;
        return xButton switch
        {
            1 => VkXButton1,
            2 => VkXButton2,
            _ => 0
        };
    }

    private bool TryTriggerHotkeys()
    {
        if (!_quickTriggered && IsHotkeyPressed(_quickHotkey))
        {
            _quickTriggered = true;
            SuppressHotkey(_quickHotkey);
            Dispatcher.UIThread.Post(() => _islandCallerService.ShowRandomStudent(1));
            return true;
        }

        if (!_advancedTriggered && IsHotkeyPressed(_advancedHotkey))
        {
            _advancedTriggered = true;
            SuppressHotkey(_advancedHotkey);
            Dispatcher.UIThread.Post(ShowPersonalCallWindow);
            return true;
        }

        return false;
    }

    private void ShowPersonalCallWindow()
    {
        var existingWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.OfType<PersonalCall>().FirstOrDefault()
            : null;

        if (existingWindow != null)
        {
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }

            existingWindow.Activate();
            return;
        }

        new PersonalCall().Show();
    }

    private void ResetTriggerFlagsIfNeeded()
    {
        if (!IsHotkeyPressed(_quickHotkey))
        {
            _quickTriggered = false;
        }

        if (!IsHotkeyPressed(_advancedHotkey))
        {
            _advancedTriggered = false;
        }
    }

    private bool IsHotkeyPressed(HotkeyDefinition hotkey)
    {
        if (hotkey.PrimaryKeyCode == 0)
        {
            return false;
        }

        bool ctrlPressed = IsCtrlPressed();
        bool shiftPressed = IsShiftPressed();
        bool altPressed = IsAltPressed();
        bool winPressed = IsWinPressed();

        if (ctrlPressed != hotkey.Ctrl || shiftPressed != hotkey.Shift || altPressed != hotkey.Alt || winPressed != hotkey.Win)
        {
            return false;
        }

        if (!_pressedKeys.Contains(hotkey.PrimaryKeyCode))
        {
            return false;
        }

        return hotkey.SecondaryKeyCode == 0 || _pressedKeys.Contains(hotkey.SecondaryKeyCode);
    }

    private bool IsCtrlPressed() =>
        _pressedKeys.Contains(VkControl) || _pressedKeys.Contains(VkLControl) || _pressedKeys.Contains(VkRControl);

    private bool IsShiftPressed() =>
        _pressedKeys.Contains(VkShift) || _pressedKeys.Contains(VkLShift) || _pressedKeys.Contains(VkRShift);

    private bool IsAltPressed() =>
        _pressedKeys.Contains(VkMenu) || _pressedKeys.Contains(VkLMenu) || _pressedKeys.Contains(VkRMenu);

    private bool IsWinPressed() =>
        _pressedKeys.Contains(VkLWin) || _pressedKeys.Contains(VkRWin);

    private void PruneReleasedKeys(int currentVkCode)
    {
        foreach (int trackedKey in _pressedKeys.ToArray())
        {
            if (trackedKey == currentVkCode)
            {
                continue;
            }

            if (!IsKeyCurrentlyDown(trackedKey))
            {
                _pressedKeys.Remove(trackedKey);
                _suppressedKeys.Remove(trackedKey);
            }
        }

        ResetTriggerFlagsIfNeeded();
    }

    private static bool IsKeyCurrentlyDown(int vkCode)
    {
        short state = GetAsyncKeyState(vkCode);
        return (state & 0x8000) != 0;
    }

    private void SuppressHotkey(HotkeyDefinition hotkey)
    {
        if (hotkey.PrimaryKeyCode != 0)
        {
            _suppressedKeys.Add(hotkey.PrimaryKeyCode);
        }

        if (hotkey.SecondaryKeyCode != 0)
        {
            _suppressedKeys.Add(hotkey.SecondaryKeyCode);
        }

        AddModifierKeysForSuppression(hotkey);
    }

    private void AddModifierKeysForSuppression(HotkeyDefinition hotkey)
    {
        if (hotkey.Ctrl)
        {
            AddPressedModifierKeys(VkControl, VkLControl, VkRControl);
        }

        if (hotkey.Shift)
        {
            AddPressedModifierKeys(VkShift, VkLShift, VkRShift);
        }

        if (hotkey.Alt)
        {
            AddPressedModifierKeys(VkMenu, VkLMenu, VkRMenu);
        }

        if (hotkey.Win)
        {
            AddPressedModifierKeys(VkLWin, VkRWin);
        }
    }

    private void AddPressedModifierKeys(params int[] keyCodes)
    {
        foreach (int keyCode in keyCodes)
        {
            if (_pressedKeys.Contains(keyCode))
            {
                _suppressedKeys.Add(keyCode);
            }
        }
    }

    private static bool TryParseHotkey(string? text, out HotkeyDefinition hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        bool ctrl = false;
        bool alt = false;
        bool shift = false;
        bool win = false;
        int primaryKeyCode = 0;
        int secondaryKeyCode = 0;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var rawPart in parts)
        {
            var part = rawPart.ToUpperInvariant();
            switch (part)
            {
                case "CTRL":
                case "CONTROL":
                    ctrl = true;
                    continue;
                case "ALT":
                    alt = true;
                    continue;
                case "SHIFT":
                    shift = true;
                    continue;
                case "WIN":
                case "WINDOWS":
                    win = true;
                    continue;
            }

            int keyCode = ParseMainKey(part);
            if (keyCode == 0)
            {
                return false;
            }

            if (primaryKeyCode == 0)
            {
                primaryKeyCode = keyCode;
                continue;
            }

            if (secondaryKeyCode == 0 && keyCode != primaryKeyCode)
            {
                secondaryKeyCode = keyCode;
                continue;
            }

            return false;
        }

        if (primaryKeyCode == 0)
        {
            return false;
        }

        if (secondaryKeyCode == 0
            && !ctrl
            && !alt
            && !shift
            && !win
            && (primaryKeyCode == VkLButton || primaryKeyCode == VkRButton))
        {
            return false;
        }

        hotkey = new HotkeyDefinition(primaryKeyCode, secondaryKeyCode, ctrl, alt, shift, win);
        return true;
    }

    private static int ParseMainKey(string key)
    {
        if (key.Length == 1)
        {
            char c = key[0];
            if (c >= 'A' && c <= 'Z')
            {
                return c;
            }

            if (c >= '0' && c <= '9')
            {
                return c;
            }
        }

        if (key.StartsWith('F') && int.TryParse(key[1..], out int fn) && fn >= 1 && fn <= 24)
        {
            return 0x70 + fn - 1;
        }

        return key switch
        {
            "SPACE" => 0x20,
            "ENTER" or "RETURN" => 0x0D,
            "TAB" => 0x09,
            "ESC" or "ESCAPE" => 0x1B,
            "MOUSELEFT" or "LBUTTON" or "鼠标左键" or "左键" => VkLButton,
            "MOUSERIGHT" or "RBUTTON" or "鼠标右键" or "右键" => VkRButton,
            "MOUSEMIDDLE" or "MBUTTON" or "鼠标中键" or "中键" => VkMButton,
            "MOUSEX1" or "XBUTTON1" => VkXButton1,
            "MOUSEX2" or "XBUTTON2" => VkXButton2,
            _ => 0
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseLowLevelHookData
    {
        public int X;
        public int Y;
        public int MouseData;
        public int Flags;
        public int Time;
        public nint DwExtraInfo;
    }

    private readonly record struct HotkeyDefinition(int PrimaryKeyCode, int SecondaryKeyCode, bool Ctrl, bool Alt, bool Shift, bool Win);

    private delegate nint HookProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);
}
