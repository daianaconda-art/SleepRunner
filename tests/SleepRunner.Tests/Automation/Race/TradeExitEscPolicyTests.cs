using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeExitEscPolicyTests
{
    [Fact]
    public void ShouldSendEsc_first_attempt_keeps_one_escape_as_fallback()
    {
        bool result = InvokeShouldSendEsc(attempt: 1, tradeScreen: false, stageMenuReady: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldSendEsc_second_attempt_stops_when_trade_screen_is_gone()
    {
        bool result = InvokeShouldSendEsc(attempt: 2, tradeScreen: false, stageMenuReady: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldSendEsc_second_attempt_continues_only_when_trade_screen_is_confirmed()
    {
        bool result = InvokeShouldSendEsc(attempt: 2, tradeScreen: true, stageMenuReady: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldSendEsc_never_sends_escape_after_stage_menu_is_ready()
    {
        bool result = InvokeShouldSendEsc(attempt: 2, tradeScreen: true, stageMenuReady: true);

        Assert.False(result);
    }

    private static bool InvokeShouldSendEsc(int attempt, bool tradeScreen, bool stageMenuReady)
    {
        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeExitEscPolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeExitEscPolicy type was not found.");

        MethodInfo method = policyType.GetMethod(
                                "ShouldSendEsc",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeExitEscPolicy.ShouldSendEsc was not found.");

        return (bool)method.Invoke(null, [attempt, tradeScreen, stageMenuReady])!;
    }
}
