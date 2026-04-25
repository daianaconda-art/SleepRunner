using OpenCvSharp;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Events;

/// <summary>
/// 事件页所有 OCR 候选区域常量 + Read*Text 多区域取最佳 + 文本归一化工具
///
/// 拆分意图：
/// - 把"在哪个 ROI 里读什么"集中到一个文件，调整事件文案区域只动这里
/// - 与 EventScreenChecks 的 Is/Score 静态函数双向调用属于正常静态依赖，C# 不构成循环
/// - 后续可以把 Read*Text 改成走 FrameContext.GetOcrAsync 享受帧缓存
/// </summary>
internal static class EventOcrRegions
{
    public const double OptionsRegionX = 0.55;
    public const double OptionsRegionY = 0.45;
    public const double OptionsRegionW = 0.40;
    public const double OptionsRegionH = 0.35;

    public static readonly (double X, double Y, double W, double H)[] EventTitleRegions =
    [
        (0.01, 0.07, 0.22, 0.12),
        (0.08, 0.20, 0.26, 0.14),
        (0.11, 0.22, 0.20, 0.12),
        (0.10, 0.24, 0.22, 0.10),
    ];

    public static readonly (double X, double Y, double W, double H)[] JourneyMarkerRegions =
    [
        (0.00, 0.00, 0.22, 0.08),
        (0.00, 0.00, 0.26, 0.12),
        (0.02, 0.00, 0.30, 0.14),
        (0.02, 0.20, 0.38, 0.16),
        (0.02, 0.22, 0.42, 0.18),
        (0.00, 0.12, 0.42, 0.20),
        (0.00, 0.18, 0.55, 0.28),
        (0.04, 0.22, 0.52, 0.24),
        (0.02, 0.24, 0.62, 0.30),
        (0.00, 0.28, 0.68, 0.34),
        (0.00, 0.30, 0.72, 0.36),
    ];

    public static readonly (double X, double Y, double W, double H)[] EventOptionHintRegions =
    [
        (0.52, 0.42, 0.44, 0.40),
        (0.55, 0.45, 0.40, 0.35),
        (0.58, 0.48, 0.38, 0.32),
        (0.50, 0.50, 0.46, 0.34),
    ];

    public static readonly (double X, double Y, double W, double H)[] EventStoryRegions =
    [
        (0.34, 0.82, 0.52, 0.12),
        (0.30, 0.80, 0.58, 0.15),
        (0.36, 0.78, 0.48, 0.14),
    ];

    public static readonly (double X, double Y, double W, double H)[] TrainPlatformHintRegions =
    [
        (0.56, 0.34, 0.40, 0.42),
        (0.58, 0.40, 0.38, 0.34),
        (0.60, 0.46, 0.36, 0.28),
        (0.62, 0.30, 0.34, 0.46),
    ];

    public static readonly (double X, double Y, double W, double H)[] RestConfirmRegions =
    [
        (0.78, 0.82, 0.18, 0.14),
        (0.80, 0.84, 0.16, 0.12),
        (0.76, 0.80, 0.20, 0.16),
    ];

    /// <summary>
    /// 多候选区域读取左上角"旅程事件"标识，命中即返回，否则返回得分最优文本
    /// </summary>
    public static string ReadJourneyMarkerText(Mat screenshot)
    {
        string firstNonEmpty = "";
        string bestHintText = "";
        foreach (var region in JourneyMarkerRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text)) continue;
            if (EventScreenChecks.IsJourneyEventMarker(text))
                return text;
            if (EventScreenChecks.ContainsJourneyHint(text) && !EventScreenChecks.IsJourneyNoise(text) && string.IsNullOrEmpty(bestHintText))
                bestHintText = text;
            if (string.IsNullOrEmpty(firstNonEmpty))
                firstNonEmpty = text;
        }

        if (!string.IsNullOrEmpty(bestHintText))
            return bestHintText;
        return firstNonEmpty;
    }

    /// <summary>
    /// 多候选区域读取右下角事件选项提示文本
    /// </summary>
    public static string ReadEventOptionHintText(Mat screenshot)
    {
        string firstNonEmpty = "";
        foreach (var region in EventOptionHintRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text)) continue;
            if (EventScreenChecks.IsEventOptionHint(text))
                return text;
            if (string.IsNullOrEmpty(firstNonEmpty))
                firstNonEmpty = text;
        }
        return firstNonEmpty;
    }

    /// <summary>
    /// 多候选区域读取右侧"列车月台"选择界面提示
    /// </summary>
    public static string ReadTrainPlatformHintText(Mat screenshot)
    {
        string firstNonEmpty = "";
        foreach (var region in TrainPlatformHintRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text)) continue;
            if (EventScreenChecks.IsTrainPlatformSingleOptionScreen(text))
                return text;
            if (string.IsNullOrEmpty(firstNonEmpty))
                firstNonEmpty = text;
        }
        return firstNonEmpty;
    }

    /// <summary>
    /// 多候选区域读取右下角休息确认按钮文本
    /// </summary>
    public static string ReadRestConfirmText(Mat screenshot)
    {
        string best = "";
        foreach (var region in RestConfirmRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text)) continue;
            if (text.Contains("休息", StringComparison.Ordinal))
                return text;
            if (string.IsNullOrEmpty(best) || text.Length > best.Length)
                best = text;
        }
        return best;
    }

    /// <summary>
    /// 读取事件标题；若读到时间轴噪声（如"4月下旬"）则返回空
    /// </summary>
    public static string ReadEventTitleText(Mat screenshot)
    {
        string best = "";
        int bestScore = int.MinValue;
        foreach (var region in EventTitleRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            int score = EventScreenChecks.ScoreEventTitleText(text);
            if (score > bestScore || (score == bestScore && text.Length > best.Length))
            {
                best = text;
                bestScore = score;
            }
        }

        if (EventScreenChecks.IsJourneyNoise(best))
            return "";
        return best;
    }

    /// <summary>
    /// 多区域读取事件选项文本，按"是否像可决策选项"打分挑最优
    /// </summary>
    public static string ReadEventOptionsText(Mat screenshot)
    {
        var regions = new List<(double X, double Y, double W, double H)>
        {
            (OptionsRegionX, OptionsRegionY, OptionsRegionW, OptionsRegionH),
        };
        regions.AddRange(EventOptionHintRegions);

        string bestText = "";
        int bestScore = int.MinValue;
        foreach (var region in regions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text)) continue;

            int score = EventScreenChecks.CountChineseChars(text);
            if (EventScreenChecks.IsEventOptionHint(text))
                score += 20;
            if (EventScreenChecks.IsMainMenuLikeText(text))
                score -= 30;
            if (text.Length > 12)
                score += 3;

            if (score > bestScore)
            {
                bestScore = score;
                bestText = text;
            }
        }
        return bestText;
    }

    /// <summary>
    /// 读取事件正文用于区分"天气-浓雾/雷雨"等选项相同标题易抖的页面
    /// </summary>
    public static string ReadEventStoryText(Mat screenshot)
    {
        string best = "";
        int bestScore = int.MinValue;
        foreach (var region in EventStoryRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text)) continue;

            int score = EventScreenChecks.CountChineseChars(text);
            if (text.Contains("浓雾", StringComparison.Ordinal) || text.Contains("大雾", StringComparison.Ordinal)) score += 8;
            if (text.Contains("雷雨", StringComparison.Ordinal) || text.Contains("雷", StringComparison.Ordinal)) score += 8;
            if (text.Contains("训练途中", StringComparison.Ordinal) || text.Contains("这种时候", StringComparison.Ordinal)) score += 6;
            if (text.Length > 12) score += 3;

            if (score > bestScore || (score == bestScore && text.Length > best.Length))
            {
                best = text;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>
    /// OCR 文字归一化：去空格、换行、全角空格
    /// </summary>
    public static string NormalizeOcr(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Trim();
    }

    /// <summary>
    /// 用于事件名匹配的归一化：把"旅程事件 / 今日 / 大雾"等同义字面对齐
    /// </summary>
    public static string NormalizeEventTitleForMatch(string raw)
    {
        string text = NormalizeOcr(raw);
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace("旅程事件", "")
            .Replace("今日", "今天")
            .Replace("大雾", "浓雾")
            .Replace("·", "")
            .Replace("-", "")
            .Replace("—", "")
            .Replace("•", "")
            .Replace("^", "");
    }

    /// <summary>
    /// 事件前后帧文本对比专用：去除标点 + 引号 + + 号，避免 hover 微差异误判
    /// </summary>
    public static string NormalizeEventCompareText(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";

        string text = NormalizeOcr(raw);
        text = text
            .Replace("“", "")
            .Replace("”", "")
            .Replace("‘", "")
            .Replace("’", "")
            .Replace("^", "")
            .Replace("、", "")
            .Replace("，", "")
            .Replace(",", "")
            .Replace("。", "")
            .Replace("！", "")
            .Replace("？", "")
            .Replace("+", "");
        return text;
    }
}
