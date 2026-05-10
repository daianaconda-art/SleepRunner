using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers.Commission;
using SleepRunner.Automation.Race.Handlers.Trade;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

public class AppraiseAcceptHandler : IRaceHandler
{
    public string Name => "评鉴战接受";
    public int Priority => 13;
    private readonly TradeStateStore _tradeStateStore;

    public AppraiseAcceptHandler()
        : this(new TradeStateStore())
    {
    }

    internal AppraiseAcceptHandler(TradeStateStore tradeStateStore)
    {
        _tradeStateStore = tradeStateStore;
    }

    private static readonly (double X, double Y, double W, double H)[] StageTitleRegions =
    [
        (0.01, 0.07, 0.22, 0.12),
        (0.00, 0.04, 0.26, 0.16),
    ];

    private static readonly (double X, double Y, double W, double H)[] PrepareRegions =
    [
        (0.58, 0.28, 0.38, 0.48),
        (0.52, 0.22, 0.44, 0.56),
        (0.50, 0.18, 0.46, 0.62),
    ];

    private static readonly (double X, double Y, double W, double H)[] AcceptTextRegions =
    [
        (0.80, 0.86, 0.18, 0.12),
        (0.76, 0.84, 0.22, 0.14),
    ];

    private const double AcceptBtnX = 0.90;
    private const double AcceptBtnY = 0.90;
    private const int AcceptHotkeySettleDelayMs = 700;
    private const double HotkeyNoReactionMeanDiffThreshold = 2.0;

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string titleText = ReadBestText(screenshot, StageTitleRegions);
        string detailText = ReadBestText(screenshot, PrepareRegions);
        string acceptText = ReadBestText(screenshot, AcceptTextRegions);
        string combined = $"{titleText}|{detailText}|{acceptText}";

        bool hasAppraiseTitle = titleText.Contains("评鉴战", StringComparison.Ordinal) ||
                                detailText.Contains("评鉴战", StringComparison.Ordinal);
        bool hasAccept = acceptText.Contains("接受", StringComparison.Ordinal) ||
                         detailText.Contains("接受", StringComparison.Ordinal);
        bool hasDetailSheet = detailText.Contains("建议综合等级", StringComparison.Ordinal) ||
                              detailText.Contains("登场敌人", StringComparison.Ordinal) ||
                              detailText.Contains("额外奖励", StringComparison.Ordinal) ||
                              detailText.Contains("队伍编制", StringComparison.Ordinal);
        bool hasPopupAction = detailText.Contains("跳过战斗", StringComparison.Ordinal) ||
                              detailText.Contains("开始委托", StringComparison.Ordinal) ||
                              acceptText.Contains("跳过", StringComparison.Ordinal);
        bool tierSelection = CommissionScreenChecks.IsCommissionTierSelectionText(detailText);
        bool hit = CommissionScreenChecks.IsAppraiseAcceptDetailText(titleText, detailText, acceptText);
        if (hit)
            Log.Log($"Appraise accept hit: '{combined}'");
        else if (hasAccept || hasDetailSheet || hasAppraiseTitle)
            Log.Log(
                $"Appraise accept miss: title='{titleText}', detail='{detailText}', accept='{acceptText}', " +
                $"flags=(title={hasAppraiseTitle}, accept={hasAccept}, detail={hasDetailSheet}, popup={hasPopupAction}, tierList={tierSelection})");
        return hit;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string titleText = ReadBestText(screenshot, StageTitleRegions);
        string detailText = ReadBestText(screenshot, PrepareRegions);
        string acceptText = ReadBestText(screenshot, AcceptTextRegions);
        return $"AppraisePrepare: title='{titleText}', detail='{detailText}', accept='{acceptText}' -> press Alt+Space, fallback click if unchanged";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        using var beforeHotkey = ctx.CaptureScreen();
        Log.Log("Appraise prepare: reset trade stage state and press accept hotkey (Alt+Space).");
        _tradeStateStore.SaveVisited(false);
        bool hotkeySent = await ctx.SendGameAction(GameActionKey.AppraiseAccept);
        await ctx.WaitUnscaled(AcceptHotkeySettleDelayMs);
        using var afterHotkey = ctx.CaptureScreen();
        bool stillOnAcceptDetailScreen = afterHotkey != null &&
                                         !afterHotkey.Empty() &&
                                         CommissionScreenChecks.IsAppraiseAcceptDetailScreen(afterHotkey);
        bool fallback = ShouldFallbackAfterHotkeyAttempt(beforeHotkey, afterHotkey, stillOnAcceptDetailScreen);

        if (!fallback)
        {
            string result = hotkeySent
                ? "accept hotkey changed the screen"
                : "accept hotkey changed the screen despite an aborted key sequence";
            Log.Log($"Appraise prepare: {result}; skip mouse fallback.");
            await ctx.Wait(600);
            return;
        }

        string fallbackReason = hotkeySent
            ? "accept hotkey had no confirmed effect"
            : "accept hotkey sequence aborted and no screen change was confirmed";
        Log.Log($"Appraise prepare: {fallbackReason}; fallback click accept.");

        await ctx.ClickAtPercent(AcceptBtnX, AcceptBtnY);
        await ctx.Wait(1300);
    }

    private static bool ShouldFallbackAfterHotkeyAttempt(
        Mat? beforeHotkey,
        Mat? afterHotkey,
        bool stillOnAcceptDetailScreen)
    {
        if (afterHotkey == null || afterHotkey.Empty())
            return true;

        if (stillOnAcceptDetailScreen)
            return true;

        return beforeHotkey != null &&
               !beforeHotkey.Empty() &&
               ShouldFallbackToMouseAfterHotkey(beforeHotkey, afterHotkey);
    }

    private static bool ShouldFallbackToMouseAfterHotkey(Mat beforeHotkey, Mat afterHotkey)
    {
        if (beforeHotkey.Empty() || afterHotkey.Empty())
            return true;

        if (beforeHotkey.Width != afterHotkey.Width ||
            beforeHotkey.Height != afterHotkey.Height ||
            beforeHotkey.Type() != afterHotkey.Type())
            return false;

        using var diff = new Mat();
        Cv2.Absdiff(beforeHotkey, afterHotkey, diff);
        Scalar mean = Cv2.Mean(diff);
        double meanDiff = (mean.Val0 + mean.Val1 + mean.Val2) / 3.0;
        return meanDiff <= HotkeyNoReactionMeanDiffThreshold;
    }

    private static string ReadBestText(
        Mat screenshot,
        (double X, double Y, double W, double H)[] regions)
    {
        string best = "";
        foreach (var r in regions)
        {
            string raw = OcrHelper.RecognizeRegion(
                    screenshot,
                    r.X,
                    r.Y,
                    r.W,
                    r.H)
                .GetAwaiter()
                .GetResult();
            string text = Normalize(raw);
            if (text.Length > best.Length)
                best = text;
        }

        return best;
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";

        return raw
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Trim();
    }

    private static readonly LogScope Log = new("Race:Appraise");
}
