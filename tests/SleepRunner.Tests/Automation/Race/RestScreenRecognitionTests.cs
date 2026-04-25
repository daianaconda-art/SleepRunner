using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class RestScreenRecognitionTests
{
    [Theory]
    [InlineData("露宿", 0)]
    [InlineData("住处", 1)]
    [InlineData("冥想室", 2)]
    [InlineData("未知标题", -1)]
    public void ResolveExpandedRestOptionIndex_returns_expected_index(string detailTitle, int expectedIndex)
    {
        int resolved = InvokeResolveExpandedRestOptionIndex(detailTitle);

        Assert.Equal(expectedIndex, resolved);
    }

    [Fact]
    public void IsRestDecisionContext_accepts_rest_detail_state_without_confirm_text()
    {
        bool matched = InvokeIsRestDecisionContext("标准住处对救援者来说是熟悉的空间效果耐力300恢复30冥想室", "");

        Assert.True(matched);
    }

    [Fact]
    public void IsRestDecisionContext_accepts_degraded_rest_options_without_confirm_text()
    {
        bool matched = InvokeIsRestDecisionContext("住处30冥想室60", "");

        Assert.True(matched);
    }

    [Fact]
    public void IsRestDecisionContext_rejects_weak_text_without_confirm_text()
    {
        bool matched = InvokeIsRestDecisionContext("住处30", "");

        Assert.False(matched);
    }

    private static bool InvokeIsRestDecisionContext(string optionText, string confirmText)
    {
        Type handlerType = Type.GetType("SleepRunner.Automation.Race.Handlers.RestDecisionHandler, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("RestDecisionHandler type was not found.");

        MethodInfo method = handlerType.GetMethod(
                                "IsRestDecisionContext",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("RestDecisionHandler.IsRestDecisionContext was not found.");

        return (bool)method.Invoke(null, [optionText, confirmText])!;
    }

    private static int InvokeResolveExpandedRestOptionIndex(string detailTitle)
    {
        Type handlerType = Type.GetType("SleepRunner.Automation.Race.Handlers.RestDecisionHandler, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("RestDecisionHandler type was not found.");

        MethodInfo method = handlerType.GetMethod(
                                "ResolveExpandedRestOptionIndex",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("RestDecisionHandler.ResolveExpandedRestOptionIndex was not found.");

        return (int)method.Invoke(null, [detailTitle])!;
    }
}
