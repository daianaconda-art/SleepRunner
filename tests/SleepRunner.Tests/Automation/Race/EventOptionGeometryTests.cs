using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class EventOptionGeometryTests
{
    [Theory]
    [InlineData(1, 2, 0.635)]
    [InlineData(2, 2, 0.740)]
    [InlineData(1, 3, 0.550)]
    [InlineData(2, 3, 0.650)]
    [InlineData(3, 3, 0.730)]
    [InlineData(1, 4, 0.535)]
    [InlineData(2, 4, 0.605)]
    [InlineData(3, 4, 0.675)]
    [InlineData(4, 4, 0.745)]
    public void CalcOptionClickY_uses_observed_fast_click_centers(int optionIndex, int totalOptions, double expectedY)
    {
        double actualY = InvokeCalcOptionClickY(optionIndex, totalOptions);

        Assert.Equal(expectedY, actualY, precision: 3);
    }

    [Fact]
    public void BuildRetrySweepYs_limits_alternate_clicks()
    {
        var ys = InvokeBuildRetrySweepYs(optionIndex: 1, totalOptions: 2, primaryY: 0.635);

        Assert.True(ys.Count <= 2);
    }

    private static double InvokeCalcOptionClickY(int optionIndex, int totalOptions)
    {
        Type geometryType = LoadSleepRunnerAssembly().GetType("SleepRunner.Automation.Race.Handlers.Events.EventOptionGeometry")
            ?? throw new Xunit.Sdk.XunitException("EventOptionGeometry type was not found.");
        MethodInfo method = geometryType.GetMethod(
                                "CalcOptionClickY",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("EventOptionGeometry.CalcOptionClickY was not found.");

        return (double)method.Invoke(null, [optionIndex, totalOptions])!;
    }

    private static IReadOnlyList<double> InvokeBuildRetrySweepYs(int optionIndex, int totalOptions, double primaryY)
    {
        Type geometryType = LoadSleepRunnerAssembly().GetType("SleepRunner.Automation.Race.Handlers.Events.EventOptionGeometry")
            ?? throw new Xunit.Sdk.XunitException("EventOptionGeometry type was not found.");
        MethodInfo method = geometryType.GetMethod(
                                "BuildRetrySweepYs",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("EventOptionGeometry.BuildRetrySweepYs was not found.");

        return ((IEnumerable<double>)method.Invoke(null, [optionIndex, totalOptions, primaryY])!).ToArray();
    }

    private static Assembly LoadSleepRunnerAssembly()
    {
        return Type.GetType("SleepRunner.Automation.Race.RaceRunner, SleepRunner")?.Assembly
               ?? throw new Xunit.Sdk.XunitException("SleepRunner assembly was not loaded.");
    }
}
