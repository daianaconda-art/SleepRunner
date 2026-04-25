using System.Reflection;
using OpenCvSharp;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class CardSelectPlannerTests
{
    [Fact]
    public void BuildAttemptOrder_moves_quantity_capped_cards_after_available_candidates()
    {
        int[] order = InvokeBuildAttemptOrder(
            [
                "持有数量达到上限受诅咒的香炉",
                "毒性鳞片",
                "逃生玩具",
            ],
            [0, 1, 2]);

        Assert.Equal([1, 2, 0], order);
    }

    [Fact]
    public void BuildAttemptOrder_preserves_preferred_order_for_available_cards()
    {
        int[] order = InvokeBuildAttemptOrder(
            [
                "第一张",
                "数量达到上限第二张",
                "第三张",
            ],
            [2, 1, 0]);

        Assert.Equal([2, 0, 1], order);
    }

    [Fact]
    public void IsSelectDoneGrayDisabled_returns_true_for_low_saturation_button()
    {
        using var screenshot = CreateButtonScreenshot(new Scalar(150, 150, 150));

        bool grayDisabled = InvokeIsSelectDoneGrayDisabled(screenshot, out double satMean, out double valMean);

        Assert.True(grayDisabled);
        Assert.True(satMean < 28, $"Expected low saturation, got {satMean:F1}.");
        Assert.InRange(valMean, 55, 230);
    }

    [Fact]
    public void IsSelectDoneGrayDisabled_returns_false_for_bright_blue_button()
    {
        using var screenshot = CreateButtonScreenshot(new Scalar(255, 170, 60));

        bool grayDisabled = InvokeIsSelectDoneGrayDisabled(screenshot, out double satMean, out _);

        Assert.False(grayDisabled);
        Assert.True(satMean > 28, $"Expected high saturation, got {satMean:F1}.");
    }

    private static int[] InvokeBuildAttemptOrder(string[] texts, int[] preferredOrder)
    {
        Type plannerType = GetPlannerType();
        MethodInfo method = plannerType.GetMethod(
                                "BuildAttemptOrder",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("CardSelectPlanner.BuildAttemptOrder was not found.");

        object? result = method.Invoke(null, [texts, preferredOrder]);
        Assert.NotNull(result);
        return ((System.Collections.IEnumerable)result!)
            .Cast<int>()
            .ToArray();
    }

    private static bool InvokeIsSelectDoneGrayDisabled(Mat screenshot, out double satMean, out double valMean)
    {
        Type plannerType = GetPlannerType();
        MethodInfo method = plannerType.GetMethod(
                                "IsSelectDoneGrayDisabled",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("CardSelectPlanner.IsSelectDoneGrayDisabled was not found.");

        object?[] args = [screenshot, 0d, 0d];
        bool grayDisabled = (bool)method.Invoke(null, args)!;
        satMean = (double)args[1]!;
        valMean = (double)args[2]!;
        return grayDisabled;
    }

    private static Type GetPlannerType()
    {
        return Type.GetType("SleepRunner.Automation.Race.Handlers.CardSelectPlanner, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("CardSelectPlanner type was not found.");
    }

    private static Mat CreateButtonScreenshot(Scalar buttonColor)
    {
        const int width = 1000;
        const int height = 1000;
        var screenshot = new Mat(new Size(width, height), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var buttonRect = new Rect(
            (int)(width * 0.42),
            (int)(height * 0.81),
            Math.Max(1, (int)(width * 0.10)),
            Math.Max(1, (int)(height * 0.04)));
        Cv2.Rectangle(screenshot, buttonRect, buttonColor, thickness: -1);
        return screenshot;
    }
}
