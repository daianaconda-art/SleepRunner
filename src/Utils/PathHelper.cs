using System.Reflection;

namespace SleepRunner.Utils;

/// <summary>
/// 路径辅助工具，确保资源文件路径始终基于 exe 所在目录
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// exe 所在目录的绝对路径
    /// </summary>
    public static readonly string BaseDir =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        ?? AppContext.BaseDirectory;

    /// <summary>
    /// 将相对路径转为基于 exe 目录的绝对路径
    /// </summary>
    public static string Resolve(string relativePath)
    {
        return Path.Combine(BaseDir, relativePath);
    }

    /// <summary>
    /// 截图目录绝对路径
    /// </summary>
    public static string ScreenshotsDir => Resolve("assets/screenshots");

    /// <summary>
    /// 日志目录绝对路径
    /// </summary>
    public static string LogsDir => Resolve("assets/logs");

    /// <summary>
    /// 监督相关文件根目录
    /// </summary>
    public static string SupervisionDir => Resolve("assets/supervision");

    /// <summary>
    /// 手动或临时快照目录
    /// </summary>
    public static string SupervisionSnapshotsDir => Resolve("assets/supervision/snapshots");

    /// <summary>
    /// watchdog 单次运行目录
    /// </summary>
    public static string SupervisionWatchRunsDir => Resolve("assets/supervision/watch_runs");

    /// <summary>
    /// watchdog 触发异常后的 incident 目录
    /// </summary>
    public static string SupervisionIncidentsDir => Resolve("assets/supervision/incidents");
}
