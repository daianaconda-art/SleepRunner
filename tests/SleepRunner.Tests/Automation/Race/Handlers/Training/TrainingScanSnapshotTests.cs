using SleepRunner.Automation.Race.Handlers.Training;
using SleepRunner.Automation.Race.Policy.Training;
using Xunit;

namespace SleepRunner.Tests.Automation.Race.Handlers.Training;

public class TrainingScanSnapshotTests
{
    [Fact]
    public void ToDecisionContext_computes_any_fail_rate_and_preserves_legacy_metadata()
    {
        var snapshot = new TrainingScanSnapshot(
            iconCounts: [3, 2, 1, 4, 0],
            failRates: [5, 15, 30, 10, 25],
            strengthStat: 410,
            staminaStat: 380);

        var profile = new TrainingRuleProfile();
        profile.LegacyStrategy.BuildDirection = SleepRunner.Automation.Race.BuildDirection.Survival;
        profile.LegacyStrategy.FailRateThreshold = 33;
        profile.LegacyStrategy.RushThreshold = 480;

        var context = snapshot.ToDecisionContext(
            profile,
            profileName: "late-game");

        Assert.Equal([3, 2, 1, 4, 0], context.IconCounts);
        Assert.Equal([5, 15, 30, 10, 25], context.FailRates);
        Assert.Equal(410, context.StrengthStat);
        Assert.Equal(380, context.StaminaStat);
        Assert.Equal(30, context.AnyFailRate);
        Assert.Equal(SleepRunner.Automation.Race.BuildDirection.Survival, context.BuildDirection);
        Assert.Equal(33, context.LegacyFailRateThreshold);
        Assert.Equal(480, context.LegacyRushThreshold);
        Assert.Equal("late-game", context.ProfileName);
    }
}
