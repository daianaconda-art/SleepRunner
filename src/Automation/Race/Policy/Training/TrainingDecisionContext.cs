using SleepRunner.Automation.Race;

namespace SleepRunner.Automation.Race.Policy.Training;

public sealed class TrainingDecisionContext
{
    private const int FullRowMask = 0b11111;

    public int[] IconCounts { get; init; } = [];

    public int[] FailRates { get; init; } = [];

    public int? KnownIconMask { get; init; }

    public int? KnownFailRateMask { get; init; }

    public int? StrengthStat { get; init; }

    public int? StaminaStat { get; init; }

    public TrainingRuleField[] UnavailableFields { get; init; } = [];

    public BuildDirection BuildDirection { get; init; } = BuildDirection.Attack;

    public int LegacyFailRateThreshold { get; init; }

    public int LegacyRushThreshold { get; init; }

    public string ProfileName { get; init; } = string.Empty;

    public int? AnyFailRate
    {
        get
        {
            int effectiveMask = GetEffectiveMask(FailRates, KnownFailRateMask);
            if ((effectiveMask & FullRowMask) != FullRowMask)
            {
                return null;
            }

            return FailRates.Length == 0 ? null : FailRates.Take(Math.Min(5, FailRates.Length)).Max();
        }
    }

    public bool TryGetMetric(TrainingRuleField field, out int value)
    {
        switch (field)
        {
            case TrainingRuleField.StrengthIcons:
                return TryGetIndexedValue(IconCounts, KnownIconMask, 0, out value);
            case TrainingRuleField.StaminaIcons:
                return TryGetIndexedValue(IconCounts, KnownIconMask, 1, out value);
            case TrainingRuleField.AgilityIcons:
                return TryGetIndexedValue(IconCounts, KnownIconMask, 2, out value);
            case TrainingRuleField.FocusIcons:
                return TryGetIndexedValue(IconCounts, KnownIconMask, 3, out value);
            case TrainingRuleField.GuardIcons:
                return TryGetIndexedValue(IconCounts, KnownIconMask, 4, out value);
            case TrainingRuleField.StrengthFailRate:
                return TryGetIndexedValue(FailRates, KnownFailRateMask, 0, out value);
            case TrainingRuleField.StaminaFailRate:
                return TryGetIndexedValue(FailRates, KnownFailRateMask, 1, out value);
            case TrainingRuleField.AgilityFailRate:
                return TryGetIndexedValue(FailRates, KnownFailRateMask, 2, out value);
            case TrainingRuleField.FocusFailRate:
                return TryGetIndexedValue(FailRates, KnownFailRateMask, 3, out value);
            case TrainingRuleField.GuardFailRate:
                return TryGetIndexedValue(FailRates, KnownFailRateMask, 4, out value);
            case TrainingRuleField.AnyFailRate:
                var anyFailRate = AnyFailRate;
                if (anyFailRate.HasValue)
                {
                    value = anyFailRate.Value;
                    return true;
                }

                value = default;
                return false;
            case TrainingRuleField.StrengthStat:
                if (StrengthStat.HasValue)
                {
                    value = StrengthStat.Value;
                    return true;
                }

                value = default;
                return false;
            case TrainingRuleField.StaminaStat:
                if (StaminaStat.HasValue)
                {
                    value = StaminaStat.Value;
                    return true;
                }

                value = default;
                return false;
            default:
                value = default;
                return false;
        }
    }

    public TrainingDecisionContext WithUpdatedStats(int? strengthStat, int? staminaStat)
    {
        return new TrainingDecisionContext
        {
            IconCounts = [.. IconCounts],
            FailRates = [.. FailRates],
            KnownIconMask = KnownIconMask,
            KnownFailRateMask = KnownFailRateMask,
            StrengthStat = MergeNonDecreasingStat(StrengthStat, strengthStat),
            StaminaStat = MergeNonDecreasingStat(StaminaStat, staminaStat),
            UnavailableFields = [.. UnavailableFields],
            BuildDirection = BuildDirection,
            LegacyFailRateThreshold = LegacyFailRateThreshold,
            LegacyRushThreshold = LegacyRushThreshold,
            ProfileName = ProfileName,
        };
    }

    public bool IsMetricUnavailable(TrainingRuleField field)
    {
        return Array.IndexOf(UnavailableFields, field) >= 0;
    }

    private static bool TryGetIndexedValue(int[] values, int? knownMask, int index, out int value)
    {
        int effectiveMask = GetEffectiveMask(values, knownMask);
        if (index < values.Length && (effectiveMask & (1 << index)) != 0)
        {
            value = values[index];
            return true;
        }

        value = default;
        return false;
    }

    private static int GetEffectiveMask(int[] values, int? knownMask)
    {
        if (knownMask.HasValue)
        {
            return knownMask.Value;
        }

        int length = Math.Min(values.Length, 5);
        return length <= 0 ? 0 : (1 << length) - 1;
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
