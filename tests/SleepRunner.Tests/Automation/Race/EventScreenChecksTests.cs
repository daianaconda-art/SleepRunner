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

    private static bool InvokeIsEventOptionHint(string text)
    {
        Type checksType = Type.GetType("SleepRunner.Automation.Race.Handlers.Events.EventScreenChecks, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("EventScreenChecks type was not found.");

        MethodInfo method = checksType.GetMethod(
                                "IsEventOptionHint",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("EventScreenChecks.IsEventOptionHint was not found.");

        return (bool)method.Invoke(null, [text])!;
    }
}
