using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.BuiltInRace;

public class BuiltInRaceScreenReaderTests
{
    [Fact]
    public void ScoreText_prioritizes_auto_journey_button_over_initial_info()
    {
        int autoJourneyScore = InvokeScoreText("自动旅程");
        int initialInfoScore = InvokeScoreText("旅程初始信息");

        Assert.True(
            autoJourneyScore > initialInfoScore,
            $"Expected auto journey button score ({autoJourneyScore}) to beat initial info score ({initialInfoScore}).");
    }

    private static int InvokeScoreText(string text)
    {
        Type readerType = Type.GetType(
            "SleepRunner.Automation.BuiltInRace.BuiltInRaceScreenReader, SleepRunner",
            throwOnError: true)!;
        MethodInfo method = readerType.GetMethod(
                                "ScoreText",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("BuiltInRaceScreenReader.ScoreText was not found.");

        return (int)method.Invoke(null, [text])!;
    }
}
