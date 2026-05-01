using OpenCvSharp;

namespace SleepRunner.Automation.Race.Handlers;

internal static class CardSelectPlanner
{
    private const double SelectDoneRegionX = 0.42;
    private const double SelectDoneRegionY = 0.81;
    private const double SelectDoneRegionW = 0.10;
    private const double SelectDoneRegionH = 0.04;
    private const double UnselectedClickX = 0.884;
    private const double UnselectedClickY = 0.057;
    private const double RecommendedBadgeBlueRatioThreshold = 0.08;
    private const double RecommendedBadgeBlueLeadThreshold = 0.05;

    private static readonly (double X, double Y, double W, double H)[] RecommendedBadgeProbeRegions =
    [
        (0.17, 0.21, 0.13, 0.065),
        (0.38, 0.21, 0.13, 0.065),
        (0.595, 0.21, 0.13, 0.065),
    ];

    private static readonly string[] QuantityCapKeywords =
    [
        "持有数量达到上限",
        "持有数达到上限",
        "数量达到上限",
    ];

    public static int[] BuildAttemptOrder(string[] normalizedTexts, int[] preferredOrder)
    {
        var available = new List<int>();
        var capped = new List<int>();
        var seen = new HashSet<int>();

        foreach (int slot in preferredOrder)
        {
            if (slot < 0 || slot >= normalizedTexts.Length || !seen.Add(slot))
            {
                continue;
            }

            if (IsQuantityCappedCard(normalizedTexts[slot]))
            {
                capped.Add(slot);
            }
            else
            {
                available.Add(slot);
            }
        }

        return [.. available, .. capped];
    }

    public static bool IsQuantityCappedCard(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (string keyword in QuantityCapKeywords)
        {
            if (text.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ShouldClickUnselectedForPriorityMiss(bool isWhitelistedPriorityRule, int priorityCandidateCount)
    {
        return isWhitelistedPriorityRule && priorityCandidateCount == 0;
    }

    public static (double X, double Y) GetUnselectedClickPercent()
    {
        return (UnselectedClickX, UnselectedClickY);
    }

    public static int ResolveNearestCardSlot(double xPct)
    {
        double[] centers = [0.20, 0.50, 0.80];
        int bestSlot = 0;
        double bestDistance = double.MaxValue;

        for (int i = 0; i < centers.Length; i++)
        {
            double distance = Math.Abs(centers[i] - xPct);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSlot = i;
            }
        }

        return bestSlot;
    }

    public static int? TryResolveRecommendedBadgeSlot(Mat screenshot, out double[] blueRatios)
    {
        blueRatios = new double[RecommendedBadgeProbeRegions.Length];
        if (screenshot == null || screenshot.Empty())
        {
            return null;
        }

        int bestSlot = -1;
        double bestRatio = 0;
        double secondRatio = 0;
        for (int i = 0; i < RecommendedBadgeProbeRegions.Length; i++)
        {
            blueRatios[i] = MeasureBlueBadgeRatio(screenshot, BuildPercentRect(screenshot, RecommendedBadgeProbeRegions[i]));
            if (blueRatios[i] > bestRatio)
            {
                secondRatio = bestRatio;
                bestRatio = blueRatios[i];
                bestSlot = i;
            }
            else if (blueRatios[i] > secondRatio)
            {
                secondRatio = blueRatios[i];
            }
        }

        if (bestSlot >= 0 &&
            bestRatio >= RecommendedBadgeBlueRatioThreshold &&
            bestRatio - secondRatio >= RecommendedBadgeBlueLeadThreshold)
        {
            return bestSlot;
        }

        return null;
    }

    public static bool IsSelectDoneGrayDisabled(Mat screenshot, out double satMean, out double valMean)
    {
        satMean = 0;
        valMean = 0;

        if (screenshot == null || screenshot.Empty())
        {
            return false;
        }

        using var roi = new Mat(screenshot, BuildSelectDoneRect(screenshot));
        using var hsv = new Mat();
        Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);
        Scalar mean = Cv2.Mean(hsv);
        satMean = mean.Val1;
        valMean = mean.Val2;

        return satMean < 28 && valMean > 55 && valMean < 230;
    }

    private static Rect BuildSelectDoneRect(Mat screenshot)
    {
        return BuildPercentRect(screenshot, (SelectDoneRegionX, SelectDoneRegionY, SelectDoneRegionW, SelectDoneRegionH));
    }

    private static Rect BuildPercentRect(Mat screenshot, (double X, double Y, double W, double H) region)
    {
        int width = screenshot.Width;
        int height = screenshot.Height;
        int x = Math.Clamp((int)(width * region.X), 0, width - 1);
        int y = Math.Clamp((int)(height * region.Y), 0, height - 1);
        int w = Math.Max(1, (int)(width * region.W));
        int h = Math.Max(1, (int)(height * region.H));
        w = Math.Min(w, width - x);
        h = Math.Min(h, height - y);
        return new Rect(x, y, w, h);
    }

    private static double MeasureBlueBadgeRatio(Mat screenshot, Rect rect)
    {
        using var roi = new Mat(screenshot, rect);
        using var hsv = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, new Scalar(90, 70, 90), new Scalar(115, 255, 255), mask);
        return Cv2.CountNonZero(mask) / (double)(rect.Width * rect.Height);
    }
}
