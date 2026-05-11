using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class EventHotkeyFallbackTests
{
    [Fact]
    public void ShouldContinueMouseFallbackAfterEventHotkey_accepts_successful_hotkey_with_unchanged_screen()
    {
        bool continueFallback = InvokeShouldContinueMouseFallbackAfterEventHotkey(
            hotkeySent: true,
            screenChanged: false);

        Assert.True(continueFallback);
    }

    [Fact]
    public void ShouldContinueMouseFallbackAfterEventHotkey_accepts_failed_hotkey_with_unchanged_screen()
    {
        bool continueFallback = InvokeShouldContinueMouseFallbackAfterEventHotkey(
            hotkeySent: false,
            screenChanged: false);

        Assert.True(continueFallback);
    }

    [Fact]
    public void ShouldContinueMouseFallbackAfterEventHotkey_rejects_changed_screen()
    {
        bool continueFallback = InvokeShouldContinueMouseFallbackAfterEventHotkey(
            hotkeySent: false,
            screenChanged: true);

        Assert.False(continueFallback);
    }

    private static bool InvokeShouldContinueMouseFallbackAfterEventHotkey(bool hotkeySent, bool screenChanged)
    {
        Type handlerType = Type.GetType("SleepRunner.Automation.Race.Handlers.EventHandler, SleepRunner")
                           ?? throw new Xunit.Sdk.XunitException("EventHandler type was not found.");
        MethodInfo method = handlerType.GetMethod(
                                "ShouldContinueMouseFallbackAfterEventHotkey",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("EventHandler.ShouldContinueMouseFallbackAfterEventHotkey was not found.");

        return (bool)method.Invoke(null, [hotkeySent, screenChanged])!;
    }
}
