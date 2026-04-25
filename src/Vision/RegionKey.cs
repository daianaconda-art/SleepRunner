namespace SleepRunner.Vision;

/// <summary>
/// FrameContext 缓存键工厂。把百分比坐标量化成字符串，避免 double 精度抖动导致 cache miss
/// </summary>
internal static class RegionKey
{
    /// <summary>
    /// 把百分比 ROI 量化为字符串 cache key，量化到 4 位小数（约 0.01% 精度，够细）
    /// </summary>
    public static string Make(double xPct, double yPct, double wPct, double hPct)
        => $"{xPct:F4},{yPct:F4},{wPct:F4},{hPct:F4}";
}
