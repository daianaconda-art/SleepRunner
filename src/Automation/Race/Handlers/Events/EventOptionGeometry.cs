using OpenCvSharp;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Events;

/// <summary>
/// 事件页选项点击的几何与文本扫描
///
/// 拆分意图：
/// - 选项 Y 校准是事件页第二大坑（"4-option / 2-option 事件点击 Y" 见 DEVNOTES §23 E）
/// - 集中后调坐标只动这一个文件，不会牵连 OCR/匹配/HandleAsync
/// - PQLOG 仍走 [Race:Event] 标签，日志风格与原 EventHandler 一致
/// </summary>
internal static class EventOptionGeometry
{
    public const double OptionClickX = 0.75;
    public const double BottomOptionY = 0.73;
    public const double OptionSpacing = 0.08;
    public const double RetryAlternateMinDeltaY = 0.012;
    public const double RetrySortAnchorThresholdY = 0.03;
    public const int MaxRetrySweepCount = 2;

    // 二选项：根据实跑日志校准到更容易首点命中的行内中心。
    public static readonly double[] TwoOptionCenters = [0.635, 0.740];
    public static readonly double[] ThreeOptionCenters = [0.55, 0.65, 0.73];
    // 四选项：整体略下移，贴近 hover/retry 实测命中点。
    public static readonly double[] FourOptionCenters = [0.535, 0.605, 0.675, 0.745];

    /// <summary>
    /// 选项从底部向上等间距排列，按 totalOptions 路由到对应配置
    /// </summary>
    public static double CalcOptionClickY(int optionIndex, int totalOptions)
    {
        if (totalOptions <= 0) return BottomOptionY;

        return totalOptions switch
        {
            2 => CalcTwoOptionClickY(optionIndex),
            3 => CalcThreeOptionClickY(optionIndex),
            4 => CalcFourOptionClickY(optionIndex),
            _ => BottomOptionY - (totalOptions - optionIndex) * OptionSpacing,
        };
    }

    public static double CalcTwoOptionClickY(int optionIndex)
    {
        if (optionIndex >= 1 && optionIndex <= TwoOptionCenters.Length)
            return TwoOptionCenters[optionIndex - 1];
        return BottomOptionY - (2 - optionIndex) * OptionSpacing;
    }

    public static double CalcThreeOptionClickY(int optionIndex)
    {
        if (optionIndex >= 1 && optionIndex <= ThreeOptionCenters.Length)
            return ThreeOptionCenters[optionIndex - 1];
        return BottomOptionY - (3 - optionIndex) * OptionSpacing;
    }

    public static double CalcFourOptionClickY(int optionIndex)
    {
        if (optionIndex >= 1 && optionIndex <= FourOptionCenters.Length)
            return FourOptionCenters[optionIndex - 1];
        return BottomOptionY - (4 - optionIndex) * OptionSpacing;
    }

    /// <summary>
    /// 解析最终点击 Y：优先使用单事件 JSON 覆盖，其次按选项数走固定布局
    /// 同时打印一条详尽 calibrate 日志便于回放
    /// </summary>
    public static double ResolveOptionClickY(Mat screenshot, RaceEvent? matchedEvent, int optionIndex, int totalOptions)
    {
        double fixedY = CalcOptionClickY(optionIndex, totalOptions);

        double? overrideY = TryGetOptionYOverride(matchedEvent, optionIndex);
        if (overrideY.HasValue)
        {
            double clamped = Math.Round(Math.Clamp(overrideY.Value, 0.10, 0.95), 3);
            Log.Log(
                $"Option click calibrate: option={optionIndex}/{totalOptions}, y={clamped:F3}, rawY={overrideY.Value:F3}, " +
                $"score=0, rawPx={FormatPoint(screenshot, OptionClickX, clamped)}, " +
                $"resolvedPx={FormatPoint(screenshot, OptionClickX, clamped)}, rect=(n/a), " +
                $"pass=override, strategy=event-yoverride, candidates=[{clamped:F3}], text='[json-override]'");
            return clamped;
        }

        string strategy = totalOptions switch
        {
            2 => "fixed-layout:two-option",
            3 => "fixed-layout:three-option",
            4 => "fixed-layout:four-option",
            _ => "fixed-layout:formula-fallback",
        };

        Log.Log(
            $"Option click calibrate: option={optionIndex}/{totalOptions}, y={fixedY:F3}, rawY={fixedY:F3}, " +
            $"score=0, rawPx={FormatPoint(screenshot, OptionClickX, fixedY)}, " +
            $"resolvedPx={FormatPoint(screenshot, OptionClickX, fixedY)}, rect=(n/a), " +
            $"pass=fixed, strategy={strategy}, candidates=[{fixedY:F3}], text='[fixed-layout]'");
        return fixedY;
    }

    public static double? TryGetOptionYOverride(RaceEvent? matchedEvent, int optionIndex)
    {
        if (matchedEvent?.OptionYOverrides == null) return null;
        if (optionIndex < 1 || optionIndex > matchedEvent.OptionYOverrides.Count) return null;
        return matchedEvent.OptionYOverrides[optionIndex - 1];
    }

    /// <summary>
    /// 构造一行选项的 Y 候选集合，用于 hover-scan 与 retry sweep
    /// </summary>
    public static double[] BuildOptionRowYCandidates(int totalOptions, int optionIndex, double fallbackY)
    {
        var candidates = new List<double>();

        AddYCandidates(candidates, fallbackY,
            totalOptions == 3
                ? [-0.08, -0.06, -0.04, -0.02, 0.00, 0.02, 0.04]
                : totalOptions >= 4
                    ? [-0.06, -0.04, -0.02, 0.00, 0.015, 0.03, 0.045, 0.06, 0.075]
                    : [-0.05, -0.03, -0.015, 0.00, 0.015, 0.03, 0.05]);

        if (totalOptions > 0)
        {
            // 按整个选项区域均分出每一行的理论中心，避免小 OCR 只围着一个错误 fallback 打转
            double slotHeight = EventOcrRegions.OptionsRegionH / totalOptions;
            double slotCenter = EventOcrRegions.OptionsRegionY + slotHeight * (optionIndex - 0.5);
            AddYCandidates(candidates, slotCenter,
                totalOptions >= 4
                    ? [-0.05, -0.035, -0.02, 0.00, 0.02, 0.035, 0.05, 0.065, 0.08]
                    : [-0.04, -0.025, 0.00, 0.025, 0.04]);
        }

        return candidates
            .Select(y => Math.Round(Math.Clamp(y, 0.42, 0.88), 3))
            .Distinct()
            .OrderBy(y => Math.Abs(y - fallbackY))
            .ToArray();
    }

    public static void AddYCandidates(List<double> candidates, double centerY, IEnumerable<double> offsets)
    {
        candidates.Add(centerY);
        foreach (double offset in offsets)
            candidates.Add(centerY + offset);
    }

    /// <summary>
    /// 收集事件选项的关键词与别名，归一化后输出，用于命中评分
    /// </summary>
    public static IEnumerable<string> GetOptionTokens(EventOption option)
    {
        if (option == null)
            yield break;

        string keyword = EventOcrRegions.NormalizeEventCompareText(option.Keyword ?? "");
        if (!string.IsNullOrEmpty(keyword))
            yield return keyword;

        if (option.Alias == null)
            yield break;

        foreach (var alias in option.Alias)
        {
            string normalized = EventOcrRegions.NormalizeEventCompareText(alias ?? "");
            if (!string.IsNullOrEmpty(normalized))
                yield return normalized;
        }
    }

    /// <summary>
    /// 围绕一行选项 Y 取多个 ROI 候选，返回 (text, rect, pass) 三元组用于评分
    /// </summary>
    public static IEnumerable<(string Text, Rect Rect, string Pass)> ReadEventOptionRowTextCandidates(Mat screenshot, double yCenter)
    {
        var regions = new (double X, double Y, double W, double H)[]
        {
            (0.66, Math.Clamp(yCenter - 0.035, 0.38, 0.89), 0.32, 0.07),
            (0.64, Math.Clamp(yCenter - 0.038, 0.36, 0.88), 0.34, 0.075),
            (0.62, Math.Clamp(yCenter - 0.045, 0.34, 0.87), 0.36, 0.09),
        };

        foreach (var region in regions)
        {
            foreach (var candidate in ReadEventOptionRegionCandidates(screenshot, region.X, region.Y, region.W, region.H))
                yield return candidate;
        }
    }

    /// <summary>
    /// 在指定 ROI 内做 native / enlarged / binary 三遍 OCR，提升弱字识别率
    /// </summary>
    public static IEnumerable<(string Text, Rect Rect, string Pass)> ReadEventOptionRegionCandidates(
        Mat screenshot,
        double xPct,
        double yPct,
        double wPct,
        double hPct)
    {
        var rect = ToPixelRect(screenshot, xPct, yPct, wPct, hPct);
        if (rect.Width <= 0 || rect.Height <= 0)
            yield break;

        using var roi = new Mat(screenshot, rect);
        using var roiClone = roi.Clone();
        yield return (EventOcrRegions.NormalizeEventCompareText(OcrHelper.RecognizeMat(roiClone).GetAwaiter().GetResult()), rect, "native");

        using var enlarged = new Mat();
        Cv2.Resize(roi, enlarged, new OpenCvSharp.Size(), 2.5, 2.5, InterpolationFlags.Cubic);
        yield return (EventOcrRegions.NormalizeEventCompareText(OcrHelper.RecognizeMat(enlarged).GetAwaiter().GetResult()), rect, "enlarged");

        using var gray = new Mat();
        using var binary = new Mat();
        using var binaryBgr = new Mat();
        Cv2.CvtColor(enlarged, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        Cv2.CvtColor(binary, binaryBgr, ColorConversionCodes.GRAY2BGR);
        yield return (EventOcrRegions.NormalizeEventCompareText(OcrHelper.RecognizeMat(binaryBgr).GetAwaiter().GetResult()), rect, "binary");
    }

    public static Rect ToPixelRect(Mat screenshot, double xPct, double yPct, double wPct, double hPct)
    {
        int x = (int)(screenshot.Width * xPct);
        int y = (int)(screenshot.Height * yPct);
        int w = (int)(screenshot.Width * wPct);
        int h = (int)(screenshot.Height * hPct);

        x = Math.Clamp(x, 0, screenshot.Width - 1);
        y = Math.Clamp(y, 0, screenshot.Height - 1);
        w = Math.Min(w, screenshot.Width - x);
        h = Math.Min(h, screenshot.Height - y);

        return new Rect(x, y, Math.Max(0, w), Math.Max(0, h));
    }

    /// <summary>
    /// 计算两帧某 ROI 内灰度差超阈值的像素占比
    /// </summary>
    public static double MeasureHoverDiffRatio(Mat before, Mat after, double xPct, double yPct, double wPct, double hPct)
    {
        if (before.Empty() || after.Empty())
            return 0;

        var beforeRect = ToPixelRect(before, xPct, yPct, wPct, hPct);
        var afterRect = ToPixelRect(after, xPct, yPct, wPct, hPct);
        if (beforeRect.Width <= 0 || beforeRect.Height <= 0 || afterRect.Width <= 0 || afterRect.Height <= 0)
            return 0;

        using var beforeRoi = new Mat(before, beforeRect);
        using var afterRoi = new Mat(after, afterRect);
        using var beforeGray = new Mat();
        using var afterGray = new Mat();
        using var diff = new Mat();
        Cv2.CvtColor(beforeRoi, beforeGray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(afterRoi, afterGray, ColorConversionCodes.BGR2GRAY);
        Cv2.Absdiff(beforeGray, afterGray, diff);
        Cv2.Threshold(diff, diff, 8, 255, ThresholdTypes.Binary);

        double changedPixels = Cv2.CountNonZero(diff);
        double total = Math.Max(1, diff.Rows * diff.Cols);
        return changedPixels / total;
    }

    /// <summary>
    /// 主点未命中后的扫描 Y 序列：合并行内候选与扇形候选，按距 sortAnchor 远近排序
    /// </summary>
    public static List<double> BuildRetrySweepYs(int optionIndex, int totalOptions, double primaryY)
    {
        double anchorY = Math.Clamp(CalcOptionClickY(optionIndex, totalOptions), 0.42, 0.88);
        double sortAnchorY = Math.Abs(primaryY - anchorY) >= RetrySortAnchorThresholdY
            ? primaryY
            : anchorY;
        var candidates = BuildOptionRowYCandidates(totalOptions, optionIndex, anchorY)
            .Append(anchorY)
            .Select(y => Math.Round(Math.Clamp(y, 0.42, 0.88), 3))
            .Distinct()
            .Where(y => Math.Abs(y - primaryY) >= RetryAlternateMinDeltaY);

        return candidates
            .OrderBy(y => Math.Abs(y - sortAnchorY))
            .ThenBy(y => y)
            .Take(MaxRetrySweepCount)
            .ToList();
    }

    public static string FormatPoint(Mat screenshot, double xPct, double yPct)
    {
        int x = Math.Clamp((int)(screenshot.Width * xPct), 0, Math.Max(0, screenshot.Width - 1));
        int y = Math.Clamp((int)(screenshot.Height * yPct), 0, Math.Max(0, screenshot.Height - 1));
        return $"({x},{y})";
    }

    public static string FormatRect(Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return "(n/a)";
        return $"({rect.X},{rect.Y},{rect.Width},{rect.Height})";
    }

    /// <summary>
    /// 给一行 OCR 文本按 target/opposite tokens 命中数打分（命中目标 +25、反目标 -26）
    /// </summary>
    public static int ScoreEventOptionRowText(string text, string[] targetTokens, string[] oppositeTokens)
    {
        if (string.IsNullOrEmpty(text))
            return -10;

        int score = EventScreenChecks.CountChineseChars(text);
        int targetHits = CountMatchedTokens(text, targetTokens);
        int oppositeHits = CountMatchedTokens(text, oppositeTokens);
        score += targetHits * 25;
        score -= oppositeHits * 26;

        return score;
    }

    public static int CountMatchedTokens(string text, string[] tokens)
    {
        if (string.IsNullOrEmpty(text) || tokens == null || tokens.Length == 0)
            return 0;

        int count = 0;
        foreach (var token in tokens.Distinct())
        {
            if (!string.IsNullOrEmpty(token) && text.Contains(token, StringComparison.Ordinal))
                count++;
        }
        return count;
    }
    private static readonly LogScope Log = new("Race:Event");
}
