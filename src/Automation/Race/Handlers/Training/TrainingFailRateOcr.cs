using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Training;

/// <summary>
/// 训练页失败率 OCR + "失败率"红色标记定位
///
/// 拆分意图：
/// - 失败率 OCR 是训练逻辑里最易抖动的部分，一票"放大/二值化/早退"魔法都集中在这里
/// - 选项行映射 (DetectSelectedOption) 也属于本模块的内部职责
/// </summary>
internal static class TrainingFailRateOcr
{
    /// <summary>失败率识别失败时的保守值：宁可休息也不误点训练</summary>
    public const int UnknownFailRateFallback = 100;

    private static readonly double[] OptionYBoundaries = [0.33, 0.43, 0.53, 0.63];
    private const double FailRateSearchX = 0.74;
    private const double FailRateSearchY = 0.20;
    private const double FailRateSearchW = 0.24;
    private const double FailRateSearchH = 0.56;

    /// <summary>
    /// 通过红色"失败率"标记的 Y 坐标判断当前选中了哪个训练选项；返回 -1 表示未识别。
    /// 第二个参数保留用于兼容旧调用；开源版不再读取外部图像资产。
    /// </summary>
    public static int DetectSelectedOption(Mat screenshot, Mat? legacyMarkerAsset)
    {
        if (!TryFindFailRateMarkerByColor(screenshot, out var markerCenter, out double redRatio))
        {
            Logger.Log("[Race:TrainingSelect]   Fail rate red marker not found on screen");
            return -1;
        }

        double yPct = (double)markerCenter.Y / screenshot.Height;
        Logger.Log($"[Race:TrainingSelect]   Fail rate red marker at y={markerCenter.Y} ({yPct:F3}), redRatio={redRatio:F3}");

        for (int i = 0; i < OptionYBoundaries.Length; i++)
        {
            if (yPct < OptionYBoundaries[i]) return i;
        }
        return 4;
    }

    internal static bool TryFindFailRateMarkerYPct(Mat screenshot, out double yPct)
    {
        yPct = 0;
        if (!TryFindFailRateMarkerByColor(screenshot, out var markerCenter, out _))
        {
            return false;
        }

        yPct = (double)markerCenter.Y / screenshot.Height;
        return true;
    }

    /// <summary>
    /// OCR 识别失败率数值（百分比整数）
    /// </summary>
    public static async Task<int> ReadFailRatePercent(
        Mat? screenshot,
        Mat? legacyMarkerAsset,
        int selectedIndex,
        (string Name, double X, double Y)[] trainingOptions)
    {
        if (screenshot == null || screenshot.Empty()) return UnknownFailRateFallback;

        var regions = new List<(double X, double Y, double W, double H)>();
        double rowY = trainingOptions[Math.Clamp(selectedIndex, 0, trainingOptions.Length - 1)].Y;
        // 调用方已经知道目标行；先读该行局部区域，避免被别行/底部红字抢走。
        regions.Add((0.80, Math.Clamp(rowY - 0.03, 0.0, 0.95), 0.16, 0.07));
        regions.Add((0.76, Math.Clamp(rowY - 0.04, 0.0, 0.94), 0.20, 0.09));
        if (TryFindFailRateMarkerByColor(screenshot, out var markerCenter, out double redRatio))
        {
            double matchYPct = (double)markerCenter.Y / screenshot.Height;
            double matchXPct = (double)markerCenter.X / screenshot.Width;
            Logger.Log($"[Race:TrainingSelect]   Fail rate marker by color: ({matchXPct:F3},{matchYPct:F3}), redRatio={redRatio:F3}");
            regions.Add((Math.Clamp(matchXPct - 0.03, 0, 0.98), Math.Clamp(matchYPct - 0.025, 0, 0.98), 0.10, 0.05));
            regions.Add((Math.Clamp(matchXPct - 0.06, 0, 0.98), Math.Clamp(matchYPct - 0.03, 0, 0.98), 0.12, 0.06));
            regions.Add((Math.Clamp(matchXPct - 0.08, 0, 0.98), Math.Clamp(matchYPct - 0.035, 0, 0.98), 0.14, 0.07));
        }
        else
        {
            Logger.Log("[Race:TrainingSelect]   Fail rate marker color miss, use selected-row fixed OCR regions");
        }

        var candidates = new List<int>();
        var candidateFreq = new Dictionary<int, int>();
        var candidateScore = new Dictionary<int, int>();
        bool sawFailWord = false;

        for (int regionIdx = 0; regionIdx < regions.Count; regionIdx++)
        {
            var r = regions[regionIdx];
            var texts = await RecognizeFailRateRegionVariants(screenshot, r.X, r.Y, r.W, r.H);
            foreach (string text in texts)
            {
                bool hasFailWord = text.Contains("败率", StringComparison.Ordinal) || text.Contains("失败率", StringComparison.Ordinal);
                bool hasPercent = text.Contains("%", StringComparison.Ordinal) || text.Contains("％", StringComparison.Ordinal);
                bool hasLv = text.Contains("Lv", StringComparison.OrdinalIgnoreCase);
                sawFailWord = sawFailWord || hasFailWord;
                var parsed = ParseFailRateCandidates(text);
                string parsedText = parsed.Count == 0 ? "N/A" : string.Join(",", parsed);
                Logger.Log($"[Race:TrainingSelect]   Fail rate OCR try: '{text}' at region ({r.X:F2},{r.Y:F2},{r.W:F2},{r.H:F2}) => {parsedText}");
                foreach (int p in parsed)
                {
                    candidates.Add(p);
                    candidateFreq[p] = candidateFreq.TryGetValue(p, out int c) ? c + 1 : 1;

                    int score = 1;
                    if (hasFailWord) score += 4;
                    if (hasPercent) score += 3;
                    if (Regex.IsMatch(text, $@"(失败率|败率|率)\D*{p}\s*[％%]?", RegexOptions.IgnoreCase))
                        score += 3;
                    if (hasLv && !hasFailWord) score -= 2;
                    if (p == 0 && Regex.IsMatch(text, @"0{3,}")) score -= 4;
                    if (p == 0 && hasLv && !hasPercent && !hasFailWord) score -= 2;

                    candidateScore[p] = candidateScore.TryGetValue(p, out int s) ? s + score : score;
                }
            }

            // 早退：前 2 个 region 已扫完 + 高置信候选（fail 关键字 + 频次≥2 + 得分≥10）即可停
            if (regionIdx >= 1 && sawFailWord && candidateScore.Count > 0)
            {
                var topEntry = candidateScore.OrderByDescending(kv => kv.Value).First();
                int topVal = topEntry.Key;
                int topScore = topEntry.Value;
                int topFreq = candidateFreq.TryGetValue(topVal, out int tf) ? tf : 0;
                if (topScore >= 10 && topFreq >= 2)
                {
                    Logger.Log($"[Race:TrainingSelect]   Fail rate early-exit after region #{regionIdx + 1}: top={topVal}% (score={topScore}, freq={topFreq})");
                    break;
                }
            }
        }

        if (candidates.Count > 0)
        {
            int best = candidateScore
                .OrderByDescending(kv => kv.Value)
                .ThenByDescending(kv => candidateFreq.TryGetValue(kv.Key, out int f) ? f : 0)
                .ThenByDescending(kv => kv.Key)
                .First()
                .Key;

            int zeroFreq = candidateFreq.TryGetValue(0, out int zf) ? zf : 0;
            int zeroScore = candidateScore.TryGetValue(0, out int zs) ? zs : 0;
            if (best == 0 && (zeroFreq < 2 || zeroScore < 6) && sawFailWord)
            {
                Logger.Log($"[Race:TrainingSelect]   Fail rate 0% low-confidence (freq={zeroFreq}, score={zeroScore}), fallback to {UnknownFailRateFallback}%");
                return UnknownFailRateFallback;
            }

            int bestFreq = candidateFreq.TryGetValue(best, out int bf) ? bf : 0;
            int bestScore = candidateScore.TryGetValue(best, out int bs) ? bs : 0;
            Logger.Log($"[Race:TrainingSelect]   Fail rate final: {best}% (score={bestScore}, freq={bestFreq})");
            return best;
        }

        Logger.Log($"[Race:TrainingSelect]   Fail rate OCR unresolved, fallback to {UnknownFailRateFallback}%");
        return UnknownFailRateFallback;
    }

    private static bool TryFindFailRateMarkerByColor(Mat screenshot, out OpenCvSharp.Point markerCenter, out double redRatio)
    {
        markerCenter = default;
        redRatio = 0;
        if (screenshot.Empty()) return false;

        Rect searchRect = ToPixelRect(screenshot, FailRateSearchX, FailRateSearchY, FailRateSearchW, FailRateSearchH);
        if (searchRect.Width <= 1 || searchRect.Height <= 1)
            return false;

        using var roi = new Mat(screenshot, searchRect);
        using var bgr = new Mat();
        using var hsv = new Mat();
        if (roi.Channels() == 4)
            Cv2.CvtColor(roi, bgr, ColorConversionCodes.BGRA2BGR);
        else
            roi.CopyTo(bgr);
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        using var lowRed = new Mat();
        using var highRed = new Mat();
        using var mask = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 70, 70), new Scalar(12, 255, 255), lowRed);
        Cv2.InRange(hsv, new Scalar(168, 70, 70), new Scalar(180, 255, 255), highRed);
        Cv2.BitwiseOr(lowRed, highRed, mask);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3)));

        int redPixels = Cv2.CountNonZero(mask);
        redRatio = (double)redPixels / Math.Max(1, searchRect.Width * searchRect.Height);
        if (redPixels < 24)
            return false;

        Cv2.FindContours(mask, out OpenCvSharp.Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (contours.Length == 0)
            return false;

        Rect bestRect = default;
        double bestArea = 0;
        foreach (var contour in contours)
        {
            Rect rect = Cv2.BoundingRect(contour);
            double area = Cv2.ContourArea(contour);
            if (area < 16 || rect.Width < 4 || rect.Height < 4)
                continue;
            if (area > bestArea)
            {
                bestArea = area;
                bestRect = rect;
            }
        }

        if (bestArea <= 0)
            return false;

        markerCenter = new OpenCvSharp.Point(
            searchRect.X + bestRect.X + bestRect.Width / 2,
            searchRect.Y + bestRect.Y + bestRect.Height / 2);
        return true;
    }

    private static Rect ToPixelRect(Mat screenshot, double x, double y, double w, double h)
    {
        int px = Math.Clamp((int)(screenshot.Width * x), 0, screenshot.Width - 1);
        int py = Math.Clamp((int)(screenshot.Height * y), 0, screenshot.Height - 1);
        int pw = Math.Max(1, (int)(screenshot.Width * w));
        int ph = Math.Max(1, (int)(screenshot.Height * h));
        pw = Math.Min(pw, screenshot.Width - px);
        ph = Math.Min(ph, screenshot.Height - py);
        return new Rect(px, py, pw, ph);
    }

    private static async Task<List<string>> RecognizeFailRateRegionVariants(Mat screenshot, double x, double y, double w, double h)
    {
        int px = (int)(screenshot.Width * x);
        int py = (int)(screenshot.Height * y);
        int pw = Math.Max(1, (int)(screenshot.Width * w));
        int ph = Math.Max(1, (int)(screenshot.Height * h));
        px = Math.Clamp(px, 0, screenshot.Width - 1);
        py = Math.Clamp(py, 0, screenshot.Height - 1);
        pw = Math.Min(pw, screenshot.Width - px);
        ph = Math.Min(ph, screenshot.Height - py);
        if (pw <= 1 || ph <= 1) return [];

        using var region = new Mat(screenshot, new Rect(px, py, pw, ph));
        var results = new List<string>();

        results.Add(NormalizeOcrText(await OcrHelper.RecognizeMat(region)));

        using (var up = new Mat())
        {
            Cv2.Resize(region, up, new OpenCvSharp.Size(region.Width * 3, region.Height * 3), 0, 0, InterpolationFlags.Cubic);
            results.Add(NormalizeOcrText(await OcrHelper.RecognizeMat(up)));
        }

        using (var gray = new Mat())
        using (var bin = new Mat())
        using (var upBin = new Mat())
        {
            Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            Cv2.Resize(bin, upBin, new OpenCvSharp.Size(bin.Width * 3, bin.Height * 3), 0, 0, InterpolationFlags.Nearest);
            Cv2.CvtColor(upBin, upBin, ColorConversionCodes.GRAY2BGR);
            results.Add(NormalizeOcrText(await OcrHelper.RecognizeMat(upBin)));
        }

        return results;
    }

    public static List<int> ParseFailRateCandidates(string text)
    {
        var results = new List<int>();
        if (string.IsNullOrEmpty(text)) return results;

        string normalized = text
            .Replace('O', '0')
            .Replace('o', '0')
            .Replace('〇', '0')
            .Replace('８', '8')
            .Replace('Ｂ', '8')
            .Replace('B', '8')
            .Replace('帙', '8')
            .Replace('捌', '8')
            .Replace('Ｓ', '5')
            .Replace('S', '5');

        // 训练等级 "Lv.1" 常被 OCR 和失败率混到一起；先剥掉，避免把等级误当 1%。
        normalized = Regex.Replace(normalized, @"L[Vv][\.:：]?\s*\d{1,2}", "", RegexOptions.IgnoreCase);

        foreach (Match m in Regex.Matches(normalized, @"(\d{1,3})"))
        {
            if (!int.TryParse(m.Groups[1].Value, out int value))
                continue;
            if (value >= 0 && value <= 100)
                results.Add(value);
        }
        return results;
    }

    public static string NormalizeOcrText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Trim();
    }
}
