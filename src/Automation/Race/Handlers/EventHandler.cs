using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers.Events;
using SleepRunner.Input;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 事件对话选择编排层：OCR 检测事件页 → 匹配事件库 → 自动选择或等待手动
///
/// 静态识别/区域/几何/数据加载已下沉到 Events/ 子模块（EventOcrRegions、
/// EventScreenChecks、EventOptionGeometry、EventCatalog），EventHandler 自身
/// 只负责接口实现与 HandleAsync/ProbeAsync/DebugHoverScanAsync 的"调度+点击"逻辑。
/// </summary>
public class EventHandler : IRaceHandler
{
    public string Name => "事件选择";
    public int Priority => 5;

    private readonly EventCatalog _catalog = new();

    public EventHandler()
    {
        Log.Log($"Initialized; events profile='{Policy.RaceProfileManager.CurrentEventsProfile}'");
    }

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        // 早退：战斗失败"重新挑战通知"弹窗
        // 弹窗正文"是否要重新挑战，再次尝试战斗？"会被后续 IsEventOptionHint("是否")
        // 误判为事件选项 → 抢走点取消 → BattleDefeat 又点重新挑战 → 死循环卡住
        if (EventScreenChecks.IsRetryDialogContext(screenshot))
        {
            Log.Log("CanHandle yield: retry-dialog detected, defer to BattleDefeatHandler.");
            return false;
        }

        string marker = EventOcrRegions.ReadJourneyMarkerText(screenshot);
        if (EventScreenChecks.IsJourneyEventMarker(marker))
        {
            Log.Log($"CanHandle fallback: journey marker hit ('{marker}')");
            return true;
        }

        bool markerHint = EventScreenChecks.ContainsJourneyHint(marker) && !EventScreenChecks.IsJourneyNoise(marker);
        string optionHint = EventOcrRegions.ReadEventOptionHintText(screenshot);
        if (EventScreenChecks.IsTrainingContext(marker, optionHint))
        {
            Log.Log($"CanHandle fallback ignored: training context (marker='{marker}', option='{optionHint}')");
            return false;
        }
        string restConfirm = EventOcrRegions.ReadRestConfirmText(screenshot);
        if (EventScreenChecks.IsRestDecisionContext(optionHint, restConfirm))
        {
            Log.Log($"CanHandle fallback ignored: rest context (confirm='{restConfirm}', option='{optionHint}')");
            return false;
        }
        if (EventScreenChecks.IsMainMenuLikeText(optionHint))
        {
            Log.Log($"CanHandle fallback ignored: main-menu-like option text ('{optionHint}')");
            return false;
        }
        if (EventScreenChecks.IsAppraiseGoalListContext(optionHint))
        {
            Log.Log($"CanHandle fallback ignored: appraise goal list ('{EventScreenChecks.ClipPreview(optionHint)}')");
            return false;
        }
        bool optionHit = EventScreenChecks.IsEventOptionHint(optionHint);
        // 强信号短路：≥2 个主菜单关键词且不是事件选项时按主菜单处理
        // 必须先排除事件选项文本，否则事件正文里出现"训练/休息/委托"会被错误识别
        if (!optionHit)
        {
            int menuKeywordCount = EventScreenChecks.CountMainMenuKeywords(optionHint);
            if (menuKeywordCount >= 2)
            {
                Log.Log($"CanHandle fallback ignored: strong main-menu signal (kw={menuKeywordCount}) ('{EventScreenChecks.ClipPreview(optionHint)}')");
                return false;
            }
        }
        if (markerHint && optionHit)
        {
            Log.Log($"CanHandle fallback: marker+option hit (marker='{marker}', option='{optionHint}')");
            return true;
        }

        // 强事件指纹：≥2 个 +号选项即视为事件二选一对话框，避免被 TradePurchase 等误抢
        if (optionHint.Count(c => c == '+') >= 2)
        {
            Log.Log($"CanHandle fallback: strong plus-bullet event hit ('{EventScreenChecks.ClipPreview(optionHint)}')");
            return true;
        }

        string platformHint = EventOcrRegions.ReadTrainPlatformHintText(screenshot);
        if (EventScreenChecks.IsTrainPlatformSingleOptionScreen(platformHint))
        {
            Log.Log($"CanHandle fallback: train-platform single-option hit ('{platformHint}')");
            return true;
        }

        if (!string.IsNullOrEmpty(marker) || !string.IsNullOrEmpty(optionHint))
            Log.Log($"CanHandle fallback miss: marker='{marker}', option='{optionHint}'");
        return false;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string platformHint = EventOcrRegions.ReadTrainPlatformHintText(screenshot);
        if (EventScreenChecks.IsTrainPlatformSingleOptionScreen(platformHint))
            return $"Train-platform single option: hit '{platformHint}' -> click option 1/1 at ({EventOptionGeometry.OptionClickX:F2},{EventOptionGeometry.BottomOptionY:F3})";

        _catalog.EnsureLoaded();
        if (!_catalog.IsReady)
            return "Event: decision data not loaded -> wait manual";

        string title = EventOcrRegions.ReadEventTitleText(screenshot);
        string options = EventOcrRegions.ReadEventOptionsText(screenshot);
        string story = EventOcrRegions.ReadEventStoryText(screenshot);
        if (EventScreenChecks.IsDefense175Decision(options))
        {
            return $"Event: defense-175 rule hit (title='{title}', options='{options}') -> try option 2 first, fallback option 3 if no screen change in 2s";
        }

        var matchedEvent = _catalog.FindMatchingEvent(title, options, story);
        if (matchedEvent == null)
            return $"Event: unknown (title='{title}', options='{options}') -> wait manual";

        int? optIdx = matchedEvent.GetRecommendedOption();
        if (matchedEvent.Status == "pending" || optIdx == null)
            return $"Event: [{matchedEvent.Id}] {matchedEvent.EventName} status={matchedEvent.Status} -> wait manual";

        int total = Math.Max(1, matchedEvent.Options.Count);
        int? fallbackIdx = GetValidFallbackOption(matchedEvent, optIdx.Value, total);
        double clickY = EventOptionGeometry.ResolveOptionClickY(screenshot, matchedEvent, optIdx.Value, total);
        if (fallbackIdx != null)
        {
            return $"Event: [{matchedEvent.Id}] {matchedEvent.EventName} -> try option {optIdx}/{total} at ({EventOptionGeometry.OptionClickX:F2},{clickY:F3}); if no screen change retry once then fallback option {fallbackIdx}/{total}";
        }
        return $"Event: [{matchedEvent.Id}] {matchedEvent.EventName} -> option {optIdx}/{total} at ({EventOptionGeometry.OptionClickX:F2},{clickY:F3})";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty()) return;

        string platformHint = EventOcrRegions.ReadTrainPlatformHintText(shot);
        if (EventScreenChecks.IsTrainPlatformSingleOptionScreen(platformHint))
        {
            Log.Log($"Train-platform single-option detected ('{platformHint}'), auto-select option 1.");
            await ctx.ClickAtPercent(EventOptionGeometry.OptionClickX, EventOptionGeometry.BottomOptionY);
            await ctx.Wait(1500);
            return;
        }

        _catalog.EnsureLoaded();
        if (!_catalog.IsReady)
        {
            Log.Log("Decision data not loaded or empty, waiting for manual input...");
            await ctx.Wait(30000);
            return;
        }

        string title = EventOcrRegions.ReadEventTitleText(shot);
        Log.Log($"Title OCR: '{title}'");

        string options = EventOcrRegions.ReadEventOptionsText(shot);
        Log.Log($"Options OCR: '{options}'");
        string story = EventOcrRegions.ReadEventStoryText(shot);
        Log.Log($"Story OCR: '{story}'");

        // 特判：防御/保护达到 175 时优先选 2；2 秒内无变化则补点 3
        if (EventScreenChecks.IsDefense175Decision(options))
        {
            Log.Log($"Defense-175 rule hit (title='{title}'), try option 2 first.");
            await ClickOptionWithFallbackAsync(ctx, shot, primaryOption: 2, fallbackOption: 3, totalOptions: 3);
            return;
        }

        var matchedEvent = _catalog.FindMatchingEvent(title, options, story);
        if (matchedEvent == null)
        {
            Log.Log($"UNKNOWN EVENT: title='{title}', options='{options}'");
            Log.Log("Pausing 30s for manual input...");
            await ctx.Wait(30000);
            return;
        }

        Log.Log($"Matched: [{matchedEvent.Id}] {matchedEvent.EventName} (status={matchedEvent.Status})");

        int? optIdx = matchedEvent.GetRecommendedOption();

        if (matchedEvent.Status == "pending" || optIdx == null)
        {
            Log.Log($"PENDING: '{matchedEvent.EventName}' - {matchedEvent.Note}");
            Log.Log("Waiting for manual selection...");
            await ctx.Wait(30000);
            return;
        }

        int total = matchedEvent.Options.Count;
        int? fallbackIdx = GetValidFallbackOption(matchedEvent, optIdx.Value, total);
        double clickY = EventOptionGeometry.ResolveOptionClickY(shot, matchedEvent, optIdx.Value, total);

        if (fallbackIdx != null)
            Log.Log($"Auto-selecting option {optIdx}/{total} at ({EventOptionGeometry.OptionClickX:F2}, {clickY:F3}) with fallback option {fallbackIdx}/{total} on no-change.");
        else
            Log.Log($"Auto-selecting option {optIdx}/{total} at ({EventOptionGeometry.OptionClickX:F2}, {clickY:F3})");
        Log.Log($"Reason: {matchedEvent.Note}");

        if (fallbackIdx != null)
        {
            await ClickOptionWithFallbackAsync(
                ctx,
                shot,
                primaryOption: optIdx.Value,
                fallbackOption: fallbackIdx.Value,
                totalOptions: total,
                matchedEvent: matchedEvent,
                expectedEventId: matchedEvent.Id,
                logTag: $"Event[{matchedEvent.Id}]",
                primaryAttempts: 2,
                waitAfterPrimaryMs: 1200,
                waitAfterFallbackMs: 1500);
            return;
        }

        await ClickOptionWithRetryOnNoChangeAsync(
            ctx,
            shot,
            optIdx.Value,
            total,
            matchedEvent,
            logTag: $"Event[{matchedEvent.Id}]");
    }

    public async Task ProbeAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
            return;

        string platformHint = EventOcrRegions.ReadTrainPlatformHintText(shot);
        if (EventScreenChecks.IsTrainPlatformSingleOptionScreen(platformHint))
        {
            int x = (int)(shot.Width * EventOptionGeometry.OptionClickX);
            int y = (int)(shot.Height * EventOptionGeometry.BottomOptionY);
            Log.Log($"Event probe: train-platform single option move=({EventOptionGeometry.OptionClickX:F3},{EventOptionGeometry.BottomOptionY:F3}) => ({x},{y})");
            await MouseSimulator.MoveToClient(ctx.WindowHandle, x, y);
            await ctx.Wait(200);
            return;
        }

        _catalog.EnsureLoaded();
        if (!_catalog.IsReady)
            return;

        string title = EventOcrRegions.ReadEventTitleText(shot);
        string options = EventOcrRegions.ReadEventOptionsText(shot);
        string story = EventOcrRegions.ReadEventStoryText(shot);

        int optionIndex;
        int totalOptions;

        if (EventScreenChecks.IsDefense175Decision(options))
        {
            optionIndex = 2;
            totalOptions = 3;
        }
        else
        {
            var matchedEvent = _catalog.FindMatchingEvent(title, options, story);
            if (matchedEvent == null)
                return;

            int? optIdx = matchedEvent.GetRecommendedOption();
            if (matchedEvent.Status == "pending" || optIdx == null)
                return;

            optionIndex = optIdx.Value;
            totalOptions = Math.Max(1, matchedEvent.Options.Count);
        }

        double clickY = EventOptionGeometry.ResolveOptionClickY(
            shot,
            matchedEvent: null,
            optionIndex,
            totalOptions);
        int px = (int)(shot.Width * EventOptionGeometry.OptionClickX);
        int py = (int)(shot.Height * clickY);
        Log.Log($"Event probe: option={optionIndex}/{totalOptions}, move=({EventOptionGeometry.OptionClickX:F3},{clickY:F3}) => ({px},{py})");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, px, py);
        await ctx.Wait(200);
    }

    public async Task DebugHoverScanAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log("Event hover scan: capture empty, skip.");
            return;
        }

        if (!CanHandle(new FrameContext(shot)))
        {
            Log.Log("Event hover scan: current frame is not an event screen.");
            return;
        }

        string platformHint = EventOcrRegions.ReadTrainPlatformHintText(shot);
        if (EventScreenChecks.IsTrainPlatformSingleOptionScreen(platformHint))
        {
            Log.Log($"Event hover scan: train-platform single option hit '{platformHint}', skip list scan.");
            return;
        }

        _catalog.EnsureLoaded();
        if (!_catalog.IsReady)
        {
            Log.Log("Event hover scan: decision data not loaded.");
            return;
        }

        string title = EventOcrRegions.ReadEventTitleText(shot);
        string options = EventOcrRegions.ReadEventOptionsText(shot);
        string story = EventOcrRegions.ReadEventStoryText(shot);
        var matchedEvent = _catalog.FindMatchingEvent(title, options, story);
        if (matchedEvent == null)
        {
            Log.Log($"Event hover scan: unknown event, title='{title}', options='{options}'.");
            return;
        }

        int? optIdx = matchedEvent.GetRecommendedOption();
        if (matchedEvent.Status == "pending" || optIdx == null)
        {
            Log.Log($"Event hover scan: matched [{matchedEvent.Id}] but decision is manual.");
            return;
        }

        int optionIndex = optIdx.Value;
        int totalOptions = Math.Max(1, matchedEvent.Options.Count);
        double fallbackY = EventOptionGeometry.CalcOptionClickY(optionIndex, totalOptions);
        double resolvedY = EventOptionGeometry.ResolveOptionClickY(shot, matchedEvent, optionIndex, totalOptions);

        var yCandidates = EventOptionGeometry.BuildOptionRowYCandidates(totalOptions, optionIndex, fallbackY)
            .Append(resolvedY)
            .Append(fallbackY)
            .Select(y => Math.Round(Math.Clamp(y, 0.42, 0.88), 3))
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        string scanDir = Path.Combine(PathHelper.ScreenshotsDir, $"event_hover_scan_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(scanDir);

        int neutralX = (int)(shot.Width * 0.40);
        int neutralY = (int)(shot.Height * 0.40);
        await MouseSimulator.MoveToClient(ctx.WindowHandle, neutralX, neutralY);
        await ctx.Wait(180);

        using var baseline = ctx.CaptureScreen();
        if (baseline == null || baseline.Empty())
        {
            Log.Log("Event hover scan: baseline capture empty.");
            return;
        }

        string baselinePath = Path.Combine(scanDir, "baseline.png");
        Cv2.ImWrite(baselinePath, baseline);
        Log.Log(
            $"Event hover scan: event=[{matchedEvent.Id}] option={optionIndex}/{totalOptions}, " +
            $"resolvedY={resolvedY:F3}, fallbackY={fallbackY:F3}, baseline='{baselinePath}', " +
            $"candidates=[{string.Join(",", yCandidates.Select(y => y.ToString("F3")))}]");

        var results = new List<(double Y, int Px, int Py, double LocalRatio, double OptionsRatio, string Path)>();
        foreach (double y in yCandidates)
        {
            int px = (int)(baseline.Width * EventOptionGeometry.OptionClickX);
            int py = (int)(baseline.Height * y);
            await MouseSimulator.MoveToClient(ctx.WindowHandle, px, py);
            await ctx.Wait(160);

            using var afterMove = ctx.CaptureScreen();
            if (afterMove == null || afterMove.Empty())
            {
                Log.Log($"Event hover scan: capture empty at y={y:F3}.");
                continue;
            }

            double localRatio = EventOptionGeometry.MeasureHoverDiffRatio(baseline, afterMove, 0.64, Math.Clamp(y - 0.06, 0.38, 0.86), 0.34, 0.12);
            double optionsRatio = EventOptionGeometry.MeasureHoverDiffRatio(baseline, afterMove,
                EventOcrRegions.OptionsRegionX, EventOcrRegions.OptionsRegionY,
                EventOcrRegions.OptionsRegionW, EventOcrRegions.OptionsRegionH);
            string path = Path.Combine(scanDir, $"hover_y_{y:F3}_x_{EventOptionGeometry.OptionClickX:F3}.png".Replace(',', '_'));
            Cv2.ImWrite(path, afterMove);

            Log.Log(
                $"Event hover scan: y={y:F3}, px=({px},{py}), localDiff={localRatio:F4}, " +
                $"optionsDiff={optionsRatio:F4}, saved='{path}'");

            results.Add((y, px, py, localRatio, optionsRatio, path));
        }

        var best = results
            .OrderByDescending(r => r.LocalRatio)
            .ThenByDescending(r => r.OptionsRatio)
            .FirstOrDefault();

        if (results.Count > 0)
        {
            Log.Log(
                $"Event hover scan: bestY={best.Y:F3}, px=({best.Px},{best.Py}), " +
                $"localDiff={best.LocalRatio:F4}, optionsDiff={best.OptionsRatio:F4}, bestShot='{best.Path}'");
        }

        await MouseSimulator.MoveToClient(ctx.WindowHandle, neutralX, neutralY);
        await ctx.Wait(120);
    }

    /// <summary>
    /// 先点主选项观察画面变化，无变化则补点兜底选项；可设主选项重试次数
    /// </summary>
    private async Task ClickOptionWithFallbackAsync(
        GameContext ctx,
        Mat beforeShot,
        int primaryOption,
        int fallbackOption,
        int totalOptions,
        RaceEvent? matchedEvent = null,
        string? expectedEventId = null,
        string logTag = "Event fallback",
        int primaryAttempts = 1,
        int waitAfterPrimaryMs = 2000,
        int waitAfterFallbackMs = 1500)
    {
        string beforeTitle = EventOcrRegions.ReadEventTitleText(beforeShot);
        string beforeOptions = EventOcrRegions.ReadEventOptionsText(beforeShot);
        string beforeMarker = EventOcrRegions.ReadJourneyMarkerText(beforeShot);

        double primaryY = matchedEvent != null
            ? EventOptionGeometry.ResolveOptionClickY(beforeShot, matchedEvent, primaryOption, totalOptions)
            : EventOptionGeometry.CalcOptionClickY(primaryOption, totalOptions);
        for (int attempt = 1; attempt <= Math.Max(1, primaryAttempts); attempt++)
        {
            Log.Log(
                $"{logTag}: click primary option {primaryOption}/{totalOptions} target pct=({EventOptionGeometry.OptionClickX:F3},{primaryY:F3}) " +
                $"px={EventOptionGeometry.FormatPoint(beforeShot, EventOptionGeometry.OptionClickX, primaryY)} shot={beforeShot.Width}x{beforeShot.Height} " +
                $"(attempt {attempt}/{Math.Max(1, primaryAttempts)}).");
            await ctx.ClickAtPercent(EventOptionGeometry.OptionClickX, primaryY);
            await ctx.Wait(waitAfterPrimaryMs);

            using var afterPrimary = ctx.CaptureScreen();
            if (afterPrimary == null || afterPrimary.Empty())
            {
                Log.Log($"{logTag}: post-click capture empty, stop fallback chain.");
                return;
            }

            bool changed = IsMeaningfulEventScreenChange(beforeShot, afterPrimary, beforeTitle, beforeOptions, beforeMarker, expectedEventId);
            if (changed)
            {
                Log.Log($"{logTag}: screen changed after primary option, keep result.");
                await ctx.Wait(800);
                return;
            }

            if (attempt < Math.Max(1, primaryAttempts))
                Log.Log($"{logTag}: no screen change after primary attempt {attempt}, retry primary option.");
        }

        double fallbackY = matchedEvent != null
            ? EventOptionGeometry.ResolveOptionClickY(beforeShot, matchedEvent, fallbackOption, totalOptions)
            : EventOptionGeometry.CalcOptionClickY(fallbackOption, totalOptions);
        Log.Log(
            $"{logTag}: no screen change after primary attempts, fallback click option {fallbackOption}/{totalOptions} " +
            $"target pct=({EventOptionGeometry.OptionClickX:F3},{fallbackY:F3}) px={EventOptionGeometry.FormatPoint(beforeShot, EventOptionGeometry.OptionClickX, fallbackY)} " +
            $"shot={beforeShot.Width}x{beforeShot.Height}.");
        await ctx.ClickAtPercent(EventOptionGeometry.OptionClickX, fallbackY);
        await ctx.Wait(waitAfterFallbackMs);

        using var afterFallback = ctx.CaptureScreen();
        if (afterFallback == null || afterFallback.Empty())
            return;

        bool changedAfterFallback = IsMeaningfulEventScreenChange(beforeShot, afterFallback, beforeTitle, beforeOptions, beforeMarker, expectedEventId);
        if (changedAfterFallback)
        {
            Log.Log($"{logTag}: screen changed after fallback option, keep result.");
            return;
        }

        Log.Log($"{logTag}: fallback option also kept same event text, stop retry loop and return to outer scheduler.");
    }

    /// <summary>
    /// 对无显式 fallback 的普通事件做一次点击后校验：首点无变化则按重试 Y 序列扫一遍
    /// </summary>
    private async Task ClickOptionWithRetryOnNoChangeAsync(
        GameContext ctx,
        Mat beforeShot,
        int optionIndex,
        int totalOptions,
        RaceEvent matchedEvent,
        string logTag)
    {
        string beforeTitle = EventOcrRegions.ReadEventTitleText(beforeShot);
        string beforeOptions = EventOcrRegions.ReadEventOptionsText(beforeShot);
        string beforeMarker = EventOcrRegions.ReadJourneyMarkerText(beforeShot);

        double primaryY = EventOptionGeometry.ResolveOptionClickY(beforeShot, matchedEvent, optionIndex, totalOptions);
        Log.Log(
            $"{logTag}: click option {optionIndex}/{totalOptions} target pct=({EventOptionGeometry.OptionClickX:F3},{primaryY:F3}) " +
            $"px={EventOptionGeometry.FormatPoint(beforeShot, EventOptionGeometry.OptionClickX, primaryY)} shot={beforeShot.Width}x{beforeShot.Height}.");
        await ctx.ClickAtPercent(EventOptionGeometry.OptionClickX, primaryY);
        await ctx.Wait(1500);

        using var afterPrimary = ctx.CaptureScreen();
        if (afterPrimary == null || afterPrimary.Empty())
        {
            Log.Log($"{logTag}: post-click capture empty, stop retry chain.");
            return;
        }

        bool changed = IsMeaningfulEventScreenChange(beforeShot, afterPrimary, beforeTitle, beforeOptions, beforeMarker, matchedEvent.Id);
        if (changed)
        {
            Log.Log($"{logTag}: screen changed after primary option, keep result.");
            return;
        }

        var retryYs = EventOptionGeometry.BuildRetrySweepYs(optionIndex, totalOptions, primaryY);
        if (retryYs.Count == 0)
        {
            Log.Log($"{logTag}: no screen change after primary option, no alternate Y left for retry sweep.");
            return;
        }

        foreach (double retryY in retryYs)
        {
            double retryDelta = Math.Abs(retryY - primaryY);
            Log.Log(
                $"{logTag}: no screen change after primary option, retry same option at anchor Y={retryY:F3} " +
                $"(delta={retryDelta:F3}, px={EventOptionGeometry.FormatPoint(beforeShot, EventOptionGeometry.OptionClickX, retryY)}, shot={beforeShot.Width}x{beforeShot.Height}).");
            await ctx.ClickAtPercent(EventOptionGeometry.OptionClickX, retryY);
            await ctx.Wait(1500);

            using var afterRetry = ctx.CaptureScreen();
            if (afterRetry == null || afterRetry.Empty())
                return;

            bool changedAfterRetry = IsMeaningfulEventScreenChange(beforeShot, afterRetry, beforeTitle, beforeOptions, beforeMarker, matchedEvent.Id);
            if (changedAfterRetry)
            {
                Log.Log($"{logTag}: screen changed after retry sweep Y={retryY:F3}, keep result.");
                return;
            }
        }

        Log.Log($"{logTag}: retry sweep also kept same event text, return to outer scheduler.");
    }

    private static int? GetValidFallbackOption(RaceEvent matchedEvent, int primaryOption, int totalOptions)
    {
        if (matchedEvent.FallbackOption == null)
            return null;

        int fallbackOption = matchedEvent.FallbackOption.Value;
        if (fallbackOption < 1 || fallbackOption > totalOptions || fallbackOption == primaryOption)
            return null;

        return fallbackOption;
    }

    /// <summary>
    /// 通过像素差异比判断画面是否发生变化（防御-175 等帧级判定也走这个）
    /// </summary>
    private static bool IsScreenChanged(Mat before, Mat after)
    {
        if (before.Empty() || after.Empty())
            return true;

        using var beforeGray = new Mat();
        using var afterGray = new Mat();
        Cv2.CvtColor(before, beforeGray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(after, afterGray, ColorConversionCodes.BGR2GRAY);

        if (beforeGray.Size() != afterGray.Size())
            Cv2.Resize(afterGray, afterGray, beforeGray.Size());

        using var diff = new Mat();
        Cv2.Absdiff(beforeGray, afterGray, diff);
        Cv2.Threshold(diff, diff, 18, 255, ThresholdTypes.Binary);

        double changedPixels = Cv2.CountNonZero(diff);
        double total = diff.Rows * diff.Cols;
        if (total <= 0) return true;

        double ratio = changedPixels / total;
        Log.Log($"Defense-175: frame diff ratio={ratio:F4}");
        return ratio > 0.010;
    }

    /// <summary>
    /// 判断"事件页是否真的换页了"：先比文本语义，再退化到像素差
    /// </summary>
    private bool IsMeaningfulEventScreenChange(
        Mat before,
        Mat after,
        string beforeTitle,
        string beforeOptions,
        string beforeMarker,
        string? expectedEventId)
    {
        string afterTitle = EventOcrRegions.ReadEventTitleText(after);
        string afterOptions = EventOcrRegions.ReadEventOptionsText(after);
        string afterMarker = EventOcrRegions.ReadJourneyMarkerText(after);

        if (!string.IsNullOrEmpty(expectedEventId))
        {
            var matchedAfter = _catalog.FindMatchingEvent(afterTitle, afterOptions);
            if (matchedAfter != null && string.Equals(matchedAfter.Id, expectedEventId, StringComparison.Ordinal))
            {
                Log.Log($"Event change check: still matched same event [{expectedEventId}] after click, treat as unchanged.");
                return false;
            }
        }

        bool sameOptions = !string.IsNullOrEmpty(beforeOptions) &&
                           !string.IsNullOrEmpty(afterOptions) &&
                           EventOcrRegions.NormalizeEventCompareText(beforeOptions) == EventOcrRegions.NormalizeEventCompareText(afterOptions);
        bool sameTitle = !string.IsNullOrEmpty(beforeTitle) &&
                         !string.IsNullOrEmpty(afterTitle) &&
                         EventOcrRegions.NormalizeEventCompareText(beforeTitle) == EventOcrRegions.NormalizeEventCompareText(afterTitle);
        bool sameMarker = !string.IsNullOrEmpty(beforeMarker) &&
                          !string.IsNullOrEmpty(afterMarker) &&
                          EventOcrRegions.NormalizeEventCompareText(beforeMarker) == EventOcrRegions.NormalizeEventCompareText(afterMarker);

        if (sameOptions || (sameTitle && sameMarker))
        {
            Log.Log($"Event change check: semantic unchanged (title='{afterTitle}', options='{afterOptions}', marker='{afterMarker}')");
            return false;
        }

        return IsScreenChanged(before, after);
    }

    private static readonly LogScope Log = new("Race:Event");
}
