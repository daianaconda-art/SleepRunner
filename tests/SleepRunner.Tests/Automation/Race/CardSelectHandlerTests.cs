using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers;
using SleepRunner.Vision;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class CardSelectHandlerTests
{
    [Fact]
    public void CanHandle_returns_true_when_title_is_only_visible_in_fallback_region()
    {
        using var screenshot = new Mat(new Size(1280, 720), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var handler = new CardSelectHandler(ReadRegion);

        bool canHandle = handler.CanHandle(new FrameContext(screenshot));

        Assert.True(canHandle);

        static string ReadRegion(Mat _, double x, double y, double w, double h)
        {
            return IsRegion(x, y, w, h, 0.01, 0.07, 0.22, 0.12)
                ? "选择奖励"
                : "";
        }
    }

    private static bool IsRegion(
        double actualX,
        double actualY,
        double actualW,
        double actualH,
        double expectedX,
        double expectedY,
        double expectedW,
        double expectedH)
    {
        const double epsilon = 0.0001;
        return Math.Abs(actualX - expectedX) < epsilon &&
               Math.Abs(actualY - expectedY) < epsilon &&
               Math.Abs(actualW - expectedW) < epsilon &&
               Math.Abs(actualH - expectedH) < epsilon;
    }
}
