using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SleepRunner.Utils;

/// <summary>
/// 游戏窗口查找与信息获取工具
/// </summary>
public static class WindowHelper
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    /// <summary>
    /// 通过进程名查找游戏窗口，遍历所有窗口找到有效的渲染窗口。
    /// 若存在多个同名进程（游戏与本工具均为 SleepRunner），优先选 UnityWndClass 且客户区最大者。
    /// </summary>
    public static IntPtr FindTargetWindow()
    {
        if (TargetProcessConfig.TryGetConfiguredProcessName(out string processName))
            return FindGameWindow(processName);

        Log.Log(
            $"Target process not configured; scanning visible Unity windows. " +
            $"Set {TargetProcessConfig.EnvironmentVariableName} to override.");

        return FindBestVisibleUnityWindow();
    }

    private static IntPtr FindBestVisibleUnityWindow()
    {
        var candidates = new List<TargetWindowCandidate>();

        EnumWindows((hWnd, _) =>
        {
            AddTargetWindowCandidate(candidates, hWnd);
            EnumChildWindows(hWnd, (chWnd, _) =>
            {
                AddTargetWindowCandidate(candidates, chWnd);
                return true;
            }, IntPtr.Zero);

            return true;
        }, IntPtr.Zero);

        TargetWindowCandidate? selected = TargetWindowSelector.SelectBestCandidate(
            candidates,
            Environment.ProcessId);
        if (selected is null)
        {
            Log.Log("No visible Unity target window found.");
            return IntPtr.Zero;
        }

        var hWnd = new IntPtr(selected.Value.Handle);
        var (fw, fh) = GetClientSize(hWnd);
        Log.Log(
            $"Selected target window: hWnd=0x{hWnd:X}, pid={selected.Value.ProcessId}, " +
            $"class='{selected.Value.ClassName}', client={fw}x{fh}");
        return hWnd;
    }

    private static void AddTargetWindowCandidate(List<TargetWindowCandidate> candidates, IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        var className = GetWindowClassName(hWnd);
        var (cw, ch) = GetClientSize(hWnd);
        candidates.Add(new TargetWindowCandidate(
            hWnd,
            unchecked((int)pid),
            className,
            cw * ch,
            IsWindowVisible(hWnd)));
    }

    public static IntPtr FindGameWindow(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
        {
            Log.Log($"Process '{processName}' not found");
            return IntPtr.Zero;
        }

        IntPtr bestUnity = IntPtr.Zero;
        int bestUnityArea = 0;
        IntPtr bestAny = IntPtr.Zero;
        int bestAnyArea = 0;

        // 多个同名进程时：仅枚举主窗口为 Unity 的 PID（游戏），排除本工具 WinForms 进程
        HashSet<uint>? unityMainPids = null;
        foreach (var proc in processes)
        {
            try
            {
                if (proc.MainWindowHandle == IntPtr.Zero) continue;
                var mainCls = GetWindowClassName(proc.MainWindowHandle);
                if (!mainCls.Contains("Unity", StringComparison.Ordinal)) continue;
                unityMainPids ??= [];
                unityMainPids.Add((uint)proc.Id);
            }
            catch
            {
                // 进程已退出等，跳过
            }
        }

        foreach (var proc in processes)
        {
            var targetPid = (uint)proc.Id;
            if (unityMainPids is { Count: > 0 } && !unityMainPids.Contains(targetPid))
                continue;

            Log.Log($"Found process '{processName}', PID={targetPid}");

            if (!TryFindLargestWindowForPid(targetPid, out var hwnd, out int area, out var className))
                continue;

            Log.Log($"  Candidate: hWnd=0x{hwnd:X}, class='{className}', clientArea={area}");

            if (className.Contains("Unity", StringComparison.Ordinal) && area > bestUnityArea)
            {
                bestUnity = hwnd;
                bestUnityArea = area;
            }

            if (area > bestAnyArea)
            {
                bestAny = hwnd;
                bestAnyArea = area;
            }
        }

        var pick = bestUnity != IntPtr.Zero ? bestUnity : bestAny;
        if (pick != IntPtr.Zero)
        {
            var (fw, fh) = GetClientSize(pick);
            Log.Log($"Selected window: hWnd=0x{pick:X}, client={fw}x{fh}");
        }

        return pick;
    }

    /// <summary>
    /// 在指定 PID 下枚举顶层与子窗口，取客户区面积最大且可见的句柄
    /// </summary>
    private static bool TryFindLargestWindowForPid(uint targetPid, out IntPtr outHwnd, out int outArea,
        out string outClass)
    {
        outHwnd = IntPtr.Zero;
        outArea = 0;
        outClass = "";

        IntPtr bestHwnd = IntPtr.Zero;
        int bestArea = 0;

        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid != targetPid) return true;

            var title = GetWindowTitle(hWnd);
            var className = GetWindowClassName(hWnd);
            var (cw, ch) = GetClientSize(hWnd);
            var (ww, wh) = GetWindowSize(hWnd);
            bool visible = IsWindowVisible(hWnd);

            Log.Log($"  Window: hWnd=0x{hWnd:X}, title='{title}', class='{className}', " +
                  $"visible={visible}, client={cw}x{ch}, window={ww}x{wh}");

            EnumChildWindows(hWnd, (chWnd, _) =>
            {
                var ct = GetWindowTitle(chWnd);
                var cc = GetWindowClassName(chWnd);
                var (ccw, cch) = GetClientSize(chWnd);
                Log.Log($"    Child: hWnd=0x{chWnd:X}, title='{ct}', class='{cc}', client={ccw}x{cch}");

                int childArea = ccw * cch;
                if (childArea > bestArea && IsWindowVisible(chWnd))
                {
                    bestArea = childArea;
                    bestHwnd = chWnd;
                }
                return true;
            }, IntPtr.Zero);

            int area = cw * ch;
            if (area > bestArea && visible)
            {
                bestArea = area;
                bestHwnd = hWnd;
            }

            return true;
        }, IntPtr.Zero);

        if (bestHwnd == IntPtr.Zero)
            return false;

        outHwnd = bestHwnd;
        outArea = bestArea;
        outClass = GetWindowClassName(bestHwnd);
        return true;
    }

    /// <summary>
    /// 获取窗口客户区尺寸
    /// </summary>
    public static (int Width, int Height) GetClientSize(IntPtr hWnd)
    {
        if (GetClientRect(hWnd, out var rect))
            return (rect.Right - rect.Left, rect.Bottom - rect.Top);
        return (0, 0);
    }

    /// <summary>
    /// 获取窗口外框尺寸
    /// </summary>
    public static (int Width, int Height) GetWindowSize(IntPtr hWnd)
    {
        if (GetWindowRect(hWnd, out var rect))
            return (rect.Right - rect.Left, rect.Bottom - rect.Top);
        return (0, 0);
    }

    /// <summary>
    /// 获取窗口标题
    /// </summary>
    public static string GetWindowTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// 获取窗口类名
    /// </summary>
    public static string GetWindowClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// 将游戏窗口置于前台
    /// </summary>
    public static void BringToFront(IntPtr hWnd)
    {
        SetForegroundWindow(hWnd);
    }
    private static readonly LogScope Log = new("Window");
}
