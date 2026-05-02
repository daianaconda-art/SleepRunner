using OpenCvSharp;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Trade;

/// <summary>
/// 交易详情页的"按钮检测 + 点击购买/确认"动作层
///
/// 拆分意图：
/// - 改坐标候选/灰态阈值/确认弹窗节奏只动这里
/// - OCR/Policy 不再直接知道按钮坐标和确认流程
/// </summary>
internal static class TradeBuyActions
{
    private const int DetailSettleDelayMs = 500;

    /// <summary>
    /// 详情页就绪：详情展开/购买按钮可见/灰态按钮三者任一即可
    /// </summary>
    public static bool IsOfferDetailReady(Mat screenshot)
    {
        return IsDetailExpanded(screenshot) ||
               HasReadableDetailTitle(screenshot) ||
               HasVisibleBuySignal(screenshot) ||
               IsBuyButtonGrayDisabled(screenshot);
    }

    public static bool HasReadableDetailTitle(Mat screenshot)
    {
        string title = TradeDetailOcr.ReadDetailTitleText(screenshot);
        return TradeDetailOcr.IsReliableSlotText(title);
    }

    public static bool IsCurrentDetailOwnedBySlot(Mat screenshot, int slotIndex)
    {
        string rowText = TradeDetailOcr.ReadRowSlotText(screenshot, slotIndex);
        string detailTitle = TradeDetailOcr.ReadDetailTitleText(screenshot);
        return TradeSlotOwnershipPolicy.BelongsToSlot(rowText, detailTitle);
    }

    public static TradeBuyabilityState ReadBuyabilityState(TradeOffer offer)
    {
        bool purchasedState = offer.IsRowSoldOut ||
                              IsPurchasedStateText(offer.SlotText) ||
                              IsPurchasedStateText(offer.EffectText);
        return TradeInteractionPolicy.EvaluateBuyability(
            offer.HasBuyButtonVisible,
            offer.IsBuyDisabled,
            purchasedState);
    }

    public static bool IsDetailExpanded(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(
                screenshot,
                TradeDetailOcr.EffectCheckX,
                TradeDetailOcr.EffectCheckY,
                TradeDetailOcr.EffectCheckW,
                TradeDetailOcr.EffectCheckH)
            .GetAwaiter()
            .GetResult();
        string text = TradeStageOcr.NormalizeOcr(raw);
        if (!text.Contains("效果", StringComparison.Ordinal))
            return false;

        return text.Contains("增加", StringComparison.Ordinal) ||
               text.Contains("提升", StringComparison.Ordinal) ||
               text.Contains("力量", StringComparison.Ordinal) ||
               text.Contains("韧性", StringComparison.Ordinal) ||
               text.Contains("专注", StringComparison.Ordinal) ||
               text.Contains("恢复", StringComparison.Ordinal) ||
               text.Contains("体力", StringComparison.Ordinal) ||
               text.Contains("状态", StringComparison.Ordinal) ||
               text.Contains("暴击", StringComparison.Ordinal) ||
               text.Contains("回合", StringComparison.Ordinal) ||
               text.Contains("队长专用", StringComparison.Ordinal) ||
               text.Contains("潜质", StringComparison.Ordinal) ||
               TradeStageOcr.CountChineseChars(text) >= 6;
    }

    public static bool HasVisibleBuySignal(Mat screenshot)
    {
        foreach (var r in TradeDetailOcr.BuyButtonRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H).GetAwaiter().GetResult();
            string text = TradeStageOcr.NormalizeOcr(raw);
            if (text.Contains("购买", StringComparison.Ordinal) ||
                text.Contains("买", StringComparison.Ordinal) ||
                text.Contains("购", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 购买按钮灰态判定：HSV 饱和度低 + 仍能 OCR 到"购买"字样
    /// </summary>
    public static bool IsBuyButtonGrayDisabled(Mat screenshot)
    {
        foreach (var r in TradeDetailOcr.BuyButtonRegions)
        {
            int x = Math.Max(0, (int)(screenshot.Width * r.X));
            int y = Math.Max(0, (int)(screenshot.Height * r.Y));
            int w = Math.Max(1, (int)(screenshot.Width * r.W));
            int h = Math.Max(1, (int)(screenshot.Height * r.H));
            if (x + w > screenshot.Width) w = screenshot.Width - x;
            if (y + h > screenshot.Height) h = screenshot.Height - y;
            if (w < 4 || h < 4)
                continue;

            using var roi = new Mat(screenshot, new Rect(x, y, w, h));
            using var hsv = new Mat();
            Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);
            Scalar mean = Cv2.Mean(hsv);
            double meanS = mean.Val1;
            double meanV = mean.Val2;

            string text = TradeStageOcr.NormalizeOcr(OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H).GetAwaiter().GetResult());
            bool hasBuyWord = text.Contains("购买", StringComparison.Ordinal) ||
                              text.Contains("买", StringComparison.Ordinal) ||
                              text.Contains("购", StringComparison.Ordinal);
            bool grayLike = meanS < 28 && meanV > 55 && meanV < 210;
            if (hasBuyWord && grayLike)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 点击商品行打开详情；上下小幅偏移做兜底，返回展开后的截图克隆
    /// </summary>
    public static async Task<Mat?> TryOpenOfferDetailAsync(
        GameContext ctx,
        int slotIndex,
        TradeDetailOcr.OfferSlotRegion slot,
        bool allowReuseCurrentDetail = false,
        bool requireOwnedAfterClick = true)
    {
        using (var current = ctx.CaptureScreen())
        {
            if (allowReuseCurrentDetail &&
                current != null &&
                !current.Empty() &&
                IsOfferDetailReady(current) &&
                IsCurrentDetailOwnedBySlot(current, slotIndex))
            {
                Logger.Log("[Race:Trade] Trade detail already open, skip re-click.");
                return current.Clone();
            }
        }

        GameActionKey slotAction = TradeHotkeyProbe.GetSelectSlotAction(slotIndex);
        bool hotkeySent = await ctx.SendGameAction(slotAction);
        if (hotkeySent)
        {
            await ctx.WaitUnscaled(DetailSettleDelayMs);
            var hotkeyShot = ctx.CaptureScreen();
            if (hotkeyShot == null || hotkeyShot.Empty())
            {
                hotkeyShot?.Dispose();
            }
            else if (IsOfferDetailReady(hotkeyShot))
            {
                bool owned = IsCurrentDetailOwnedBySlot(hotkeyShot, slotIndex);
                if (!requireOwnedAfterClick || owned)
                {
                    Logger.Log($"[Race:Trade] Trade detail opened by hotkey {slotAction}, owned={owned}.");
                    var clone = hotkeyShot.Clone();
                    hotkeyShot.Dispose();
                    return clone;
                }

                Logger.Log($"[Race:Trade] Trade detail hotkey {slotAction} did not select slot {slotIndex + 1}, falling back to row click.");
                hotkeyShot.Dispose();
            }
            else
            {
                Logger.Log($"[Race:Trade] Trade detail hotkey {slotAction} did not select slot {slotIndex + 1}, falling back to row click.");
                hotkeyShot.Dispose();
            }
        }
        else
        {
            Logger.Log($"[Race:Trade] Trade detail hotkey {slotAction} failed, falling back to row click.");
        }

        await ctx.ClickAtPercent(slot.ClickX, slot.ClickY);
        await ctx.WaitUnscaled(DetailSettleDelayMs);

        var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
        {
            shot?.Dispose();
            return null;
        }

        if (IsOfferDetailReady(shot) &&
            (!requireOwnedAfterClick || IsCurrentDetailOwnedBySlot(shot, slotIndex)))
        {
            var clone = shot.Clone();
            shot.Dispose();
            return clone;
        }

        shot.Dispose();

        return null;
    }

    /// <summary>
    /// 点击购买按钮（OCR 命中 + 固定坐标兜底），并做后验确认
    /// </summary>
    public static async Task<bool> TryClickBuyAsync(GameContext ctx, TradeOffer target, int knownBudget)
    {
        const int maxAttempts = 1;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var shot = ctx.CaptureScreen();
            if (shot == null || shot.Empty())
                continue;

            bool visibleBuy = HasVisibleBuySignal(shot);
            bool grayDisabled = IsBuyButtonGrayDisabled(shot);
            bool purchasedState = IsPurchasedStateText(TradeDetailOcr.ReadSlotText(shot, target.SlotIndex));
            TradeBuyabilityState buyability = TradeInteractionPolicy.EvaluateBuyability(visibleBuy, grayDisabled, purchasedState);

            if (buyability != TradeBuyabilityState.Enabled)
            {
                Logger.Log($"[Race:Trade] Trade executor: buy click aborted, state={buyability}, attempt={attempt}.");
                return false;
            }

            bool keySent = await ctx.SendGameAction(GameActionKey.TradePurchase);
            if (keySent)
            {
                await ctx.Wait(450);
                bool accepted = await VerifyBuyClickAcceptedAsync(ctx, target, knownBudget);
                Logger.Log($"[Race:Trade] Trade executor: buy key action sent, attempt={attempt}, accepted={accepted}");
                if (accepted)
                    return true;
            }
            else
            {
                Logger.Log($"[Race:Trade] Trade executor: buy key action failed, attempt={attempt}, falling back to fixed click points.");
            }

            foreach (var point in GetBuyFallbackClickPoints())
            {
                await ctx.ClickAtPercent(point.X, point.Y);
                await ctx.Wait(450);
                bool accepted = await VerifyBuyClickAcceptedAsync(ctx, target, knownBudget);
                Logger.Log($"[Race:Trade] Trade executor: buy fixed fallback clicked at ({point.X:F3},{point.Y:F3}), attempt={attempt}, accepted={accepted}");
                if (accepted)
                    return true;
            }

            Logger.Log($"[Race:Trade] Trade executor: fixed buy fallback exhausted (attempt={attempt}).");
            return false;
        }

        return false;
    }

    internal static IReadOnlyList<(double X, double Y)> GetBuyFallbackClickPoints()
    {
        return TradeDetailOcr.BuyButtonClickPoints;
    }

    /// <summary>
    /// 点击购买后多轮快速轮询：弹窗出现 / 预算下降 / 按钮态变化 / 槽位文本变化 任一即视为成功
    /// </summary>
    public static async Task<bool> VerifyBuyClickAcceptedAsync(GameContext ctx, TradeOffer target, int knownBudget)
    {
        const int pollCount = 8;
        const int pollDelayMs = 180;
        string beforeSlotText = TradePurchasePolicy.NormalizeTradeSignalText(target.SlotText);

        for (int poll = 0; poll < pollCount; poll++)
        {
            await ctx.Wait(poll == 0 ? 120 : pollDelayMs);
            using var shot = ctx.CaptureScreen();
            if (shot == null || shot.Empty())
                continue;

            bool hasConfirmSignal = HasConfirmSignal(shot);
            int moneyAfter = TradeDetailOcr.ReadCurrentMoney(shot);
            bool visibleBuy = HasVisibleBuySignal(shot);
            bool grayDisabled = IsBuyButtonGrayDisabled(shot);
            string slotAfter = TradePurchasePolicy.NormalizeTradeSignalText(TradeDetailOcr.ReadSlotText(shot, target.SlotIndex));

            if (TradeInteractionPolicy.IsStrongPurchaseSuccess(
                    beforeSlotText,
                    hasConfirmSignal,
                    knownBudget,
                    target.Price,
                    moneyAfter,
                    visibleBuy,
                    grayDisabled,
                    slotAfter))
            {
                string reason = hasConfirmSignal
                    ? "confirm-signal"
                    : (knownBudget > 0 && knownBudget != int.MaxValue && target.Price > 0 &&
                       moneyAfter > 0 && moneyAfter <= knownBudget - target.Price)
                        ? $"budget-drop {knownBudget}->{moneyAfter}"
                        : $"slot-state '{beforeSlotText}' -> '{slotAfter}'";
                Logger.Log($"[Race:Trade] Trade executor verify: strong success signal={reason}.");
                return true;
            }
        }

        return false;
    }

    public static bool IsPurchasedStateText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        return text.Contains("已购买", StringComparison.Ordinal) ||
               text.Contains("已购", StringComparison.Ordinal) ||
               text.Contains("售罄", StringComparison.Ordinal) ||
               text.Contains("已售", StringComparison.Ordinal) ||
               text.Contains("不可购买", StringComparison.Ordinal);
    }

    public static bool HasConfirmSignal(Mat screenshot)
    {
        foreach (var r in TradeDetailOcr.ConfirmRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H).GetAwaiter().GetResult();
            string text = TradeStageOcr.NormalizeOcr(raw);
            if (text.Contains("确认", StringComparison.Ordinal) ||
                text.Contains("购买", StringComparison.Ordinal) ||
                text.Contains("确定", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public static async Task TryClickConfirmAsync(GameContext ctx)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var shot = ctx.CaptureScreen();
            if (shot == null || shot.Empty())
                continue;

            foreach (var r in TradeDetailOcr.ConfirmRegions)
            {
                string raw = OcrHelper.RecognizeRegion(shot, r.X, r.Y, r.W, r.H).GetAwaiter().GetResult();
                string text = TradeStageOcr.NormalizeOcr(raw);
                if (!text.Contains("确认", StringComparison.Ordinal) &&
                    !text.Contains("购买", StringComparison.Ordinal))
                    continue;

                double clickX = r.X + r.W / 2;
                double clickY = r.Y + r.H / 2;
                await ctx.ClickAtPercent(clickX, clickY);
                await ctx.Wait(500);
                Logger.Log($"[Race:Trade] Trade executor: confirm clicked at ({clickX:F3},{clickY:F3}), attempt={attempt}, text='{text}'");
                return;
            }
        }
    }
}
