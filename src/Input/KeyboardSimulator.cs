using System.Runtime.InteropServices;
using SleepRunner.Utils;

namespace SleepRunner.Input;

/// <summary>
/// 键盘模拟工具，使用 SendInput 发送系统级键盘事件
/// INPUT 结构体在 x64 上为 40 字节，需要用 Explicit 布局精确控制偏移
/// </summary>
public static class KeyboardSimulator
{
    private static readonly Random _rng = new();

    #region Win32 API

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, KINPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    // 常用虚拟键码
    public const ushort VK_MENU = 0x12;    // Alt
    public const ushort VK_LMENU = 0xA4;   // Left Alt
    public const ushort VK_CONTROL = 0x11; // Ctrl
    public const ushort VK_SHIFT = 0x10;   // Shift
    public const ushort VK_ESCAPE = 0x1B;  // Esc
    public const ushort VK_SPACE = 0x20;   // Space
    public const ushort VK_1 = 0x31;
    public const ushort VK_2 = 0x32;
    public const ushort VK_3 = 0x33;
    public const ushort VK_4 = 0x34;
    public const ushort VK_5 = 0x35;
    public const ushort VK_Z = 0x5A;

    /// <summary>
    /// x64 上 INPUT 结构体为 40 字节，type 在 offset 0，键盘字段从 offset 8 开始
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct KINPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public ushort wVk;
        [FieldOffset(10)] public ushort wScan;
        [FieldOffset(12)] public uint dwFlags;
        [FieldOffset(16)] public uint time;
        [FieldOffset(24)] public IntPtr dwExtraInfo;
    }

    #endregion

    /// <summary>
    /// 发送组合键（如 Alt+1），模拟按住修饰键再按目标键
    /// </summary>
    public static async Task SendHotkey(IntPtr hWnd, ushort modifier, ushort key)
    {
        if (!await EnsureWindowFocused(hWnd))
        {
            Log.Log($"Hotkey aborted: target window not focused before modifier=0x{modifier:X2} + key=0x{key:X2}.");
            return;
        }

        bool modifierDown = false;
        bool keyDown = false;
        try
        {
            if (!IsWindowFocused(hWnd))
            {
                Log.Log($"Hotkey aborted: target window lost focus before modifier down 0x{modifier:X2}.");
                return;
            }

            // 按下修饰键，保持足够久让游戏检测到
            if (!SendSingleKey(modifier, false))
            {
                Log.Log($"Hotkey aborted: modifier down failed 0x{modifier:X2}.");
                return;
            }
            modifierDown = true;
            await Task.Delay(500);

            if (!IsWindowFocused(hWnd))
            {
                Log.Log($"Hotkey aborted: target window lost focus before key down 0x{key:X2}.");
                return;
            }

            // 按下并释放目标键
            if (!SendSingleKey(key, false))
            {
                Log.Log($"Hotkey aborted: key down failed 0x{key:X2}.");
                return;
            }
            keyDown = true;
            await Task.Delay(100);

            if (!IsWindowFocused(hWnd))
            {
                Log.Log($"Hotkey aborted: target window lost focus before key up 0x{key:X2}.");
                return;
            }

            if (!SendSingleKey(key, true))
                Log.Log($"Hotkey key up failed 0x{key:X2}.");
            keyDown = false;
            await Task.Delay(200);
        }
        finally
        {
            if (keyDown)
                SendSingleKey(key, true);
            if (modifierDown)
                SendSingleKey(modifier, true);
        }

        Log.Log($"Hotkey sent: modifier=0x{modifier:X2} + key=0x{key:X2}");
        await Task.Delay(_rng.Next(50, 120));
    }

    /// <summary>
    /// 发送单个按键（按下+释放）
    /// </summary>
    public static async Task SendKey(IntPtr hWnd, ushort key)
    {
        if (!await EnsureWindowFocused(hWnd))
        {
            Log.Log($"Key aborted: target window not focused before key=0x{key:X2}.");
            return;
        }

        bool keyDown = false;
        try
        {
            if (!IsWindowFocused(hWnd))
            {
                Log.Log($"Key aborted: target window lost focus before key down 0x{key:X2}.");
                return;
            }

            if (!SendSingleKey(key, false))
            {
                Log.Log($"Key aborted: key down failed 0x{key:X2}.");
                return;
            }
            keyDown = true;
            await Task.Delay(_rng.Next(30, 80));

            if (!IsWindowFocused(hWnd))
            {
                Log.Log($"Key aborted: target window lost focus before key up 0x{key:X2}.");
                return;
            }

            if (!SendSingleKey(key, true))
                Log.Log($"Key up failed 0x{key:X2}.");
            keyDown = false;
        }
        finally
        {
            if (keyDown)
                SendSingleKey(key, true);
        }

        Log.Log($"Key sent: 0x{key:X2}");
        await Task.Delay(_rng.Next(50, 120));
    }

    public static bool SendKeyDown(ushort key)
    {
        return SendSingleKey(key, false);
    }

    public static bool SendKeyUp(ushort key)
    {
        return SendSingleKey(key, true);
    }

    public static bool IsWindowFocused(IntPtr hWnd)
    {
        return hWnd != IntPtr.Zero && GetForegroundWindow() == hWnd;
    }

    public static ushort GetHardwareScanCode(ushort virtualKey)
    {
        return virtualKey switch
        {
            VK_ESCAPE => 0x01,
            VK_1 => 0x02,
            VK_2 => 0x03,
            VK_3 => 0x04,
            VK_4 => 0x05,
            VK_5 => 0x06,
            VK_Z => 0x2C,
            VK_SPACE => 0x39,
            VK_LMENU => 0x38,
            VK_MENU => 0x38,
            VK_CONTROL => 0x1D,
            VK_SHIFT => 0x2A,
            _ => 0,
        };
    }

    /// <summary>
    /// 抢占并确认游戏窗口前台焦点，避免热键发送到错误窗口
    /// </summary>
    public static async Task<bool> EnsureWindowFocused(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            SetForegroundWindow(hWnd);
            await Task.Delay(_rng.Next(90, 150));

            if (GetForegroundWindow() == hWnd)
            {
                if (attempt > 1)
                    Log.Log($"Focus acquired on attempt {attempt}.");
                return true;
            }

            // 发送一次 Alt 键按下释放，帮助系统接受前台切换请求
            SendSingleKey(VK_MENU, false);
            await Task.Delay(40);
            SendSingleKey(VK_MENU, true);
            await Task.Delay(80);
        }

        bool focused = GetForegroundWindow() == hWnd;
        Log.Log($"Focus acquire result: {focused}");
        return focused;
    }

    private static bool SendSingleKey(ushort vk, bool keyUp)
    {
        ushort scanCode = GetHardwareScanCode(vk);
        bool useScanCode = scanCode != 0;
        var input = new KINPUT
        {
            type = INPUT_KEYBOARD,
            wVk = useScanCode ? (ushort)0 : vk,
            wScan = scanCode,
            dwFlags = (keyUp ? KEYEVENTF_KEYUP : 0) | (useScanCode ? KEYEVENTF_SCANCODE : 0),
            time = 0,
            dwExtraInfo = IntPtr.Zero,
        };

        var inputs = new[] { input };
        uint sent = SendInput(1, inputs, Marshal.SizeOf<KINPUT>());
        if (sent == 0)
        {
            int err = Marshal.GetLastWin32Error();
            Log.Log($"SendInput failed! vk=0x{vk:X2} keyUp={keyUp} error={err}");
            return false;
        }

        return true;
    }
    private static readonly LogScope Log = new("Keyboard");
}
