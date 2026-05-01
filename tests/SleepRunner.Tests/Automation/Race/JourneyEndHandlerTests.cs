using System.Reflection;
using SleepRunner.Automation.Race.Handlers;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class JourneyEndHandlerTests
{
    [Theory]
    [InlineData("旅程结束")]
    [InlineData("继承旅程总获得潜质点数244点击以继续")]
    [InlineData("继承旅程是时候为旅程画下句号剩余的古币与护符将退还并换取奖励")]
    public void IsJourneyEndText_accepts_natural_completion_screens(string text)
    {
        Assert.True(InvokeIsJourneyEndText(text));
    }

    [Fact]
    public void IsJourneyEndText_rejects_regular_journey_event_text()
    {
        Assert.False(InvokeIsJourneyEndText("旅程事件里面该不会有奇怪的东西吧"));
    }

    private static bool InvokeIsJourneyEndText(string text)
    {
        MethodInfo method = typeof(JourneyEndHandler).GetMethod(
                                "IsJourneyEndText",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("JourneyEndHandler.IsJourneyEndText was not found.");

        return (bool)method.Invoke(null, [text])!;
    }
}
