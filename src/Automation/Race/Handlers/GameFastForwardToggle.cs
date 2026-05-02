using OpenCvSharp;

namespace SleepRunner.Automation.Race.Handlers;

internal enum GameFastForwardState
{
    Unknown,
    OffGray,
    OneSpeed,
    TwoSpeed,
}

internal static class GameFastForwardToggle
{
    private const double LeftRegionX = 0.766;
    private const double RightRegionX = 0.785;
    private const double IconRegionY = 0.050;
    private const double IconRegionW = 0.025;
    private const double IconRegionH = 0.055;

    public const double ClickX = 0.787;
    public const double ClickY = 0.067;

    public static GameFastForwardState DetectState(Mat screenshot, out double grayRatio, out double brightRatio)
    {
        grayRatio = 0;
        brightRatio = 0;
        if (screenshot.Empty())
            return GameFastForwardState.Unknown;

        using var hsv = new Mat();
        Cv2.CvtColor(screenshot, hsv, ColorConversionCodes.BGR2HSV);

        var left = MeasureTriangleRegion(hsv, BuildIconRegionRect(screenshot, LeftRegionX));
        var right = MeasureTriangleRegion(hsv, BuildIconRegionRect(screenshot, RightRegionX));

        grayRatio = (left.GrayRatio + right.GrayRatio) / 2.0;
        brightRatio = (left.BrightRatio + right.BrightRatio) / 2.0;

        bool leftBright = left.IsBright;
        bool rightBright = right.IsBright;
        bool leftGray = left.IsGray;
        bool rightGray = right.IsGray;

        if (leftBright && rightBright)
            return GameFastForwardState.TwoSpeed;
        if (leftBright && rightGray)
            return GameFastForwardState.OneSpeed;
        if (leftGray && rightGray)
            return GameFastForwardState.OffGray;
        return GameFastForwardState.Unknown;
    }

    private static (double GrayRatio, double BrightRatio, bool IsGray, bool IsBright) MeasureTriangleRegion(Mat hsvScreenshot, Rect rect)
    {
        using var region = new Mat(hsvScreenshot, rect);

        using var grayMask = new Mat();
        Cv2.InRange(region, new Scalar(0, 0, 75), new Scalar(180, 65, 220), grayMask);
        int grayPixels = Cv2.CountNonZero(grayMask);

        using var brightMask = new Mat();
        Cv2.InRange(region, new Scalar(0, 0, 225), new Scalar(180, 70, 255), brightMask);
        int brightPixels = Cv2.CountNonZero(brightMask);

        int total = Math.Max(1, region.Width * region.Height);
        double grayRatio = (double)grayPixels / total;
        double brightRatio = (double)brightPixels / total;
        return (
            grayRatio,
            brightRatio,
            IsGrayIconLike(grayPixels, grayRatio),
            IsBrightIconLike(brightPixels, brightRatio));
    }

    private static bool IsGrayIconLike(int pixels, double ratio)
    {
        // A gray triangle occupies part of the probe box. If the whole box is gray,
        // it is usually a transition panel or background, not the fast-forward icon.
        return pixels >= 12 && ratio >= 0.018 && ratio <= 0.70;
    }

    private static bool IsBrightIconLike(int pixels, double ratio)
    {
        // The lit arrow can fill most of the right probe box on the real 2-speed
        // button, while a fully white region is still more likely to be background.
        return pixels >= 12 && ratio >= 0.018 && ratio <= 0.92;
    }

    private static Rect BuildIconRegionRect(Mat screenshot, double xPct)
    {
        int w = screenshot.Width;
        int h = screenshot.Height;
        int x = Math.Clamp((int)(w * xPct), 0, w - 1);
        int y = Math.Clamp((int)(h * IconRegionY), 0, h - 1);
        int rw = Math.Max(1, (int)(w * IconRegionW));
        int rh = Math.Max(1, (int)(h * IconRegionH));
        rw = Math.Min(rw, w - x);
        rh = Math.Min(rh, h - y);
        return new Rect(x, y, rw, rh);
    }
}
