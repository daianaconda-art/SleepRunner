using System.Runtime.InteropServices;
using SleepRunner.Automation.Race;
using SleepRunner.Utils;

namespace SleepRunner.Input;

/// <summary>
/// 鼠标模拟工具
/// 使用 SendInput（系统级硬件输入队列），加入人类行为模拟降低检测风险
/// </summary>
public static class MouseSimulator
{
    private static readonly Random _rng = new();

    #region Win32 API

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetMessageExtraInfo();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int ENUM_CURRENT_SETTINGS = -1;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion;
        public short dmSize, dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY;
        public int dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public int dmReserved1, dmReserved2;
        public int dmPanningWidth, dmPanningHeight;
    }

    #endregion

    /// <summary>
    /// 检测 DPI 缩放因子
    /// </summary>
    public static float GetDpiScale()
    {
        int logicalW = GetSystemMetrics(SM_CXSCREEN);

        var dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
        EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm);
        int physicalW = dm.dmPelsWidth;

        if (logicalW > 0 && physicalW > logicalW)
            return (float)physicalW / logicalW;

        return 1.0f;
    }

    /// <summary>
    /// 在游戏窗口的指定客户区坐标处执行人类模拟点击
    /// </summary>
    public static async Task ClickAtClient(IntPtr hWnd, int clientX, int clientY)
    {
        float dpiScale = GetDpiScale();

        // 随机偏移：人不会每次都精准点中心，加 ±3~8 像素的随机抖动
        int offsetX = _rng.Next(-5, 6);
        int offsetY = _rng.Next(-5, 6);
        int finalX = clientX + offsetX;
        int finalY = clientY + offsetY;

        // DPI 补偿
        int adjustedX = (int)(finalX / dpiScale);
        int adjustedY = (int)(finalY / dpiScale);

        var pt = new POINT { X = adjustedX, Y = adjustedY };
        ClientToScreen(hWnd, ref pt);

        Log.Log(
            $"Click: clientPhysicalTarget=({clientX},{clientY}), offset=({offsetX},{offsetY}), " +
            $"clientPhysicalFinal=({finalX},{finalY}), clientLogicalFinal=({adjustedX},{adjustedY}), " +
            $"screen=({pt.X},{pt.Y}), dpi={dpiScale:F1}");

        // 置顶游戏窗口
        SetForegroundWindow(hWnd);
        await Task.Delay(RandDelay(150, 300));

        // 模拟鼠标移动轨迹（不是瞬移，分多步曲线移动）
        GetCursorPos(out var start);
        await SimulateMouseMove(start.X, start.Y, pt.X, pt.Y);

        // 随机等待（人移动到目标后会有短暂犹豫）
        await Task.Delay(RandDelay(50, 150));

        // 按下（人按下鼠标的时长有随机性）
        SendMouseEvent(MOUSEEVENTF_LEFTDOWN);
        await Task.Delay(RandDelay(60, 140));

        // 抬起
        SendMouseEvent(MOUSEEVENTF_LEFTUP);

        Log.Log("Human-like click completed");
    }

    /// <summary>
    /// 在指定客户区位置滚动鼠标滚轮
    /// </summary>
    public static async Task ScrollAtClient(IntPtr hWnd, int clientX, int clientY, int clicks)
    {
        float dpiScale = GetDpiScale();
        int adjustedX = (int)(clientX / dpiScale);
        int adjustedY = (int)(clientY / dpiScale);
        var pt = new POINT { X = adjustedX, Y = adjustedY };
        ClientToScreen(hWnd, ref pt);

        SetForegroundWindow(hWnd);
        await Task.Delay(RandDelay(100, 200));

        SetCursorPos(pt.X, pt.Y);
        await Task.Delay(RandDelay(50, 100));

        // 每个 click = 120 WHEEL_DELTA 单位，负值向下滚
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                mouseData = (uint)(clicks * 120),
                dwFlags = MOUSEEVENTF_WHEEL,
                dwExtraInfo = GetMessageExtraInfo()
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
        Log.Log($"Scroll: clicks={clicks} at screen=({pt.X},{pt.Y})");
    }

    /// <summary>
    /// 仅移动鼠标到窗口客户区目标点，不执行点击
    /// </summary>
    public static async Task MoveToClient(IntPtr hWnd, int clientX, int clientY)
    {
        float dpiScale = GetDpiScale();
        int adjustedX = (int)(clientX / dpiScale);
        int adjustedY = (int)(clientY / dpiScale);

        var pt = new POINT { X = adjustedX, Y = adjustedY };
        ClientToScreen(hWnd, ref pt);

        Log.Log($"Move only: target=({clientX},{clientY}), DPI/{dpiScale:F1}=({adjustedX},{adjustedY}), screen=({pt.X},{pt.Y})");

        SetForegroundWindow(hWnd);
        await Task.Delay(RandDelay(80, 160));

        GetCursorPos(out var start);
        await SimulateMouseMove(start.X, start.Y, pt.X, pt.Y);
        Log.Log("Move only completed");
    }

    /// <summary>
    /// 模拟人类鼠标移动轨迹：贝塞尔曲线 + 速度变化
    /// </summary>
    private static async Task SimulateMouseMove(int fromX, int fromY, int toX, int toY)
    {
        int distance = (int)Math.Sqrt(Math.Pow(toX - fromX, 2) + Math.Pow(toY - fromY, 2));

        // 根据距离决定步数（距离越远步数越多，模拟真实移动）
        int steps = Math.Clamp(distance / 15, 8, 40);

        // 随机控制点，让轨迹有轻微弧度（人手移动不是完美直线）
        double ctrlX = (fromX + toX) / 2.0 + _rng.Next(-20, 21);
        double ctrlY = (fromY + toY) / 2.0 + _rng.Next(-15, 16);

        for (int i = 1; i <= steps; i++)
        {
            // 缓入缓出的 t 值（开始和结束慢，中间快）
            double t = (double)i / steps;
            double ease = t < 0.5
                ? 2 * t * t
                : 1 - Math.Pow(-2 * t + 2, 2) / 2;

            // 二次贝塞尔曲线
            double x = (1 - ease) * (1 - ease) * fromX + 2 * (1 - ease) * ease * ctrlX + ease * ease * toX;
            double y = (1 - ease) * (1 - ease) * fromY + 2 * (1 - ease) * ease * ctrlY + ease * ease * toY;

            SetCursorPos((int)x, (int)y);

            // 每步之间随机间隔（模拟手速不均匀）
            await Task.Delay(RandDelay(3, 12));
        }

        // 确保最终位置精确
        SetCursorPos(toX, toY);
    }

    /// <summary>
    /// 发送鼠标事件（在当前光标位置）
    /// </summary>
    private static void SendMouseEvent(uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dwFlags = flags,
                dwExtraInfo = GetMessageExtraInfo()
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// 生成随机延迟（毫秒），模拟人类反应时间的不确定性
    /// 受 RaceConfig.ClickSpeedMultiplier 缩放：&lt;1.0 更快、&gt;1.0 更慢
    /// 5ms 保底，避免极端配置直接退化成 0 触发 SendInput 节流
    /// </summary>
    private static int RandDelay(int min, int max)
    {
        int raw = _rng.Next(min, max + 1);
        double mul = RaceConfig.ClickSpeedMultiplier;
        if (Math.Abs(mul - 1.0) < 0.001) return raw;
        return Math.Max(5, (int)Math.Round(raw * mul));
    }
    private static readonly LogScope Log = new("Mouse");
}
