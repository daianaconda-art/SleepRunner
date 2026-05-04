using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Handlers.Training;
using SleepRunner.Automation.Race.Policy;

namespace SleepRunner.Automation.Race.Policy.Training;

public static class TrainingBuiltInDecision
{
    private const int RushStatMaxValue = RaceStatCapPolicy.AttributeCap;
    private static readonly (TrainingDecisionAction Action, int RowIndex)[] RowMap =
    [
        (TrainingDecisionAction.TrainStrength, 0),
        (TrainingDecisionAction.TrainStamina, 1),
        (TrainingDecisionAction.TrainAgility, 2),
        (TrainingDecisionAction.TrainFocus, 3),
        (TrainingDecisionAction.TrainGuard, 4),
    ];

    public static TrainingDecisionResult Evaluate(TrainingDecisionContext context, string matchedRuleId)
    {
        if (GetAnyFailRate(context) > context.LegacyFailRateThreshold)
        {
            return BuildResult(context, matchedRuleId, TrainingDecisionAction.Rest, null, usedBuiltinDefault: true);
        }

        int rushRowIndex = context.BuildDirection == BuildDirection.Survival ? 1 : 0;
        int rushStat = GetRushStat(context);
        int normalSelection = TrainingIconCounter.ApplyPriorityRule(EnsureFiveCounts(context.IconCounts), context.BuildDirection);

        if (rushStat >= RushStatMaxValue)
        {
            int selected = SelectBestFrontThreeExceptRush(context.IconCounts, rushRowIndex, normalSelection);
            return BuildResult(context, matchedRuleId, RowMap[selected].Action, selected, usedBuiltinDefault: true);
        }

        if (rushStat > context.LegacyRushThreshold)
        {
            return BuildResult(context, matchedRuleId, RowMap[rushRowIndex].Action, rushRowIndex, usedBuiltinDefault: true);
        }

        return BuildResult(context, matchedRuleId, RowMap[normalSelection].Action, normalSelection, usedBuiltinDefault: true);
    }

    public static TrainingRuleProbeResult Probe(TrainingDecisionContext context, string matchedRuleId)
    {
        if (!context.TryGetMetric(TrainingRuleField.AnyFailRate, out int anyFailRate))
        {
            return Missing(TrainingRuleField.AnyFailRate);
        }

        if (anyFailRate > context.LegacyFailRateThreshold)
        {
            return Resolved(BuildResult(context, matchedRuleId, TrainingDecisionAction.Rest, null, usedBuiltinDefault: true));
        }

        int rushRowIndex = context.BuildDirection == BuildDirection.Survival ? 1 : 0;
        TrainingRuleField rushStatField = context.BuildDirection == BuildDirection.Survival
            ? TrainingRuleField.StaminaStat
            : TrainingRuleField.StrengthStat;
        if (!context.TryGetMetric(rushStatField, out int rushStat))
        {
            if (!context.IsMetricUnavailable(rushStatField))
            {
                return Missing(rushStatField);
            }

            rushStat = 0;
        }

        if (rushStat >= RushStatMaxValue)
        {
            if (TryGetFrontThreeCounts(context, out int[] frontThreeCounts, out TrainingRuleField? missingField))
            {
                int selected = SelectBestFrontThreeExceptRush(frontThreeCounts, rushRowIndex, fallbackSelection: rushRowIndex == 0 ? 1 : 0);
                return Resolved(BuildResult(context, matchedRuleId, RowMap[selected].Action, selected, usedBuiltinDefault: true));
            }

            return Missing(missingField!.Value);
        }

        if (rushStat > context.LegacyRushThreshold)
        {
            return Resolved(BuildResult(context, matchedRuleId, RowMap[rushRowIndex].Action, rushRowIndex, usedBuiltinDefault: true));
        }

        if (TryGetAllCounts(context, out int[] counts, out TrainingRuleField? iconMissingField))
        {
            int normalSelection = TrainingIconCounter.ApplyPriorityRule(EnsureFiveCounts(counts), context.BuildDirection);
            return Resolved(BuildResult(context, matchedRuleId, RowMap[normalSelection].Action, normalSelection, usedBuiltinDefault: true));
        }

        return Missing(iconMissingField!.Value);
    }

    private static int GetRushStat(TrainingDecisionContext context) => context.BuildDirection == BuildDirection.Survival
        ? context.StaminaStat ?? 0
        : context.StrengthStat ?? 0;

    private static int GetAnyFailRate(TrainingDecisionContext context)
    {
        return context.AnyFailRate ?? 0;
    }

    private static int[] EnsureFiveCounts(int[] counts)
    {
        var result = new int[5];
        for (int i = 0; i < result.Length && i < counts.Length; i++)
        {
            result[i] = counts[i];
        }

        return result;
    }

    private static int SelectBestFrontThreeExceptRush(int[] counts, int rushRowIndex, int fallbackSelection)
    {
        var safeCounts = EnsureFiveCounts(counts);
        int selected = -1;
        int bestCount = int.MinValue;

        for (int i = 0; i < 3; i++)
        {
            if (i == rushRowIndex)
            {
                continue;
            }

            if (safeCounts[i] > bestCount)
            {
                bestCount = safeCounts[i];
                selected = i;
            }
        }

        return selected >= 0 ? selected : fallbackSelection;
    }

    private static bool TryGetFrontThreeCounts(TrainingDecisionContext context, out int[] counts, out TrainingRuleField? missingField)
    {
        counts = new int[5];
        missingField = null;

        for (int i = 0; i < 3; i++)
        {
            TrainingRuleField field = IconFieldForRow(i);
            if (!context.TryGetMetric(field, out counts[i]))
            {
                missingField = field;
                return false;
            }
        }

        return true;
    }

    private static bool TryGetAllCounts(TrainingDecisionContext context, out int[] counts, out TrainingRuleField? missingField)
    {
        counts = new int[5];
        missingField = null;

        for (int i = 0; i < 5; i++)
        {
            TrainingRuleField field = IconFieldForRow(i);
            if (!context.TryGetMetric(field, out counts[i]))
            {
                missingField = field;
                return false;
            }
        }

        return true;
    }

    private static TrainingRuleField IconFieldForRow(int rowIndex) => rowIndex switch
    {
        0 => TrainingRuleField.StrengthIcons,
        1 => TrainingRuleField.StaminaIcons,
        2 => TrainingRuleField.AgilityIcons,
        3 => TrainingRuleField.FocusIcons,
        4 => TrainingRuleField.GuardIcons,
        _ => throw new ArgumentOutOfRangeException(nameof(rowIndex)),
    };

    private static TrainingRuleProbeResult Resolved(TrainingDecisionResult decision)
    {
        return new TrainingRuleProbeResult
        {
            Decision = decision,
        };
    }

    private static TrainingRuleProbeResult Missing(TrainingRuleField field)
    {
        return new TrainingRuleProbeResult
        {
            MissingField = field,
        };
    }

    private static TrainingDecisionResult BuildResult(
        TrainingDecisionContext context,
        string matchedRuleId,
        TrainingDecisionAction action,
        int? targetRowIndex,
        bool usedBuiltinDefault)
    {
        return new TrainingDecisionResult
        {
            MatchedRuleId = matchedRuleId,
            Action = action,
            TargetRowIndex = targetRowIndex,
            UsedBuiltinDefault = usedBuiltinDefault,
            Summary = $"profile={context.ProfileName}, matched={matchedRuleId}, action={action}, target={targetRowIndex}, builtin_default={usedBuiltinDefault}",
        };
    }
}
