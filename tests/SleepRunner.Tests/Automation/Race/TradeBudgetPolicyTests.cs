using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeBudgetPolicyTests
{
    [Theory]
    [InlineData(120, 120)]
    [InlineData(30, 30)]
    public void ResolveExecutionBudget_keeps_detected_budget_when_available(int detectedBudget, int expectedBudget)
    {
        int resolved = InvokeResolveExecutionBudget(detectedBudget);

        Assert.Equal(expectedBudget, resolved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolveExecutionBudget_falls_back_to_unknown_budget_when_ocr_fails(int detectedBudget)
    {
        int resolved = InvokeResolveExecutionBudget(detectedBudget);

        Assert.Equal(int.MaxValue, resolved);
    }

    [Theory]
    [InlineData(120, true, 120)]
    [InlineData(30, true, 30)]
    public void TryResolveBudget_accepts_positive_detected_budget(int detectedBudget, bool expectedSuccess, int expectedBudget)
    {
        var (success, budget) = InvokeTryResolveBudget(detectedBudget);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedBudget, budget);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryResolveBudget_rejects_missing_or_invalid_budget(int detectedBudget)
    {
        var (success, budget) = InvokeTryResolveBudget(detectedBudget);

        Assert.False(success);
        Assert.Equal(0, budget);
    }

    private static (bool Success, int Budget) InvokeTryResolveBudget(int detectedBudget)
    {
        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeBudgetPolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeBudgetPolicy type was not found.");

        MethodInfo method = policyType.GetMethod(
                                "TryResolveBudget",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeBudgetPolicy.TryResolveBudget was not found.");

        object?[] args = [detectedBudget, 0];
        bool success = (bool)method.Invoke(null, args)!;
        return (success, (int)args[1]!);
    }

    private static int InvokeResolveExecutionBudget(int detectedBudget)
    {
        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeBudgetPolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeBudgetPolicy type was not found.");

        MethodInfo method = policyType.GetMethod(
                                "ResolveExecutionBudget",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeBudgetPolicy.ResolveExecutionBudget was not found.");

        return (int)method.Invoke(null, [detectedBudget])!;
    }
}
