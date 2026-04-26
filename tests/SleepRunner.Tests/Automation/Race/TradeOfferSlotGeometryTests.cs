using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class TradeOfferSlotGeometryTests
{
    [Fact]
    public void OfferSlots_click_text_center_for_current_trade_menu_rows()
    {
        var slots = GetOfferSlots();

        Assert.Equal(3, slots.Length);
        AssertSlotClick(slots[0], expectedX: 0.88, expectedY: 0.425);
        AssertSlotClick(slots[1], expectedX: 0.88, expectedY: 0.525);
        AssertSlotClick(slots[2], expectedX: 0.88, expectedY: 0.626);
    }

    private static object[] GetOfferSlots()
    {
        Type ocrType = Type.GetType("SleepRunner.Automation.Race.Handlers.Trade.TradeDetailOcr, SleepRunner")
            ?? throw new Xunit.Sdk.XunitException("TradeDetailOcr type was not found.");

        FieldInfo field = ocrType.GetField(
                              "OfferSlots",
                              BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                          ?? throw new Xunit.Sdk.XunitException("TradeDetailOcr.OfferSlots field was not found.");

        return ((Array)field.GetValue(null)!).Cast<object>().ToArray();
    }

    private static void AssertSlotClick(object slot, double expectedX, double expectedY)
    {
        Type slotType = slot.GetType();
        double clickX = (double)(slotType.GetField("ClickX")?.GetValue(slot)
            ?? throw new Xunit.Sdk.XunitException("OfferSlotRegion.ClickX field was not found."));
        double clickY = (double)(slotType.GetField("ClickY")?.GetValue(slot)
            ?? throw new Xunit.Sdk.XunitException("OfferSlotRegion.ClickY field was not found."));

        Assert.Equal(expectedX, clickX, 3);
        Assert.Equal(expectedY, clickY, 3);
    }
}
