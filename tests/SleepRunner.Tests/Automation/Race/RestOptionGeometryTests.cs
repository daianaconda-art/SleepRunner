using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class RestOptionGeometryTests
{
    [Fact]
    public void ResolveOptionY_uses_base_when_target_option_is_not_directly_anchored()
    {
        double y = InvokeResolveOptionY(
            resolvedY: new[] { -1d, 0.571d, 0.656d },
            anchored: new[] { false, true, true },
            optionIndex: 0,
            baseY: 0.420d);

        Assert.Equal(0.420d, y, 3);
    }

    [Fact]
    public void ResolveOptionY_uses_resolved_value_when_target_option_is_directly_anchored()
    {
        double y = InvokeResolveOptionY(
            resolvedY: new[] { 0.420d, 0.495d, 0.571d },
            anchored: new[] { true, true, true },
            optionIndex: 0,
            baseY: 0.420d);

        Assert.Equal(0.420d, y, 3);
    }

    private static double InvokeResolveOptionY(double[] resolvedY, bool[] anchored, int optionIndex, double baseY)
    {
        Type handlerType = Type.GetType("SleepRunner.Automation.Race.Handlers.RestDecisionHandler, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("RestDecisionHandler type was not found.");

        MethodInfo method = handlerType.GetMethod(
                                "ResolveTargetOptionY",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("RestDecisionHandler.ResolveTargetOptionY was not found.");

        return (double)method.Invoke(null, [resolvedY, anchored, optionIndex, baseY])!;
    }
}
