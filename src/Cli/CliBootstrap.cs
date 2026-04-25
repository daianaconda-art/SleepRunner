using SleepRunner.Automation.Race;
using SleepRunner.Capture;
using SleepRunner.Utils;

namespace SleepRunner.Cli;

/// <summary>
/// CLI 共用工具：游戏窗口/截图/控制台编码/参数解析
/// </summary>
internal static class CliBootstrap
{
    /// <summary>
    /// 查找游戏窗口；找不到时打印 ERROR 并返回 IntPtr.Zero
    /// </summary>
    public static IntPtr FindGameOrLogError()
    {
        var hWnd = WindowHelper.FindTargetWindow();
        if (hWnd == IntPtr.Zero)
            Console.WriteLine("ERROR: Target window not found.");
        return hWnd;
    }

    /// <summary>
    /// 同上，但 ERROR 信息为 "Game not running."（保留原文用于脚本识别）
    /// </summary>
    public static IntPtr FindGameOrLogNotRunning()
    {
        var hWnd = WindowHelper.FindTargetWindow();
        if (hWnd == IntPtr.Zero)
            Console.WriteLine("ERROR: Target not running.");
        return hWnd;
    }

    /// <summary>
    /// 切换控制台为 UTF-8，避免中文输出乱码
    /// </summary>
    public static void EnsureUtf8Console()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
    }

    /// <summary>
    /// 解析跑马 CLI 基调：args[startIndex] 可为 "survival"，否则 Attack
    /// </summary>
    public static BuildDirection ParseRaceBuildDirection(string[] args, int startIndex)
    {
        if (args.Length > startIndex &&
            string.Equals(args[startIndex], "survival", StringComparison.OrdinalIgnoreCase))
            return BuildDirection.Survival;
        return BuildDirection.Attack;
    }

    /// <summary>
    /// 启动一个游戏截图采集器（已 Start），失败时返回 null 并打印 ERROR
    /// </summary>
    public static BitBltCapture? StartCapture(IntPtr hWnd)
    {
        var capture = new BitBltCapture();
        capture.Start(hWnd);
        return capture;
    }
}
