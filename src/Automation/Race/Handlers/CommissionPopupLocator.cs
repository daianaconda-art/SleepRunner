using OpenCvSharp;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 委托确认弹窗按钮定位器。
/// 当前优先实现“跳过战斗”蓝色按钮的定位，返回按钮包围框与中心点。
/// </summary>
internal static class CommissionPopupLocator
{
    // 只看弹窗底部左半区域，避开中部“建议综合等级/一般”蓝条干扰
    private const double SkipSearchX = 0.26;
    private const double SkipSearchY = 0.66;
    private const double SkipSearchW = 0.30;
    private const double SkipSearchH = 0.16;

    public static bool TryLocateSkipButton(
        Mat screenshot,
        out Rect buttonRect,
        out OpenCvSharp.Point buttonCenter,
        out double blueRatio)
    {
        buttonRect = default;
        buttonCenter = default;
        blueRatio = 0;

        Rect searchRect = ToPixelRect(screenshot, SkipSearchX, SkipSearchY, SkipSearchW, SkipSearchH);
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = -1;
        int maxY = -1;
        int bluePixels = 0;

        for (int y = searchRect.Y; y < searchRect.Bottom; y++)
        {
            for (int x = searchRect.X; x < searchRect.Right; x++)
            {
                Vec3b bgr = screenshot.At<Vec3b>(y, x);
                int b = bgr.Item0;
                int g = bgr.Item1;
                int r = bgr.Item2;
                int score = b - Math.Max(r, g);

                // 实机按钮是亮蓝底，B 通道明显高于 R/G。
                if (b <= 150 || score <= 55)
                    continue;

                bluePixels++;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (bluePixels == 0 || maxX <= minX || maxY <= minY)
            return false;

        buttonRect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        int area = Math.Max(1, buttonRect.Width * buttonRect.Height);
        blueRatio = bluePixels / (double)area;
        buttonCenter = new OpenCvSharp.Point(buttonRect.X + buttonRect.Width / 2, buttonRect.Y + buttonRect.Height / 2);
        return true;
    }

    private static Rect ToPixelRect(Mat screenshot, double xPct, double yPct, double wPct, double hPct)
    {
        int w = screenshot.Width;
        int h = screenshot.Height;
        int x = Math.Clamp((int)(w * xPct), 0, w - 1);
        int y = Math.Clamp((int)(h * yPct), 0, h - 1);
        int rw = Math.Min(Math.Max(1, (int)(w * wPct)), w - x);
        int rh = Math.Min(Math.Max(1, (int)(h * hPct)), h - y);
        return new Rect(x, y, rw, rh);
    }
}
