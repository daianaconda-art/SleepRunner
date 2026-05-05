using OpenCvSharp;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Training;

internal static class TrainingIconCounter
{
    public const double IconCenterX = 0.73;
    public const double IconStartY = 0.13;
    public const double IconSpacing = 0.08;
    public const int MaxIconSlots = 8;
    public const double IconCheckRadius = 0.015;

    public static bool DebugDumpEnabled = false;

    private enum SlotVerdict
    {
        Icon,
        Ambiguous,
        Empty,
    }

    private readonly record struct SlotScan(
        int Slot,
        double SlotY,
        double SatMean,
        double ValMean,
        double SatStd,
        double ValStd,
        SlotVerdict Verdict,
        string Path);

    public static int CountCircularIcons(Mat screenshot, string? rowLabel = null)
    {
        int w = screenshot.Width;
        int h = screenshot.Height;

        using var hsv = new Mat();
        Cv2.CvtColor(screenshot, hsv, ColorConversionCodes.BGR2HSV);

        int checkSize = Math.Max(1, (int)(w * IconCheckRadius));
        string labelPrefix = string.IsNullOrEmpty(rowLabel) ? "" : $"[{rowLabel}] ";
        var scans = new List<SlotScan>(MaxIconSlots);

        for (int slot = 0; slot < MaxIconSlots; slot++)
        {
            double slotY = IconStartY + slot * IconSpacing;
            int cx = (int)(w * IconCenterX);
            int cy = (int)(h * slotY);

            int x1 = Math.Max(0, cx - checkSize);
            int y1 = Math.Max(0, cy - checkSize);
            int x2 = Math.Min(w, cx + checkSize);
            int y2 = Math.Min(h, cy + checkSize);

            if (x2 <= x1 || y2 <= y1)
            {
                break;
            }

            using var region = new Mat(hsv, new Rect(x1, y1, x2 - x1, y2 - y1));
            SlotScan scan = ScanSlot(region, slot, slotY);
            scans.Add(scan);

            string verdict = scan.Verdict switch
            {
                SlotVerdict.Icon => "ICON",
                SlotVerdict.Ambiguous => "ambiguous",
                _ => "empty",
            };

            Logger.Log($"[Race:TrainingSelect] {labelPrefix}Slot {slot}: y={scan.SlotY:F3} satMean={scan.SatMean:F1} valMean={scan.ValMean:F1} satStd={scan.SatStd:F1} valStd={scan.ValStd:F1} => {verdict} ({scan.Path})");

            if (DebugDumpEnabled)
            {
                DumpSlotRegion(
                    screenshot,
                    x1,
                    y1,
                    x2 - x1,
                    y2 - y1,
                    rowLabel,
                    slot,
                    scan.Verdict != SlotVerdict.Empty,
                    scan.SatMean,
                    scan.ValMean,
                    scan.SatStd,
                    scan.ValStd);
            }
        }

        return SummarizeScans(scans, labelPrefix);
    }

    private static SlotScan ScanSlot(Mat hsvRegion, int slot, double slotY)
    {
        Cv2.MeanStdDev(hsvRegion, out var mean, out var stddev);

        double satMean = mean[1];
        double valMean = mean[2];
        double satStd = stddev[1];
        double valStd = stddev[2];

        bool colorIcon = satMean > 40 && valMean > 70 && satStd > 25 && valStd > 30;
        if (colorIcon)
        {
            colorIcon = satMean < 75
                ? satStd > 35
                : satStd > 40;
            colorIcon &= LooksLikeColoredIconShape(hsvRegion);
        }

        bool grayIcon = valMean > 135 && valStd > 30;
        if (grayIcon)
        {
            grayIcon = LooksLikeGrayIconShape(hsvRegion, valueThreshold: 150);
        }

        if (colorIcon)
        {
            return new SlotScan(slot, slotY, satMean, valMean, satStd, valStd, SlotVerdict.Icon, "color");
        }

        if (grayIcon)
        {
            return new SlotScan(slot, slotY, satMean, valMean, satStd, valStd, SlotVerdict.Icon, "gray");
        }

        if (LooksLikeRecoverableMiss(hsvRegion, satMean, valMean, satStd, valStd))
        {
            return new SlotScan(slot, slotY, satMean, valMean, satStd, valStd, SlotVerdict.Ambiguous, "recoverable");
        }

        return new SlotScan(slot, slotY, satMean, valMean, satStd, valStd, SlotVerdict.Empty, "none");
    }

    private static int SummarizeScans(IReadOnlyList<SlotScan> scans, string labelPrefix)
    {
        int lastCountedSlot = -1;
        int consecutiveNonIcons = 0;

        foreach (SlotScan scan in scans)
        {
            if (scan.Verdict == SlotVerdict.Icon)
            {
                lastCountedSlot = scan.Slot;
                consecutiveNonIcons = 0;
                continue;
            }

            consecutiveNonIcons++;
            if (consecutiveNonIcons >= 2)
            {
                break;
            }
        }

        int count = lastCountedSlot >= 0 ? lastCountedSlot + 1 : 0;
        string verdicts = string.Join("", scans.Select(s => s.Verdict switch
        {
            SlotVerdict.Icon => "I",
            SlotVerdict.Ambiguous => "A",
            _ => "_",
        }));
        Logger.Log($"[Race:TrainingSelect] {labelPrefix}Slot summary: verdicts={verdicts}, count={count}");
        return count;
    }

    private static bool LooksLikeRecoverableMiss(
        Mat hsvRegion,
        double satMean,
        double valMean,
        double satStd,
        double valStd)
    {
        if (valMean > 55 && valStd > 24 && LooksLikeGrayIconShape(hsvRegion, valueThreshold: 145))
        {
            return true;
        }

        return satMean > 34 &&
               valMean > 60 &&
               satStd > 18 &&
               LooksLikeColoredIconShape(hsvRegion);
    }

    private static bool LooksLikeGrayIconShape(Mat hsvRegion, int valueThreshold)
    {
        int area = Math.Max(1, hsvRegion.Rows * hsvRegion.Cols);
        int brightPixels = 0;
        int minX = hsvRegion.Cols;
        int minY = hsvRegion.Rows;
        int maxX = -1;
        int maxY = -1;
        double sumX = 0;
        double sumY = 0;
        double sumXX = 0;
        double sumYY = 0;
        double sumXY = 0;

        for (int y = 0; y < hsvRegion.Rows; y++)
        {
            for (int x = 0; x < hsvRegion.Cols; x++)
            {
                Vec3b hsv = hsvRegion.At<Vec3b>(y, x);
                if (hsv.Item2 <= valueThreshold)
                {
                    continue;
                }

                brightPixels++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                sumX += x;
                sumY += y;
                sumXX += x * x;
                sumYY += y * y;
                sumXY += x * y;
            }
        }

        if (brightPixels < area * 0.18 || maxX < minX || maxY < minY)
        {
            return false;
        }

        int brightWidth = maxX - minX + 1;
        int brightHeight = maxY - minY + 1;
        double aspect = brightWidth / (double)Math.Max(1, brightHeight);
        if (aspect is < 0.65 or > 1.55)
        {
            return false;
        }

        double regionCenterX = (hsvRegion.Cols - 1) / 2.0;
        double regionCenterY = (hsvRegion.Rows - 1) / 2.0;
        double brightCenterX = minX + (brightWidth - 1) / 2.0;
        double brightCenterY = minY + (brightHeight - 1) / 2.0;
        double centerTolerance = Math.Max(1.0, Math.Min(hsvRegion.Cols, hsvRegion.Rows) * 0.20);

        return Math.Abs(brightCenterX - regionCenterX) <= centerTolerance &&
               Math.Abs(brightCenterY - regionCenterY) <= centerTolerance &&
               IsPixelCloudCompact(brightPixels, sumX, sumY, sumXX, sumYY, sumXY, maxElongation: 1.55);
    }

    private static bool LooksLikeColoredIconShape(Mat hsvRegion)
    {
        int area = Math.Max(1, hsvRegion.Rows * hsvRegion.Cols);
        int coloredPixels = 0;
        int minX = hsvRegion.Cols;
        int minY = hsvRegion.Rows;
        int maxX = -1;
        int maxY = -1;
        double sumX = 0;
        double sumY = 0;
        double sumXX = 0;
        double sumYY = 0;
        double sumXY = 0;

        for (int y = 0; y < hsvRegion.Rows; y++)
        {
            for (int x = 0; x < hsvRegion.Cols; x++)
            {
                Vec3b hsv = hsvRegion.At<Vec3b>(y, x);
                if (hsv.Item1 <= 60 || hsv.Item2 <= 55)
                {
                    continue;
                }

                coloredPixels++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                sumX += x;
                sumY += y;
                sumXX += x * x;
                sumYY += y * y;
                sumXY += x * y;
            }
        }

        if (coloredPixels < area * 0.12 || maxX < minX || maxY < minY)
        {
            return false;
        }

        int coloredWidth = maxX - minX + 1;
        int coloredHeight = maxY - minY + 1;
        double aspect = coloredWidth / (double)Math.Max(1, coloredHeight);
        if (aspect is < 0.65 or > 1.55)
        {
            return false;
        }

        double regionCenterX = (hsvRegion.Cols - 1) / 2.0;
        double regionCenterY = (hsvRegion.Rows - 1) / 2.0;
        double coloredCenterX = minX + (coloredWidth - 1) / 2.0;
        double coloredCenterY = minY + (coloredHeight - 1) / 2.0;
        double centerTolerance = Math.Max(1.0, Math.Min(hsvRegion.Cols, hsvRegion.Rows) * 0.20);

        return Math.Abs(coloredCenterX - regionCenterX) <= centerTolerance &&
               Math.Abs(coloredCenterY - regionCenterY) <= centerTolerance &&
               IsPixelCloudCompact(coloredPixels, sumX, sumY, sumXX, sumYY, sumXY, maxElongation: 1.55);
    }

    private static bool IsPixelCloudCompact(
        int pixels,
        double sumX,
        double sumY,
        double sumXX,
        double sumYY,
        double sumXY,
        double maxElongation)
    {
        if (pixels <= 1)
        {
            return false;
        }

        double meanX = sumX / pixels;
        double meanY = sumY / pixels;
        double varianceX = Math.Max(0, sumXX / pixels - meanX * meanX);
        double varianceY = Math.Max(0, sumYY / pixels - meanY * meanY);
        double covariance = sumXY / pixels - meanX * meanY;
        double trace = varianceX + varianceY;
        double discriminant = Math.Sqrt(Math.Max(0, (varianceX - varianceY) * (varianceX - varianceY) + 4 * covariance * covariance));
        double major = (trace + discriminant) / 2;
        double minor = (trace - discriminant) / 2;
        if (minor <= 0)
        {
            return false;
        }

        double elongation = Math.Sqrt(major / minor);
        return elongation <= maxElongation;
    }

    private static void DumpSlotRegion(
        Mat screenshotBgr,
        int x,
        int y,
        int w,
        int h,
        string? rowLabel,
        int slot,
        bool hasIcon,
        double satMean,
        double valMean,
        double satStd,
        double valStd)
    {
        try
        {
            string dir = Path.Combine(PathHelper.ScreenshotsDir, "debug_icons");
            Directory.CreateDirectory(dir);

            string ts = DateTime.Now.ToString("HHmmss_fff");
            string label = string.IsNullOrEmpty(rowLabel) ? "row" : SanitizeLabel(rowLabel);
            string verdict = hasIcon ? "ICON" : "empty";
            string fname = $"{ts}_{label}_slot{slot}_{verdict}_sm{satMean:F0}_vm{valMean:F0}_ss{satStd:F0}_vs{valStd:F0}.png";

            using var sub = new Mat(screenshotBgr, new Rect(x, y, w, h));
            Cv2.ImWrite(Path.Combine(dir, fname), sub);
        }
        catch
        {
        }
    }

    private static string SanitizeLabel(string s)
    {
        var chars = s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars).Trim('_');
    }

    public static int ApplyPriorityRule(int[] counts, BuildDirection buildDirection)
    {
        if (counts[3] >= 4 || counts[4] >= 4)
        {
            int special = counts[3] >= counts[4] ? 3 : 4;
            Logger.Log($"[Race:TrainingSelect] ApplyPriorityRule: special row {special + 1} has {counts[special]} icons (>=4)");
            return special;
        }

        int bestCount = Math.Max(counts[0], Math.Max(counts[1], counts[2]));
        var tied = new List<int>(3);
        for (int i = 0; i < 3; i++)
        {
            if (counts[i] == bestCount)
            {
                tied.Add(i);
            }
        }

        if (tied.Count == 1)
        {
            Logger.Log($"[Race:TrainingSelect] ApplyPriorityRule: front-3 unique max row {tied[0] + 1}, counts=[{counts[0]},{counts[1]},{counts[2]}]");
            return tied[0];
        }

        int[] preference = buildDirection == BuildDirection.Attack ? [0, 1, 2] : [2, 1, 0];
        foreach (int idx in preference)
        {
            if (tied.Contains(idx))
            {
                Logger.Log($"[Race:TrainingSelect] ApplyPriorityRule: tie among front-3 rows [{string.Join(",", tied)}], build={buildDirection} -> row {idx + 1}");
                return idx;
            }
        }

        return tied[0];
    }
}
