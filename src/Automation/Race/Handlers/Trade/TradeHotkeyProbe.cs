using OpenCvSharp;
using SleepRunner.Input;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Trade;

internal static class TradeHotkeyProbe
{
    public static async Task RunAsync(GameContext ctx)
    {
        Logger.Log("[Race:TradeHotkeys] Probe start: Alt+1/2/3 detail switching only, no purchase.");

        for (int slotIndex = 0; slotIndex < TradeDetailOcr.OfferSlots.Length; slotIndex++)
        {
            GameActionKey action = GetSelectSlotAction(slotIndex);
            Logger.Log($"[Race:TradeHotkeys] Slot {slotIndex + 1}: sending {action}.");
            bool sent = await ctx.SendGameAction(action);
            Logger.Log($"[Race:TradeHotkeys] Slot {slotIndex + 1}: keySent={sent}.");
            await ctx.Wait(500);

            using var shot = ctx.CaptureScreen();
            if (shot == null || shot.Empty())
            {
                Logger.Log($"[Race:TradeHotkeys] Slot {slotIndex + 1}: capture empty after hotkey.");
                continue;
            }

            LogDetailState(shot, slotIndex);
        }

        Logger.Log("[Race:TradeHotkeys] Probe complete.");
    }

    internal static GameActionKey GetSelectSlotAction(int slotIndex)
    {
        return slotIndex switch
        {
            0 => GameActionKey.TradeSelectSlot1,
            1 => GameActionKey.TradeSelectSlot2,
            2 => GameActionKey.TradeSelectSlot3,
            _ => throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Trade slot index must be 0, 1, or 2."),
        };
    }

    private static void LogDetailState(Mat shot, int slotIndex)
    {
        bool ready = TradeBuyActions.IsOfferDetailReady(shot);
        string rowText = TradeDetailOcr.ReadRowSlotText(shot, slotIndex);
        string detailTitle = TradeDetailOcr.ReadDetailTitleText(shot);
        bool owned = ready && TradeBuyActions.IsCurrentDetailOwnedBySlot(shot, slotIndex);

        int price = 0;
        string effect = "";
        if (ready)
        {
            TradeOffer offer = TradePurchasePolicy.BuildOfferFromShot(shot, slotIndex);
            price = offer.Price;
            effect = offer.EffectText;
        }

        Logger.Log(
            $"[Race:TradeHotkeys] Slot {slotIndex + 1}: ready={ready}, owned={owned}, " +
            $"price={price}, row='{rowText}', detail='{detailTitle}', effect='{effect}'.");
    }
}
