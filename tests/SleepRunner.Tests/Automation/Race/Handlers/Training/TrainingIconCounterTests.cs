using System.Reflection;
using OpenCvSharp;
using Xunit;

namespace SleepRunner.Tests.Automation.Race.Handlers.Training;

public class TrainingIconCounterTests
{
    [Fact]
    public void CountCircularIcons_fills_one_missed_middle_slot_when_later_icons_are_present()
    {
        using var screenshot = new Mat(new Size(1000, 1000), MatType.CV_8UC3, new Scalar(30, 30, 30));
        DrawGrayIcon(screenshot, slot: 0);
        DrawFaintGrayIcon(screenshot, slot: 1);
        DrawGrayIcon(screenshot, slot: 2);
        DrawGrayIcon(screenshot, slot: 3);

        int count = CountCircularIcons(screenshot);

        Assert.Equal(4, count);
    }

    [Fact]
    public void CountCircularIcons_stops_after_two_icons_when_next_slot_is_bright_non_circular_noise()
    {
        using var screenshot = new Mat(new Size(1000, 1000), MatType.CV_8UC3, new Scalar(30, 30, 30));
        DrawGrayIcon(screenshot, slot: 0);
        DrawGrayIcon(screenshot, slot: 1);
        DrawBrightVerticalNoise(screenshot, slot: 2);

        int count = CountCircularIcons(screenshot);

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountCircularIcons_stops_after_two_icons_when_next_slot_is_colored_non_circular_noise()
    {
        using var screenshot = new Mat(new Size(1000, 1000), MatType.CV_8UC3, new Scalar(30, 30, 30));
        DrawGrayIcon(screenshot, slot: 0);
        DrawGrayIcon(screenshot, slot: 1);
        DrawColoredHorizontalNoise(screenshot, slot: 2);

        int count = CountCircularIcons(screenshot);

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountCircularIcons_stops_after_two_icons_when_next_slot_is_colored_diagonal_noise()
    {
        using var screenshot = new Mat(new Size(1000, 1000), MatType.CV_8UC3, new Scalar(30, 30, 30));
        DrawGrayIcon(screenshot, slot: 0);
        DrawGrayIcon(screenshot, slot: 1);
        DrawColoredDiagonalNoise(screenshot, slot: 2);

        int count = CountCircularIcons(screenshot);

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountCircularIcons_stops_after_two_icons_when_next_slot_is_gray_diagonal_noise()
    {
        using var screenshot = new Mat(new Size(1000, 1000), MatType.CV_8UC3, new Scalar(30, 30, 30));
        DrawGrayIcon(screenshot, slot: 0);
        DrawGrayIcon(screenshot, slot: 1);
        DrawGrayDiagonalNoise(screenshot, slot: 2);

        int count = CountCircularIcons(screenshot);

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountCircularIcons_stops_tail_after_two_non_icon_slots_before_later_noise()
    {
        using var screenshot = new Mat(new Size(1000, 1000), MatType.CV_8UC3, new Scalar(30, 30, 30));
        DrawGrayIcon(screenshot, slot: 0);
        DrawGrayIcon(screenshot, slot: 1);
        DrawFaintGrayIcon(screenshot, slot: 3);
        DrawFaintGrayIcon(screenshot, slot: 4);
        DrawGrayIcon(screenshot, slot: 6);

        int count = CountCircularIcons(screenshot);

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountCircularIcons_does_not_count_a_missed_slot_at_the_tail()
    {
        using var screenshot = new Mat(new Size(1000, 1000), MatType.CV_8UC3, new Scalar(30, 30, 30));
        DrawGrayIcon(screenshot, slot: 0);
        DrawGrayIcon(screenshot, slot: 1);
        DrawFaintGrayIcon(screenshot, slot: 2);

        int count = CountCircularIcons(screenshot);

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountCircularIcons_ignores_early_strength_transition_hair_at_slot_three()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "training_strength_early_slots_synthetic.png");
        using var screenshot = Cv2.ImRead(fixturePath);

        int count = CountCircularIcons(screenshot);

        Assert.Equal(2, count);
    }

    private static int CountCircularIcons(Mat screenshot)
    {
        Type type = Type.GetType("SleepRunner.Automation.Race.Handlers.Training.TrainingIconCounter, SleepRunner")
                    ?? throw new Xunit.Sdk.XunitException("TrainingIconCounter type was not found.");
        MethodInfo method = type.GetMethod("CountCircularIcons", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TrainingIconCounter.CountCircularIcons was not found.");
        return Assert.IsType<int>(method.Invoke(null, [screenshot, null]));
    }

    private static void DrawGrayIcon(Mat screenshot, int slot)
    {
        var center = SlotCenter(screenshot, slot);
        int radius = (int)(screenshot.Width * 0.014);
        Cv2.Circle(screenshot, center, radius, new Scalar(225, 225, 225), thickness: -1);
        Cv2.Circle(screenshot, center, Math.Max(2, radius / 3), new Scalar(130, 130, 130), thickness: -1);
    }

    private static void DrawFaintGrayIcon(Mat screenshot, int slot)
    {
        var center = SlotCenter(screenshot, slot);
        int radius = (int)(screenshot.Width * 0.014);
        Cv2.Circle(screenshot, center, radius, new Scalar(170, 170, 170), thickness: -1);
        Cv2.Circle(screenshot, center, Math.Max(2, radius / 3), new Scalar(100, 100, 100), thickness: -1);
    }

    private static void DrawBrightVerticalNoise(Mat screenshot, int slot)
    {
        var center = SlotCenter(screenshot, slot);
        int halfSize = (int)(screenshot.Width * 0.015);
        var rect = new Rect(center.X - halfSize, center.Y - halfSize, halfSize, halfSize * 2);
        Cv2.Rectangle(screenshot, rect, new Scalar(245, 245, 245), thickness: -1);
    }

    private static void DrawColoredHorizontalNoise(Mat screenshot, int slot)
    {
        var center = SlotCenter(screenshot, slot);
        int halfWidth = (int)(screenshot.Width * 0.015);
        int halfHeight = (int)(screenshot.Width * 0.007);

        Cv2.Rectangle(
            screenshot,
            new Rect(center.X - halfWidth, center.Y - halfHeight, halfWidth, halfHeight * 2),
            new Scalar(255, 210, 40),
            thickness: -1);
        Cv2.Rectangle(
            screenshot,
            new Rect(center.X, center.Y - halfHeight, halfWidth, halfHeight * 2),
            new Scalar(45, 45, 45),
            thickness: -1);
    }

    private static void DrawColoredDiagonalNoise(Mat screenshot, int slot)
    {
        var center = SlotCenter(screenshot, slot);
        int halfSize = (int)(screenshot.Width * 0.015);
        Cv2.Line(
            screenshot,
            new Point(center.X - halfSize, center.Y + halfSize),
            new Point(center.X + halfSize, center.Y - halfSize),
            new Scalar(230, 170, 80),
            thickness: halfSize + halfSize / 3);
    }

    private static void DrawGrayDiagonalNoise(Mat screenshot, int slot)
    {
        var center = SlotCenter(screenshot, slot);
        int halfSize = (int)(screenshot.Width * 0.015);
        Cv2.Line(
            screenshot,
            new Point(center.X - halfSize, center.Y + halfSize),
            new Point(center.X + halfSize, center.Y - halfSize),
            new Scalar(210, 210, 210),
            thickness: halfSize + halfSize / 3);
    }

    private static Point SlotCenter(Mat screenshot, int slot)
    {
        const double iconCenterX = 0.73;
        const double iconStartY = 0.13;
        const double iconSpacing = 0.08;
        return new Point(
            (int)(screenshot.Width * iconCenterX),
            (int)(screenshot.Height * (iconStartY + slot * iconSpacing)));
    }
}
