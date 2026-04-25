using SleepRunner.Automation.Race.Policy.Training;
using Xunit;

namespace SleepRunner.Tests.Automation.Race.Policy.Training;

public class TrainingDecisionContextTests
{
    [Fact]
    public void WithUpdatedStats_keeps_the_higher_strength_value_when_new_read_regresses()
    {
        var context = new TrainingDecisionContext
        {
            StrengthStat = 514,
            StaminaStat = 300,
        };

        TrainingDecisionContext updated = context.WithUpdatedStats(strengthStat: 345, staminaStat: null);

        Assert.Equal(514, updated.StrengthStat);
        Assert.Equal(300, updated.StaminaStat);
    }

    [Fact]
    public void WithUpdatedStats_accepts_higher_strength_value()
    {
        var context = new TrainingDecisionContext
        {
            StrengthStat = 345,
        };

        TrainingDecisionContext updated = context.WithUpdatedStats(strengthStat: 514, staminaStat: null);

        Assert.Equal(514, updated.StrengthStat);
    }

    [Fact]
    public void AnyFailRate_is_unavailable_until_all_fail_rate_rows_are_known()
    {
        var context = new TrainingDecisionContext
        {
            FailRates = [12, 9, 0, 0, 0],
            KnownFailRateMask = 0b00011,
        };

        Assert.Null(context.AnyFailRate);
        Assert.False(context.TryGetMetric(TrainingRuleField.AnyFailRate, out _));
    }
}
