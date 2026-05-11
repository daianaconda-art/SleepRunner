using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers.Commission;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 委托分支：识别到「讨伐委托」界面后，固定选择第 3 项并点击下方接受。
///
/// 重构后职责（编排层）：
/// - CanHandle / DescribeDecision：调用 Commission/* 子模块做指纹识别
/// - HandleAsync / ProbeAsync：完成"列表点击 → 详情接受 → 弹窗循环决策"编排
/// - 弹窗 OCR / 按钮定位 / 困难判定 全部委托给 Commission/ 子模块
/// </summary>
public class CommissionHandler : IRaceHandler
{
    public string Name => "委托决策";
    public int Priority => 14;

    private const int PopupCacheTtlMs = 5000;

    // 委托列表第 2/3 项与"接受"按钮锚点
    private const double ListOptionX = 0.88;
    private const double SecondOptionY = 0.50;
    // 之前 0.68 偏下，提升到 0.62 更贴近第三项文本中线
    private const double ThirdOptionY = 0.62;
    private const double AcceptBtnX = 0.88;
    // 0.84 偏上，回调到 0.87 作为中间值
    private const double AcceptBtnY = 0.87;
    private const double DetailAcceptBtnX = 0.90;
    private const double DetailAcceptBtnY = 0.90;

    private string _lastPopupText = "";
    private PopupDecisionMode _lastPopupMode = PopupDecisionMode.None;
    private DateTime _lastPopupUtc = DateTime.MinValue;
    private readonly CommissionCardRewardStateStore _cardRewardStateStore;

    public CommissionHandler()
        : this(new CommissionCardRewardStateStore())
    {
    }

    internal CommissionHandler(CommissionCardRewardStateStore cardRewardStateStore)
    {
        _cardRewardStateStore = cardRewardStateStore ?? throw new ArgumentNullException(nameof(cardRewardStateStore));
    }

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        if (CommissionScreenChecks.IsBattleResultScreen(screenshot))
        {
            Log.Log("Commission screen ignored: battle result screen still active.");
            return false;
        }

        if (CommissionScreenChecks.IsAppraiseAcceptDetailScreen(screenshot))
        {
            Log.Log("Commission screen ignored: appraise accept detail screen.");
            return false;
        }

        bool popupHit = CommissionPopupChecks.DetectDecisionPopup(screenshot, out string popupText);
        if (popupHit)
        {
            var popupMode = CommissionPopupChecks.ClassifyPopupDecisionMode(popupText);
            RememberPopupDetection(popupText, popupMode);
            Log.Log($"Commission popup hit: mode={popupMode}, text='{popupText}'");
            return true;
        }

        if (MainMenuScreenChecks.IsMainMenuScreen(screenshot, out string mainMenuSummary))
        {
            Log.Log($"Commission screen ignored: main menu detected ({mainMenuSummary}).");
            return false;
        }

        string text = CommissionOcrRegions.ReadCommissionText(screenshot);
        string fallbackText = "";
        bool hit = CommissionScreenChecks.IsCommissionEntryText(text, popupText);
        if (!hit)
        {
            fallbackText = CommissionOcrRegions.ReadCommissionFallbackText(screenshot);
            hit = CommissionScreenChecks.IsCommissionEntryText(text, fallbackText);
        }

        if (hit)
        {
            Log.Log($"Commission screen hit: list='{text}', popup='{popupText}', fallback='{fallbackText}'");
        }
        else if (!string.IsNullOrEmpty(popupText) || !string.IsNullOrEmpty(text) || !string.IsNullOrEmpty(fallbackText))
        {
            Log.Log($"Commission miss: popup='{popupText}', list='{text}', fallback='{fallbackText}'");
        }

        return hit;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        if (CommissionScreenChecks.IsCommissionAcceptDetailScreen(screenshot))
            return "Commission detail: click accept";

        if (TryGetPopupDecision(screenshot, out string popupText, out PopupDecisionMode popupMode, allowCached: false))
        {
            if (popupMode == PopupDecisionMode.StartOnly)
                return $"Commission popup: start-only ('{popupText}') -> click start commission";

            bool isRedDifficult = CommissionPopupChecks.DetectRedDifficult(screenshot, popupText, out double redRatio, out bool hasDifficultKeyword);
            bool shouldStart = CommissionAppraiseDifficultyPolicy.ShouldStartDifficultyBasedPopup(isRedDifficult);
            return shouldStart
                ? $"Commission popup: difficultKeyword={hasDifficultKeyword}, redRatio={redRatio:F3} -> click start commission"
                : $"Commission popup: difficultKeyword={hasDifficultKeyword}, redRatio={redRatio:F3} -> click skip battle";
        }

        int optionIndex = CommissionAppraiseDifficultyPolicy.ResolveListOptionIndex(RaceConfig.AppraiseDifficultyMode);
        return $"Commission list: mode={RaceConfig.AppraiseDifficultyMode}, click option #{optionIndex} -> accept -> popup decision";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        using var firstShot = ctx.CaptureScreen();
        if (firstShot != null && !firstShot.Empty() && CommissionScreenChecks.IsCommissionAcceptDetailScreen(firstShot))
        {
            Log.Log("Commission detail screen detected: click accept directly.");
            await ctx.ClickAtPercent(DetailAcceptBtnX, DetailAcceptBtnY);
            await ctx.Wait(1300);
            return;
        }

        if (firstShot != null && !firstShot.Empty() &&
            TryGetPopupDecision(firstShot, out string popupTextOnly, out PopupDecisionMode popupModeOnly, allowCached: false))
        {
            Log.Log($"Popup-only branch: mode={popupModeOnly}, text='{popupTextOnly}'");
            await HandlePopupDecisionLoopAsync(ctx, firstShot);
            return;
        }

        var listTarget = ResolveListOptionTarget();
        Log.Log($"Commission branch step1: mode={RaceConfig.AppraiseDifficultyMode}, clicking option #{listTarget.Index}...");
        await ctx.ClickAtPercent(listTarget.X, listTarget.Y);
        await ctx.Wait(700);

        Log.Log("Commission branch step2: clicking bottom accept...");
        await ctx.ClickAtPercent(AcceptBtnX, AcceptBtnY);
        await ctx.Wait(900);

        using var popupShot = ctx.CaptureScreen();
        if (popupShot == null || popupShot.Empty())
        {
            Log.Log("Popup capture empty, keep current flow");
            await ctx.Wait(600);
            return;
        }

        bool hasPopup = TryGetPopupDecision(popupShot, out string popupText, out PopupDecisionMode popupMode, allowCached: false);
        if (!hasPopup)
        {
            Log.Log("No commission popup detected after accept");
            await ctx.Wait(600);
            return;
        }

        if (popupMode == PopupDecisionMode.StartOnly)
        {
            Log.Log($"Popup detected: start-only, text='{popupText}'");
        }
        else
        {
            bool isRedDifficult = CommissionPopupChecks.DetectRedDifficult(popupShot, popupText, out double redRatio, out bool hasDifficultKeyword);
            Log.Log($"Popup detected: difficultKeyword={hasDifficultKeyword}, redRatio={redRatio:F3}");
        }

        await HandlePopupDecisionLoopAsync(ctx, popupShot);
    }

    public async Task ProbeAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log("Commission probe: capture empty, skip move.");
            return;
        }

        if (CommissionScreenChecks.IsCommissionAcceptDetailScreen(shot))
        {
            int detailPx = CommissionPopupButtons.ToClientPixelX(shot, DetailAcceptBtnX);
            int detailPy = CommissionPopupButtons.ToClientPixelY(shot, DetailAcceptBtnY);
            Log.Log($"Commission probe move: detail accept, x={DetailAcceptBtnX:F3}, y={DetailAcceptBtnY:F3}, move=({detailPx},{detailPy})");
            await MouseSimulator.MoveToClient(ctx.WindowHandle, detailPx, detailPy);
            await ctx.Wait(200);
            return;
        }

        if (TryGetPopupDecision(shot, out string popupText, out _, allowCached: false))
        {
            bool isRedDifficult = CommissionPopupChecks.DetectRedDifficult(shot, popupText, out _, out _);
            bool wantStart = CommissionAppraiseDifficultyPolicy.ShouldStartDifficultyBasedPopup(isRedDifficult);
            string buttonType = wantStart ? "start" : "skip";
            CommissionPopupButtons.ResolveTarget(shot, buttonType, out double x, out double y, out string reason, out string hitText, out int bestScore);

            int px = CommissionPopupButtons.ToClientPixelX(shot, x);
            int py = CommissionPopupButtons.ToClientPixelY(shot, y);
            Log.Log($"Commission probe move: button={buttonType}, x={x:F3}, y={y:F3}, reason='{reason}', score={bestScore}, text='{hitText}', move=({px},{py})");
            await MouseSimulator.MoveToClient(ctx.WindowHandle, px, py);
            await ctx.Wait(200);
            return;
        }

        var listTarget = ResolveListOptionTarget();
        int listPx = CommissionPopupButtons.ToClientPixelX(shot, listTarget.X);
        int listPy = CommissionPopupButtons.ToClientPixelY(shot, listTarget.Y);
        Log.Log($"Commission probe move: list select option{listTarget.Index}, x={listTarget.X:F3}, y={listTarget.Y:F3}, move=({listPx},{listPy})");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, listPx, listPy);
        await ctx.Wait(200);
    }

    /// <summary>
    /// 委托弹窗决策循环：弹窗仍在时继续点击（例如连续“跳过战斗”确认）
    /// </summary>
    private async Task HandlePopupDecisionLoopAsync(GameContext ctx, Mat initialShot)
    {
        Mat? current = initialShot;
        const int maxRounds = 3;
        bool rewardMarkerWritten = false;
        for (int round = 1; round <= maxRounds; round++)
        {
            if (current == null || current.Empty())
                break;

            bool allowCached = round == 1;
            if (!TryGetPopupDecision(current, out string popupText, out PopupDecisionMode popupMode, allowCached: allowCached))
            {
                Log.Log($"Popup decision loop: popup cleared at round {round}.");
                break;
            }

            if (popupMode == PopupDecisionMode.StartOnly)
            {
                Log.Log($"Popup decision r{round}: start-only popup, text='{popupText}'");
                await CommissionPopupButtons.ProbeAndClickAsync(ctx, current, "start");
            }
            else
            {
                bool isRedDifficult = CommissionPopupChecks.DetectRedDifficult(current, popupText, out double redRatio, out bool hasDifficultKeyword);
                bool shouldStart = CommissionAppraiseDifficultyPolicy.ShouldStartDifficultyBasedPopup(isRedDifficult);
                bool isBattleCommissionPopup = CommissionPopupChecks.IsBattleCommissionQuestionPopup(popupText);
                bool shouldMarkReward = CommissionAppraiseDifficultyPolicy.ShouldMarkRedCommissionCardReward(
                    isBattleCommissionPopup,
                    isRedDifficult);
                Log.Log($"Popup decision r{round}: difficultKeyword={hasDifficultKeyword}, redRatio={redRatio:F3}, text='{popupText}'");
                if (shouldMarkReward && !rewardMarkerWritten)
                {
                    Log.Log($"Commission branch: mark red commission card reward (battleCommission={isBattleCommissionPopup}, redDifficult={isRedDifficult})");
                    _cardRewardStateStore.MarkRedDifficultCommissionStarted();
                    rewardMarkerWritten = true;
                }

                if (shouldStart)
                {
                    Log.Log("Commission branch: red difficult => click start commission");
                    await CommissionPopupButtons.ProbeAndClickAsync(ctx, current, "start");
                }
                else
                {
                    Log.Log("Commission branch: not red difficult => click skip battle");
                    await CommissionPopupButtons.ProbeAndClickAsync(ctx, current, "skip");
                }
            }

            await ctx.Wait(900);
            if (!ReferenceEquals(current, initialShot))
                current.Dispose();
            current = ctx.CaptureScreen();
        }

        if (!ReferenceEquals(current, initialShot))
            current?.Dispose();
    }

    /// <summary>
    /// 弹窗决策入口：先实时检测，找不到再回退短 TTL 缓存
    /// </summary>
    private bool TryGetPopupDecision(
        Mat screenshot,
        out string popupText,
        out PopupDecisionMode popupMode,
        bool allowCached)
    {
        if (CommissionPopupChecks.DetectDecisionPopup(screenshot, out popupText))
        {
            popupMode = CommissionPopupChecks.ClassifyPopupDecisionMode(popupText);
            RememberPopupDetection(popupText, popupMode);
            return true;
        }

        if (allowCached && HasRecentPopupCache())
        {
            popupText = _lastPopupText;
            popupMode = _lastPopupMode;
            Log.Log($"Popup decision reused from cache: mode={popupMode}, text='{popupText}'");
            return true;
        }

        popupText = "";
        popupMode = PopupDecisionMode.None;
        return false;
    }

    private void RememberPopupDetection(string popupText, PopupDecisionMode popupMode)
    {
        _lastPopupText = popupText;
        _lastPopupMode = popupMode;
        _lastPopupUtc = DateTime.UtcNow;
    }

    private bool HasRecentPopupCache()
    {
        return !string.IsNullOrEmpty(_lastPopupText) &&
               _lastPopupMode != PopupDecisionMode.None &&
               (DateTime.UtcNow - _lastPopupUtc).TotalMilliseconds <= PopupCacheTtlMs;
    }

    private static (int Index, double X, double Y) ResolveListOptionTarget()
    {
        int optionIndex = CommissionAppraiseDifficultyPolicy.ResolveListOptionIndex(RaceConfig.AppraiseDifficultyMode);
        double y = optionIndex == 2 ? SecondOptionY : ThirdOptionY;
        return (optionIndex, ListOptionX, y);
    }

    private static readonly LogScope Log = new("Race:Commission");
}
