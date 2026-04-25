using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers.Training;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Input;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

public class TrainingSelectHandler : IRaceHandler
{
    public string Name => "训练选择";
    public int Priority => 6;

    private readonly IRaceStepGate? _innerGate;

    private static readonly (string Name, double X, double Y)[] TrainingOptions =
    [
        ("力量训练", 0.86, 0.28),
        ("体力训练", 0.86, 0.38),
        ("韧性训练", 0.86, 0.48),
        ("集中训练", 0.86, 0.58),
        ("保护训练", 0.86, 0.68),
    ];

    private const double TrainBtnX = 0.89;
    private const double TrainBtnY = 0.89;
    private const double RestBtnX = 0.95;
    private const double RestBtnY = 0.70;

    public TrainingSelectHandler(
        IRaceStepGate? innerGate = null)
    {
        _innerGate = innerGate;
    }

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string branchText = TrainingScreenChecks.ReadBranchText(screenshot);
        if (TrainingScreenChecks.IsTradeItemText(branchText))
        {
            Log.Log($"Trade-like text hit, skip training handler: '{branchText}'");
            return false;
        }

        if (branchText.Contains("讨伐委托", StringComparison.Ordinal) ||
            branchText.Contains("受理委托", StringComparison.Ordinal))
        {
            return false;
        }

        if (TrainingScreenChecks.IsTrainingDetailText(branchText))
        {
            Log.Log($"Training detail text hit: '{branchText}'");
            return true;
        }

        return false;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var strategy = TrainingRuleStore.CurrentProfile.LegacyStrategy;
        return $"TrainingSelect: lazy rule-driven scan -> training rule evaluation (trainingProfile={TrainingRuleProfileManager.CurrentProfile}, build={strategy.BuildDirection}, failRate={strategy.FailRateThreshold}%, rush={strategy.RushThreshold})";
    }

    public async Task<TrainingScanSnapshot> RunFullScanAsync(GameContext ctx)
    {
        Log.Log("Running full training scan snapshot...");

        int? strengthStat;
        int? staminaStat;
        using (var preShot = ctx.CaptureScreen())
        {
            strengthStat = NormalizeStat(await TrainingPowerStat.ReadPowerStatAsync(preShot));
            staminaStat = NormalizeStat(await TrainingPowerStat.ReadStaminaStatAsync(preShot));
        }

        var iconCounts = new int[TrainingOptions.Length];
        var failRates = new int[TrainingOptions.Length];

        for (int i = 0; i < TrainingOptions.Length; i++)
        {
            var opt = TrainingOptions[i];
            ctx.CheckCancellation();

            using var shot = await CaptureRowShotAsync(ctx, i, opt);
            if (shot == null || shot.Empty())
            {
                Log.Log($"  WARNING: capture empty after row [{i + 1}]{opt.Name}");
                failRates[i] = TrainingFailRateOcr.UnknownFailRateFallback;
                continue;
            }

            int verifiedIdx = TrainingFailRateOcr.DetectSelectedOption(shot, null);
            if (verifiedIdx >= 0 && verifiedIdx != i)
            {
                Log.Log($"  WARN: clicked row [{i + 1}]{opt.Name}, fail-rate Y maps to [{verifiedIdx + 1}] - still attributing count to row [{i + 1}]");
            }
            else if (verifiedIdx < 0)
            {
                Log.Log($"  Fail rate marker not found after row [{i + 1}]{opt.Name}, count still attributed to this row");
            }

            iconCounts[i] = TrainingIconCounter.CountCircularIcons(shot, opt.Name);
            failRates[i] = await TrainingFailRateOcr.ReadFailRatePercent(shot, null, i, TrainingOptions);
            Log.Log($"  [{i + 1}] {opt.Name}: icons={iconCounts[i]}, failRate={failRates[i]}%");
        }

        Log.Log($"Full scan snapshot: icons=[{string.Join(",", iconCounts)}], fails=[{string.Join(",", failRates)}], strength={FormatStat(strengthStat)}, stamina={FormatStat(staminaStat)}");
        return new TrainingScanSnapshot(iconCounts, failRates, strengthStat, staminaStat);
    }

    public async Task<int> ProbeFailRateNowAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            Log.Log($"Probe fail rate: capture empty, fallback={TrainingFailRateOcr.UnknownFailRateFallback}%");
            return TrainingFailRateOcr.UnknownFailRateFallback;
        }

        int selectedIdx = TrainingFailRateOcr.DetectSelectedOption(shot, null);
        if (selectedIdx < 0)
            selectedIdx = 0;

        int failRate = await TrainingFailRateOcr.ReadFailRatePercent(shot, null, selectedIdx, TrainingOptions);
        Log.Log($"Probe fail rate result: selectedIdx={selectedIdx + 1}, failRate={failRate}%");
        return failRate;
    }

    public static async Task<int> DebugReadPowerStatAsync(Mat screenshot)
    {
        return await TrainingPowerStat.ReadPowerStatAsync(screenshot);
    }

    public static int CountCircularIcons(Mat screenshot)
    {
        return TrainingIconCounter.CountCircularIcons(screenshot);
    }

    public async Task HandleAsync(GameContext ctx)
    {
        string profileName = TrainingRuleProfileManager.CurrentProfile;
        TrainingRuleProfile profile = TrainingRuleStore.CurrentProfile;
        Log.Log($"Training select screen detected (profile={profileName}), routing through rule engine...");

        var (snapshot, decision) = await ResolveDecisionAsync(ctx, profile, profileName);

        Log.Log($"Rule evaluation: profile={profileName}, matched={decision.MatchedRuleId}, action={decision.Action}, builtinDefault={decision.UsedBuiltinDefault}");
        Log.Log($"Rule summary: {decision.Summary}");

        await ExecuteDecisionAsync(ctx, snapshot, decision);
    }

    private async Task<Mat?> CaptureRowShotAsync(GameContext ctx, int rowIndex, (string Name, double X, double Y) opt)
    {
        if (rowIndex == 0)
        {
            var preShot = ctx.CaptureScreen();
            if (preShot != null && !preShot.Empty() && TrainingFailRateOcr.DetectSelectedOption(preShot, null) == 0)
            {
                Log.Log($"  [{rowIndex + 1}] {opt.Name}: row already expanded on entry, skip click");
                return preShot;
            }

            preShot?.Dispose();
        }

        await ctx.ClickAtPercent(opt.X, opt.Y);
        await ctx.Wait(650);
        return ctx.CaptureScreen();
    }

    private async Task ExecuteDecisionAsync(GameContext ctx, TrainingScanSnapshot snapshot, TrainingDecisionResult decision)
    {
        string gateSummary = BuildDecisionSummary(snapshot, decision);
        Log.Log($"Execute decision: {gateSummary}");

        if (_innerGate != null)
        {
            Log.Log($"Inner gate: waiting for user to confirm action: {decision.Action}");
            await _innerGate.WaitForContinueAsync(gateSummary, ctx.CancellationToken);
            Log.Log("Inner gate: confirmed, executing...");
        }

        if (decision.Action == TrainingDecisionAction.Rest)
        {
            await ExecuteRestFlowAsync(ctx);
            return;
        }

        if (!decision.TargetRowIndex.HasValue ||
            decision.TargetRowIndex.Value < 0 ||
            decision.TargetRowIndex.Value >= TrainingOptions.Length)
        {
            Log.Log($"Decision target missing for action {decision.Action}, falling back to rest for safety.");
            await ExecuteRestFlowAsync(ctx);
            return;
        }

        int rowIndex = decision.TargetRowIndex.Value;
        var option = TrainingOptions[rowIndex];
        Log.Log($"Clicking row {rowIndex + 1} {option.Name} before bottom Train button.");
        await ctx.ClickAtPercent(option.X, option.Y);
        await ctx.Wait(600);

        Log.Log("Clicking train button...");
        await ctx.ClickAtPercent(TrainBtnX, TrainBtnY);
        await ctx.Wait(3500);
    }

    private static string BuildDecisionSummary(TrainingScanSnapshot snapshot, TrainingDecisionResult decision)
    {
        return $"profile={TrainingRuleProfileManager.CurrentProfile}, icons={FormatMetricList(snapshot.IconCounts, snapshot.KnownIconMask)}, fails={FormatMetricList(snapshot.FailRates, snapshot.KnownFailRateMask)}, strength={FormatStat(snapshot.StrengthStat)}, stamina={FormatStat(snapshot.StaminaStat)}, matched={decision.MatchedRuleId}, action={decision.Action}, builtinDefault={decision.UsedBuiltinDefault}";
    }

    private static int? NormalizeStat(int value)
    {
        return value >= 0 ? value : null;
    }

    private static string FormatStat(int? value)
    {
        return value?.ToString() ?? "N/A";
    }

    private static string FormatMetricList(int[] values, int knownMask)
    {
        var parts = new string[TrainingOptions.Length];
        for (int i = 0; i < TrainingOptions.Length; i++)
        {
            parts[i] = (knownMask & (1 << i)) != 0 && i < values.Length
                ? values[i].ToString()
                : "?";
        }

        return $"[{string.Join(",", parts)}]";
    }

    private static readonly LogScope Log = new("Race:TrainingSelect");

    private async Task<(TrainingScanSnapshot Snapshot, TrainingDecisionResult Decision)> ResolveDecisionAsync(
        GameContext ctx,
        TrainingRuleProfile profile,
        string profileName)
    {
        Log.Log("Running lazy training scan snapshot...");

        var state = new TrainingMetricScanState();

        while (true)
        {
            ctx.CheckCancellation();

            TrainingDecisionContext decisionContext = state.ToDecisionContext(profile, profileName);
            TrainingRuleProbeResult probe = TrainingRuleEngine.Probe(decisionContext, profile);
            if (probe.Decision != null)
            {
                TrainingScanSnapshot snapshot = state.ToSnapshot();
                Log.Log($"Lazy scan snapshot: icons={FormatMetricList(snapshot.IconCounts, snapshot.KnownIconMask)}, fails={FormatMetricList(snapshot.FailRates, snapshot.KnownFailRateMask)}, strength={FormatStat(snapshot.StrengthStat)}, stamina={FormatStat(snapshot.StaminaStat)}");
                return (snapshot, probe.Decision);
            }

            if (!probe.MissingField.HasValue)
            {
                Log.Log("Lazy scan probe produced no decision and no missing field, falling back to full scan.");
                TrainingScanSnapshot fullSnapshot = await RunFullScanAsync(ctx);
                TrainingDecisionResult fullDecision = TrainingRuleEngine.Evaluate(fullSnapshot.ToDecisionContext(profile, profileName), profile);
                return (fullSnapshot, fullDecision);
            }

            Log.Log($"Lazy scan requires metric: {probe.MissingField.Value}");
            await EnsureMetricAsync(ctx, state, probe.MissingField.Value);
        }
    }

    private async Task EnsureMetricAsync(GameContext ctx, TrainingMetricScanState state, TrainingRuleField field)
    {
        switch (field)
        {
            case TrainingRuleField.StrengthStat:
                if (!state.StrengthStat.HasValue)
                {
                    using var shot = ctx.CaptureScreen();
                    state.UpdateStats(NormalizeStat(await TrainingPowerStat.ReadPowerStatAsync(shot)), null);
                    Log.Log($"Lazy scan stat: strength={FormatStat(state.StrengthStat)}");
                }

                return;

            case TrainingRuleField.StaminaStat:
                if (!state.StaminaStat.HasValue)
                {
                    using var shot = ctx.CaptureScreen();
                    state.UpdateStats(null, NormalizeStat(await TrainingPowerStat.ReadStaminaStatAsync(shot)));
                    Log.Log($"Lazy scan stat: stamina={FormatStat(state.StaminaStat)}");
                }

                return;

            case TrainingRuleField.AnyFailRate:
                for (int i = 0; i < TrainingOptions.Length; i++)
                {
                    if (!state.HasFailRate(i))
                    {
                        await EnsureRowMetricsAsync(ctx, state, i);
                    }
                }

                return;

            case TrainingRuleField.StrengthIcons:
            case TrainingRuleField.StrengthFailRate:
                await EnsureRowMetricsAsync(ctx, state, 0);
                return;

            case TrainingRuleField.StaminaIcons:
            case TrainingRuleField.StaminaFailRate:
                await EnsureRowMetricsAsync(ctx, state, 1);
                return;

            case TrainingRuleField.AgilityIcons:
            case TrainingRuleField.AgilityFailRate:
                await EnsureRowMetricsAsync(ctx, state, 2);
                return;

            case TrainingRuleField.FocusIcons:
            case TrainingRuleField.FocusFailRate:
                await EnsureRowMetricsAsync(ctx, state, 3);
                return;

            case TrainingRuleField.GuardIcons:
            case TrainingRuleField.GuardFailRate:
                await EnsureRowMetricsAsync(ctx, state, 4);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown training metric field.");
        }
    }

    private async Task EnsureRowMetricsAsync(GameContext ctx, TrainingMetricScanState state, int rowIndex)
    {
        if (state.HasIconCount(rowIndex) && state.HasFailRate(rowIndex))
        {
            return;
        }

        var opt = TrainingOptions[rowIndex];
        using var shot = await CaptureRowShotAsync(ctx, rowIndex, opt);
        if (shot == null || shot.Empty())
        {
            Log.Log($"  WARNING: capture empty after row [{rowIndex + 1}]{opt.Name}");
            state.SetRowMetrics(rowIndex, iconCount: 0, failRate: TrainingFailRateOcr.UnknownFailRateFallback);
            return;
        }

        int verifiedIdx = TrainingFailRateOcr.DetectSelectedOption(shot, null);
        if (verifiedIdx >= 0 && verifiedIdx != rowIndex)
        {
            Log.Log($"  WARN: clicked row [{rowIndex + 1}]{opt.Name}, fail-rate Y maps to [{verifiedIdx + 1}] - still attributing count to row [{rowIndex + 1}]");
        }
        else if (verifiedIdx < 0)
        {
            Log.Log($"  Fail rate marker not found after row [{rowIndex + 1}]{opt.Name}, count still attributed to this row");
        }

        int iconCount = TrainingIconCounter.CountCircularIcons(shot, opt.Name);
        int failRate = await TrainingFailRateOcr.ReadFailRatePercent(shot, null, rowIndex, TrainingOptions);
        state.SetRowMetrics(rowIndex, iconCount, failRate);
        Log.Log($"  [{rowIndex + 1}] {opt.Name}: icons={iconCount}, failRate={failRate}%");
    }

    private sealed class TrainingMetricScanState
    {
        public int[] IconCounts { get; } = new int[TrainingOptions.Length];

        public int[] FailRates { get; } = new int[TrainingOptions.Length];

        public int KnownIconMask { get; private set; }

        public int KnownFailRateMask { get; private set; }

        public int? StrengthStat { get; set; }

        public int? StaminaStat { get; set; }

        public bool HasIconCount(int rowIndex) => (KnownIconMask & (1 << rowIndex)) != 0;

        public bool HasFailRate(int rowIndex) => (KnownFailRateMask & (1 << rowIndex)) != 0;

        public void UpdateStats(int? strengthStat, int? staminaStat)
        {
            StrengthStat = MergeNonDecreasingStat(StrengthStat, strengthStat);
            StaminaStat = MergeNonDecreasingStat(StaminaStat, staminaStat);
        }

        public void SetRowMetrics(int rowIndex, int iconCount, int failRate)
        {
            IconCounts[rowIndex] = iconCount;
            FailRates[rowIndex] = failRate;
            KnownIconMask |= 1 << rowIndex;
            KnownFailRateMask |= 1 << rowIndex;
        }

        public TrainingDecisionContext ToDecisionContext(TrainingRuleProfile profile, string profileName)
        {
            var strategy = profile.LegacyStrategy;
            return new TrainingDecisionContext
            {
                IconCounts = [.. IconCounts],
                FailRates = [.. FailRates],
                KnownIconMask = KnownIconMask,
                KnownFailRateMask = KnownFailRateMask,
                StrengthStat = StrengthStat,
                StaminaStat = StaminaStat,
                BuildDirection = strategy.BuildDirection,
                LegacyFailRateThreshold = strategy.FailRateThreshold,
                LegacyRushThreshold = strategy.RushThreshold,
                ProfileName = profileName,
            };
        }

        public TrainingScanSnapshot ToSnapshot()
        {
            return new TrainingScanSnapshot(
                IconCounts,
                FailRates,
                StrengthStat,
                StaminaStat,
                KnownIconMask,
                KnownFailRateMask);
        }

        private static int? MergeNonDecreasingStat(int? previous, int? current)
        {
            if (!current.HasValue)
            {
                return previous;
            }

            if (!previous.HasValue)
            {
                return current;
            }

            return Math.Max(previous.Value, current.Value);
        }
    }

    private async Task ExecuteRestFlowAsync(GameContext ctx)
    {
        Log.Log("Rest flow: focus game window and send ESC");
        await KeyboardSimulator.SendKey(ctx.WindowHandle, KeyboardSimulator.VK_ESCAPE);
        await ctx.Wait(1200);

        Log.Log($"Rest flow: click rest at fixed percent ({RestBtnX:F2},{RestBtnY:F2})");
        await ctx.ClickAtPercent(RestBtnX, RestBtnY);

        await ctx.Wait(1500);
    }
}
