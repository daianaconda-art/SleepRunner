using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class HudMoneyOcrTests
{
    [Fact]
    public void TryResolveFromRawRegions_prefers_clean_primary_region_over_noisy_backup()
    {
        var (success, money) = InvokeTryResolveFromRawRegions(["LOG", "0337", "BEST02870"]);

        Assert.True(success);
        Assert.Equal(337, money);
    }

    [Fact]
    public void TryResolveFromRawRegions_salvages_backup_region_when_primary_is_missing()
    {
        var (success, money) = InvokeTryResolveFromRawRegions(["LDG", "0", "BEST02300"]);

        Assert.True(success);
        Assert.Equal(230, money);
    }

    [Fact]
    public void TryResolveFromRawRegions_returns_false_when_all_regions_are_unreadable()
    {
        var (success, money) = InvokeTryResolveFromRawRegions(["", "0", "BEsr"]);

        Assert.False(success);
        Assert.Equal(0, money);
    }

    private static (bool Success, int Money) InvokeTryResolveFromRawRegions(string[] raws)
    {
        Type helperType = Type.GetType("SleepRunner.Automation.Race.HudMoneyOcr, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("HudMoneyOcr type was not found.");

        MethodInfo method = helperType.GetMethod(
                                "TryResolveFromRawRegions",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("HudMoneyOcr.TryResolveFromRawRegions was not found.");

        object?[] args = [raws, 0];
        bool success = (bool)method.Invoke(null, args)!;
        return (success, (int)args[1]!);
    }
}
