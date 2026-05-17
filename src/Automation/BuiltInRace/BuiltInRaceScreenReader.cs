using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.BuiltInRace;

internal static class BuiltInRaceScreenReader
{
    private static readonly OcrRegion[] JourneyTitleRegions =
    [
        new(0.12, 0.03, 0.14, 0.08),
        new(0.10, 0.02, 0.18, 0.10),
    ];

    private static readonly OcrRegion[] BottomRightRegions =
    [
        new(0.78, 0.87, 0.20, 0.11),
        new(0.50, 0.63, 0.22, 0.12),
    ];

    private static readonly OcrRegion[] BottomJourneyRegions =
    [
        new(0.62, 0.88, 0.16, 0.10),
        new(0.40, 0.74, 0.22, 0.12),
        new(0.64, 0.90, 0.20, 0.10),
        new(0.03, 0.66, 0.18, 0.08),
    ];

    private static readonly OcrRegion[] DialogTitleRegions =
    [
        new(0.15, 0.17, 0.70, 0.12),
        new(0.24, 0.26, 0.52, 0.12),
        new(0.35, 0.08, 0.30, 0.12),
        new(0.28, 0.38, 0.44, 0.26),
    ];

    private static readonly OcrRegion[] DialogBodyRegions =
    [
        new(0.26, 0.28, 0.48, 0.34),
        new(0.24, 0.38, 0.52, 0.24),
        new(0.30, 0.16, 0.40, 0.16),
        new(0.30, 0.72, 0.40, 0.12),
        new(0.30, 0.78, 0.40, 0.16),
        new(0.36, 0.88, 0.28, 0.10),
    ];

    public static BuiltInRaceScreenSnapshot Read(Mat screenshot)
    {
        return new BuiltInRaceScreenSnapshot(
            JourneyTitleText: ReadBestText(screenshot, JourneyTitleRegions),
            BottomRightText: ReadBestText(screenshot, BottomRightRegions),
            BottomJourneyText: ReadBestText(screenshot, BottomJourneyRegions),
            DialogTitleText: ReadBestText(screenshot, DialogTitleRegions),
            DialogBodyText: ReadBestText(screenshot, DialogBodyRegions));
    }

    private static string ReadBestText(Mat screenshot, IEnumerable<OcrRegion> regions)
    {
        string best = string.Empty;
        int bestScore = int.MinValue;
        foreach (OcrRegion region in regions)
        {
            string text = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string normalized = BuiltInRacePlanner.Normalize(text);
            int score = ScoreText(normalized);
            if (score > bestScore)
            {
                bestScore = score;
                best = normalized;
            }
        }

        return best;
    }

    private static int ScoreText(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int score = text.Length;
        if (text.Contains("旅程", StringComparison.Ordinal)) score += 8;
        if (text.Contains("完成", StringComparison.Ordinal)) score += 8;
        if (text.Contains("继续", StringComparison.Ordinal)) score += 8;
        if (text.Contains("点击", StringComparison.Ordinal)) score += 6;
        if (text.Contains("确认", StringComparison.Ordinal)) score += 5;
        if (text.Contains("选择", StringComparison.Ordinal)) score += 5;
        if (text.Contains("开始", StringComparison.Ordinal)) score += 5;
        if (text.Contains("自动旅程", StringComparison.Ordinal)) score += 30;
        if (text.Contains("开始旅程", StringComparison.Ordinal)) score += 30;
        if (text.Contains("继承", StringComparison.Ordinal)) score += 10;
        if (text.Contains("潜质", StringComparison.Ordinal)) score += 30;
        if (text.Contains("初始信息", StringComparison.Ordinal)) score += 10;
        if (text.Contains("JOURNEY", StringComparison.OrdinalIgnoreCase)) score += 12;
        if (text.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase)) score += 12;
        return score;
    }

    private readonly record struct OcrRegion(double X, double Y, double W, double H);
}
