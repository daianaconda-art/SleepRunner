namespace SleepRunner.Automation.Race.Handlers.Trade;

internal enum TradeBuyabilityState
{
    Enabled,
    Disabled,
    Purchased,
    NoButton,
}

internal static class TradeInteractionPolicy
{
    internal static TradeBuyabilityState EvaluateBuyability(bool visibleBuy, bool grayDisabled, bool purchasedState)
    {
        if (visibleBuy && !grayDisabled)
            return TradeBuyabilityState.Enabled;
        if (purchasedState)
            return TradeBuyabilityState.Purchased;
        if (grayDisabled)
            return TradeBuyabilityState.Disabled;
        if (visibleBuy)
            return TradeBuyabilityState.Enabled;
        return TradeBuyabilityState.NoButton;
    }

    internal static bool IsStrongPurchaseSuccess(
        string beforeSlotText,
        bool hasConfirmSignal,
        int knownBudget,
        int price,
        int moneyAfter,
        bool visibleBuy,
        bool grayDisabled,
        string slotAfter)
    {
        if (hasConfirmSignal)
            return true;

        if (ShouldReadMoneyForPurchaseVerification(knownBudget, price) &&
            moneyAfter > 0 &&
            moneyAfter < knownBudget)
        {
            return true;
        }

        if (LooksPurchased(slotAfter))
            return true;

        return grayDisabled &&
               visibleBuy &&
               !string.IsNullOrEmpty(beforeSlotText) &&
               !string.Equals(beforeSlotText, slotAfter, StringComparison.Ordinal);
    }

    internal static bool ShouldReadMoneyForPurchaseVerification(int knownBudget, int price)
    {
        return knownBudget > 0 &&
               knownBudget != int.MaxValue &&
               price > 0;
    }

    internal static bool ShouldUseHotkeyDetail(bool detailReady, bool ownedByRequestedSlot, bool requireOwnedAfterClick)
    {
        return detailReady && (!requireOwnedAfterClick || ownedByRequestedSlot);
    }

    private static bool LooksPurchased(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return text.Contains("已购买", StringComparison.Ordinal) ||
               text.Contains("已购", StringComparison.Ordinal) ||
               text.Contains("售罄", StringComparison.Ordinal) ||
               text.Contains("已售", StringComparison.Ordinal) ||
               text.Contains("不可购买", StringComparison.Ordinal);
    }
}
