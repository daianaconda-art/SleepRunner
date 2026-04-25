using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Policy.Training;

namespace SleepRunner.Automation.Race.Handlers.Training;

public sealed class TrainingScanSnapshot
{
    public TrainingScanSnapshot(
        int[] iconCounts,
        int[] failRates,
        int? strengthStat = null,
        int? staminaStat = null,
        int? knownIconMask = null,
        int? knownFailRateMask = null)
    {
        IconCounts = iconCounts is null ? [] : [.. iconCounts];
        FailRates = failRates is null ? [] : [.. failRates];
        StrengthStat = strengthStat;
        StaminaStat = staminaStat;
        KnownIconMask = knownIconMask ?? BuildDefaultMask(IconCounts.Length);
        KnownFailRateMask = knownFailRateMask ?? BuildDefaultMask(FailRates.Length);
    }

    public int[] IconCounts { get; }

    public int[] FailRates { get; }

    public int? StrengthStat { get; }

    public int? StaminaStat { get; }

    public int KnownIconMask { get; }

    public int KnownFailRateMask { get; }

    public TrainingDecisionContext ToDecisionContext(
        TrainingRuleProfile profile,
        string profileName)
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

    private static int BuildDefaultMask(int length)
    {
        int clampedLength = Math.Min(length, 5);
        return clampedLength <= 0 ? 0 : (1 << clampedLength) - 1;
    }
}
