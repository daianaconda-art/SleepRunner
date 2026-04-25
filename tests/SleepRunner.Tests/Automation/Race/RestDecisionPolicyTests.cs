using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class RestDecisionPolicyTests
{
    [Theory]
    [InlineData(60, true, 3)]
    [InlineData(30, true, 2)]
    [InlineData(10, true, 1)]
    public void TryChooseOption_returns_expected_choice_when_money_is_available(int money, bool expectedSuccess, int expectedOption)
    {
        var (success, option) = InvokeTryChooseOption(money);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedOption, option);
    }

    [Fact]
    public void TryChooseOption_returns_manual_when_money_ocr_is_missing()
    {
        var (success, option) = InvokeTryChooseOption(null);

        Assert.False(success);
        Assert.Equal(0, option);
    }

    private static (bool Success, int Option) InvokeTryChooseOption(int? money)
    {
        Type policyType = Type.GetType("SleepRunner.Automation.Race.Handlers.RestDecisionPolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("RestDecisionPolicy type was not found.");

        MethodInfo method = policyType.GetMethod(
                                "TryChooseOption",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("RestDecisionPolicy.TryChooseOption was not found.");

        object?[] args = [money, 0];
        bool success = (bool)method.Invoke(null, args)!;
        return (success, (int)args[1]!);
    }
}
