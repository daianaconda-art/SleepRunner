using System.Text.RegularExpressions;
using SleepRunner.Automation.Race.Policy;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Trade;

internal static class TradePurchasePolicy
{
    public static readonly string[] MustBuyKeywords = ["抽奖券", "耐力"];

    public static TradeOffer BuildOfferFromShot(OpenCvSharp.Mat detailShot, int slotIndex)
    {
        string rowSlotText = TradeDetailOcr.ReadRowSlotText(detailShot, slotIndex);
        string detailTitle = TradeDetailOcr.ReadDetailTitleText(detailShot);
        return BuildOfferFromShot(detailShot, slotIndex, rowSlotText, detailTitle);
    }

    public static TradeOffer BuildOfferFromShot(OpenCvSharp.Mat detailShot, int slotIndex, string rowSlotText, string detailTitle)
    {
        string slotText = TradeDetailOcr.IsReliableSlotText(detailTitle)
            ? detailTitle
            : rowSlotText;
        bool hasReliableSlotText = TradeDetailOcr.IsReliableSlotText(slotText);
        int price = TradeDetailOcr.ReadOfferPrice(detailShot, slotIndex, rowSlotText);
        string effectText = TradeDetailOcr.ReadEffectText(detailShot);
        int strengthGain = ExtractStrengthGain(effectText);
        bool isStrengthIncrease = IsStrengthIncreaseEffect(effectText, strengthGain);
        bool affectsStrengthStat = TargetsStrengthStat(slotText, effectText, isStrengthIncrease);
        int staminaRecover = ExtractStaminaRecover($"{slotText}{effectText}");
        bool isStaminaRecover = IsStaminaRecoverByKeyword(slotText, effectText) || staminaRecover >= 20;
        bool affectsStaminaStat = TargetsStaminaStat(slotText, effectText, isStaminaRecover);
        bool isPotentialPoint = IsPotentialPointOffer(slotText, effectText);
        bool isMustBuy = isPotentialPoint || IsTradeKeywordOffer(slotText, effectText, hasReliableSlotText) || isStaminaRecover;
        bool scanBuyButton = ShouldScanBuyButtonState(
            hasReliableSlotText,
            isPotentialPoint,
            isMustBuy,
            isStrengthIncrease,
            isStaminaRecover);
        bool hasBuyButtonVisible = scanBuyButton && TradeBuyActions.HasVisibleBuySignal(detailShot);
        bool isBuyDisabled = scanBuyButton && TradeBuyActions.IsBuyButtonGrayDisabled(detailShot);
        bool isRowSoldOut = scanBuyButton
            ? TradeDetailOcr.IsRowMarkedSoldOut(detailShot, slotIndex)
            : TradeDetailOcr.HasRowSoldOutStamp(detailShot, slotIndex);

        var offer = new TradeOffer
        {
            SlotIndex = slotIndex,
            SlotText = slotText,
            HasReliableSlotText = hasReliableSlotText,
            Price = price,
            EffectText = effectText,
            StrengthGain = strengthGain,
            IsStrengthIncrease = isStrengthIncrease,
            AffectsStrengthStat = affectsStrengthStat,
            IsMustBuy = isMustBuy,
            IsPotentialPoint = isPotentialPoint,
            StaminaRecover = staminaRecover,
            IsStaminaRecover = isStaminaRecover,
            AffectsStaminaStat = affectsStaminaStat,
            HasBuyButtonVisible = hasBuyButtonVisible,
            IsBuyDisabled = isBuyDisabled,
            IsRowSoldOut = isRowSoldOut
        };

        Logger.Log($"[Race:Trade] Trade offer[{slotIndex + 1}]: price={price}, strengthGain={strengthGain}, staminaRecover={staminaRecover}, strengthMatch={isStrengthIncrease}, staminaMatch={isStaminaRecover}, affectsStrengthStat={affectsStrengthStat}, affectsStaminaStat={affectsStaminaStat}, potentialPoint={isPotentialPoint}, mustBuy={isMustBuy}, slotReliable={hasReliableSlotText}, buyVisible={hasBuyButtonVisible}, buyDisabled={isBuyDisabled}, rowSoldOut={isRowSoldOut}, slot='{slotText}', effect='{effectText}'");
        return offer;
    }

    public static bool ShouldScanBuyButtonState(
        bool hasReliableSlotText,
        bool isPotentialPoint,
        bool isMustBuy,
        bool isStrengthIncrease,
        bool isStaminaRecover)
    {
        return !hasReliableSlotText ||
               isPotentialPoint ||
               isMustBuy ||
               isStrengthIncrease ||
               isStaminaRecover;
    }

    public static TradeOffer BuildOfferFromRowFallback(int slotIndex, string rowText, int price, bool soldOut)
    {
        string slotText = TradeStageOcr.NormalizeOcr(rowText);
        string effectText = soldOut ? "售罄" : "";
        bool hasReliableSlotText = TradeDetailOcr.IsReliableSlotText(slotText);
        int strengthGain = 0;
        bool isStrengthIncrease = false;
        bool affectsStrengthStat = false;
        int staminaRecover = ExtractStaminaRecover($"{slotText}{effectText}");
        bool isStaminaRecover = IsStaminaRecoverByKeyword(slotText, effectText) || staminaRecover >= 20;
        bool affectsStaminaStat = TargetsStaminaStat(slotText, effectText, isStaminaRecover);
        bool isPotentialPoint = IsPotentialPointOffer(slotText, effectText);
        bool isMustBuy = isPotentialPoint || IsTradeKeywordOffer(slotText, effectText, hasReliableSlotText) || isStaminaRecover;

        return new TradeOffer
        {
            SlotIndex = slotIndex,
            SlotText = slotText,
            HasReliableSlotText = hasReliableSlotText,
            Price = price,
            EffectText = effectText,
            StrengthGain = strengthGain,
            IsStrengthIncrease = isStrengthIncrease,
            AffectsStrengthStat = affectsStrengthStat,
            IsMustBuy = isMustBuy,
            IsPotentialPoint = isPotentialPoint,
            StaminaRecover = staminaRecover,
            IsStaminaRecover = isStaminaRecover,
            AffectsStaminaStat = affectsStaminaStat,
            HasBuyButtonVisible = false,
            IsBuyDisabled = soldOut,
            IsRowSoldOut = soldOut
        };
    }

    public static TradeOffer? TryBuildSoldOutRowFallback(int slotIndex, string rowText, bool rowSoldOut)
    {
        if (!rowSoldOut)
            return null;

        string normalizedRow = TradeStageOcr.NormalizeOcr(rowText);
        int price = TradeDetailOcr.ExtractPrice(normalizedRow);
        if (price <= 0 || !TradeDetailOcr.IsReliableSlotText(normalizedRow))
            return null;

        Logger.Log($"[Race:Trade] Trade offer[{slotIndex + 1}]: sold-out row fallback '{normalizedRow}' price={price}.");
        return BuildOfferFromRowFallback(slotIndex, normalizedRow, price, soldOut: true);
    }

    public static bool IsOfferReadable(TradeOffer offer)
    {
        bool hasSlotText = TradeStageOcr.CountChineseChars(offer.SlotText) >= 2;
        bool hasEffectText = TradeStageOcr.CountChineseChars(offer.EffectText) >= 2;
        bool hasPrice = offer.Price > 0 && offer.Price < 10000;
        bool hasSignal = offer.StrengthGain > 0 || offer.StaminaRecover > 0 || offer.IsMustBuy;
        bool isPurchaseCandidate = offer.HasBuyButtonVisible &&
                                   (offer.IsMustBuy || offer.IsStrengthIncrease || offer.IsStaminaRecover);

        if (!offer.HasReliableSlotText)
            return IsStrongDetailPurchaseCandidate(offer);

        if (isPurchaseCandidate && !hasPrice)
        {
            if (offer.IsBuyDisabled || !offer.HasBuyButtonVisible)
                return false;
            return hasEffectText;
        }

        if (hasPrice && hasEffectText)
            return true;

        return hasSlotText && hasEffectText && (hasPrice || hasSignal);
    }

    public static bool IsStrongDetailPurchaseCandidate(TradeOffer offer)
    {
        bool hasEffectText = TradeStageOcr.CountChineseChars(offer.EffectText) >= 2;
        bool hasPrice = offer.Price > 0 && offer.Price < 10000;
        bool hasEnabledBuyButton = offer.HasBuyButtonVisible && !offer.IsBuyDisabled;
        bool hasPurchaseSignal = offer.IsPotentialPoint ||
                                 offer.IsMustBuy ||
                                 offer.IsStrengthIncrease ||
                                 offer.IsStaminaRecover ||
                                 offer.StrengthGain > 0 ||
                                 offer.StaminaRecover > 0;

        return hasEnabledBuyButton && hasPrice && hasEffectText && hasPurchaseSignal;
    }

    public static List<TradeOffer> BuildPurchaseQueue(List<TradeOffer> offers, bool preferStrengthItems, int budget)
    {
        return BuildPurchaseQueue(offers, preferStrengthItems, budget, strengthStat: null, staminaStat: null);
    }

    public static List<TradeOffer> BuildPurchaseQueue(
        List<TradeOffer> offers,
        bool preferStrengthItems,
        int budget,
        int? strengthStat,
        int? staminaStat)
    {
        var queue = new List<TradeOffer>();
        int remaining = budget;

        void TryAddOffer(TradeOffer offer, string logLabel)
        {
            if (queue.Any(q => q.SlotIndex == offer.SlotIndex))
                return;
            if (remaining != int.MaxValue && offer.Price > remaining)
                return;
            queue.Add(offer);
            if (remaining != int.MaxValue)
                remaining = Math.Max(0, remaining - offer.Price);
            string priceTag = offer.Price > 0 ? offer.Price.ToString() : "?";
            Logger.Log($"[Race:Trade] Trade executor: add {logLabel} slot {offer.SlotIndex + 1} (strength={offer.StrengthGain}, price={priceTag}, remaining={(remaining == int.MaxValue ? "INF" : remaining.ToString())}).");
        }

        var mustBuyOffers = offers
            .Where(o => o.HasBuyButtonVisible && !o.IsBuyDisabled)
            .Where(o => o.IsMustBuy)
            .Where(o => !IsOfferBlockedByStatCap(o, strengthStat, staminaStat))
            .OrderBy(o => o.SlotIndex)
            .ToList();
        foreach (var offer in mustBuyOffers)
            TryAddOffer(offer, offer.IsPotentialPoint ? "must-buy potential" : "must-buy");

        if (preferStrengthItems)
        {
            var strengthOffers = offers
                .Where(o => o.HasBuyButtonVisible && !o.IsBuyDisabled)
                .Where(o => o.IsStrengthIncrease)
                .Where(o => !IsOfferBlockedByStatCap(o, strengthStat, staminaStat))
                .OrderByDescending(o => o.StrengthGain)
                .ThenBy(o => o.Price > 0 ? o.Price : 999)
                .ThenBy(o => o.SlotIndex)
                .ToList();
            foreach (var offer in strengthOffers)
                TryAddOffer(offer, "strength");
        }

        return queue;
    }

    public static bool ShouldBuyOffer(
        TradeOffer offer,
        bool preferStrengthItems,
        int budget,
        int? strengthStat,
        int? staminaStat)
    {
        if (offer.Price <= 0)
            return false;

        if (budget != int.MaxValue && offer.Price > budget)
            return false;

        if (IsOfferBlockedByStatCap(offer, strengthStat, staminaStat))
            return false;

        if (offer.IsMustBuy)
            return true;

        return preferStrengthItems && offer.IsStrengthIncrease;
    }

    private static bool IsOfferBlockedByStatCap(TradeOffer offer, int? strengthStat, int? staminaStat)
    {
        bool strengthBlocked = offer.AffectsStrengthStat && RaceStatCapPolicy.IsStrengthCapped(strengthStat);
        bool staminaBlocked = offer.AffectsStaminaStat && RaceStatCapPolicy.IsStaminaCapped(staminaStat);
        if (!strengthBlocked && !staminaBlocked)
            return false;

        bool hasOtherUncappedValue = offer.IsPotentialPoint ||
                                     offer.IsStaminaRecover ||
                                     (offer.AffectsStrengthStat && !strengthBlocked) ||
                                     (offer.AffectsStaminaStat && !staminaBlocked);
        return !hasOtherUncappedValue;
    }

    public static bool ContainsTradeItemKeyword(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        return text.Contains("药水", StringComparison.Ordinal) ||
               text.Contains("秘笈", StringComparison.Ordinal) ||
               text.Contains("抽奖券", StringComparison.Ordinal) ||
               text.Contains("商品券", StringComparison.Ordinal) ||
               text.Contains("鸡排", StringComparison.Ordinal) ||
               text.Contains("蛋糕", StringComparison.Ordinal) ||
               text.Contains("义大利面", StringComparison.Ordinal) ||
               text.Contains("意大利面", StringComparison.Ordinal) ||
               text.Contains("牛肉", StringComparison.Ordinal) ||
               text.Contains("牛奶", StringComparison.Ordinal) ||
               text.Contains("料理食物", StringComparison.Ordinal) ||
               text.Contains("沙拉", StringComparison.Ordinal) ||
               text.Contains("甜甜圈", StringComparison.Ordinal) ||
               text.Contains("炒菜", StringComparison.Ordinal);
    }

    public static int ExtractStrengthGain(string effectText)
    {
        if (string.IsNullOrEmpty(effectText))
            return 0;

        var patterns = new[]
        {
            @"力量[+＋]\s*(\d+)",
            @"力量\s*(\d+)\s*增加",
            @"力量\s*(\d+)",
            @"力量增加\s*(\d+)",
            @"力量提升\s*(\d+)",
        };
        foreach (var p in patterns)
        {
            var m = Regex.Match(effectText, p);
            if (m.Success && m.Groups.Count > 1 && int.TryParse(m.Groups[1].Value, out int v))
                return v;
        }

        return 0;
    }

    public static bool IsStrengthIncreaseEffect(string effectText, int strengthGain)
    {
        if (string.IsNullOrEmpty(effectText))
            return false;
        if (!effectText.Contains("力量", StringComparison.Ordinal))
            return false;
        if (strengthGain > 0)
            return true;
        return effectText.Contains("增加", StringComparison.Ordinal) ||
               effectText.Contains("提升", StringComparison.Ordinal) ||
               effectText.Contains("+", StringComparison.Ordinal) ||
               effectText.Contains("＋", StringComparison.Ordinal);
    }

    public static bool TargetsStrengthStat(string slotText, string effectText, bool isStrengthIncrease)
    {
        if (isStrengthIncrease)
            return true;

        string text = NormalizeTradeSignalText($"{slotText}{effectText}");
        if (!text.Contains("力量", StringComparison.Ordinal))
            return false;

        return text.Contains("训练经验值增加", StringComparison.Ordinal) ||
               text.Contains("力量训练", StringComparison.Ordinal);
    }

    public static bool TargetsStaminaStat(string slotText, string effectText, bool isStaminaRecover)
    {
        if (isStaminaRecover)
            return false;

        string text = NormalizeTradeSignalText($"{slotText}{effectText}");
        if (text.Contains("体力训练", StringComparison.Ordinal) &&
            text.Contains("训练经验值增加", StringComparison.Ordinal))
        {
            return true;
        }

        if (!effectText.Contains("体力", StringComparison.Ordinal))
            return false;

        if (effectText.Contains("恢复", StringComparison.Ordinal) ||
            effectText.Contains("耐力", StringComparison.Ordinal))
        {
            return false;
        }

        return effectText.Contains("增加", StringComparison.Ordinal) ||
               effectText.Contains("提升", StringComparison.Ordinal) ||
               Regex.IsMatch(effectText, @"体力\s*\d+");
    }

    public static bool IsTradeKeywordOffer(string slotText, string effectText, bool allowSlotTextKeyword)
    {
        if (IsStaminaRecoverByKeyword(slotText, effectText))
            return true;

        foreach (var kw in RaceUserPolicy.TradeKeywords)
        {
            if (string.IsNullOrWhiteSpace(kw))
                continue;

            string keyword = kw.Trim();
            bool inEffect = !string.IsNullOrEmpty(effectText) && effectText.Contains(keyword, StringComparison.Ordinal);
            bool inSlot = allowSlotTextKeyword && !string.IsNullOrEmpty(slotText) && slotText.Contains(keyword, StringComparison.Ordinal);
            if (inEffect || inSlot)
                return true;
        }

        return false;
    }

    public static bool IsMustBuyOffer(string slotText, string effectText, bool allowSlotTextKeyword)
    {
        return IsTradeKeywordOffer(slotText, effectText, allowSlotTextKeyword);
    }

    public static bool IsPotentialPointOffer(string slotText, string effectText)
    {
        string text = NormalizeTradeSignalText($"{slotText}{effectText}");
        if (string.IsNullOrEmpty(text))
            return false;

        if (text.Contains("潜质点数", StringComparison.Ordinal))
            return true;

        return text.Contains("潜质", StringComparison.Ordinal) &&
               (text.Contains("点数", StringComparison.Ordinal) ||
                text.Contains("潜质点", StringComparison.Ordinal) ||
                text.Contains("点", StringComparison.Ordinal));
    }

    public static bool IsStaminaRecoverByKeyword(string slotText, string effectText)
    {
        string text = NormalizeTradeSignalText($"{slotText}{effectText}");
        return text.Contains("耐力", StringComparison.Ordinal) ||
               text.Contains("体力恢复", StringComparison.Ordinal) ||
               text.Contains("恢复体力", StringComparison.Ordinal) ||
               text.Contains("甜甜圈", StringComparison.Ordinal);
    }

    public static int ExtractStaminaRecover(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        string normalized = NormalizeTradeSignalText(text);
        var patterns = new[]
        {
            @"体力\s*(\d{1,3})\s*恢复",
            @"恢复\s*(\d{1,3})\s*体力",
            @"(\d{1,3})\s*体力恢复",
            @"耐力\s*[+＋]?\s*(\d{1,3})",
            @"恢复\s*(\d{1,3})",
        };

        int best = 0;
        foreach (var p in patterns)
        {
            foreach (Match m in Regex.Matches(normalized, p))
            {
                if (!m.Success || m.Groups.Count < 2)
                    continue;
                if (!int.TryParse(m.Groups[1].Value, out int value))
                    continue;
                if (value < 1 || value > 200)
                    continue;
                if (value > best)
                    best = value;
            }
        }

        return best;
    }

    public static string NormalizeTradeSignalText(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        string text = TradeStageOcr.NormalizeOcr(raw);
        text = Regex.Replace(text, @"\d+/\d+", "");
        return text;
    }
}
