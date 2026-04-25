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
