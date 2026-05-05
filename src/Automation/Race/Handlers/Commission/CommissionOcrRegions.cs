using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race.Handlers.Commission;

/// <summary>
/// 委托相关的 OCR 区域常量 + 文本归一化 + 多区域取最佳读取
/// </summary>
internal static class CommissionOcrRegions
{
    // 委托界面 OCR 检测区域（右侧列表）
    // 评鉴战/战斗结束后回到委托列表时，标题“XX讨伐委托 / 高阶委托”常落得更靠上，
    // 放宽到更高更大的区域，避免只截到下半部分任务说明而漏掉“委托”标题。
    public const double CommissionRegionX = 0.66;
    public const double CommissionRegionY = 0.34;
    public const double CommissionRegionW = 0.32;
    public const double CommissionRegionH = 0.44;

    public const double VictoryTitleX = 0.72;
    public const double VictoryTitleY = 0.10;
    public const double VictoryTitleW = 0.26;
    public const double VictoryTitleH = 0.16;

    public const double LeaveBtnTextX = 0.80;
    public const double LeaveBtnTextY = 0.86;
    public const double LeaveBtnTextW = 0.18;
    public const double LeaveBtnTextH = 0.12;

    public static readonly (double X, double Y, double W, double H)[] AppraiseTitleRegions =
    [
        (0.01, 0.07, 0.22, 0.12),
        (0.00, 0.04, 0.26, 0.16),
    ];

    public static readonly (double X, double Y, double W, double H)[] AppraiseDetailRegions =
    [
        (0.58, 0.28, 0.38, 0.48),
        (0.52, 0.22, 0.44, 0.56),
    ];

    public static readonly (double X, double Y, double W, double H)[] AppraiseAcceptTextRegions =
    [
        (0.80, 0.86, 0.18, 0.12),
        (0.76, 0.84, 0.22, 0.14),
    ];

    public static readonly (double X, double Y, double W, double H)[] CommissionCurrentTitleRegions =
    [
        (0.55, 0.24, 0.28, 0.12),
        (0.53, 0.22, 0.32, 0.14),
    ];

    public static readonly (double X, double Y, double W, double H)[] CommissionCurrentTierRegions =
    [
        (0.59, 0.34, 0.16, 0.10),
        (0.57, 0.32, 0.20, 0.12),
    ];

    // 二次弹窗候选区域（评鉴战/战斗委托确认弹窗的关键文本与按钮行）
    public static readonly (double X, double Y, double W, double H)[] PopupDetectRegions =
    [
        (0.20, 0.18, 0.60, 0.52),
        // 评鉴战后的委托弹窗文本更偏右，增加右侧候选区域
        (0.58, 0.28, 0.38, 0.48),
        // 复用已验证能读到“开始委托”的右侧操作区
        (0.55, 0.45, 0.40, 0.35),
        // 当前“是否要进行战斗委托？”弹窗的关键文案主要落在底部按钮行
        (0.22, 0.72, 0.56, 0.22),
        (0.28, 0.76, 0.46, 0.18),
    ];

    public const double DifficultyRegionX = 0.46;
    public const double DifficultyRegionY = 0.33;
    public const double DifficultyRegionW = 0.12;
    public const double DifficultyRegionH = 0.08;

    /// <summary>
    /// 标准化 OCR 输出：去掉空白与全角空格，便于关键词命中
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
    /// 多候选区域 OCR，返回长度最长的归一化文本
    /// </summary>
    public static string ReadBestText(
        Mat screenshot,
        (double X, double Y, double W, double H)[] regions)
    {
        string best = "";
        foreach (var r in regions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (text.Length > best.Length)
                best = text;
        }
        return best;
    }

    /// <summary>
    /// 委托列表区 OCR，用于分支识别
    /// </summary>
    public static string ReadCommissionText(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(
                screenshot,
                CommissionRegionX,
                CommissionRegionY,
                CommissionRegionW,
                CommissionRegionH)
            .GetAwaiter()
            .GetResult();
        return NormalizeOcr(raw);
    }

    public static string ReadCommissionFallbackText(Mat screenshot)
    {
        var texts = new List<string>
        {
            ReadBestText(screenshot, AppraiseDetailRegions),
            ReadBestText(screenshot, CommissionCurrentTitleRegions),
            ReadBestText(screenshot, CommissionCurrentTierRegions),
        };

        return string.Join("|", texts.Where(static text => !string.IsNullOrEmpty(text)).Distinct());
    }
}
