using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeExecutionResultPolicyTests
{
    [Fact]
    public void ShouldExitTrade_returns_false_for_manual_required()
    {
        bool shouldExit = InvokeShouldExitTrade("ManualRequired");

        Assert.False(shouldExit);
    }

    [Fact]
    public void ShouldExitTrade_returns_true_for_normal_results()
    {
        Assert.True(InvokeShouldExitTrade("NoPurchase"));
        Assert.True(InvokeShouldExitTrade("Purchased"));
    }

    private static bool InvokeShouldExitTrade(string enumName)
    {
        Type resultType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeExecutionResult, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeExecutionResult type was not found.");

        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeExecutionResultPolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeExecutionResultPolicy type was not found.");

        MethodInfo method = policyType.GetMethod(
                                "ShouldExitTrade",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeExecutionResultPolicy.ShouldExitTrade was not found.");

        object enumValue = Enum.Parse(resultType, enumName);
        return (bool)method.Invoke(null, [enumValue])!;
    }
}
