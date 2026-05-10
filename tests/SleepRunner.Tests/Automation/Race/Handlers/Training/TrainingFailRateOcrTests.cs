using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race.Handlers.Training;

public class TrainingFailRateOcrTests
{
    [Fact]
    public void ParseFailRateCandidates_ignores_training_level_digits()
    {
        List<int> values = ParseFailRateCandidates("力量训练Lv.1失败率0％");

        Assert.Equal([0], values);
    }

    [Fact]
    public void ParseFailRateCandidates_keeps_regular_fail_rate_numbers_without_level_tokens()
    {
        List<int> values = ParseFailRateCandidates("失败率15％");

        Assert.Equal([15], values);
    }

    private static List<int> ParseFailRateCandidates(string text)
    {
        Type type = Type.GetType("SleepRunner.Automation.Race.Handlers.Training.TrainingFailRateOcr, SleepRunner")
                    ?? throw new Xunit.Sdk.XunitException("TrainingFailRateOcr type was not found.");
        MethodInfo method = type.GetMethod("ParseFailRateCandidates", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TrainingFailRateOcr.ParseFailRateCandidates was not found.");
        return Assert.IsType<List<int>>(method.Invoke(null, [text]));
    }
}
