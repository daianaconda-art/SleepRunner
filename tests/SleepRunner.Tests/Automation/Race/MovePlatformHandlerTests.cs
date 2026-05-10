using System.Reflection;
using SleepRunner.Automation.Race.Handlers;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class MovePlatformHandlerTests
{
    [Fact]
    public void StabilizeOptionPick_keeps_recent_two_option_preview_when_execute_ocr_drops_second_line()
    {
        var pick = InvokeStabilizeOptionPick(
            currentCount: 1,
            currentPickY: 0.30,
            currentReason: "single-option line1='A' line2=''",
            cachedCount: 2,
            cachedPickY: 0.42,
            cachedReason: "two-options profile-second line1='A' line2='B'",
            cachedAgeMs: 500);

        Assert.Equal(2, pick.Count);
        Assert.Equal(0.42, pick.PickY, precision: 3);
        Assert.Contains("cached-preview", pick.Reason);
    }

    [Fact]
    public void StabilizeOptionPick_ignores_stale_preview()
    {
        var pick = InvokeStabilizeOptionPick(
            currentCount: 1,
            currentPickY: 0.30,
            currentReason: "single-option line1='A' line2=''",
            cachedCount: 2,
            cachedPickY: 0.42,
            cachedReason: "two-options profile-second line1='A' line2='B'",
            cachedAgeMs: 3500);

        Assert.Equal(1, pick.Count);
        Assert.Equal(0.30, pick.PickY, precision: 3);
    }

    private static (int Count, double PickY, string Reason) InvokeStabilizeOptionPick(
        int currentCount,
        double currentPickY,
        string currentReason,
        int cachedCount,
        double cachedPickY,
        string cachedReason,
        double cachedAgeMs)
    {
        MethodInfo method = typeof(MovePlatformHandler).GetMethod(
                                "StabilizeOptionPick",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("MovePlatformHandler.StabilizeOptionPick was not found.");

        return ((int Count, double PickY, string Reason))method.Invoke(
            null,
            [currentCount, currentPickY, currentReason, cachedCount, cachedPickY, cachedReason, cachedAgeMs])!;
    }
}
