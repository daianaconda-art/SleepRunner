using System.Reflection;
using OpenCvSharp;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeStageOcrTests
{
    [Fact]
    public void ScoreOptionText_treats_trade_arrival_hint_as_trade_option()
    {
        string text = "\u5168\u65b0\u5546\u54c1\u5230\u8d27\uff01";

        int score = InvokeScoreOptionText(text, isTrade: true);

        Assert.True(score >= 3);
    }

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

    [Fact]
    public void HasRowSoldOutStamp_returns_true_for_red_row_stamp()
    {
        using var screenshot = new Mat(new Size(2559, 1440), MatType.CV_8UC3, new Scalar(30, 45, 55));
        Cv2.Rectangle(screenshot, new Rect(1900, 535, 230, 95), new Scalar(20, 20, 210), -1);

        bool soldOut = InvokeHasRowSoldOutStamp(screenshot, slotIndex: 0);

        Assert.True(soldOut);
    }

    [Fact]
    public void IsAppraiseTradeStageMenuText_accepts_commission_only_menu_ocr()
    {
        bool hit = InvokeIsAppraiseTradeStageMenuText(
            "\u8bc4\u9274\u6218D-DAY",
            "\u8ba8\u4f10\u8bc4\u9274\u6218");

        Assert.True(hit);
    }

    [Fact]
    public void IsAppraiseTradeStageMenuText_rejects_prepare_detail_text()
    {
        bool hit = InvokeIsAppraiseTradeStageMenuText(
            "\u8bc4\u9274\u6218D-DAY",
            "\u51fa\u51fb\u524d\u51c6\u5907\u6311\u6218\u8ba8\u4f10\u8bc4\u9274\u6218");

        Assert.False(hit);
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

    private static int InvokeScoreOptionText(string text, bool isTrade)
    {
        Type helperType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeStageGeometry, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeStageGeometry type was not found.");

        MethodInfo method = helperType.GetMethod(
                                "ScoreOptionText",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeStageGeometry.ScoreOptionText was not found.");

        return (int)method.Invoke(null, [text, isTrade])!;
    }

    private static bool InvokeIsAppraiseTradeStageMenuText(string titleText, string menuText)
    {
        Type helperType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeStageOcr, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeStageOcr type was not found.");

        MethodInfo method = helperType.GetMethod(
                                "IsAppraiseTradeStageMenuText",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeStageOcr.IsAppraiseTradeStageMenuText was not found.");

        return (bool)method.Invoke(null, [titleText, menuText])!;
    }

    private static bool InvokeHasRowSoldOutStamp(Mat screenshot, int slotIndex)
    {
        Type helperType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeDetailOcr, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeDetailOcr type was not found.");

        MethodInfo method = helperType.GetMethod(
                                "HasRowSoldOutStamp",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("TradeDetailOcr.HasRowSoldOutStamp was not found.");

        return (bool)method.Invoke(null, [screenshot, slotIndex])!;
    }
}
