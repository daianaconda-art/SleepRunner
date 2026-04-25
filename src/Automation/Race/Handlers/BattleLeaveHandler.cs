using OpenCvSharp;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 战斗结算离开分支：识别右下角「离开」按钮并执行点击
/// </summary>
public class BattleLeaveHandler : IRaceHandler
{
    public string Name => "战斗结算离开";
    public int Priority => 14;

    private const double LeaveTextX = 0.74;
    private const double LeaveTextY = 0.78;
    private const double LeaveTextW = 0.24;
    private const double LeaveTextH = 0.20;
    private const double LeaveBtnFocusX = 0.80;
    private const double LeaveBtnFocusY = 0.86;
    private const double LeaveBtnFocusW = 0.18;
    private const double LeaveBtnFocusH = 0.12;
    private const double LeaveFallbackX = 0.62;
    private const double LeaveFallbackY = 0.76;
    private const double LeaveFallbackW = 0.34;
    private const double LeaveFallbackH = 0.20;
    private const double VictoryTitleX = 0.72;
    private const double VictoryTitleY = 0.10;
    private const double VictoryTitleW = 0.26;
    private const double VictoryTitleH = 0.16;

    private const double LeaveBtnX = 0.91;
    private const double LeaveBtnY = 0.90;
    private const double StageTitleX = 0.00;
    private const double StageTitleY = 0.04;
    private const double StageTitleW = 0.30;
    private const double StageTitleH = 0.16;

    /// <summary>
    /// 检测当前是否为战斗结算离开界面
    /// </summary>
    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        if (IsVictoryResultScreen(screenshot))
        {
            string leaveText = ReadLeaveButtonText(screenshot);
            if (IsLeaveText(leaveText) || HasBlueLeaveButton(screenshot))
            {
                Log.Log($"Leave marker hit on victory screen: '{leaveText}'");
                return true;
            }
        }

        if (IsMoveStageScreen(screenshot))
        {
            Log.Log("Leave marker ignored on move-stage screen.");
            return false;
        }

        if (IsTrainingDetailScreen(screenshot))
        {
            Log.Log("Leave marker ignored on training-detail screen.");
            return false;
        }

        string text = ReadLeaveTextAny(screenshot);
        if (IsCommissionLikeText(text))
        {
            Log.Log($"Leave marker ignored on commission-like text: '{text}'");
            return false;
        }

        if (IsAcceptOnlyText(text))
        {
            Log.Log($"Leave marker ignored on accept-only text: '{text}'");
            return false;
        }

        if (IsRestLikeText(text))
        {
            Log.Log($"Leave marker ignored on rest-like text: '{text}'");
            return false;
        }

        if (IsAppraiseAcceptLikeText(text))
        {
            Log.Log($"Leave marker ignored on appraise-accept text: '{text}'");
            return false;
        }

        if (IsTradeLikeText(text))
        {
            Log.Log($"Leave marker ignored on trade-like text: '{text}'");
            return false;
        }

        if (IsTrainingLikeText(text))
        {
            Log.Log($"Leave marker ignored on training-like text: '{text}'");
            return false;
        }

        bool matched = IsLeaveText(text);
        if (matched)
            Log.Log($"Leave marker hit: '{text}'");
        return matched;
    }

    /// <summary>
    /// 输出当前离开分支决策描述
    /// </summary>
    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string text = ReadLeaveTextAny(screenshot);
        return $"BattleLeave: text='{text}' -> click leave";
    }

    /// <summary>
    /// 执行离开按钮点击
    /// </summary>
    public async Task HandleAsync(GameContext ctx)
    {
        Log.Log("Battle leave branch: click leave button.");
        await ctx.ClickAtPercent(LeaveBtnX, LeaveBtnY);
        await ctx.Wait(1200);
    }

    /// <summary>
    /// 探测模式下仅移动到离开按钮，不执行点击
    /// </summary>
    public async Task ProbeAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log("Battle leave probe: capture empty, skip move.");
            return;
        }

        int x = (int)(shot.Width * LeaveBtnX);
        int y = (int)(shot.Height * LeaveBtnY);
        Log.Log($"Battle leave probe: move cursor to leave ({x},{y}).");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, x, y);
        await ctx.Wait(300);
    }

    /// <summary>
    /// 读取右下角按钮区域 OCR 文本
    /// </summary>
    private static string ReadLeaveText(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, LeaveTextX, LeaveTextY, LeaveTextW, LeaveTextH)
            .GetAwaiter()
            .GetResult();
        return Normalize(raw);
    }

    /// <summary>
    /// 更聚焦地读取右下角蓝色离开按钮文字，避免被整块结算说明干扰。
    /// </summary>
    private static string ReadLeaveButtonText(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, LeaveBtnFocusX, LeaveBtnFocusY, LeaveBtnFocusW, LeaveBtnFocusH)
            .GetAwaiter()
            .GetResult();
        return Normalize(raw);
    }

    /// <summary>
    /// 读取离开候选区域文本，优先右下角，失败时回退到底部区域
    /// </summary>
    private static string ReadLeaveTextAny(Mat screenshot)
    {
        string primary = ReadLeaveButtonText(screenshot);
        if (IsLeaveText(primary))
            return primary;

        primary = ReadLeaveText(screenshot);
        if (IsLeaveText(primary))
            return primary;

        string fallbackRaw = OcrHelper.RecognizeRegion(screenshot, LeaveFallbackX, LeaveFallbackY, LeaveFallbackW, LeaveFallbackH)
            .GetAwaiter()
            .GetResult();
        string fallback = Normalize(fallbackRaw);
        return string.IsNullOrEmpty(primary) ? fallback : $"{primary}|{fallback}";
    }

    /// <summary>
    /// 检测是否为战斗胜利结算页。
    /// </summary>
    private static bool IsVictoryResultScreen(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, VictoryTitleX, VictoryTitleY, VictoryTitleW, VictoryTitleH)
            .GetAwaiter()
            .GetResult();
        string text = Normalize(raw).ToUpperInvariant();
        if (string.IsNullOrEmpty(text))
            return false;

        return text.Contains("VICTORY", StringComparison.Ordinal) ||
               text.Contains("回合以内获胜", StringComparison.Ordinal) ||
               text.Contains("达成度", StringComparison.Ordinal);
    }

    /// <summary>
    /// 兜底检测右下角蓝色离开按钮块。
    /// </summary>
    private static bool HasBlueLeaveButton(Mat screenshot)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(screenshot, hsv, ColorConversionCodes.BGR2HSV);

        int w = screenshot.Width;
        int h = screenshot.Height;
        int x = Math.Clamp((int)(w * LeaveBtnFocusX), 0, w - 1);
        int y = Math.Clamp((int)(h * LeaveBtnFocusY), 0, h - 1);
        int rw = Math.Min(Math.Max(1, (int)(w * LeaveBtnFocusW)), w - x);
        int rh = Math.Min(Math.Max(1, (int)(h * LeaveBtnFocusH)), h - y);

        using var region = new Mat(hsv, new Rect(x, y, rw, rh));
        using var mask = new Mat();
        Cv2.InRange(region, new Scalar(95, 80, 80), new Scalar(125, 255, 255), mask);

        int bluePixels = Cv2.CountNonZero(mask);
        int totalPixels = Math.Max(1, rw * rh);
        double ratio = (double)bluePixels / totalPixels;
        return ratio >= 0.18;
    }

    /// <summary>
    /// 判断 OCR 文本是否包含离开关键词
    /// </summary>
    private static bool IsLeaveText(string text)
    {
        return text.Contains("离开", StringComparison.Ordinal) ||
               text.Contains("離開", StringComparison.Ordinal) ||
               text.Contains("离", StringComparison.Ordinal) && text.Contains("开", StringComparison.Ordinal);
    }

    /// <summary>
    /// 过滤交易界面文案，避免将“购买/交易”误判为离开页
    /// </summary>
    private static bool IsTradeLikeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("交易", StringComparison.Ordinal) ||
               text.Contains("购买", StringComparison.Ordinal) ||
               text.Contains("商品", StringComparison.Ordinal);
    }

    /// <summary>
    /// 过滤委托列表页文案，避免把“讨伐委托/中阶委托”误判为离开页
    /// </summary>
    private static bool IsCommissionLikeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("委托", StringComparison.Ordinal) ||
               text.Contains("讨伐", StringComparison.Ordinal) ||
               text.Contains("RANK", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 过滤仅有“接受”按钮的界面（无离开关键词时不应归类为离开页）
    /// </summary>
    private static bool IsAcceptOnlyText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        bool hasAccept = text.Contains("接受", StringComparison.Ordinal);
        bool hasLeave = IsLeaveText(text);
        return hasAccept && !hasLeave;
    }

    /// <summary>
    /// 过滤休息界面文案，避免把“休息/冥想室”误判为离开页
    /// </summary>
    private static bool IsRestLikeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("休息", StringComparison.Ordinal) ||
               text.Contains("冥想室", StringComparison.Ordinal) ||
               text.Contains("免费", StringComparison.Ordinal);
    }

    /// <summary>
    /// 过滤评鉴战接受页文案，避免把“接受评鉴战”误判为离开
    /// </summary>
    private static bool IsAppraiseAcceptLikeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        bool hasAppraise = text.Contains("评鉴战", StringComparison.Ordinal);
        bool hasAccept = text.Contains("接受", StringComparison.Ordinal);
        bool hasPrepare = text.Contains("战前准备", StringComparison.Ordinal) ||
                          text.Contains("即将开始", StringComparison.Ordinal);
        return hasAppraise && (hasAccept || hasPrepare);
    }

    /// <summary>
    /// 标准化 OCR 文本，提升关键词匹配稳定性
    /// </summary>
    private static string Normalize(string raw)
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
    /// 识别“地区移动”阶段标题，避免将“前往”误判为战斗结算离开
    /// </summary>
    private static bool IsMoveStageScreen(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, StageTitleX, StageTitleY, StageTitleW, StageTitleH)
            .GetAwaiter()
            .GetResult();
        string text = Normalize(raw);
        if (string.IsNullOrEmpty(text))
            return false;
        return text.Contains("地区移动", StringComparison.Ordinal) ||
               text.Contains("目标地区移动", StringComparison.Ordinal);
    }

    /// <summary>
    /// 过滤训练详细页文案，避免把训练条目误判为离开页
    /// </summary>
    private static bool IsTrainingLikeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Contains("训练", StringComparison.Ordinal) &&
            !text.Contains("离开", StringComparison.Ordinal))
            return true;
        int trainingHits = 0;
        if (text.Contains("训练", StringComparison.Ordinal)) trainingHits++;
        if (text.Contains("力量", StringComparison.Ordinal)) trainingHits++;
        if (text.Contains("体力", StringComparison.Ordinal)) trainingHits++;
        if (text.Contains("韧性", StringComparison.Ordinal)) trainingHits++;
        if (text.Contains("集中", StringComparison.Ordinal) || text.Contains("专注", StringComparison.Ordinal)) trainingHits++;
        if (text.Contains("保护", StringComparison.Ordinal)) trainingHits++;
        return trainingHits >= 3;
    }

    /// <summary>
    /// 识别训练详细页（右侧训练列表），避免被离开分支抢处理
    /// </summary>
    private static bool IsTrainingDetailScreen(Mat screenshot)
    {
        const double x = 0.72;
        const double y = 0.18;
        const double w = 0.26;
        const double h = 0.62;
        string raw = OcrHelper.RecognizeRegion(screenshot, x, y, w, h)
            .GetAwaiter()
            .GetResult();
        string text = Normalize(raw);
        if (string.IsNullOrEmpty(text))
            return false;

        int hits = 0;
        if (text.Contains("力量训练", StringComparison.Ordinal)) hits++;
        if (text.Contains("体力训练", StringComparison.Ordinal)) hits++;
        if (text.Contains("韧性训练", StringComparison.Ordinal)) hits++;
        if (text.Contains("集中训练", StringComparison.Ordinal) || text.Contains("专注训练", StringComparison.Ordinal)) hits++;
        if (text.Contains("保护训练", StringComparison.Ordinal)) hits++;
        if (text.Contains("训练Lv", StringComparison.OrdinalIgnoreCase)) hits++;
        return hits >= 2;
    }

    private static readonly LogScope Log = new("Race:BattleLeave");
}
