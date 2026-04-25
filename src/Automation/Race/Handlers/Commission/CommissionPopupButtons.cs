using OpenCvSharp;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Commission;

/// <summary>
/// 委托弹窗按钮位置解析（OCR 文本 + 视觉颜色双策略），并封装移动鼠标 + 点击
/// </summary>
internal static class CommissionPopupButtons
{
    // 弹窗按钮标准锚点（fallback）
    public const double SkipBattleBtnX = 0.43;
    public const double SkipBattleBtnY = 0.725;
    public const double StartCommissionBtnX = 0.58;
    public const double StartCommissionBtnY = 0.725;

    // 候选按钮 X：want=start 时优先右侧，want=skip 时优先左侧
    private static readonly double[] PopupBtnXCandidatesRightFirst = [0.58, 0.62, 0.66, 0.50, 0.42];
    private static readonly double[] PopupBtnXCandidatesLeftFirst = [0.42, 0.50, 0.58, 0.62, 0.66];
    private static readonly double[] PopupBtnYCandidates = [0.725, 0.74, 0.71, 0.76, 0.69];

    private const double PopupButtonOcrW = 0.18;
    private const double PopupButtonOcrH = 0.08;
    private const double PopupButtonVisualW = 0.20;
    private const double PopupButtonVisualH = 0.10;

    /// <summary>
    /// 弹窗按钮点击前先做探测：优先 OCR 命中目标按钮文案，再移动鼠标确认后点击
    /// </summary>
    public static async Task ProbeAndClickAsync(GameContext ctx, Mat popupShot, string buttonType)
    {
        ResolveTarget(popupShot, buttonType, out double chosenX, out double chosenY, out string reason, out string hitText, out int bestScore);

        int px = ToClientPixelX(popupShot, chosenX);
        int py = ToClientPixelY(popupShot, chosenY);
        Log.Log($"Popup probe: button={buttonType}, x={chosenX:F3}, y={chosenY:F3}, reason='{reason}', score={bestScore}, text='{hitText}', move=({px},{py})");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, px, py);
        await ctx.Wait(120);
        await ctx.ClickAtPercent(chosenX, chosenY);
    }

    /// <summary>
    /// 解析按钮目标坐标：依次尝试 OCR 高分命中 → 视觉颜色 → 锚点回退
    /// </summary>
    public static void ResolveTarget(
        Mat screenshot,
        string buttonType,
        out double chosenX,
        out double chosenY,
        out string reason,
        out string hitText,
        out int bestScore)
    {
        bool wantStart = string.Equals(buttonType, "start", StringComparison.OrdinalIgnoreCase);
        string[] targetKeywords = wantStart
            ? ["开始委托", "开始", "委托"]
            : ["跳过战斗", "跳过", "战斗"];
        string[] oppositeKeywords = wantStart
            ? ["跳过战斗", "跳过", "战斗"]
            : ["开始委托", "开始", "委托"];

        chosenX = wantStart ? StartCommissionBtnX : SkipBattleBtnX;
        chosenY = wantStart ? StartCommissionBtnY : SkipBattleBtnY;
        hitText = "";
        bestScore = int.MinValue;
        var xCandidates = wantStart ? PopupBtnXCandidatesRightFirst : PopupBtnXCandidatesLeftFirst;

        foreach (double x in xCandidates)
        {
            foreach (double y in PopupBtnYCandidates)
            {
                string text = ReadButtonText(screenshot, x, y);
                int score = ScoreButtonText(text, targetKeywords, oppositeKeywords, wantStart);
                if (score > bestScore)
                {
                    bestScore = score;
                    chosenX = x;
                    chosenY = y;
                    hitText = text;
                }

                if (score >= 50)
                {
                    if (!wantStart &&
                        CommissionPopupLocator.TryLocateSkipButton(screenshot, out _, out var skipCenter, out double skipRatio))
                    {
                        chosenX = skipCenter.X / (double)screenshot.Width;
                        chosenY = skipCenter.Y / (double)screenshot.Height;
                        if (bestScore < 0)
                            bestScore = 0;
                        reason = "ocr+visual-center";
                        return;
                    }

                    reason = "ocr";
                    return;
                }
            }
        }

        if (TryResolveByVisual(screenshot, wantStart, out double visualX, out double visualY, out double visualRatio))
        {
            chosenX = visualX;
            chosenY = visualY;
            reason = wantStart ? $"visual-light:{visualRatio:F3}" : $"visual-blue:{visualRatio:F3}";
            if (bestScore < 0)
                bestScore = 0;
            return;
        }

        reason = "default-fallback";
        Log.Log($"Popup probe fallback: button={buttonType}, no OCR hit, bestScore={bestScore}, bestText='{hitText}'");
    }

    /// <summary>
    /// 百分比 X 转客户区像素，便于探测日志与移动确认
    /// </summary>
    public static int ToClientPixelX(Mat screenshot, double xPct) => (int)(screenshot.Width * xPct);

    /// <summary>
    /// 百分比 Y 转客户区像素
    /// </summary>
    public static int ToClientPixelY(Mat screenshot, double yPct) => (int)(screenshot.Height * yPct);

    /// <summary>
    /// 读取弹窗按钮附近文本用于命中确认
    /// </summary>
    private static string ReadButtonText(Mat screenshot, double xCenter, double yCenter)
    {
        double x = Math.Clamp(xCenter - PopupButtonOcrW / 2, 0, 1 - PopupButtonOcrW);
        double y = Math.Clamp(yCenter - PopupButtonOcrH / 2, 0, 1 - PopupButtonOcrH);
        string raw = OcrHelper.RecognizeRegion(screenshot, x, y, PopupButtonOcrW, PopupButtonOcrH)
            .GetAwaiter()
            .GetResult();
        return CommissionOcrRegions.NormalizeOcr(raw);
    }

    /// <summary>
    /// 弹窗按钮文本打分：命中目标关键词加分，命中“取消”/反向关键词降分
    /// </summary>
    private static int ScoreButtonText(string text, string[] targetKeywords, string[] oppositeKeywords, bool wantStart)
    {
        if (string.IsNullOrEmpty(text))
            return -10;

        int score = 0;
        foreach (var kw in targetKeywords)
        {
            if (!string.IsNullOrEmpty(kw) && text.Contains(kw, StringComparison.Ordinal))
                score += 25;
        }

        foreach (var kw in oppositeKeywords)
        {
            if (!string.IsNullOrEmpty(kw) && text.Contains(kw, StringComparison.Ordinal))
                score -= 30;
        }

        if (text.Contains("取消", StringComparison.Ordinal))
            score -= 30;

        // 跳过战斗二次确认常见“跳过故事”，可作为额外线索
        if (!wantStart && text.Contains("跳过", StringComparison.Ordinal))
            score += 10;

        return score;
    }

    /// <summary>
    /// 视觉颜色定位：start=亮色按钮，skip=蓝色按钮。优先复用 SkipButton 视觉定位器。
    /// </summary>
    private static bool TryResolveByVisual(
        Mat screenshot,
        bool wantStart,
        out double chosenX,
        out double chosenY,
        out double chosenRatio)
    {
        chosenX = wantStart ? StartCommissionBtnX : SkipBattleBtnX;
        chosenY = wantStart ? StartCommissionBtnY : SkipBattleBtnY;
        chosenRatio = 0;

        if (!wantStart &&
            CommissionPopupLocator.TryLocateSkipButton(screenshot, out _, out var skipCenter, out double skipRatio))
        {
            chosenX = skipCenter.X / (double)screenshot.Width;
            chosenY = skipCenter.Y / (double)screenshot.Height;
            chosenRatio = skipRatio;
            return true;
        }

        var xCandidates = wantStart ? PopupBtnXCandidatesRightFirst : PopupBtnXCandidatesLeftFirst;
        foreach (double x in xCandidates)
        {
            foreach (double y in PopupBtnYCandidates)
            {
                double ratio = wantStart
                    ? MeasureLightButtonRatio(screenshot, x, y)
                    : MeasureBlueButtonRatio(screenshot, x, y);
                if (ratio > chosenRatio)
                {
                    chosenRatio = ratio;
                    chosenX = x;
                    chosenY = y;
                }
            }
        }

        return wantStart ? chosenRatio >= 0.28 : chosenRatio >= 0.16;
    }

    private static double MeasureBlueButtonRatio(Mat screenshot, double xCenter, double yCenter)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(screenshot, hsv, ColorConversionCodes.BGR2HSV);
        using var region = CreateButtonRegion(hsv, xCenter, yCenter);
        using var mask = new Mat();
        Cv2.InRange(region, new Scalar(95, 80, 80), new Scalar(125, 255, 255), mask);

        int pixels = Cv2.CountNonZero(mask);
        int total = Math.Max(1, region.Width * region.Height);
        return (double)pixels / total;
    }

    private static double MeasureLightButtonRatio(Mat screenshot, double xCenter, double yCenter)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(screenshot, hsv, ColorConversionCodes.BGR2HSV);
        using var region = CreateButtonRegion(hsv, xCenter, yCenter);
        using var mask = new Mat();
        Cv2.InRange(region, new Scalar(0, 0, 175), new Scalar(180, 45, 255), mask);

        int pixels = Cv2.CountNonZero(mask);
        int total = Math.Max(1, region.Width * region.Height);
        return (double)pixels / total;
    }

    private static Mat CreateButtonRegion(Mat hsv, double xCenter, double yCenter)
    {
        int w = hsv.Width;
        int h = hsv.Height;
        int x = Math.Clamp((int)(w * (xCenter - PopupButtonVisualW / 2)), 0, w - 1);
        int y = Math.Clamp((int)(h * (yCenter - PopupButtonVisualH / 2)), 0, h - 1);
        int rw = Math.Min(Math.Max(1, (int)(w * PopupButtonVisualW)), w - x);
        int rh = Math.Min(Math.Max(1, (int)(h * PopupButtonVisualH)), h - y);
        return new Mat(hsv, new Rect(x, y, rw, rh));
    }
    private static readonly LogScope Log = new("Race:Commission");
}
