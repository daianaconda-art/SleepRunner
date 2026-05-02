using OpenCvSharp;

namespace SleepRunner.Automation.Race.Handlers;

internal static class CommissionPopupLocator
{
    // Covers both the first popup's left skip button and the confirmation popup's right skip button.
    private const double SkipSearchX = 0.26;
    private const double SkipSearchY = 0.58;
    private const double SkipSearchW = 0.52;
    private const double SkipSearchH = 0.24;

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
        using var region = new Mat(screenshot, searchRect);
        using var blueMask = new Mat(region.Rows, region.Cols, MatType.CV_8UC1, Scalar.Black);

        for (int y = 0; y < region.Rows; y++)
        {
            for (int x = 0; x < region.Cols; x++)
            {
                Vec3b bgr = region.At<Vec3b>(y, x);
                int b = bgr.Item0;
                int g = bgr.Item1;
                int r = bgr.Item2;
                int score = b - Math.Max(r, g);

                if (b > 150 && score > 55)
                    blueMask.Set(y, x, 255);
            }
        }

        Cv2.FindContours(
            blueMask,
            out OpenCvSharp.Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        Rect bestLocalRect = default;
        int bestBluePixels = 0;
        foreach (var contour in contours)
        {
            Rect localRect = Cv2.BoundingRect(contour);
            if (!IsButtonLike(localRect, screenshot))
                continue;

            using var localMask = new Mat(blueMask, localRect);
            int bluePixels = Cv2.CountNonZero(localMask);
            int area = Math.Max(1, localRect.Width * localRect.Height);
            double fillRatio = bluePixels / (double)area;
            if (fillRatio < 0.20)
                continue;

            if (bluePixels > bestBluePixels)
            {
                bestBluePixels = bluePixels;
                bestLocalRect = localRect;
            }
        }

        if (bestBluePixels == 0)
            return false;

        buttonRect = new Rect(
            searchRect.X + bestLocalRect.X,
            searchRect.Y + bestLocalRect.Y,
            bestLocalRect.Width,
            bestLocalRect.Height);
        int buttonArea = Math.Max(1, buttonRect.Width * buttonRect.Height);
        blueRatio = bestBluePixels / (double)buttonArea;
        buttonCenter = new OpenCvSharp.Point(buttonRect.X + buttonRect.Width / 2, buttonRect.Y + buttonRect.Height / 2);
        return true;
    }

    private static bool IsButtonLike(Rect rect, Mat screenshot)
    {
        double widthRatio = rect.Width / (double)screenshot.Width;
        double heightRatio = rect.Height / (double)screenshot.Height;
        double aspect = rect.Width / (double)Math.Max(1, rect.Height);

        return widthRatio >= 0.05 &&
               heightRatio >= 0.025 &&
               aspect is >= 1.5 and <= 8.0;
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
