using System.Reflection;
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
}
