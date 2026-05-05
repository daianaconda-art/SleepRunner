using System.Reflection;
using SleepRunner.Automation.Race.Policy.Training;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TrainingSelectMetricScanStateTests
{
    [Fact]
    public void UpdateStats_reports_no_progress_when_ocr_returns_no_stat_values()
    {
        object state = CreateScanState();
        MethodInfo updateStats = GetUpdateStatsMethod(state);

        object? result = updateStats.Invoke(state, [null, null]);

        bool madeProgress = Assert.IsType<bool>(result);
        Assert.False(madeProgress);
    }

    [Fact]
    public void UpdateStats_reports_progress_when_a_missing_stat_is_read()
    {
        object state = CreateScanState();
        MethodInfo updateStats = GetUpdateStatsMethod(state);

        object? result = updateStats.Invoke(state, [null, 9]);

        bool madeProgress = Assert.IsType<bool>(result);
        Assert.True(madeProgress);
    }

    [Fact]
    public void MarkMetricUnavailable_flows_to_decision_context()
    {
        object state = CreateScanState();
        MethodInfo markMetricUnavailable = GetMarkMetricUnavailableMethod(state);

        object? result = markMetricUnavailable.Invoke(state, [TrainingRuleField.StrengthStat]);

        bool madeProgress = Assert.IsType<bool>(result);
        Assert.True(madeProgress);

        object context = InvokeToDecisionContext(state);
        TrainingRuleField[] fields = GetUnavailableFields(context);

        Assert.Contains(TrainingRuleField.StrengthStat, fields);
    }

    [Fact]
    public void UpdateStats_clears_unavailable_stat_when_the_value_is_read_later()
    {
        object state = CreateScanState();
        MethodInfo markMetricUnavailable = GetMarkMetricUnavailableMethod(state);
        MethodInfo updateStats = GetUpdateStatsMethod(state);

        markMetricUnavailable.Invoke(state, [TrainingRuleField.StrengthStat]);
        updateStats.Invoke(state, [777, null]);

        object context = InvokeToDecisionContext(state);
        TrainingRuleField[] fields = GetUnavailableFields(context);

        Assert.DoesNotContain(TrainingRuleField.StrengthStat, fields);
    }

    private static object CreateScanState()
    {
        Type type = Type.GetType("SleepRunner.Automation.Race.Handlers.TrainingSelectHandler+TrainingMetricScanState, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TrainingMetricScanState type was not found.");
        return Activator.CreateInstance(type, nonPublic: true)
               ?? throw new Xunit.Sdk.XunitException("TrainingMetricScanState could not be created.");
    }

    private static MethodInfo GetUpdateStatsMethod(object state)
    {
        return state.GetType().GetMethod("UpdateStats", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
               ?? throw new Xunit.Sdk.XunitException("TrainingMetricScanState.UpdateStats was not found.");
    }

    private static MethodInfo GetMarkMetricUnavailableMethod(object state)
    {
        return state.GetType().GetMethod("MarkMetricUnavailable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
               ?? throw new Xunit.Sdk.XunitException("TrainingMetricScanState.MarkMetricUnavailable was not found.");
    }

    private static object InvokeToDecisionContext(object state)
    {
        MethodInfo method = state.GetType().GetMethod("ToDecisionContext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TrainingMetricScanState.ToDecisionContext was not found.");
        var profile = new TrainingRuleProfile();
        return method.Invoke(state, [profile, "test-profile.json"])
               ?? throw new Xunit.Sdk.XunitException("TrainingMetricScanState.ToDecisionContext returned null.");
    }

    private static TrainingRuleField[] GetUnavailableFields(object context)
    {
        PropertyInfo property = context.GetType().GetProperty("UnavailableFields")
                                ?? throw new Xunit.Sdk.XunitException("TrainingDecisionContext.UnavailableFields was not found.");
        return Assert.IsType<TrainingRuleField[]>(property.GetValue(context));
    }
}
