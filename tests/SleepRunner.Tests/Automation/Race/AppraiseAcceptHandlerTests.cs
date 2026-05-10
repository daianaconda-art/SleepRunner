using System.Reflection;
using OpenCvSharp;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class AppraiseAcceptHandlerTests
{
    [Fact]
    public void ShouldFallbackToMouseAfterHotkey_accepts_unchanged_snapshot()
    {
        using var before = MakeSolidShot(24, 32, new Scalar(30, 40, 50));
        using var after = MakeSolidShot(24, 32, new Scalar(30, 40, 50));

        bool fallback = InvokeShouldFallbackToMouseAfterHotkey(before, after);

        Assert.True(fallback);
    }

    [Fact]
    public void ShouldFallbackToMouseAfterHotkey_rejects_changed_snapshot()
    {
        using var before = MakeSolidShot(24, 32, new Scalar(30, 40, 50));
        using var after = MakeSolidShot(24, 32, new Scalar(170, 180, 190));

        bool fallback = InvokeShouldFallbackToMouseAfterHotkey(before, after);

        Assert.False(fallback);
    }

    [Fact]
    public void ShouldFallbackAfterHotkeyAttempt_skips_mouse_when_screen_changed_even_if_key_sequence_aborted()
    {
        using var before = MakeSolidShot(24, 32, new Scalar(30, 40, 50));
        using var after = MakeSolidShot(24, 32, new Scalar(170, 180, 190));

        bool fallback = InvokeShouldFallbackAfterHotkeyAttempt(before, after, stillOnAcceptDetailScreen: false);

        Assert.False(fallback);
    }

    [Fact]
    public void ShouldFallbackAfterHotkeyAttempt_clicks_mouse_when_still_on_accept_detail()
    {
        using var before = MakeSolidShot(24, 32, new Scalar(30, 40, 50));
        using var after = MakeSolidShot(24, 32, new Scalar(170, 180, 190));

        bool fallback = InvokeShouldFallbackAfterHotkeyAttempt(before, after, stillOnAcceptDetailScreen: true);

        Assert.True(fallback);
    }

    private static Mat MakeSolidShot(int rows, int cols, Scalar color)
    {
        return new Mat(rows, cols, MatType.CV_8UC3, color);
    }

    private static bool InvokeShouldFallbackToMouseAfterHotkey(Mat before, Mat after)
    {
        Type handlerType = Type.GetType("SleepRunner.Automation.Race.Handlers.AppraiseAcceptHandler, SleepRunner")
                           ?? throw new Xunit.Sdk.XunitException("AppraiseAcceptHandler type was not found.");
        MethodInfo method = handlerType.GetMethod(
                                "ShouldFallbackToMouseAfterHotkey",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("AppraiseAcceptHandler.ShouldFallbackToMouseAfterHotkey was not found.");

        return (bool)method.Invoke(null, [before, after])!;
    }

    private static bool InvokeShouldFallbackAfterHotkeyAttempt(Mat before, Mat after, bool stillOnAcceptDetailScreen)
    {
        Type handlerType = Type.GetType("SleepRunner.Automation.Race.Handlers.AppraiseAcceptHandler, SleepRunner")
                           ?? throw new Xunit.Sdk.XunitException("AppraiseAcceptHandler type was not found.");
        MethodInfo method = handlerType.GetMethod(
                                "ShouldFallbackAfterHotkeyAttempt",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("AppraiseAcceptHandler.ShouldFallbackAfterHotkeyAttempt was not found.");

        return (bool)method.Invoke(null, [before, after, stillOnAcceptDetailScreen])!;
    }
}
