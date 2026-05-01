using System.Reflection;
using OpenCvSharp;
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

    [Fact]
    public void TryBuildCoinAnchoredMoneyRect_finds_top_hud_money_next_to_gold_coin()
    {
        using var screenshot = new Mat(new Size(2559, 1440), MatType.CV_8UC3, new Scalar(24, 48, 70));
        Cv2.Rectangle(screenshot, new Rect(1390, 60, 280, 65), new Scalar(35, 48, 70), -1);
        Cv2.Circle(screenshot, new Point(1520, 86), 18, new Scalar(30, 180, 235), -1);

        var (success, rect) = InvokeTryBuildCoinAnchoredMoneyRect(screenshot);

        Assert.True(success);
        Assert.True(rect.X > 1520);
        Assert.True(rect.Y < 120);
        Assert.True(rect.Width >= 50);
    }

    private static (bool Success, Rect Rect) InvokeTryBuildCoinAnchoredMoneyRect(Mat screenshot)
    {
        Type helperType = Type.GetType("SleepRunner.Automation.Race.HudMoneyOcr, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("HudMoneyOcr type was not found.");

        MethodInfo method = helperType.GetMethod(
                                "TryBuildCoinAnchoredMoneyRect",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("HudMoneyOcr.TryBuildCoinAnchoredMoneyRect was not found.");

        object?[] args = [screenshot, default(Rect)];
        bool success = (bool)method.Invoke(null, args)!;
        return (success, (Rect)args[1]!);
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
