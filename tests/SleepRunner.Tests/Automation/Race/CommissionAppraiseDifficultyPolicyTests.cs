using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class CommissionAppraiseDifficultyPolicyTests
{
    [Fact]
    public void Normal_mode_targets_second_appraise_option()
    {
        object normal = ParseMode("Normal");

        int index = InvokeResolveListOptionIndex(normal);

        Assert.Equal(2, index);
    }

    [Fact]
    public void Hard_mode_targets_third_appraise_option()
    {
        object hard = ParseMode("Hard");

        int index = InvokeResolveListOptionIndex(hard);

        Assert.Equal(3, index);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Difficulty_based_popup_start_decision_keeps_original_red_difficulty_rule(
        bool isRedDifficult,
        bool expectedStart)
    {
        bool shouldStart = InvokeShouldStartDifficultyBasedPopup(isRedDifficult);

        Assert.Equal(expectedStart, shouldStart);
    }

    private static object ParseMode(string name)
    {
        Type enumType = Type.GetType("SleepRunner.Automation.Race.AppraiseDifficultyMode, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("AppraiseDifficultyMode type was not found.");
        return Enum.Parse(enumType, name);
    }

    private static int InvokeResolveListOptionIndex(object mode)
    {
        Type type = GetPolicyType();
        MethodInfo method = type.GetMethod(
                                "ResolveListOptionIndex",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("ResolveListOptionIndex was not found.");
        return (int)method.Invoke(null, [mode])!;
    }

    private static bool InvokeShouldStartDifficultyBasedPopup(bool isRedDifficult)
    {
        Type type = GetPolicyType();
        MethodInfo method = type.GetMethod(
                                "ShouldStartDifficultyBasedPopup",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                                [typeof(bool)])
                            ?? throw new Xunit.Sdk.XunitException("ShouldStartDifficultyBasedPopup was not found.");
        return (bool)method.Invoke(null, [isRedDifficult])!;
    }

    private static Type GetPolicyType()
    {
        return Type.GetType("SleepRunner.Automation.Race.Handlers.Commission.CommissionAppraiseDifficultyPolicy, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("CommissionAppraiseDifficultyPolicy type was not found.");
    }
}
