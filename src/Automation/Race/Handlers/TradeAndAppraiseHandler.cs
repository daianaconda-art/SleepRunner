using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers.Trade;
using SleepRunner.Input;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 评鉴战准备阶段的"委托/交易"分流：先进入交易，完成后返回再点委托
///
/// 重构后职责（路由/编排层）：
/// - CanHandle/Describe：阶段命中识别
/// - HandleAsync：三阶段流程 enter-trade → execute-trade → exit-and-click-appraise
/// - 具体 OCR/几何/状态/购买动作 全部委托给 Trade/ 子模块
/// </summary>
public class TradeAndAppraiseHandler : IRaceHandler
{
    public string Name => "交易与委托";
    public int Priority => 16;

    private readonly ITradeFlowExecutor _tradeExecutor;
    private readonly TradeStateStore _stateStore;
    private bool _tradeVisitedInCurrentStage;

    public TradeAndAppraiseHandler(
        ITradeFlowExecutor? tradeExecutor = null)
    {
        _tradeExecutor = tradeExecutor ?? new DefaultTradeFlowExecutor();
        _stateStore = new TradeStateStore();
        _tradeVisitedInCurrentStage = _stateStore.LoadVisited();
    }

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string titleText = TradeStageOcr.ReadStageTitleText(screenshot);
        string menuText = TradeStageOcr.ReadMenuText(screenshot);
        string combined = $"{titleText}|{menuText}";
        bool hasPrepTitle = TradeStageOcr.ContainsTradeStageTitleKeyword(titleText);
        bool hasTrade = TradeStageOcr.ContainsTradeKeyword(menuText) || TradeStageOcr.ContainsTradeStageHint(menuText);
        bool hasCommission = TradeStageOcr.ContainsCommissionKeyword(menuText);
        bool looksLikeMainMenu = TradeStageOcr.ContainsMainMenuKeyword(menuText);
        bool looksLikePrepDetail = TradeStageOcr.ContainsPrepDetailKeyword(menuText);
        // OCR 有时只读到委托/讨伐侧，分流页判定仍需接手；出击前详情页由 helper 排除。
        bool hit = TradeStageOcr.IsAppraiseTradeStageMenuText(titleText, menuText);
        if (hit)
            Log.Log($"FLOW Stage hit: appraise/trade two-choice menu ('{combined}')");
        else if (!string.IsNullOrEmpty(titleText) || !string.IsNullOrEmpty(menuText))
            Log.Log($"FLOW Stage miss: title='{titleText}', menu='{menuText}', prep={hasPrepTitle}, trade={hasTrade}, commission={hasCommission}, prepDetail={looksLikePrepDetail}, mainMenu={looksLikeMainMenu}");
        return hit;
    }

    public string DescribeDecision(FrameContext frame)
    {
        return _tradeVisitedInCurrentStage
            ? "Trade/Appraise stage: trade already visited -> click appraise"
            : "Trade/Appraise stage: enter trade first, then return and click appraise";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        using var before = ctx.CaptureScreen();
        double tradeClickY = before == null || before.Empty()
            ? TradeStageGeometry.TradeY
            : TradeStageGeometry.DetectOptionClickY(before, isTrade: true, fallbackY: TradeStageGeometry.TradeY);
        double appraiseClickY = before == null || before.Empty()
            ? TradeStageGeometry.CommissionY
            : TradeStageGeometry.DetectOptionClickY(before, isTrade: false, fallbackY: TradeStageGeometry.CommissionY);

        Log.Log($"FLOW Handle start: tradeVisited={_tradeVisitedInCurrentStage}, tradeClickY={tradeClickY:F3}, appraiseClickY={appraiseClickY:F3}");

        if (!_tradeVisitedInCurrentStage)
        {
            // 第一阶段：进入交易做购买
            Log.Log("FLOW Phase=enter-trade: trade not visited yet, try to enter trade branch.");
            bool enteredTrade = await TryEnterTradeBranchAsync(ctx, tradeClickY);
            if (!enteredTrade)
            {
                _tradeVisitedInCurrentStage = false;
                _stateStore.SaveVisited(false);
                Log.Log("FLOW Phase=enter-trade: FAILED to verify trade screen after retries, will retry on next loop.");
                return;
            }

            Log.Log("FLOW Phase=execute-trade: trade screen verified, run trade executor.");
            TradeExecutionResult execResult = await _tradeExecutor.ExecuteAsync(ctx);
            Log.Log($"FLOW Phase=execute-trade: executor finished, result={execResult}");

            if (!TradeExecutionResultPolicy.ShouldExitTrade(execResult))
            {
                _tradeVisitedInCurrentStage = true;
                _stateStore.SaveVisited(true);
                Log.Log("FLOW Phase=execute-trade: manual required on trade screen, stay here and wait for user handling.");
                return;
            }

            // 第二阶段：退出交易并在同轮内点入评鉴战
            Log.Log("FLOW Phase=exit-trade: try to esc back to two-choice menu and click appraise.");
            bool finishedToAppraise = await TryExitTradeAndClickCommissionAsync(ctx, appraiseClickY);
            if (finishedToAppraise)
            {
                _tradeVisitedInCurrentStage = false;
                _stateStore.SaveVisited(false);
                Log.Log("FLOW DONE: trade -> appraise completed in one pass.");
                return;
            }

            using var afterTradeClick = ctx.CaptureScreen();
            bool stillStageMenu = afterTradeClick != null && !afterTradeClick.Empty() && CanHandle(new FrameContext(afterTradeClick));
            _tradeVisitedInCurrentStage = true;
            _stateStore.SaveVisited(true);
            if (stillStageMenu)
                Log.Log("FLOW Phase=exit-trade: still on two-choice menu, keep tradeVisited=true; next loop will only click appraise.");
            else
                Log.Log("FLOW Phase=exit-trade: left two-choice menu but appraise not yet confirmed; keep tradeVisited=true to avoid re-entering trade.");
            return;
        }

        // 第三阶段（独立 loop 进入）：交易已完成，仅需点入评鉴战
        Log.Log("FLOW Phase=click-appraise: trade already visited, click appraise branch only.");
        bool appraiseClicked = await TryClickCommissionBranchAsync(ctx, appraiseClickY);
        if (!appraiseClicked)
        {
            Log.Log("FLOW Phase=click-appraise: click verification FAILED, keep tradeVisited=true for retry.");
            _tradeVisitedInCurrentStage = true;
            _stateStore.SaveVisited(true);
            return;
        }

        _tradeVisitedInCurrentStage = false;
        _stateStore.SaveVisited(false);
        Log.Log("FLOW DONE: appraise click confirmed, state reset.");
    }

    /// <summary>
    /// 进入交易分支并校验是否成功进入，避免误点到委托
    /// </summary>
    private async Task<bool> TryEnterTradeBranchAsync(GameContext ctx, double preferredTradeY)
    {
        using var before = ctx.CaptureScreen();
        var probes = TradeStageGeometry.BuildOptionProbes(before, isTrade: true, preferredY: preferredTradeY, fallbackY: TradeStageGeometry.TradeY);
        if (probes.Count == 0)
            probes.Add(new TradeStageGeometry.OptionProbe(TradeStageGeometry.TradeY, -999, ""));

        for (int i = 0; i < probes.Count; i++)
        {
            var probe = probes[i];
            double y = probe.Y;
            Log.Log($"Step1: try enter trade branch (attempt={i + 1}, y={y:F3}, score={probe.Score}, text='{probe.Text}').");
            bool verifiedBeforeClick = await TradeStageGeometry.VerifyTargetBeforeClickAsync(ctx, TradeStageGeometry.TradeX, y, expectTrade: true);
            if (!verifiedBeforeClick)
            {
                Log.Log($"Step1: skip click, pre-click verify failed at y={y:F3}.");
                continue;
            }
            await ctx.ClickAtPercent(TradeStageGeometry.TradeX, y);
            await ctx.Wait(1000);

            using var shot = ctx.CaptureScreen();
            if (shot == null || shot.Empty())
            {
                Log.Log("Step1: capture empty after trade click, retry.");
                continue;
            }

            bool tradeScreen = TradeStageOcr.IsTradeScreen(shot);
            bool stillStageMenu = CanHandle(new FrameContext(shot));
            bool commissionLike = TradeStageOcr.IsCommissionLike(shot);
            string titleAfter = TradeStageOcr.ReadStageTitleText(shot);
            string menuAfter = TradeStageOcr.ReadMenuText(shot);
            Log.Log($"Step1 post-click snapshot: tradeScreen={tradeScreen}, stageMenu={stillStageMenu}, commissionLike={commissionLike}, title='{titleAfter}', menu='{menuAfter}'");

            if (tradeScreen)
            {
                Log.Log($"Step1: trade screen verified on attempt {i + 1}.");
                return true;
            }

            if (stillStageMenu)
            {
                Log.Log($"Step1: still in trade/commission stage menu after attempt {i + 1}, retry.");
                continue;
            }

            if (commissionLike)
            {
                Log.Log("Step1: likely entered commission by mistake, send ESC and retry trade.");
                await KeyboardSimulator.SendKey(ctx.WindowHandle, KeyboardSimulator.VK_ESCAPE);
                await ctx.Wait(800);
                continue;
            }

            Log.Log("Step1: state uncertain after trade click, retry.");
        }

        return false;
    }

    /// <summary>
    /// 点击委托分支前先做文本验证，降低误点到交易的概率
    /// </summary>
    private async Task<bool> TryClickCommissionBranchAsync(GameContext ctx, double preferredCommissionY)
    {
        using var shot = ctx.CaptureScreen();
        var probes = TradeStageGeometry.BuildOptionProbes(shot, isTrade: false, preferredY: preferredCommissionY, fallbackY: TradeStageGeometry.CommissionY);
        if (probes.Count == 0)
            probes.Add(new TradeStageGeometry.OptionProbe(TradeStageGeometry.CommissionY, -999, ""));

        for (int i = 0; i < probes.Count; i++)
        {
            var probe = probes[i];
            Log.Log($"Step3: click commission branch (attempt={i + 1}, y={probe.Y:F3}, score={probe.Score}, text='{probe.Text}').");
            bool verifiedBeforeClick = await TradeStageGeometry.VerifyTargetBeforeClickAsync(ctx, TradeStageGeometry.CommissionX, probe.Y, expectTrade: false);
            if (!verifiedBeforeClick)
            {
                Log.Log($"Step3: skip click, pre-click verify failed at y={probe.Y:F3}.");
                continue;
            }
            await ctx.ClickAtPercent(TradeStageGeometry.CommissionX, probe.Y);
            await ctx.Wait(1200);

            using var after = ctx.CaptureScreen();
            if (after == null || after.Empty())
                continue;
            bool stillStageMenu = CanHandle(new FrameContext(after));
            bool tradeScreen = TradeStageOcr.IsTradeScreen(after);
            bool commissionLike = TradeStageOcr.IsCommissionLike(after);
            string titleAfter = TradeStageOcr.ReadStageTitleText(after);
            string menuAfter = TradeStageOcr.ReadMenuText(after);
            Log.Log($"Step3 post-click snapshot: tradeScreen={tradeScreen}, stageMenu={stillStageMenu}, commissionLike={commissionLike}, title='{titleAfter}', menu='{menuAfter}'");

            if (stillStageMenu)
            {
                Log.Log("Step3: still in stage menu after commission click, retry.");
                continue;
            }
            return true;
        }

        var bestProbe = probes[0];
        if (bestProbe.Score >= 0)
        {
            Log.Log($"Step3: blind-click fallback at y={bestProbe.Y:F3}, score={bestProbe.Score}, text='{bestProbe.Text}'.");
            await ctx.ClickAtPercent(TradeStageGeometry.CommissionX, bestProbe.Y);
            await ctx.Wait(1200);

            using var after = ctx.CaptureScreen();
            if (after == null || after.Empty())
                return false;

            bool stillStageMenu = CanHandle(new FrameContext(after));
            bool tradeScreen = TradeStageOcr.IsTradeScreen(after);
            bool commissionLike = TradeStageOcr.IsCommissionLike(after);
            string titleAfter = TradeStageOcr.ReadStageTitleText(after);
            string menuAfter = TradeStageOcr.ReadMenuText(after);
            Log.Log($"Step3 blind fallback snapshot: tradeScreen={tradeScreen}, stageMenu={stillStageMenu}, commissionLike={commissionLike}, title='{titleAfter}', menu='{menuAfter}'");

            if (stillStageMenu)
                return false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 若仍在交易详情页则先 Esc 返回，再尝试识别二选一菜单并点击评鉴战
    /// </summary>
    private async Task<bool> TryExitTradeAndClickCommissionAsync(GameContext ctx, double appraiseClickY)
    {
        const int maxRounds = 4;
        for (int round = 1; round <= maxRounds; round++)
        {
            using var shot = ctx.CaptureScreen();
            if (shot == null || shot.Empty())
            {
                Log.Log($"FLOW exit-trade r{round}: capture empty, abort.");
                return false;
            }

            bool stageMenu = CanHandle(new FrameContext(shot));
            bool tradeScreen = TradeStageOcr.IsTradeScreen(shot);
            bool commissionLike = TradeStageOcr.IsCommissionLike(shot);
            string title = TradeStageOcr.ReadStageTitleText(shot);
            string menu = TradeStageOcr.ReadMenuText(shot);
            Log.Log($"FLOW exit-trade r{round}: tradeScreen={tradeScreen}, stageMenu={stageMenu}, commissionLike={commissionLike}, title='{title}', menu='{menu}'");

            if (stageMenu)
            {
                Log.Log($"FLOW exit-trade r{round}: two-choice menu detected, click appraise (y={appraiseClickY:F3}).");
                bool clicked = await TryClickCommissionBranchAsync(ctx, appraiseClickY);
                Log.Log($"FLOW exit-trade r{round}: appraise click result={clicked}.");
                return clicked;
            }

            if (commissionLike)
            {
                // 已离开二选一菜单且页面带讨伐/受理/委托/评鉴战字样，视为已进入评鉴战相关页
                Log.Log($"FLOW exit-trade r{round}: already on commission/appraise-like screen, treat as success.");
                return true;
            }

            bool appraiseFlowStillActive = tradeScreen ||
                                           TradeStageOcr.ContainsTradeStageTitleKeyword(title) ||
                                           TradeStageOcr.ContainsAppraiseKeyword(title) ||
                                           TradeStageOcr.LooksLikeTradeListText(menu);
            if (!appraiseFlowStillActive && round > 1)
            {
                if (round < maxRounds)
                {
                    Log.Log($"FLOW exit-trade r{round}: context unclear, wait 500ms and retry.");
                    await ctx.Wait(500);
                    continue;
                }

                Log.Log($"FLOW exit-trade r{round}: appraise/trade context lost, give up exit retry.");
                return false;
            }

            Log.Log($"FLOW exit-trade r{round}: send ESC to leave trade screen.");
            await KeyboardSimulator.SendKey(ctx.WindowHandle, KeyboardSimulator.VK_ESCAPE);
            await ctx.Wait(1000);
        }

        Log.Log($"FLOW exit-trade: reached maxRounds={maxRounds} without confirming appraise click.");
        return false;
    }
    private static readonly LogScope Log = new("Race:Trade");
}
