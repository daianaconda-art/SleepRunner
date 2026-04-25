using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradePriceParsingTests
{
    [Theory]
    [InlineData("活力药水050", 50)]
    [InlineData("《利面高级奶油40", 40)]
    [InlineData("80 40", 40)]
    [InlineData("原价80现价40", 40)]
    public void ExtractPriceValue_prefers_effective_display_price(string text, int expected)
    {
        int actual = InvokeExtractPriceValue(text);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CreateSlotFallbackPriceCandidate_penalizes_single_digit_noise()
    {
        int score = InvokeCreateSlotFallbackPriceCandidate("1面．以高级奶油，", 1, 0, 1);

        Assert.True(score < 0);
    }

    private static int InvokeExtractPriceValue(string text)
    {
        Type type = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeDetailOcr, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeDetailOcr type was not found.");

        MethodInfo method = type.GetMethod(
                                "ExtractPriceValue",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeDetailOcr.ExtractPriceValue was not found.");

        return (int)method.Invoke(null, [text])!;
    }

    private static int InvokeCreateSlotFallbackPriceCandidate(string text, int value, int matchIndex, int digitCount)
    {
        Type type = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeDetailOcr, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeDetailOcr type was not found.");

        MethodInfo method = type.GetMethod(
                                "CreateSlotFallbackPriceCandidate",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeDetailOcr.CreateSlotFallbackPriceCandidate was not found.");

        object candidate = method.Invoke(null, [text, value, matchIndex, digitCount])!;
        PropertyInfo score = candidate.GetType().GetProperty("Score")!;
        return (int)score.GetValue(candidate)!;
    }
}
