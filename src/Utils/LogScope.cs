using System.Globalization;

namespace SleepRunner.Utils;

/// <summary>
/// 带前缀的轻量日志包装：替代每个 handler 都重复声明 PQLOG 静态方法的旧模式。
///
/// 用法：
///   private static readonly LogScope Log = new("Race:Battle");
///   Log.Log("AUTO toggled");
///   Log.Log($"redRatio={ratio:F2}");
///
/// 输出格式与旧 PQLOG 完全一致：[Tag] message
/// </summary>
public sealed class LogScope
{
    private readonly string _prefix;

    public LogScope(string tag)
    {
        _prefix = $"[{tag}] ";
    }

    /// <summary>普通字符串日志（已含插值结果）</summary>
    public void Log(string message)
    {
        Logger.Log(_prefix + message);
    }

    /// <summary>FormattableString 重载：使用 Invariant culture 格式化，避免本地化小数点 / 千分位差异</summary>
    public void Log(FormattableString message)
    {
        Logger.Log(_prefix + message.ToString(CultureInfo.InvariantCulture));
    }
}
