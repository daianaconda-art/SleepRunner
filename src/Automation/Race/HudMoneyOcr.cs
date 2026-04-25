using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race;

internal static class HudMoneyOcr
{
    public static readonly (double X, double Y, double W, double H)[] Regions =
    [
        (0.58, 0.00, 0.36, 0.14),
        (0.42, 0.00, 0.30, 0.14),
        (0.66, 0.00, 0.30, 0.12),
    ];

    public static bool TryReadMoney(Mat screenshot, out int money, out string regionSummary)
    {
        var logs = new List<string>(Regions.Length);
        var candidates = new List<MoneyCandidate>();
        for (int i = 0; i < Regions.Length; i++)
        {
            var r = Regions[i];
            var regionCandidates = ReadRegionCandidates(screenshot, r, i).ToList();
            logs.Add($"({r.X:F2},{r.Y:F2}) cands=[{string.Join(", ", regionCandidates.Take(4).Select(c => $"'{ClipForLog(c.Text, 12)}'"))}]");
            candidates.AddRange(regionCandidates);
        }

        bool ok = TryResolveBestCandidate(candidates, out money);
        regionSummary = string.Join(" | ", logs);
        return ok;
    }

    internal static bool TryResolveFromRawRegions(string[] raws, out int money)
    {
        var candidates = raws
            .Select((raw, idx) => BuildCandidate(raw, regionIndex: idx, source: "raw"))
            .Where(static c => c.HasValue)
            .Select(static c => c!.Value)
            .ToList();

        return TryResolveBestCandidate(candidates, out money);
    }

    private static IEnumerable<MoneyCandidate> ReadRegionCandidates(
        Mat screenshot,
        (double X, double Y, double W, double H) region,
        int regionIndex)
    {
        var rect = ToPixelRect(screenshot, region.X, region.Y, region.W, region.H);
        using var roi = new Mat(screenshot, rect);

        foreach (string candidate in ReadMoneyRegionCandidates(roi))
        {
            MoneyCandidate? parsed = BuildCandidate(candidate, regionIndex, "processed");
            if (parsed.HasValue)
                yield return parsed.Value;
        }
    }

    private static IEnumerable<string> ReadMoneyRegionCandidates(Mat roi)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void YieldCandidate(string text, List<string> bucket)
        {
            string normalized = NormalizeRaw(text);
            if (string.IsNullOrEmpty(normalized))
                return;
            if (seen.Add(normalized))
                bucket.Add(normalized);
        }

        var results = new List<string>();

        using (var full = roi.Clone())
            YieldCandidate(OcrHelper.RecognizeMat(full).GetAwaiter().GetResult(), results);

        using (var enlarged = new Mat())
        {
            Cv2.Resize(roi, enlarged, new OpenCvSharp.Size(), 4.0, 4.0, InterpolationFlags.Cubic);
            YieldCandidate(OcrHelper.RecognizeMat(enlarged).GetAwaiter().GetResult(), results);

            foreach (bool invert in new[] { false, true })
            {
                using var gray = new Mat();
                using var binary = new Mat();
                using var binaryBgr = new Mat();
                Cv2.CvtColor(enlarged, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.Threshold(gray, binary, 0, 255,
                    (invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary) | ThresholdTypes.Otsu);
                Cv2.CvtColor(binary, binaryBgr, ColorConversionCodes.GRAY2BGR);
                YieldCandidate(OcrHelper.RecognizeMat(binaryBgr).GetAwaiter().GetResult(), results);
            }
        }

        foreach (var subRect in BuildSubRects(roi))
        {
            using var sub = new Mat(roi, subRect);
            using var enlargedSub = new Mat();
            Cv2.Resize(sub, enlargedSub, new OpenCvSharp.Size(), 5.0, 5.0, InterpolationFlags.Cubic);
            YieldCandidate(OcrHelper.RecognizeMat(enlargedSub).GetAwaiter().GetResult(), results);

            using var gray = new Mat();
            using var binary = new Mat();
            using var binaryBgr = new Mat();
            Cv2.CvtColor(enlargedSub, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            Cv2.CvtColor(binary, binaryBgr, ColorConversionCodes.GRAY2BGR);
            YieldCandidate(OcrHelper.RecognizeMat(binaryBgr).GetAwaiter().GetResult(), results);
        }

        return results;
    }

    private static IEnumerable<Rect> BuildSubRects(Mat roi)
    {
        int w = roi.Width;
        int h = roi.Height;
        yield return new Rect((int)(w * 0.15), 0, Math.Max(1, (int)(w * 0.60)), h);
        yield return new Rect((int)(w * 0.30), 0, Math.Max(1, (int)(w * 0.50)), h);
        yield return new Rect((int)(w * 0.45), 0, Math.Max(1, (int)(w * 0.40)), h);
    }

    private static MoneyCandidate? BuildCandidate(string raw, int regionIndex, string source)
    {
        if (!TryParseCandidate(raw, out int money, out int score))
            return null;

        score += regionIndex switch
        {
            0 => 20,
            1 => 10,
            _ => 0
        };

        if (source == "processed")
            score += 5;

        return new MoneyCandidate(money, score, NormalizeRaw(raw));
    }

    private static bool TryResolveBestCandidate(List<MoneyCandidate> candidates, out int money)
    {
        money = 0;
        if (candidates.Count == 0)
            return false;

        var best = candidates
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.Value)
            .First();

        if (best.Score < 20)
            return false;

        money = best.Value;
        return true;
    }

    private static bool TryParseCandidate(string raw, out int money, out int score)
    {
        money = 0;
        score = 0;

        if (string.IsNullOrEmpty(raw))
            return false;

        string normalized = NormalizeRaw(raw);
        if (string.IsNullOrEmpty(normalized))
            return false;

        if (Regex.IsMatch(normalized, @"^\d+$"))
        {
            if (!int.TryParse(normalized, out int digitsOnly) || digitsOnly <= 0)
                return false;

            money = digitsOnly;
            score = ScoreValue(money, pureDigits: true, hasHudNoise: false);
            return true;
        }

        if (HasUnexpectedLatinNoise(normalized))
            return false;

        int largest = ExtractLargestNumber(normalized);
        if (largest <= 0)
            return false;

        bool hasHudNoise = HasHudPrefixNoise(normalized);
        if (hasHudNoise)
        {
            while (largest > 999 && largest % 10 == 0)
                largest /= 10;
        }

        if (largest <= 0)
            return false;

        money = largest;
        score = ScoreValue(money, pureDigits: false, hasHudNoise: hasHudNoise);
        return score > 0;
    }

    private static bool HasHudPrefixNoise(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return text.Contains("BEST", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("GOOD", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("NORMAL", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasUnexpectedLatinNoise(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        string stripped = text
            .Replace("BEST", "", StringComparison.OrdinalIgnoreCase)
            .Replace("GOOD", "", StringComparison.OrdinalIgnoreCase)
            .Replace("NORMAL", "", StringComparison.OrdinalIgnoreCase);

        return Regex.IsMatch(stripped, "[A-Za-z]");
    }

    private static int ScoreValue(int value, bool pureDigits, bool hasHudNoise)
    {
        if (value <= 0)
            return -100;
        if (value < 10)
            return -20;
        if (value > 999)
            return -30;

        int score = 0;
        if (pureDigits)
            score += 40;
        if (hasHudNoise)
            score += 12;

        int digits = value.ToString().Length;
        score += digits switch
        {
            2 => 18,
            3 => 24,
            _ => 0,
        };

        if (value >= 20 && value <= 399)
            score += 16;
        else if (value <= 999)
            score += 6;

        return score;
    }

    private static int ExtractLargestNumber(string text)
    {
        int max = 0;
        foreach (Match match in Regex.Matches(text, @"\d+"))
        {
            if (!int.TryParse(match.Value, out int value))
                continue;
            if (value > max)
                max = value;
        }

        return max;
    }

    private static string NormalizeRaw(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";

        return Regex.Replace(raw, @"[\s\u3000]+", "")
            .Replace('O', '0')
            .Replace('o', '0')
            .Replace('I', '1')
            .Replace('l', '1')
            .Trim();
    }

    private static string ClipForLog(string text, int max)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Length <= max ? text : text[..max] + "..";
    }

    private static Rect ToPixelRect(Mat screenshot, double xPct, double yPct, double wPct, double hPct)
    {
        int x = Math.Clamp((int)(screenshot.Width * xPct), 0, screenshot.Width - 1);
        int y = Math.Clamp((int)(screenshot.Height * yPct), 0, screenshot.Height - 1);
        int w = Math.Min(Math.Max(1, (int)(screenshot.Width * wPct)), screenshot.Width - x);
        int h = Math.Min(Math.Max(1, (int)(screenshot.Height * hPct)), screenshot.Height - y);
        return new Rect(x, y, w, h);
    }

    private readonly record struct MoneyCandidate(int Value, int Score, string Text);
}
