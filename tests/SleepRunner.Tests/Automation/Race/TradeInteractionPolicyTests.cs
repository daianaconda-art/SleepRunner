using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeInteractionPolicyTests
{
    [Fact]
    public void EvaluatePurchaseConfirmation_rejects_disappearing_buy_button_without_strong_signal()
    {
        bool accepted = InvokePurchaseAccepted(
            beforeSlotText: "香甜甜甜圈",
            hasConfirmSignal: false,
            knownBudget: 126,
            price: 21,
            moneyAfter: 126,
            visibleBuy: false,
            grayDisabled: false,
            slotAfter: "香甜甜甜圈");

        Assert.False(accepted);
    }

    [Fact]
    public void EvaluatePurchaseConfirmation_accepts_budget_drop()
    {
        bool accepted = InvokePurchaseAccepted(
            beforeSlotText: "香甜甜甜圈",
            hasConfirmSignal: false,
            knownBudget: 126,
            price: 21,
            moneyAfter: 105,
            visibleBuy: true,
            grayDisabled: false,
            slotAfter: "香甜甜甜圈");

        Assert.True(accepted);
    }

    [Fact]
    public void EvaluateBuyability_marks_gray_button_as_disabled()
    {
        string state = InvokeBuyabilityState(
            visibleBuy: true,
            grayDisabled: true,
            purchasedState: false);

        Assert.Equal("Disabled", state);
    }

    [Fact]
    public void EvaluateBuyability_treats_enabled_buy_button_as_buyable_even_when_sold_out_marker_is_noisy()
    {
        string state = InvokeBuyabilityState(
            visibleBuy: true,
            grayDisabled: false,
            purchasedState: true);

        Assert.Equal("Enabled", state);
    }

    private static bool InvokePurchaseAccepted(
        string beforeSlotText,
        bool hasConfirmSignal,
        int knownBudget,
        int price,
        int moneyAfter,
        bool visibleBuy,
        bool grayDisabled,
        string slotAfter)
    {
        Type policyType = LoadSleepRunnerAssembly().GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeInteractionPolicy")
            ?? throw new Xunit.Sdk.XunitException("TradeInteractionPolicy type was not found.");
        MethodInfo method = policyType.GetMethod(
                                "IsStrongPurchaseSuccess",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeInteractionPolicy.IsStrongPurchaseSuccess was not found.");

        return (bool)method.Invoke(
            null,
            [beforeSlotText, hasConfirmSignal, knownBudget, price, moneyAfter, visibleBuy, grayDisabled, slotAfter])!;
    }

    private static string InvokeBuyabilityState(bool visibleBuy, bool grayDisabled, bool purchasedState)
    {
        Type policyType = LoadSleepRunnerAssembly().GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeInteractionPolicy")
            ?? throw new Xunit.Sdk.XunitException("TradeInteractionPolicy type was not found.");
        MethodInfo method = policyType.GetMethod(
                                "EvaluateBuyability",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeInteractionPolicy.EvaluateBuyability was not found.");

        object result = method.Invoke(null, [visibleBuy, grayDisabled, purchasedState])!;
        return result.ToString()!;
    }

    private static Assembly LoadSleepRunnerAssembly()
    {
        string? overridePath = Environment.GetEnvironmentVariable("STAR_SAVIOR_TEST_ASSEMBLY_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var alc = new AssemblyLoadContext($"trade-tests-{Guid.NewGuid():N}", isCollectible: true);
            return alc.LoadFromAssemblyPath(overridePath);
        }

        return Assembly.Load("SleepRunner");
    }
}
