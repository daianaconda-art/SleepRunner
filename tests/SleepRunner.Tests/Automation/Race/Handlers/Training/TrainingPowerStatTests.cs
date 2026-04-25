using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race.Handlers.Training;

public class TrainingPowerStatTests
{
    [Fact]
    public void Focused_power_stat_rejects_values_prefixed_with_plus_noise()
    {
        bool parsed = InvokeTryParseStatValueFocused("+345/1250", "力量", out int value, out string reason);

        Assert.False(parsed);
        Assert.Equal(-1, value);
        Assert.Equal("prefixed-bonus-noise", reason);
    }

    [Fact]
    public void Focused_power_stat_accepts_clean_current_over_max_text()
    {
        bool parsed = InvokeTryParseStatValueFocused("力量514/1250", "力量", out int value, out string reason);

        Assert.True(parsed);
        Assert.Equal(514, value);
        Assert.Equal("current/max", reason);
    }

    private static bool InvokeTryParseStatValueFocused(string text, string statName, out int value, out string reason)
    {
        Type type = Type.GetType("SleepRunner.Automation.Race.Handlers.Training.TrainingPowerStat, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TrainingPowerStat type was not found.");

        MethodInfo method = type.GetMethod(
                                "TryParseStatValueFocused",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TrainingPowerStat.TryParseStatValueFocused was not found.");

        object?[] args = [text, statName, 0, ""];
        bool parsed = (bool)method.Invoke(null, args)!;
        value = (int)args[2]!;
        reason = (string)args[3]!;
        return parsed;
    }
}
