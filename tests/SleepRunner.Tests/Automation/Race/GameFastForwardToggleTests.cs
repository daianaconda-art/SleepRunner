using System.Reflection;
using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers;
using SleepRunner.Vision;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class GameFastForwardToggleTests
{
    [Fact]
    public void DetectState_returns_off_gray_for_gray_fast_forward_icon()
    {
        using var screenshot = CreateTopBarScreenshot();
        DrawFastForwardIcon(screenshot, new Scalar(145, 145, 145));

        string state = InvokeDetectState(screenshot, out double grayRatio, out double brightRatio);

        Assert.Equal("OffGray", state);
        Assert.True(grayRatio > 0.02, $"Expected gray pixels, got {grayRatio:F3}.");
        Assert.True(brightRatio < 0.02, $"Expected no bright enabled pixels, got {brightRatio:F3}.");
    }

    [Fact]
    public void DetectState_returns_on_bright_for_bright_fast_forward_icon()
    {
        using var screenshot = CreateTopBarScreenshot();
        DrawFastForwardIcon(screenshot, new Scalar(255, 255, 255));

        string state = InvokeDetectState(screenshot, out _, out double brightRatio);

        Assert.Equal("TwoSpeed", state);
        Assert.True(brightRatio > 0.02, $"Expected bright enabled pixels, got {brightRatio:F3}.");
    }

    [Fact]
    public void DetectState_returns_two_speed_when_right_arrow_fills_most_of_probe_box()
    {
        using var screenshot = CreateTopBarScreenshot();
        DrawLeftFastForwardTriangle(screenshot, new Scalar(255, 255, 255));
        DrawRightFastForwardProbeWithShadow(screenshot);

        string state = InvokeDetectState(screenshot, out double grayRatio, out double brightRatio);

        Assert.Equal("TwoSpeed", state);
        Assert.True(grayRatio > 0.01, $"Expected gray edge pixels from the arrow shadow, got {grayRatio:F3}.");
        Assert.True(brightRatio > 0.35, $"Expected bright two-speed pixels, got {brightRatio:F3}.");
    }

    [Fact]
    public void DetectState_returns_one_speed_for_left_bright_right_gray_icon()
    {
        using var screenshot = CreateTopBarScreenshot();
        DrawLeftFastForwardTriangle(screenshot, new Scalar(255, 255, 255));
        DrawRightFastForwardTriangle(screenshot, new Scalar(145, 145, 145));

        string state = InvokeDetectState(screenshot, out double grayRatio, out double brightRatio);

        Assert.Equal("OneSpeed", state);
        Assert.True(grayRatio > 0.01, $"Expected gray second-tier pixels, got {grayRatio:F3}.");
        Assert.True(brightRatio > 0.01, $"Expected bright first-tier pixels, got {brightRatio:F3}.");
    }

    [Fact]
    public void CanHandle_returns_true_for_one_speed_because_target_is_two_speed()
    {
        using var screenshot = CreateTopBarScreenshot();
        DrawLeftFastForwardTriangle(screenshot, new Scalar(255, 255, 255));
        DrawRightFastForwardTriangle(screenshot, new Scalar(145, 145, 145));
        var handler = new GameFastForwardHandler();

        bool canHandle = handler.CanHandle(new FrameContext(screenshot));

        Assert.True(canHandle);
    }

    [Fact]
    public void CanHandle_returns_false_for_two_speed()
    {
        using var screenshot = CreateTopBarScreenshot();
        DrawFastForwardIcon(screenshot, new Scalar(255, 255, 255));
        var handler = new GameFastForwardHandler();

        bool canHandle = handler.CanHandle(new FrameContext(screenshot));

        Assert.False(canHandle);
    }

    [Fact]
    public void CanHandle_returns_false_for_gray_state_after_two_speed_was_seen()
    {
        using var twoSpeedScreenshot = CreateTopBarScreenshot();
        DrawFastForwardIcon(twoSpeedScreenshot, new Scalar(255, 255, 255));
        using var laterScreenshot = CreateTopBarScreenshot();
        DrawFastForwardIcon(laterScreenshot, new Scalar(145, 145, 145));
        var handler = new GameFastForwardHandler();

        Assert.False(handler.CanHandle(new FrameContext(twoSpeedScreenshot)));
        Assert.False(handler.CanHandle(new FrameContext(laterScreenshot)));
    }

    [Fact]
    public void StartupState_completes_after_one_speed_click_action_is_executed()
    {
        object startup = CreateStartupState();

        string action = InvokeStartupObserve(startup, "OneSpeed");

        Assert.Equal("ClickAndComplete", action);
        Assert.False(GetStartupIsComplete(startup));

        InvokeStartupMarkActionExecuted(startup, action);

        Assert.True(GetStartupIsComplete(startup));
        Assert.Equal("None", InvokeStartupObserve(startup, "OneSpeed"));
    }

    [Fact]
    public void StartupState_keeps_checking_after_off_gray_click_action()
    {
        object startup = CreateStartupState();

        string action = InvokeStartupObserve(startup, "OffGray");

        Assert.Equal("Click", action);

        InvokeStartupMarkActionExecuted(startup, action);

        Assert.False(GetStartupIsComplete(startup));
        Assert.Equal("ClickAndComplete", InvokeStartupObserve(startup, "OneSpeed"));
    }

    [Fact]
    public void DetectState_returns_unknown_when_icon_is_absent()
    {
        using var screenshot = CreateTopBarScreenshot();

        string state = InvokeDetectState(screenshot, out double grayRatio, out double brightRatio);

        Assert.Equal("Unknown", state);
        Assert.True(grayRatio < 0.02, $"Expected no gray icon pixels, got {grayRatio:F3}.");
        Assert.True(brightRatio < 0.02, $"Expected no bright icon pixels, got {brightRatio:F3}.");
    }

    [Fact]
    public void DetectState_returns_unknown_for_uniform_gray_region_without_icon_shape()
    {
        using var screenshot = CreateTopBarScreenshot();
        DrawFastForwardArea(screenshot, new Scalar(125, 125, 125));

        string state = InvokeDetectState(screenshot, out double grayRatio, out _);

        Assert.Equal("Unknown", state);
        Assert.True(grayRatio > 0.90, $"Regression setup should mimic full gray false positive, got {grayRatio:F3}.");
    }

    [Fact]
    public void DetectState_returns_unknown_for_uniform_bright_region_without_icon_shape()
    {
        using var screenshot = CreateTopBarScreenshot();
        DrawFastForwardArea(screenshot, new Scalar(255, 255, 255));

        string state = InvokeDetectState(screenshot, out _, out double brightRatio);

        Assert.Equal("Unknown", state);
        Assert.True(brightRatio > 0.90, $"Regression setup should mimic full bright false positive, got {brightRatio:F3}.");
    }

    [Fact]
    public void CanHandle_returns_false_for_uniform_gray_region_without_icon_shape()
    {
        using var screenshot = CreateTopBarScreenshot();
        DrawFastForwardArea(screenshot, new Scalar(125, 125, 125));
        var handler = new GameFastForwardHandler();

        bool canHandle = handler.CanHandle(new FrameContext(screenshot));

        Assert.False(canHandle);
    }

    private static string InvokeDetectState(Mat screenshot, out double grayRatio, out double brightRatio)
    {
        Type toggleType = Type.GetType("SleepRunner.Automation.Race.Handlers.GameFastForwardToggle, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("GameFastForwardToggle type was not found.");
        MethodInfo method = toggleType.GetMethod(
                                "DetectState",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("GameFastForwardToggle.DetectState was not found.");

        object?[] args = [screenshot, 0d, 0d];
        object result = method.Invoke(null, args)!;
        grayRatio = (double)args[1]!;
        brightRatio = (double)args[2]!;
        return result.ToString()!;
    }

    private static object CreateStartupState()
    {
        Type startupType = Type.GetType("SleepRunner.Automation.Race.Handlers.GameFastForwardStartupState, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("GameFastForwardStartupState type was not found.");
        return Activator.CreateInstance(startupType, nonPublic: true)
               ?? throw new Xunit.Sdk.XunitException("GameFastForwardStartupState could not be created.");
    }

    private static string InvokeStartupObserve(object startup, string stateName)
    {
        Type startupType = startup.GetType();
        MethodInfo method = startupType.GetMethod(
                                "Observe",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("GameFastForwardStartupState.Observe was not found.");

        object state = ParseGameFastForwardState(stateName);
        object result = method.Invoke(startup, [state])!;
        return result.ToString()!;
    }

    private static void InvokeStartupMarkActionExecuted(object startup, string actionName)
    {
        Type startupType = startup.GetType();
        MethodInfo method = startupType.GetMethod(
                                "MarkActionExecuted",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("GameFastForwardStartupState.MarkActionExecuted was not found.");

        object action = ParseStartupAction(actionName);
        method.Invoke(startup, [action]);
    }

    private static bool GetStartupIsComplete(object startup)
    {
        PropertyInfo property = startup.GetType().GetProperty(
                                    "IsComplete",
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                ?? throw new Xunit.Sdk.XunitException("GameFastForwardStartupState.IsComplete was not found.");
        return (bool)property.GetValue(startup)!;
    }

    private static object ParseGameFastForwardState(string stateName)
    {
        Type stateType = Type.GetType("SleepRunner.Automation.Race.Handlers.GameFastForwardState, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("GameFastForwardState type was not found.");
        return Enum.Parse(stateType, stateName);
    }

    private static object ParseStartupAction(string actionName)
    {
        Type actionType = Type.GetType("SleepRunner.Automation.Race.Handlers.GameFastForwardStartupAction, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("GameFastForwardStartupAction type was not found.");
        return Enum.Parse(actionType, actionName);
    }

    private static Mat CreateTopBarScreenshot()
    {
        const int width = 2048;
        const int height = 1120;
        var screenshot = new Mat(new Size(width, height), MatType.CV_8UC3, new Scalar(35, 40, 55));
        Cv2.Rectangle(
            screenshot,
            new Rect((int)(width * 0.70), (int)(height * 0.02), (int)(width * 0.26), (int)(height * 0.12)),
            new Scalar(43, 45, 60),
            thickness: -1);
        return screenshot;
    }

    private static void DrawFastForwardIcon(Mat screenshot, Scalar color)
    {
        DrawLeftFastForwardTriangle(screenshot, color);
        DrawRightFastForwardTriangle(screenshot, color);
    }

    private static void DrawFastForwardArea(Mat screenshot, Scalar color)
    {
        DrawRect(screenshot, 0.766, 0.050, 0.044, 0.055, color);
    }

    private static void DrawLeftFastForwardTriangle(Mat screenshot, Scalar color)
    {
        DrawTriangle(screenshot, [(0.772, 0.061), (0.772, 0.088), (0.786, 0.074)], color);
    }

    private static void DrawRightFastForwardTriangle(Mat screenshot, Scalar color)
    {
        DrawTriangle(screenshot, [(0.789, 0.061), (0.789, 0.088), (0.803, 0.074)], color);
    }

    private static void DrawRightFastForwardProbeWithShadow(Mat screenshot)
    {
        DrawRect(screenshot, 0.785, 0.050, 0.025, 0.055, new Scalar(145, 145, 145));
        DrawRect(screenshot, 0.787, 0.050, 0.021, 0.055, new Scalar(255, 255, 255));
    }

    private static void DrawTriangle(Mat screenshot, (double X, double Y)[] points, Scalar color)
    {
        Point[] pixels = points
            .Select(p => new Point((int)(screenshot.Width * p.X), (int)(screenshot.Height * p.Y)))
            .ToArray();
        Cv2.FillConvexPoly(screenshot, pixels, color);
    }

    private static void DrawRect(Mat screenshot, double x, double y, double w, double h, Scalar color)
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
}
