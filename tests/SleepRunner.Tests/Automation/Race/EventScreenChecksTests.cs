using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class EventScreenChecksTests
{
    [Fact]
    public void IsEventOptionHint_rejects_main_menu_text_with_character_speech_question()
    {
        const string text = "整理今日可执行作战的预计所需时间和优点一咦？今天脑子怎么特别清委托受理讨伐委托！休息";

        bool matched = InvokeIsEventOptionHint(text);

        Assert.False(matched);
    }

    [Fact]
    public void IsEventOptionHint_keeps_regular_two_choice_event_text()
    {
        const string text = "里面该不会有奇怪的东西吧？";

        bool matched = InvokeIsEventOptionHint(text);

        Assert.True(matched);
    }

    [Fact]
    public void IsEventOptionHint_rejects_appraise_prepare_sheet_text()
    {
        const string text = "远征评鉴战。评鉴战即将开始，请完成战前准备。建议综合等级RANK35登场敌人000可获得奖励060";

        bool matched = InvokeIsEventOptionHint(text);

        Assert.False(matched);
    }

    [Fact]
    public void EventScreenChecks_does_not_expose_defense_175_decision_shortcut()
    {
        Type checksType = GetEventScreenChecksType();

        MethodInfo? method = checksType.GetMethod(
            "IsDefense175Decision",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.Null(method);
    }

    private static bool InvokeIsEventOptionHint(string text)
    {
        Type checksType = GetEventScreenChecksType();

        MethodInfo method = checksType.GetMethod(
                                "IsEventOptionHint",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("EventScreenChecks.IsEventOptionHint was not found.");

        return (bool)method.Invoke(null, [text])!;
    }

    private static Type GetEventScreenChecksType()
    {
        return Type.GetType("SleepRunner.Automation.Race.Handlers.Events.EventScreenChecks, SleepRunner")
               ?? throw new Xunit.Sdk.XunitException("EventScreenChecks type was not found.");
    }
}
