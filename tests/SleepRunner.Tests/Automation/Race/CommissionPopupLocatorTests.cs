using System.Reflection;
using OpenCvSharp;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class CommissionPopupLocatorTests
{
    [Fact]
    public void TryLocateSkipButton_finds_left_skip_button()
    {
        using var screenshot = CreatePopupScreenshot();
        DrawBlueButton(screenshot, 0.36, 0.675, 0.14, 0.07);

        bool located = InvokeTryLocateSkipButton(screenshot, out Point center, out double blueRatio);

        Assert.True(located);
        Assert.InRange(center.X / (double)screenshot.Width, 0.42, 0.44);
        Assert.InRange(center.Y / (double)screenshot.Height, 0.70, 0.72);
        Assert.True(blueRatio > 0.90, $"Expected a solid blue button, got ratio={blueRatio:F3}.");
    }

    [Fact]
    public void TryLocateSkipButton_finds_right_confirmation_skip_button()
    {
        using var screenshot = CreatePopupScreenshot();
        DrawLightButton(screenshot, 0.36, 0.675, 0.14, 0.07);
        DrawBlueButton(screenshot, 0.505, 0.675, 0.14, 0.07);

        bool located = InvokeTryLocateSkipButton(screenshot, out Point center, out double blueRatio);

        Assert.True(located);
        Assert.InRange(center.X / (double)screenshot.Width, 0.565, 0.585);
        Assert.InRange(center.Y / (double)screenshot.Height, 0.70, 0.72);
        Assert.True(blueRatio > 0.90, $"Expected a solid blue button, got ratio={blueRatio:F3}.");
    }

    [Fact]
    public void TryLocateSkipButton_finds_higher_right_confirmation_skip_button()
    {
        using var screenshot = CreatePopupScreenshot();
        DrawLightButton(screenshot, 0.36, 0.605, 0.14, 0.07);
        DrawBlueButton(screenshot, 0.505, 0.605, 0.14, 0.07);

        bool located = InvokeTryLocateSkipButton(screenshot, out Point center, out double blueRatio);

        Assert.True(located);
        Assert.InRange(center.X / (double)screenshot.Width, 0.565, 0.585);
        Assert.InRange(center.Y / (double)screenshot.Height, 0.63, 0.65);
        Assert.True(blueRatio > 0.90, $"Expected a solid blue button, got ratio={blueRatio:F3}.");
    }

    private static Mat CreatePopupScreenshot()
    {
        const int width = 2048;
        const int height = 1120;
        var screenshot = new Mat(new Size(width, height), MatType.CV_8UC3, new Scalar(18, 18, 18));
        Cv2.Rectangle(
            screenshot,
            new Rect((int)(width * 0.24), (int)(height * 0.35), (int)(width * 0.52), (int)(height * 0.40)),
            new Scalar(245, 245, 245),
            thickness: -1);
        return screenshot;
    }

    private static void DrawBlueButton(Mat screenshot, double x, double y, double w, double h)
    {
        DrawButton(screenshot, x, y, w, h, new Scalar(214, 133, 45));
    }

    private static void DrawLightButton(Mat screenshot, double x, double y, double w, double h)
    {
        DrawButton(screenshot, x, y, w, h, new Scalar(245, 245, 245));
    }

    private static void DrawButton(Mat screenshot, double x, double y, double w, double h, Scalar color)
    {
        Cv2.Rectangle(
            screenshot,
            new Rect(
                (int)(screenshot.Width * x),
                (int)(screenshot.Height * y),
                Math.Max(1, (int)(screenshot.Width * w)),
                Math.Max(1, (int)(screenshot.Height * h))),
            color,
            thickness: -1);
    }

    private static bool InvokeTryLocateSkipButton(Mat screenshot, out Point center, out double blueRatio)
    {
        Type locatorType = Type.GetType("SleepRunner.Automation.Race.Handlers.CommissionPopupLocator, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("CommissionPopupLocator type was not found.");
        MethodInfo method = locatorType.GetMethod(
                                "TryLocateSkipButton",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("CommissionPopupLocator.TryLocateSkipButton was not found.");

        object?[] args = [screenshot, default(Rect), default(Point), 0d];
        bool located = (bool)method.Invoke(null, args)!;
        center = (Point)args[2]!;
        blueRatio = (double)args[3]!;
        return located;
    }
}
