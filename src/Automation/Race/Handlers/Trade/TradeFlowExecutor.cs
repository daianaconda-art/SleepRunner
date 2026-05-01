using OpenCvSharp;
using SleepRunner.Automation.Race.Handlers.Training;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Trade;

public interface ITradeFlowExecutor
{
    Task<TradeExecutionResult> ExecuteAsync(GameContext ctx);
}

public sealed class DefaultTradeFlowExecutor : ITradeFlowExecutor
{
    private readonly bool _validationOnly;

    public DefaultTradeFlowExecutor(bool validationOnly = false)
    {
        _validationOnly = validationOnly;
    }

    public static async Task DebugScanAsync(GameContext ctx)
    {
        using var initialShot = ctx.CaptureScreen();
        if (initialShot == null || initialShot.Empty())
        {
            Logger.Log("[Race:Trade] Trade debug: capture empty.");
            return;
        }

        int budget = TradeDetailOcr.ReadCurrentMoney(initialShot);
        Logger.Log($"[Race:Trade] Trade debug: detected budget={budget}");
        int? strengthStat = NormalizeStat(await TrainingPowerStat.ReadPowerStatAsync(initialShot));
        int? staminaStat = NormalizeStat(await TrainingPowerStat.ReadStaminaStatAsync(initialShot));
        Logger.Log($"[Race:Trade] Trade debug: stats strength={FormatStat(strengthStat)}, stamina={FormatStat(staminaStat)}");

        var offers = new List<TradeOffer>();
        for (int i = 0; i < TradeDetailOcr.OfferSlots.Length; i++)
        {
            var offer = await ScanOfferWithRetriesAsync(ctx, i, TradeDetailOcr.OfferSlots[i], detailMayAlreadyBeOpen: false);
            if (offer == null)
            {
                Logger.Log($"[Race:Trade] Trade debug: slot {i + 1} unreadable.");
                continue;
            }

            Logger.Log(
                $"[Race:Trade] Trade debug slot {i + 1}: price={offer.Price}, strength={offer.StrengthGain}, " +
                $"stamina={offer.StaminaRecover}, mustBuy={offer.IsMustBuy}, potential={offer.IsPotentialPoint}, " +
                $"buyVisible={offer.HasBuyButtonVisible}, buyDisabled={offer.IsBuyDisabled}, " +
                $"slot='{offer.SlotText}', effect='{offer.EffectText}'");
            offers.Add(offer);
        }

        var queue = TradePurchasePolicy.BuildPurchaseQueue(
            offers,
            RaceUserPolicy.TradePreferStrengthItems,
            int.MaxValue,
            strengthStat,
            staminaStat);

        if (queue.Count == 0)
        {
            Logger.Log("[Race:Trade] Trade debug queue: <empty>");
            return;
        }

        for (int i = 0; i < queue.Count; i++)
        {
            var q = queue[i];
            string reason = q.IsPotentialPoint
                ? "potential-points must-buy"
                : q.IsMustBuy
                    ? "must-buy whitelist"
                    : "attack-build strength item";
            Logger.Log(
                $"[Race:Trade] Trade debug queue[{i + 1}]: slot={q.SlotIndex + 1}, price={q.Price}, " +
                $"strength={q.StrengthGain}, reason={reason}, slot='{q.SlotText}', effect='{q.EffectText}'");
        }
    }

    public async Task<TradeExecutionResult> ExecuteAsync(GameContext ctx)
    {
        using var initialShot = ctx.CaptureScreen();
        if (initialShot == null || initialShot.Empty())
        {
            Logger.Log("[Race:Trade] Trade executor: capture empty, skip.");
            return TradeExecutionResult.ManualRequired;
        }

        int detectedBudget = TradeDetailOcr.ReadCurrentMoney(initialShot);
        int budget = TradeBudgetPolicy.ResolveExecutionBudget(detectedBudget);
        if (budget == int.MaxValue)
            Logger.Log("[Race:Trade] Trade executor: budget OCR failed, continue with unknown-budget fallback.");
        else
            Logger.Log($"[Race:Trade] Trade executor: detected budget={budget}");

        int? strengthStat = NormalizeStat(await TrainingPowerStat.ReadPowerStatAsync(initialShot));
        int? staminaStat = NormalizeStat(await TrainingPowerStat.ReadStaminaStatAsync(initialShot));
        Logger.Log($"[Race:Trade] Trade executor: stats strength={FormatStat(strengthStat)}, stamina={FormatStat(staminaStat)}");

        int remainingBudget = budget;
        bool boughtAny = false;

        for (int slotIndex = 0; slotIndex < TradeDetailOcr.OfferSlots.Length; slotIndex++)
        {
            var offer = await ScanOfferWithRetriesAsync(
                ctx,
                slotIndex,
                TradeDetailOcr.OfferSlots[slotIndex],
                detailMayAlreadyBeOpen: false);

            if (offer == null)
            {
                Logger.Log($"[Race:Trade] Trade executor: slot {slotIndex + 1} unreadable, continue.");
                continue;
            }

            if (remainingBudget != int.MaxValue &&
                remainingBudget > 0 &&
                remainingBudget <= 5 &&
                offer.HasBuyButtonVisible &&
                !offer.IsBuyDisabled &&
                offer.Price > remainingBudget)
            {
                Logger.Log(
                    $"[Race:Trade] Trade executor: budget={remainingBudget} suspicious for slot {slotIndex + 1} " +
                    $"(button alive, price={offer.Price}), fallback to no-budget filter.");
                remainingBudget = int.MaxValue;
            }

            LogOfferEvaluation(offer);
            bool shouldBuy = TradePurchasePolicy.ShouldBuyOffer(
                offer,
                RaceUserPolicy.TradePreferStrengthItems,
                remainingBudget,
                strengthStat,
                staminaStat);
            Logger.Log($"[Race:Trade] Trade executor: slot {slotIndex + 1} strategy={(shouldBuy ? "buy" : "skip")}.");
            if (!shouldBuy)
            {
                continue;
            }

            TradeOffer activeOffer = offer;
            TradeBuyabilityState buyability = TradeBuyActions.ReadBuyabilityState(activeOffer);
            if (buyability == TradeBuyabilityState.NoButton)
            {
                var refreshed = await RefreshOfferForBuyabilityAsync(ctx, slotIndex, TradeDetailOcr.OfferSlots[slotIndex]);
                if (refreshed != null)
                {
                    activeOffer = refreshed;
                    buyability = TradeBuyActions.ReadBuyabilityState(activeOffer);
                }
            }

            Logger.Log($"[Race:Trade] Trade executor: slot {slotIndex + 1} buyability={buyability}.");
            if (buyability != TradeBuyabilityState.Enabled)
                continue;

            if (_validationOnly)
            {
                Logger.Log($"[Race:Trade] Trade executor: validation-only, would buy slot {slotIndex + 1} (price={activeOffer.Price}).");
                continue;
            }

            Logger.Log(
                $"[Race:Trade] Trade executor decision: choose slot={activeOffer.SlotIndex + 1}, price={activeOffer.Price}, " +
                $"gain={activeOffer.StrengthGain}, reason={(activeOffer.IsPotentialPoint ? "potential-points must-buy" : activeOffer.IsMustBuy ? "must-buy whitelist" : "attack-build strength item")}.");

            if (!await TradeBuyActions.TryClickBuyAsync(ctx, activeOffer, remainingBudget))
            {
                Logger.Log($"[Race:Trade] Trade executor: purchase not confirmed for slot {slotIndex + 1}, continue next slot.");
                continue;
            }

            await TradeBuyActions.TryClickConfirmAsync(ctx);
            await ctx.Wait(800);

            boughtAny = true;
            if (remainingBudget != int.MaxValue)
                remainingBudget = Math.Max(0, remainingBudget - activeOffer.Price);

            Logger.Log($"[Race:Trade] Trade executor: slot {slotIndex + 1} purchase succeeded.");
        }

        return boughtAny ? TradeExecutionResult.Purchased : TradeExecutionResult.NoPurchase;
    }

    private static void LogOfferEvaluation(TradeOffer offer)
    {
        var reasons = new List<string>();
        if (!offer.HasBuyButtonVisible) reasons.Add("no-buy-button");
        if (offer.IsBuyDisabled) reasons.Add("buy-disabled");
        if (offer.Price <= 0) reasons.Add("no-price");
        if (offer.IsPotentialPoint) reasons.Add("potential-points");
        else if (offer.IsMustBuy) reasons.Add("must-buy");
        if (offer.IsStrengthIncrease) reasons.Add($"strength+{offer.StrengthGain}");
        if (reasons.Count == 0) reasons.Add("no-signal");
        Logger.Log($"[Race:Trade] Trade executor evaluate slot {offer.SlotIndex + 1}: {string.Join(", ", reasons)}");
    }

    private static int? NormalizeStat(int value)
    {
        return value >= 0 ? value : null;
    }

    private static string FormatStat(int? value)
    {
        return value?.ToString() ?? "N/A";
    }

    private static async Task<TradeOffer?> ScanOfferWithRetriesAsync(
        GameContext ctx,
        int slotIndex,
        TradeDetailOcr.OfferSlotRegion slot,
        bool detailMayAlreadyBeOpen)
    {
        const int maxAttempts = 2;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            Mat? detailShot = null;
            {
                var current = ctx.CaptureScreen();
                if (current != null && !current.Empty())
                {
                    bool currentReady = TradeBuyActions.IsOfferDetailReady(current);
                    string currentRowText = TradeDetailOcr.ReadRowSlotText(current, slotIndex);
                    string currentDetailTitle = TradeDetailOcr.ReadDetailTitleText(current);
                    bool currentOwned = currentReady && TradeBuyActions.IsCurrentDetailOwnedBySlot(current, slotIndex);
                    Logger.Log(
                        $"[Race:Trade] Trade offer[{slotIndex + 1}]: current precheck ready={currentReady}, owned={currentOwned}, " +
                        $"row='{currentRowText}', detail='{currentDetailTitle}', attempt={attempt}.");
                    if (currentReady && currentOwned)
                    {
                        detailShot = current;
                        Logger.Log($"[Race:Trade] Trade offer[{slotIndex + 1}]: current detail already belongs to this slot on attempt {attempt}.");
                    }
                    else
                    {
                        current.Dispose();
                    }
                }
            }

            if (detailShot == null && detailMayAlreadyBeOpen)
            {
                var current = ctx.CaptureScreen();
                if (current != null &&
                    !current.Empty() &&
                    TradeBuyActions.IsOfferDetailReady(current) &&
                    TradeBuyActions.IsCurrentDetailOwnedBySlot(current, slotIndex))
                {
                    detailShot = current;
                    Logger.Log($"[Race:Trade] Trade offer[{slotIndex + 1}]: reuse already-open detail on attempt {attempt}.");
                }
                else
                {
                    current?.Dispose();
                }
            }

            detailShot ??= await TradeBuyActions.TryOpenOfferDetailAsync(
                ctx,
                slotIndex,
                slot,
                detailMayAlreadyBeOpen,
                requireOwnedAfterClick: false);
            using (detailShot)
            {
                if (detailShot == null || detailShot.Empty())
                {
                    Logger.Log($"[Race:Trade] Trade offer[{slotIndex + 1}]: detail not expanded (attempt={attempt}).");
                    continue;
                }

                using var freshShot = ctx.CaptureScreen();
                if (freshShot == null || freshShot.Empty())
                {
                    Logger.Log($"[Race:Trade] Trade offer[{slotIndex + 1}]: fresh capture empty after detail open (attempt={attempt}).");
                    continue;
                }

                if (!TradeBuyActions.IsOfferDetailReady(freshShot))
                {
                    Logger.Log($"[Race:Trade] Trade offer[{slotIndex + 1}]: fresh capture no longer shows detail ready (attempt={attempt}).");
                    continue;
                }

                bool detailOwnedBySlot = TradeBuyActions.IsCurrentDetailOwnedBySlot(freshShot, slotIndex);
                var offer = TradePurchasePolicy.BuildOfferFromShot(freshShot, slotIndex);
                bool strongUnownedDetail = !detailOwnedBySlot && TradePurchasePolicy.IsStrongDetailPurchaseCandidate(offer);
                if (!detailOwnedBySlot && !strongUnownedDetail)
                {
                    Logger.Log($"[Race:Trade] Trade offer[{slotIndex + 1}]: fresh capture belongs to another slot (attempt={attempt}).");
                    continue;
                }

                if (strongUnownedDetail)
                {
                    string rowText = TradeDetailOcr.ReadRowSlotText(freshShot, slotIndex);
                    string detailTitle = TradeDetailOcr.ReadDetailTitleText(freshShot);
                    Logger.Log(
                        $"[Race:Trade] Trade offer[{slotIndex + 1}]: ownership uncertain but strong detail signal accepted " +
                        $"(row='{rowText}', detail='{detailTitle}', price={offer.Price}, effect='{offer.EffectText}').");
                }

                if (TradePurchasePolicy.IsOfferReadable(offer))
                {
                    Logger.Log($"[Race:Trade] Trade offer[{slotIndex + 1}]: read success on attempt {attempt}.");
                    return offer;
                }

                Logger.Log(
                    $"[Race:Trade] Trade offer[{slotIndex + 1}]: low-confidence read (attempt={attempt}), " +
                    $"slot='{offer.SlotText}', effect='{offer.EffectText}'.");
            }

            detailMayAlreadyBeOpen = true;
            await ctx.Wait(200);
        }

        return null;
    }

    private static async Task<TradeOffer?> RefreshOfferForBuyabilityAsync(
        GameContext ctx,
        int slotIndex,
        TradeDetailOcr.OfferSlotRegion slot)
    {
        const int maxAttempts = 1;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var detailShot = await TradeBuyActions.TryOpenOfferDetailAsync(
                ctx,
                slotIndex,
                slot,
                allowReuseCurrentDetail: false);

            if (detailShot == null || detailShot.Empty())
            {
                Logger.Log($"[Race:Trade] Trade executor: slot {slotIndex + 1} reopen for buyability failed (attempt={attempt}).");
                continue;
            }

            var offer = TradePurchasePolicy.BuildOfferFromShot(detailShot, slotIndex);
            Logger.Log($"[Race:Trade] Trade executor: slot {slotIndex + 1} refreshed buyability on attempt {attempt}.");
            if (TradeBuyActions.ReadBuyabilityState(offer) != TradeBuyabilityState.NoButton)
                return offer;
        }

        using var fallbackShot = ctx.CaptureScreen();
        if (fallbackShot != null && !fallbackShot.Empty())
        {
            string rowText = TradeDetailOcr.ReadRowSlotText(fallbackShot, slotIndex);
            bool soldOut = TradeDetailOcr.IsRowMarkedSoldOut(fallbackShot, slotIndex);
            int price = TradeDetailOcr.ExtractPrice(rowText);
            if (soldOut && TradeDetailOcr.IsReliableSlotText(rowText) && price > 0)
            {
                Logger.Log($"[Race:Trade] Trade offer[{slotIndex + 1}]: row fallback resolved sold-out item '{rowText}' price={price}.");
                return TradePurchasePolicy.BuildOfferFromRowFallback(slotIndex, rowText, price, soldOut: true);
            }
        }

        return null;
    }
}
