using OpenCvSharp;

namespace SleepRunner.Automation.Race.Handlers.Battle;

/// <summary>
/// 战斗自动按钮状态
/// </summary>
internal enum AutoState
{
    Unknown,
    OffGray,
    OnBright
}

/// <summary>
/// 自动按钮检测结果（点击点 + 触发原因）
/// </summary>
internal readonly record struct AutoClickPoint(int X, int Y, string Reason);

/// <summary>
/// 战斗 AUTO 按钮检测器：固定区域 HSV 颜色判断 ON/OFF，并提供点击点解析
/// </summary>
internal sealed class BattleAutoToggle
{
    // 右上角战斗控制条中的 AUTO 图标区域
    private const double AutoRegionX = 0.78;
    private const double AutoRegionY = 0.00;
    private const double AutoRegionW = 0.10;
    private const double AutoRegionH = 0.10;
    // AUTO 图标回退锚点（按当前实机截图校准）
    private const double AutoBtnX = 0.817;
    private const double AutoBtnY = 0.042;
    private const int AutoPointOffsetX = 12;
    private const int AutoPointOffsetY = 10;
    private const double GrayButtonMinRatio = 0.018;

    /// <summary>
    /// 自动按钮状态判断：灰色=关闭，彩色=开启
    /// </summary>
    public AutoState DetectAutoState(
        Mat screenshot,
        out double satMean,
        out double valMean,
        out OpenCvSharp.Point? autoCenter,
        out double autoConf)
    {
        autoCenter = null;
        autoConf = 0;
        satMean = 0;
        valMean = 0;
        if (screenshot.Empty())
            return AutoState.Unknown;

        using var hsv = new Mat();
        Cv2.CvtColor(screenshot, hsv, ColorConversionCodes.BGR2HSV);
        var rect = BuildAutoRegionRect(screenshot);

        using var region = new Mat(hsv, rect);
        Cv2.MeanStdDev(region, out var mean, out _);
        satMean = mean[1];
        valMean = mean[2];

        if (satMean >= 85)
            return AutoState.OnBright;
        if (HasGrayButtonPixels(region))
            return AutoState.OffGray;
        return AutoState.Unknown;
    }

    /// <summary>
    /// 解析自动按钮点击点：没有可靠检测中心时回退到固定锚点
    /// </summary>
    public static AutoClickPoint ResolveClickPoint(Mat screenshot, OpenCvSharp.Point? detectedCenter)
    {
        int fallbackX = (int)(screenshot.Width * AutoBtnX) + AutoPointOffsetX;
        int fallbackY = (int)(screenshot.Height * AutoBtnY) + AutoPointOffsetY;
        if (detectedCenter == null)
            return new AutoClickPoint(fallbackX, fallbackY, "fallback-anchor(no-center)");

        double xPct = (double)detectedCenter.Value.X / screenshot.Width;
        double yPct = (double)detectedCenter.Value.Y / screenshot.Height;

        // 检测中心必须落在右上角控制条附近，否则视为误判并回退
        bool inExpected = xPct >= 0.76 && xPct <= 0.88 && yPct >= 0.00 && yPct <= 0.10;
        if (!inExpected)
            return new AutoClickPoint(fallbackX, fallbackY, $"fallback-anchor(out-of-range {xPct:F3},{yPct:F3})");

        return new AutoClickPoint(
            detectedCenter.Value.X + AutoPointOffsetX,
            detectedCenter.Value.Y + AutoPointOffsetY,
            "detected-center+offset");
    }

    /// <summary>
    /// 计算自动按钮候选检测区域，统一用于颜色阈值判断
    /// </summary>
    private static Rect BuildAutoRegionRect(Mat screenshot)
    {
        int w = screenshot.Width;
        int h = screenshot.Height;
        int x = (int)(w * AutoRegionX);
        int y = (int)(h * AutoRegionY);
        int rw = Math.Max(1, (int)(w * AutoRegionW));
        int rh = Math.Max(1, (int)(h * AutoRegionH));
        x = Math.Clamp(x, 0, w - 1);
        y = Math.Clamp(y, 0, h - 1);
        rw = Math.Min(rw, w - x);
        rh = Math.Min(rh, h - y);
        return new Rect(x, y, rw, rh);
    }

    /// <summary>
    /// 灰色 AUTO 关闭态在按钮区域内表现为低饱和、亮度适中的像素簇。
    /// </summary>
    private static bool HasGrayButtonPixels(Mat hsvRegion)
    {
        using var mask = new Mat();
        Cv2.InRange(hsvRegion, new Scalar(0, 0, 70), new Scalar(180, 55, 230), mask);
        int grayPixels = Cv2.CountNonZero(mask);
        int total = Math.Max(1, hsvRegion.Width * hsvRegion.Height);
        double ratio = (double)grayPixels / total;
        return grayPixels >= 24 && ratio >= GrayButtonMinRatio;
    }

}
