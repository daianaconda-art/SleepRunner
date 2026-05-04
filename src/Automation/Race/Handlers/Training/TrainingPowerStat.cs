using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Training;

/// <summary>
/// 左侧属性面板单项数值 OCR（力量 / 体力 通用）
///
/// 拆分意图：
/// - rush fast-path（攻击=力量、生存=体力）共用同一套两段式 OCR 策略
/// - 两段：标准多区域 → 聚焦区域 + 放大 + 二值化兜底
/// - 通过 statName 参数切换匹配关键字与排除规则；其余像素布局完全一致
/// </summary>
internal static class TrainingPowerStat
{
    internal readonly record struct AttributePanelStats(
        int? Strength,
        int? Stamina,
        int? Agility,
        int? Focus,
        int? Guard,
        int? PotentialPoints);

    private readonly record struct DigitSpan(int X1, int X2, int Y1, int Y2)
    {
        public int Width => X2 - X1 + 1;
        public int Height => Y2 - Y1 + 1;
    }

    private static readonly (double X, double Y, double W, double H)[] PowerStatRegions =
    [
        (0.01, 0.12, 0.30, 0.25),
        (0.00, 0.08, 0.35, 0.45),
        (0.02, 0.27, 0.15, 0.12),
    ];

    // 聚焦力量首行数值区域：避免上方 RANK 头部被一起裁进来
    private static readonly (double X, double Y, double W, double H)[] PowerValueFocusedRegions =
    [
        (0.045, 0.335, 0.17, 0.10),
        (0.050, 0.345, 0.16, 0.09),
        (0.060, 0.360, 0.15, 0.07),
    ];

    /// <summary>力量值 OCR；兼容旧调用，等价于 ReadStatAsync(shot, "力量")</summary>
    public static Task<int> ReadPowerStatAsync(Mat? shot) => ReadStatAsync(shot, "力量");

    /// <summary>体力值 OCR；Survival 基调下用于 stamina rush 触发判断</summary>
    public static Task<int> ReadStaminaStatAsync(Mat? shot) => ReadStatAsync(shot, "体力");

    internal static async Task<AttributePanelStats> ReadAttributePanelStatsAsync(Mat? shot)
    {
        if (shot == null || shot.Empty())
        {
            Logger.Log("[Race:TrainingSelect] attribute panel stat read: capture empty");
            return new AttributePanelStats(null, null, null, null, null, null);
        }

        using var panel = ExtractAttributePanel(shot);
        AttributePanelStats lineStats = await ReadAttributePanelLineStatsAsync(panel);

        int? strength = ChooseAttributePanelValue(
            "力量",
            lineStats.Strength,
            await ReadAttributePanelValueAsync(panel, "力量", (0.06, 0.11, 0.58, 0.14), (0.08, 0.14, 0.30, 0.07), maxValue: 1250));
        int? stamina = ChooseAttributePanelValue(
            "体力",
            lineStats.Stamina,
            await ReadAttributePanelValueAsync(panel, "体力", (0.06, 0.24, 0.58, 0.14), (0.08, 0.265, 0.30, 0.07), maxValue: 1250));
        int? agility = ChooseAttributePanelValue(
            "韧性",
            lineStats.Agility,
            await ReadAttributePanelValueAsync(panel, "韧性", (0.06, 0.37, 0.58, 0.14), (0.08, 0.39, 0.30, 0.07), maxValue: 1250));
        int? focus = ChooseAttributePanelValue(
            "专注",
            lineStats.Focus,
            await ReadAttributePanelValueAsync(panel, "专注", (0.06, 0.51, 0.58, 0.14), (0.08, 0.515, 0.30, 0.07), maxValue: 1250));
        int? guard = ChooseAttributePanelValue(
            "保护",
            lineStats.Guard,
            await ReadAttributePanelValueAsync(panel, "保护", (0.06, 0.64, 0.58, 0.14), (0.08, 0.638, 0.30, 0.07), maxValue: 1250));
        int? potentialPoints = ChooseAttributePanelValue(
            "潜质点数",
            lineStats.PotentialPoints,
            await ReadAttributePanelValueAsync(panel, "潜质点数", (0.06, 0.78, 0.58, 0.18), (0.08, 0.765, 0.36, 0.08), maxValue: 9999));

        return new AttributePanelStats(strength, stamina, agility, focus, guard, potentialPoints);
    }

    /// <summary>
    /// 通用属性 OCR：根据 statName 切换匹配关键字与排除规则；
    /// 区域百分比沿用力量探测的最佳实践（左侧属性面板上半段）
    /// </summary>
    public static async Task<int> ReadStatAsync(Mat? shot, string statName)
    {
        if (shot == null || shot.Empty())
        {
            Logger.Log($"[Race:TrainingSelect] {statName} stat read: capture empty");
            return -1;
        }

        // Phase 1: 标准多区域 OCR，匹配 "<statName>XXX" 格式
        foreach (var r in PowerStatRegions)
        {
            string raw = await OcrHelper.RecognizeRegion(shot, r.X, r.Y, r.W, r.H);
            string text = TrainingFailRateOcr.NormalizeOcrText(raw);
            if (string.IsNullOrEmpty(text)) continue;

            int value = ParseStatValue(text, statName);
            Logger.Log($"[Race:TrainingSelect] {statName} stat OCR ({r.X:F2},{r.Y:F2},{r.W:F2},{r.H:F2}): '{text}' => {(value >= 0 ? value.ToString() : "N/A")}");
            if (value >= 0) return value;
        }

        // Phase 2: 聚焦数值区域 + 放大/二值化
        Logger.Log($"[Race:TrainingSelect] {statName} stat: standard OCR missed, trying focused regions with preprocessing...");
        foreach (var r in PowerValueFocusedRegions)
        {
            var variants = await RecognizePowerValueVariants(shot, r.X, r.Y, r.W, r.H);
            foreach (string text in variants)
            {
                if (string.IsNullOrEmpty(text)) continue;
                if (TryParseStatValueFocused(text, statName, out int val, out string reason))
                {
                    Logger.Log($"[Race:TrainingSelect] {statName} stat focused ({r.X:F3},{r.Y:F3},{r.W:F3},{r.H:F3}): '{text}' => {val} ({reason})");
                    return val;
                }
                Logger.Log($"[Race:TrainingSelect] {statName} stat focused ({r.X:F3},{r.Y:F3},{r.W:F3},{r.H:F3}): reject '{text}'");
            }
        }

        Logger.Log($"[Race:TrainingSelect] {statName} stat read: no valid value found in any region");
        return -1;
    }

    private static async Task<AttributePanelStats> ReadAttributePanelLineStatsAsync(Mat panel)
    {
        var lines = await OcrHelper.RecognizeRegionLines(panel, 0, 0, 1, 1);
        int? strength = ReadLineBandValue(lines, "力量", 0.11, 0.25, maxValue: 1250, allowPlainNumber: false);
        int? stamina = ReadLineBandValue(lines, "体力", 0.24, 0.38, maxValue: 1250, allowPlainNumber: false);
        int? agility = ReadLineBandValue(lines, "韧性", 0.37, 0.52, maxValue: 1250, allowPlainNumber: false);
        int? focus = ReadLineBandValue(lines, "专注", 0.51, 0.66, maxValue: 1250, allowPlainNumber: false);
        int? guard = ReadLineBandValue(lines, "保护", 0.64, 0.80, maxValue: 1250, allowPlainNumber: false);
        int? potentialPoints = ReadLineBandValue(lines, "潜质点数", 0.78, 0.98, maxValue: 9999, allowPlainNumber: true);
        return new AttributePanelStats(strength, stamina, agility, focus, guard, potentialPoints);
    }

    private static int? ReadLineBandValue(
        IReadOnlyList<OcrHelper.OcrLineHit> lines,
        string statName,
        double minY,
        double maxY,
        int maxValue,
        bool allowPlainNumber)
    {
        (int Value, int Score, string Text, double Y)? best = null;

        foreach (var line in lines)
        {
            string text = NormalizeAttributePanelDigits(TrainingFailRateOcr.NormalizeOcrText(line.Text));
            if (string.IsNullOrEmpty(text))
                continue;

            Logger.Log($"[Race:TrainingSelect] attribute panel line: y={line.CenterYPct:F3}, text='{text}'");

            double y = line.CenterYPct;
            if (y < minY || y > maxY)
                continue;

            if (!TryParseAttributePanelLineValue(text, maxValue, allowPlainNumber, out int value, out int score))
                continue;

            double center = (minY + maxY) * 0.5;
            if (Math.Abs(y - center) <= 0.045)
                score += 2;

            if (best == null || score > best.Value.Score)
                best = (value, score, text, y);
        }

        if (best != null)
        {
            Logger.Log($"[Race:TrainingSelect] attribute panel {statName}: line-context {best.Value.Value} from y={best.Value.Y:F3} '{best.Value.Text}' (score={best.Value.Score})");
            return best.Value.Value;
        }

        Logger.Log($"[Race:TrainingSelect] attribute panel {statName}: line-context no value");
        return null;
    }

    private static bool TryParseAttributePanelLineValue(
        string text,
        int maxValue,
        bool allowPlainNumber,
        out int value,
        out int score)
    {
        value = -1;
        score = 0;
        if (string.IsNullOrEmpty(text))
            return false;

        var slashMatch = Regex.Match(text, @"(\d{1,4})\s*(?:/|\uFF0F)\s*1250");
        if (slashMatch.Success &&
            int.TryParse(slashMatch.Groups[1].Value, out int current) &&
            current >= 0 &&
            current <= maxValue)
        {
            value = current;
            score = current >= 100 ? 14 : 10;
            return true;
        }

        if (!allowPlainNumber)
            return false;

        var matches = Regex.Matches(text, @"\d{1,4}")
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();
        if (matches.Count == 0)
            return false;

        string digits = matches
            .Where(m => m != "1250")
            .OrderByDescending(m => m.Length)
            .FirstOrDefault() ?? "";
        if (digits.Length == 0)
            return false;

        if (int.TryParse(digits, out current) && current >= 0 && current <= maxValue)
        {
            value = current;
            score = current >= 100 ? 12 : 6;
            return true;
        }

        return false;
    }

    private static int? ChooseAttributePanelValue(string statName, int? lineValue, int? rowValue)
    {
        if (!lineValue.HasValue)
            return rowValue;
        if (!rowValue.HasValue)
            return lineValue;
        if (lineValue.Value == rowValue.Value)
            return rowValue;

        bool rowLooksTruncated = rowValue.Value < 100 && lineValue.Value >= 100;
        if (rowLooksTruncated)
        {
            Logger.Log($"[Race:TrainingSelect] attribute panel {statName}: prefer line-context {lineValue.Value} over truncated row {rowValue.Value}");
            return lineValue;
        }

        Logger.Log($"[Race:TrainingSelect] attribute panel {statName}: keep row {rowValue.Value} over line-context {lineValue.Value}");
        return rowValue;
    }

    private static async Task<List<string>> RecognizePowerValueVariants(Mat screenshot, double x, double y, double w, double h)
    {
        int px = Math.Clamp((int)(screenshot.Width * x), 0, screenshot.Width - 1);
        int py = Math.Clamp((int)(screenshot.Height * y), 0, screenshot.Height - 1);
        int pw = Math.Min(Math.Max(1, (int)(screenshot.Width * w)), screenshot.Width - px);
        int ph = Math.Min(Math.Max(1, (int)(screenshot.Height * h)), screenshot.Height - py);
        if (pw <= 1 || ph <= 1) return [];

        using var region = new Mat(screenshot, new Rect(px, py, pw, ph));
        var results = new List<string>();

        results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(region)));

        using (var up = new Mat())
        {
            Cv2.Resize(region, up, new OpenCvSharp.Size(region.Width * 3, region.Height * 3), 0, 0, InterpolationFlags.Cubic);
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(up)));
        }

        using (var gray = new Mat())
        using (var bin = new Mat())
        using (var upBin = new Mat())
        {
            Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            Cv2.Resize(bin, upBin, new OpenCvSharp.Size(bin.Width * 3, bin.Height * 3), 0, 0, InterpolationFlags.Nearest);
            Cv2.CvtColor(upBin, upBin, ColorConversionCodes.GRAY2BGR);
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(upBin)));
        }

        return results;
    }

    private static Mat ExtractAttributePanel(Mat shot)
    {
        double aspect = (double)shot.Width / Math.Max(1, shot.Height);
        if (aspect < 0.75)
            return shot.Clone();

        Rect rect = ToPixelRect(shot, 0.045, 0.29, 0.20, 0.56);
        using var region = new Mat(shot, rect);
        return region.Clone();
    }

    private static async Task<int?> ReadAttributePanelValueAsync(
        Mat panel,
        string statName,
        (double X, double Y, double W, double H) region,
        (double X, double Y, double W, double H) currentValueRegion,
        int maxValue)
    {
        (int Value, int Score, string Text)? best = null;

        foreach (string text in await RecognizeAttributePanelCurrentValueVariants(panel, currentValueRegion.X, currentValueRegion.Y, currentValueRegion.W, currentValueRegion.H))
        {
            if (IsIgnorableAttributePanelOcrCandidate(text))
                continue;

            if (TryParseAttributePanelCurrentValue(text, maxValue, out int value, out int score))
            {
                Logger.Log($"[Race:TrainingSelect] attribute panel {statName} current-only: '{text}' => {value} (score={score})");
                if (best == null || score > best.Value.Score)
                    best = (value, score, text);
                continue;
            }

            Logger.Log($"[Race:TrainingSelect] attribute panel {statName} current-only: reject '{text}'");
        }

        foreach (string text in await RecognizeAttributePanelValueVariants(panel, region.X, region.Y, region.W, region.H))
        {
            if (IsIgnorableAttributePanelOcrCandidate(text))
                continue;

            if (TryParseAttributePanelValue(text, maxValue, out int value, out int score))
            {
                Logger.Log($"[Race:TrainingSelect] attribute panel {statName}: '{text}' => {value} (score={score})");
                if (best == null || score > best.Value.Score)
                    best = (value, score, text);
                continue;
            }

            Logger.Log($"[Race:TrainingSelect] attribute panel {statName}: reject '{text}'");
        }

        if (TryReadAttributePanelCurrentValueByPixels(panel, currentValueRegion, maxValue, out int pixelValue, out string pixelDigits, out double pixelConfidence))
        {
            Logger.Log($"[Race:TrainingSelect] attribute panel {statName} pixel-current: '{pixelDigits}' => {pixelValue} (confidence={pixelConfidence:F3})");
            bool acceptPixel = best == null ||
                               best.Value.Value == pixelValue ||
                               (best.Value.Value < 100 && pixelValue >= 100);
            if (acceptPixel)
                best = (pixelValue, 20, $"pixel:{pixelDigits}");
        }
        else
        {
            Logger.Log($"[Race:TrainingSelect] attribute panel {statName} pixel-current: no confident value");
        }

        if (best != null)
        {
            Logger.Log($"[Race:TrainingSelect] attribute panel {statName}: final {best.Value.Value} from '{best.Value.Text}' (score={best.Value.Score})");
            return best.Value.Value;
        }

        Logger.Log($"[Race:TrainingSelect] attribute panel {statName}: no valid value found");
        return null;
    }

    private static async Task<List<string>> RecognizeAttributePanelCurrentValueVariants(
        Mat screenshot,
        double x,
        double y,
        double w,
        double h)
    {
        Rect rect = ToPixelRect(screenshot, x, y, w, h);
        if (rect.Width <= 1 || rect.Height <= 1) return [];

        using var region = new Mat(screenshot, rect);
        var results = new List<string>();

        results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(region)));
        results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(region, "en-US")));

        using (var up = new Mat())
        {
            Cv2.Resize(region, up, new OpenCvSharp.Size(region.Width * 4, region.Height * 4), 0, 0, InterpolationFlags.Cubic);
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(up)));
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(up, "en-US")));
        }

        using (var gray = new Mat())
        using (var bin = new Mat())
        using (var upBin = new Mat())
        using (var upBinBgr = new Mat())
        {
            Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
            Cv2.Resize(bin, upBin, new OpenCvSharp.Size(bin.Width * 4, bin.Height * 4), 0, 0, InterpolationFlags.Nearest);
            Cv2.CvtColor(upBin, upBinBgr, ColorConversionCodes.GRAY2BGR);
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(upBinBgr)));
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(upBinBgr, "en-US")));
        }

        return results;
    }

    private static bool TryReadAttributePanelCurrentValueByPixels(
        Mat panel,
        (double X, double Y, double W, double H) region,
        int maxValue,
        out int value,
        out string digits,
        out double confidence)
    {
        value = -1;
        digits = "";
        confidence = 0;

        Rect rect = ToPixelRect(panel, region.X, region.Y, region.W, region.H);
        if (rect.Width <= 8 || rect.Height <= 8)
            return false;

        using var roi = new Mat(panel, rect);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(gray, mask, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        var spans = FindAttributePanelDigitSpans(mask);
        if (spans.Count == 0)
            return false;

        int tallHeight = spans.Max(s => s.Height);
        var chars = new List<char>();
        double minConfidence = 1.0;

        foreach (var span in spans.OrderBy(s => s.X1))
        {
            if (span.Height < Math.Max(10, tallHeight * 0.70))
            {
                if (chars.Count > 0)
                    break;
                continue;
            }

            if (!TryClassifyAttributePanelDigit(mask, span, out char ch, out double digitConfidence))
            {
                if (chars.Count > 0)
                    break;
                continue;
            }

            chars.Add(ch);
            minConfidence = Math.Min(minConfidence, digitConfidence);
            if (chars.Count >= 4)
                break;
        }

        if (chars.Count == 0)
            return false;

        digits = new string(chars.ToArray());
        if (!int.TryParse(digits, out value) || value < 0 || value > maxValue)
            return false;

        confidence = minConfidence;
        return confidence >= 0.76;
    }

    private static List<DigitSpan> FindAttributePanelDigitSpans(Mat mask)
    {
        var columnCounts = new int[mask.Width];
        for (int x = 0; x < mask.Width; x++)
        {
            int count = 0;
            for (int y = 0; y < mask.Height; y++)
            {
                if (mask.At<byte>(y, x) != 0)
                    count++;
            }
            columnCounts[x] = count;
        }

        var rawRuns = new List<(int Start, int End)>();
        bool inRun = false;
        int start = 0;
        for (int x = 0; x < columnCounts.Length; x++)
        {
            if (columnCounts[x] > 0 && !inRun)
            {
                inRun = true;
                start = x;
            }

            if ((columnCounts[x] == 0 || x == columnCounts.Length - 1) && inRun)
            {
                int end = columnCounts[x] == 0 ? x - 1 : x;
                rawRuns.Add((start, end));
                inRun = false;
            }
        }

        var boxes = new List<DigitSpan>();
        foreach (var run in rawRuns)
        {
            if (TryBuildDigitSpan(mask, run.Start, run.End, out var span) && span.Width >= 2 && span.Height >= 8)
                boxes.Add(span);
        }

        if (boxes.Count == 0)
            return boxes;

        int maxHeight = boxes.Max(b => b.Height);
        int expectedDigitWidth = Math.Clamp((int)Math.Round(maxHeight * 0.68), 8, 24);
        var split = new List<DigitSpan>();
        foreach (var box in boxes)
            SplitWideDigitSpan(mask, columnCounts, box, expectedDigitWidth, split);

        return split
            .Where(s => s.Width >= 2 && s.Height >= 8)
            .OrderBy(s => s.X1)
            .ToList();
    }

    private static void SplitWideDigitSpan(
        Mat mask,
        int[] columnCounts,
        DigitSpan span,
        int expectedDigitWidth,
        List<DigitSpan> output)
    {
        int estimatedCount = (int)Math.Round(span.Width / (double)Math.Max(1, expectedDigitWidth));
        if (estimatedCount <= 1 || estimatedCount > 4 || span.Width < expectedDigitWidth * 1.45)
        {
            output.Add(span);
            return;
        }

        var boundaries = new List<int> { span.X1 };
        for (int i = 1; i < estimatedCount; i++)
        {
            int target = span.X1 + (int)Math.Round(span.Width * i / (double)estimatedCount);
            int radius = Math.Max(2, expectedDigitWidth / 4);
            int searchStart = Math.Max(span.X1 + 1, target - radius);
            int searchEnd = Math.Min(span.X2 - 1, target + radius);
            int bestX = target;
            int bestCount = int.MaxValue;
            for (int x = searchStart; x <= searchEnd; x++)
            {
                if (columnCounts[x] < bestCount)
                {
                    bestCount = columnCounts[x];
                    bestX = x;
                }
            }
            boundaries.Add(bestX + 1);
        }
        boundaries.Add(span.X2 + 1);

        for (int i = 0; i < boundaries.Count - 1; i++)
        {
            int x1 = boundaries[i];
            int x2 = boundaries[i + 1] - 1;
            if (TryBuildDigitSpan(mask, x1, x2, out var piece))
                output.Add(piece);
        }
    }

    private static bool TryBuildDigitSpan(Mat mask, int x1, int x2, out DigitSpan span)
    {
        span = default;
        x1 = Math.Clamp(x1, 0, mask.Width - 1);
        x2 = Math.Clamp(x2, 0, mask.Width - 1);
        if (x2 < x1)
            return false;

        int minX = mask.Width;
        int minY = mask.Height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = x1; x <= x2; x++)
            {
                if (mask.At<byte>(y, x) == 0)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return false;

        span = new DigitSpan(minX, maxX, minY, maxY);
        return true;
    }

    private static bool TryClassifyAttributePanelDigit(Mat mask, DigitSpan span, out char digit, out double confidence)
    {
        digit = '\0';
        confidence = 0;

        string[] grid = BuildAttributePanelDigitGrid(mask, span);
        var bestByDigit = new Dictionary<char, double>();
        foreach (var template in AttributePanelDigitTemplates)
        {
            double score = ScoreAttributePanelDigitGrid(grid, template.Pattern);
            if (!bestByDigit.TryGetValue(template.Digit, out double current) || score > current)
                bestByDigit[template.Digit] = score;
        }

        if (bestByDigit.Count == 0)
            return false;

        var ranked = bestByDigit
            .OrderByDescending(kv => kv.Value)
            .Take(2)
            .ToArray();
        var best = ranked[0];
        double secondScore = ranked.Length > 1 ? ranked[1].Value : 0;
        double margin = best.Value - secondScore;
        confidence = best.Value;
        if (confidence < 0.76 || margin < 0.035)
            return false;

        digit = best.Key;
        return true;
    }

    private static readonly (char Digit, string[] Pattern)[] AttributePanelDigitTemplates =
    [
        ('0', ["11111", "11111", "11011", "11011", "11011", "11011", "11111"]),
        ('0', ["11111", "11011", "11001", "10001", "10001", "11001", "11111"]),
        ('1', ["11110", "11110", "01110", "01110", "01110", "01110", "11111"]),
        ('1', ["11100", "00100", "00100", "00100", "00100", "00100", "11111"]),
        ('2', ["11111", "11011", "00011", "00111", "01110", "11100", "11111"]),
        ('2', ["11111", "10011", "00011", "00111", "01110", "11100", "11111"]),
        ('3', ["11111", "11011", "00011", "01111", "00011", "00011", "11111"]),
        ('3', ["11111", "00011", "00011", "01111", "00011", "00011", "11111"]),
        ('4', ["00011", "00111", "01111", "11111", "11111", "11111", "00011"]),
        ('4', ["00011", "00111", "01111", "01111", "11111", "11111", "00011"]),
        ('5', ["11111", "11000", "11000", "11111", "00011", "00011", "11111"]),
        ('6', ["11111", "11110", "11110", "11111", "11011", "11011", "11111"]),
        ('7', ["11111", "11111", "00011", "00110", "00110", "01100", "01100"]),
        ('8', ["11111", "11011", "11111", "01110", "11111", "11011", "11111"]),
        ('9', ["11111", "11111", "11011", "11111", "01111", "00011", "11110"]),
    ];

    private static string[] BuildAttributePanelDigitGrid(Mat mask, DigitSpan span)
    {
        const int gridW = 5;
        const int gridH = 7;
        var rows = new string[gridH];
        for (int gy = 0; gy < gridH; gy++)
        {
            var chars = new char[gridW];
            for (int gx = 0; gx < gridW; gx++)
            {
                int sx = span.X1 + (int)Math.Floor(span.Width * gx / (double)gridW);
                int ex = span.X1 + (int)Math.Floor(span.Width * (gx + 1) / (double)gridW) - 1;
                int sy = span.Y1 + (int)Math.Floor(span.Height * gy / (double)gridH);
                int ey = span.Y1 + (int)Math.Floor(span.Height * (gy + 1) / (double)gridH) - 1;
                int total = 0;
                int filled = 0;
                for (int y = sy; y <= ey; y++)
                {
                    for (int x = sx; x <= ex; x++)
                    {
                        total++;
                        if (mask.At<byte>(y, x) != 0)
                            filled++;
                    }
                }

                chars[gx] = filled / (double)Math.Max(1, total) >= 0.13 ? '1' : '0';
            }
            rows[gy] = new string(chars);
        }

        return rows;
    }

    private static double ScoreAttributePanelDigitGrid(string[] grid, string[] template)
    {
        int matches = 0;
        int total = 0;
        for (int y = 0; y < grid.Length && y < template.Length; y++)
        {
            int width = Math.Min(grid[y].Length, template[y].Length);
            for (int x = 0; x < width; x++)
            {
                if (grid[y][x] == template[y][x])
                    matches++;
                total++;
            }
        }

        return matches / (double)Math.Max(1, total);
    }

    private static async Task<List<string>> RecognizeAttributePanelValueVariants(
        Mat screenshot,
        double x,
        double y,
        double w,
        double h)
    {
        Rect rect = ToPixelRect(screenshot, x, y, w, h);
        if (rect.Width <= 1 || rect.Height <= 1) return [];

        using var region = new Mat(screenshot, rect);
        var results = new List<string>();

        results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(region)));
        results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(region, "en-US")));

        using (var up = new Mat())
        {
            Cv2.Resize(region, up, new OpenCvSharp.Size(region.Width * 3, region.Height * 3), 0, 0, InterpolationFlags.Cubic);
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(up)));
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(up, "en-US")));
        }

        using (var gray = new Mat())
        using (var bin = new Mat())
        using (var upBin = new Mat())
        {
            Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
            Cv2.Resize(bin, upBin, new OpenCvSharp.Size(bin.Width * 3, bin.Height * 3), 0, 0, InterpolationFlags.Nearest);
            Cv2.CvtColor(upBin, upBin, ColorConversionCodes.GRAY2BGR);
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(upBin)));
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(upBin, "en-US")));
        }

        return results;
    }

    private static bool TryParseAttributePanelValue(string text, int maxValue, out int value, out int score)
    {
        value = -1;
        score = 0;
        if (string.IsNullOrEmpty(text)) return false;
        if (IsOcrEngineDiagnostic(text)) return false;

        string normalized = NormalizeAttributePanelDigits(text);

        var slashMatch = Regex.Match(normalized, @"(\d{1,4})\s*(?:/|\uFF0F)\s*1250");
        if (slashMatch.Success &&
            int.TryParse(slashMatch.Groups[1].Value, out int current) &&
            current >= 0 &&
            current <= maxValue)
        {
            value = current;
            score = 10;
            return true;
        }

        var matches = Regex.Matches(normalized, @"\d{1,4}")
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();

        if (matches.Count == 0)
            return false;

        if (matches.Count >= 2 && matches[1] == "1250")
        {
            if (int.TryParse(matches[0], out current) && current >= 0 && current <= maxValue)
            {
                value = current;
                score = 8;
                return true;
            }
            return false;
        }

        if (int.TryParse(matches[0], out current) && current >= 0 && current <= maxValue)
        {
            value = current;
            score = 2;
            return true;
        }

        return false;
    }

    private static bool TryParseAttributePanelCurrentValue(string text, int maxValue, out int value, out int score)
    {
        value = -1;
        score = 0;
        if (string.IsNullOrEmpty(text)) return false;
        if (IsOcrEngineDiagnostic(text)) return false;

        string normalized = NormalizeAttributePanelDigits(text);
        var matches = Regex.Matches(normalized, @"\d{1,4}")
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(m => m != "1250")
            .ToList();

        if (matches.Count == 0)
            return false;

        string digits = matches.OrderByDescending(m => m.Length).First();
        if (!int.TryParse(digits, out int current) || current < 0 || current > maxValue)
            return false;

        value = current;
        score = digits.Length switch
        {
            >= 3 => 16,
            2 => 12,
            _ => 3,
        };
        return true;
    }

    private static string NormalizeAttributePanelDigits(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace('O', '0')
            .Replace('o', '0')
            .Replace('〇', '0')
            .Replace('８', '8')
            .Replace('Ｂ', '8')
            .Replace('B', '8')
            .Replace('Ｓ', '5')
            .Replace('S', '5');
    }

    private static bool IsOcrEngineDiagnostic(string text)
    {
        return text.Contains("OCRengineunavailable", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Unsupportedimageformat", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Bitmapconversionfailed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnorableAttributePanelOcrCandidate(string text)
    {
        return string.IsNullOrEmpty(text) || IsOcrEngineDiagnostic(text);
    }

    private static Rect ToPixelRect(Mat screenshot, double x, double y, double w, double h)
    {
        int px = Math.Clamp((int)(screenshot.Width * x), 0, screenshot.Width - 1);
        int py = Math.Clamp((int)(screenshot.Height * y), 0, screenshot.Height - 1);
        int pw = Math.Min(Math.Max(1, (int)(screenshot.Width * w)), screenshot.Width - px);
        int ph = Math.Min(Math.Max(1, (int)(screenshot.Height * h)), screenshot.Height - py);
        return new Rect(px, py, pw, ph);
    }

    private static int ParseStatValue(string text, string statName)
    {
        if (string.IsNullOrEmpty(text)) return -1;
        if (LooksLikeRankText(text)) return -1;

        // 排除 "/" 后的上限值 1250
        var match = Regex.Match(text, $@"{Regex.Escape(statName)}[^\d/]{{0,5}}(\d{{1,4}})\s*/?\s*(?:1250)?");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int val) && val >= 0 && val <= 1300)
            return val;

        match = Regex.Match(text, @"(\d{1,4})\s*/\s*1250");
        if (match.Success && int.TryParse(match.Groups[1].Value, out val) && val >= 0 && val <= 1300)
            return val;

        return -1;
    }

    private static bool TryParseStatValueFocused(string text, string statName, out int value, out string reason)
    {
        value = -1;
        reason = "no-match";
        if (string.IsNullOrEmpty(text)) return false;

        if (LooksLikeRankText(text)) { reason = "rank-like"; return false; }
        if (LooksLikeOtherStatText(text, statName)) { reason = "other-stat-like"; return false; }

        if (LooksLikePrefixedBonusNoise(text))
        {
            reason = "prefixed-bonus-noise";
            return false;
        }

        if (Regex.IsMatch(text, @"^[^\d]*[+＋]\d{3,4}\s*/\s*1250"))
        {
            reason = "prefixed-bonus-noise";
            return false;
        }

        if (Regex.IsMatch(text, @"^[^\d]*(?:/|\uFF0F)\s*1250\D*$"))
        {
            reason = "max-denominator-only";
            return false;
        }

        if (IsStaminaStat(statName) &&
            !text.Contains(statName, StringComparison.Ordinal) &&
            Regex.IsMatch(text, @"^\D*1250\s*(?:/|\uFF0F)\s*1250\D*$"))
        {
            reason = "unlabeled-cap-ratio";
            return false;
        }

        if (IsStaminaStat(statName) &&
            !text.Contains(statName, StringComparison.Ordinal) &&
            Regex.IsMatch(text, @"^\D*1250\D*$"))
        {
            reason = "unlabeled-cap-digits";
            return false;
        }

        var match = Regex.Match(text, @"(\d{1,4})\s*/\s*1250");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int slashVal) && slashVal >= 0 && slashVal <= 1300)
        { value = slashVal; reason = "current/max"; return true; }

        match = Regex.Match(text, $@"{Regex.Escape(statName)}[^\d/]{{0,5}}(\d{{1,4}})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int labelVal) && labelVal >= 0 && labelVal <= 1300)
        { value = labelVal; reason = "label+digits"; return true; }

        match = Regex.Match(text, @"^\D*(\d{3,4})\D*$");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int plainVal) && plainVal >= 100 && plainVal <= 1300)
        { value = plainVal; reason = "plain-digits"; return true; }

        reason = "digits-not-trustworthy";
        return false;
    }

    private static bool LooksLikeRankText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("RANK", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(text, @"^R\w*ANK?\d{2,4}$", RegexOptions.IgnoreCase);
    }

    private static bool LooksLikePrefixedBonusNoise(string text)
    {
        return Regex.IsMatch(text, @"^[^\d]*(?:[+＋]|锛媇)\s*\d{1,4}\s*/\s*1250");
    }

    private static bool IsStaminaStat(string statName)
    {
        return string.Equals(statName, "\u4f53\u529b", StringComparison.Ordinal);
    }

    /// <summary>
    /// 排除非目标属性的 OCR 串扰；statName 自身不算"其他属性"
    /// </summary>
    private static bool LooksLikeOtherStatText(string text, string statName)
    {
        if (string.IsNullOrEmpty(text)) return false;
        string[] allStats = ["力量", "体力", "韧性", "专注", "保护", "集中"];
        foreach (var s in allStats)
        {
            if (s == statName) continue;
            if (text.Contains(s, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
