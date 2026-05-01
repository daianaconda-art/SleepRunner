using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeSlotOwnershipTests
{
    [Fact]
    public void BelongsToSlot_returns_true_when_row_and_detail_match_after_normalization()
    {
        bool owned = InvokeBelongsToSlot("香甜甜甜圈21", "香甜甜甜圈");
        Assert.True(owned);
    }

    [Fact]
    public void BelongsToSlot_returns_false_when_detail_points_to_another_slot()
    {
        bool owned = InvokeBelongsToSlot("香甜甜甜圈21", "手持随身风扇护符");
        Assert.False(owned);
    }

    [Fact]
    public void BelongsToSlot_returns_true_when_detail_extends_row_name_with_extra_suffix()
    {
        bool owned = InvokeBelongsToSlot("手持随身风扇乪35", "手持随身风扇护符");
        Assert.True(owned);
    }

    [Theory]
    [InlineData("皇家炸肉组@80", "《家炸肉组料理食物0")]
    [InlineData("集中训练的禁书丿60", "0集中j川练的禁书训练书籍0")]
    [InlineData("，0《高级炸肉40", "0哥级炸肉料理食物0")]
    [InlineData("及牛肉义大利面乪40", "高级牛肉义大利面料理食物0")]
    [InlineData("面组'一皇家奶油@，80", "！家奶油义大利面组料理食物0")]
    [InlineData("总利面组丿80皇家奶", "！家奶油义大利面组料理食物0")]
    [InlineData("利面组80皇家奶", "！家奶油义大利面组料理食物0")]
    [InlineData("早晨咖啡020", "·晨咖丨啡料理食物001")]
    [InlineData("早晨日非乪20", "·晨咖丨啡料理食物001")]
    [InlineData("。丿奶油义大利面20", "'山义大利面料理食物001")]
    [InlineData("奶．油义大利面丿20", "'山义大利面料理食物001")]
    [InlineData("鸡排乪30", "鸡捕三料理食物001")]
    [InlineData("夕牛肉义呔利面乪16", "^义大利面料理食物001")]
    [InlineData("少牛肉戈大利面了16", "^义大利面料理食物001")]
    public void BelongsToSlot_tolerates_trade_detail_ocr_noise_seen_in_logs(string rowText, string detailTitle)
    {
        bool owned = InvokeBelongsToSlot(rowText, detailTitle);
        Assert.True(owned);
    }

    [Fact]
    public void BelongsToSlot_returns_false_when_pasta_rows_only_share_generic_suffix()
    {
        bool owned = InvokeBelongsToSlot("：奶油义大利面组乪80", "高级牛肉义大利面料理食物0");
        Assert.False(owned);
    }

    private static bool InvokeBelongsToSlot(string rowText, string detailTitle)
    {
        Type policyType = LoadSleepRunnerAssembly().GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeSlotOwnershipPolicy")
            ?? throw new Xunit.Sdk.XunitException("TradeSlotOwnershipPolicy type was not found.");
        MethodInfo method = policyType.GetMethod(
                                "BelongsToSlot",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeSlotOwnershipPolicy.BelongsToSlot was not found.");

        return (bool)method.Invoke(null, [rowText, detailTitle])!;
    }

    private static Assembly LoadSleepRunnerAssembly()
    {
        string? overridePath = Environment.GetEnvironmentVariable("STAR_SAVIOR_TEST_ASSEMBLY_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var alc = new AssemblyLoadContext($"trade-slot-tests-{Guid.NewGuid():N}", isCollectible: true);
            return alc.LoadFromAssemblyPath(overridePath);
        }

        return Assembly.Load("SleepRunner");
    }
}
