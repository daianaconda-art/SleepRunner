using System.Reflection;
using OpenCvSharp;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeStageOcrTests
{
    [Fact]
    public void IsTradeScreen_returns_true_when_rows_show_sold_out_stamps()
    {
        using var screenshot = new Mat(new Size(2559, 1440), MatType.CV_8UC3, new Scalar(30, 45, 55));

        // The trade screen can have no enabled buy button after all items are sold.
        // A red SOLD OUT stamp in a row is still a strong structural trade signal.
        Cv2.Rectangle(screenshot, new Rect(1900, 535, 230, 95), new Scalar(20, 20, 210), -1);

        bool isTradeScreen = InvokeIsTradeScreen(screenshot);

        Assert.True(isTradeScreen);
    }

    private static bool InvokeIsTradeScreen(Mat screenshot)
    {
        Type helperType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeStageOcr, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeStageOcr type was not found.");

        MethodInfo method = helperType.GetMethod(
                                "IsTradeScreen",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeStageOcr.IsTradeScreen was not found.");

        return (bool)method.Invoke(null, [screenshot])!;
    }
}
