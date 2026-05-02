using System.Reflection;
using OpenCvSharp;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class EventFastForwardSettingsScreenChecksTests
{
    [Fact]
    public void IsFastForwardSettingsText_accepts_startup_event_fast_forward_modal()
    {
        bool matched = InvokeIsFastForwardSettingsText(
            "\u4e8b\u4ef6\u5feb\u8f6c\u8bbe\u5b9a",
            "\u4e0d\u5feb\u8f6c \u4ec5\u5feb\u8f6c\u5df2\u89c2\u8d4f\u7684\u4e8b\u4ef6 \u5feb\u8f6c\u6240\u6709\u4e8b\u4ef6",
            "\u51b3\u5b9a");

        Assert.True(matched);
    }

    [Fact]
    public void ShouldAcceptSignals_accepts_log_text_even_when_layout_gate_misses()
    {
        bool matched = InvokeShouldAcceptSignals(
            layoutHit: false,
            titleText: "0\uff0e\u4e8b\u4ef6\u5feb\u8f6c\u8bbe\u5b9a",
            cardText: "0\u5feb\u8f6c\u6240\u6709\u4e8b\u4ef6\u8d4f\u7684\u4e8b\u4ef6\u4e2d\u5feb\u8f6c\u7279\u5b9a\u4e8b\u4ef6\uff1f",
            confirmText: "");

        Assert.True(matched);
    }

    [Fact]
    public void LooksLikeSettingsLayout_accepts_center_dialog_with_three_choice_cards_and_confirm_button()
    {
        using var screenshot = CreateFastForwardSettingsLikeScreenshot();

        bool matched = InvokeLooksLikeSettingsLayout(screenshot);

        Assert.True(matched);
    }

    [Fact]
    public void LooksLikeSettingsLayout_rejects_plain_center_dialog_without_choice_cards()
    {
        using var screenshot = CreatePlainDialogScreenshot();

        bool matched = InvokeLooksLikeSettingsLayout(screenshot);

        Assert.False(matched);
    }

    private static bool InvokeIsFastForwardSettingsText(string titleText, string cardText, string confirmText)
    {
        Type checksType = GetChecksType();
        MethodInfo method = checksType.GetMethod(
                                "IsFastForwardSettingsText",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("EventFastForwardSettingsScreenChecks.IsFastForwardSettingsText was not found.");

        return (bool)method.Invoke(null, [titleText, cardText, confirmText])!;
    }

    private static bool InvokeLooksLikeSettingsLayout(Mat screenshot)
    {
        Type checksType = GetChecksType();
        MethodInfo method = checksType.GetMethod(
                                "LooksLikeSettingsLayout",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("EventFastForwardSettingsScreenChecks.LooksLikeSettingsLayout was not found.");

        return (bool)method.Invoke(null, [screenshot])!;
    }

    private static bool InvokeShouldAcceptSignals(bool layoutHit, string titleText, string cardText, string confirmText)
    {
        Type checksType = GetChecksType();
        MethodInfo method = checksType.GetMethod(
                                "ShouldAcceptSignals",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("EventFastForwardSettingsScreenChecks.ShouldAcceptSignals was not found.");

        return (bool)method.Invoke(null, [layoutHit, titleText, cardText, confirmText])!;
    }

    private static Type GetChecksType()
    {
        return Type.GetType("SleepRunner.Automation.Race.Handlers.Events.EventFastForwardSettingsScreenChecks, SleepRunner")
               ?? throw new Xunit.Sdk.XunitException("EventFastForwardSettingsScreenChecks type was not found.");
    }

    private static Mat CreateFastForwardSettingsLikeScreenshot()
    {
        const int width = 2048;
        const int height = 1120;
        var screenshot = new Mat(new Size(width, height), MatType.CV_8UC3, new Scalar(0, 0, 0));

        DrawRect(screenshot, 0.19, 0.24, 0.62, 0.55, new Scalar(244, 244, 244));
        DrawRect(screenshot, 0.21, 0.38, 0.19, 0.21, new Scalar(210, 240, 220));
        DrawRect(screenshot, 0.41, 0.38, 0.19, 0.21, new Scalar(230, 220, 195));
        DrawRect(screenshot, 0.60, 0.38, 0.19, 0.21, new Scalar(230, 210, 245));
        DrawRect(screenshot, 0.21, 0.59, 0.19, 0.055, new Scalar(220, 135, 40));
        DrawRect(screenshot, 0.41, 0.59, 0.19, 0.055, new Scalar(190, 190, 190));
        DrawRect(screenshot, 0.60, 0.59, 0.19, 0.055, new Scalar(190, 190, 190));
        DrawRect(screenshot, 0.43, 0.72, 0.14, 0.055, new Scalar(220, 135, 40));

        return screenshot;
    }

    private static Mat CreatePlainDialogScreenshot()
    {
        const int width = 2048;
        const int height = 1120;
        var screenshot = new Mat(new Size(width, height), MatType.CV_8UC3, new Scalar(0, 0, 0));

        DrawRect(screenshot, 0.25, 0.25, 0.50, 0.45, new Scalar(244, 244, 244));
        DrawRect(screenshot, 0.43, 0.62, 0.14, 0.055, new Scalar(220, 135, 40));

        return screenshot;
    }

    private static void DrawRect(Mat screenshot, double x, double y, double w, double h, Scalar color)
    {
        int px = (int)(screenshot.Width * x);
        int py = (int)(screenshot.Height * y);
        int pw = Math.Max(1, (int)(screenshot.Width * w));
        int ph = Math.Max(1, (int)(screenshot.Height * h));
        Cv2.Rectangle(screenshot, new Rect(px, py, pw, ph), color, thickness: -1);
    }
}
