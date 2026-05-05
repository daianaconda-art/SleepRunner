using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Trade;

/// <summary>
/// 交易详情页（左侧详情卡 + 右侧三槽位列表）OCR 区域、读取与评分
///
/// 拆分意图：
/// - 调商品名/价格/效果/购买按钮区域时只动这里
/// - TradeOffer / PriceCandidate / OfferSlotRegion 记录类型也归在这一层，
///   因为它们是 OCR 的"原始结果结构"
/// </summary>
internal static class TradeDetailOcr
{
    public static readonly (double X, double Y, double W, double H)[] MoneyRegions =
    [
        (0.66, 0.00, 0.30, 0.12),
        (0.58, 0.00, 0.36, 0.14),
        (0.42, 0.00, 0.30, 0.14),
    ];

    public static readonly OfferSlotRegion[] OfferSlots =
    [
        new OfferSlotRegion(0.88, 0.425, 0.78, 0.34, 0.19, 0.12),
        new OfferSlotRegion(0.88, 0.525, 0.78, 0.47, 0.19, 0.12),
        new OfferSlotRegion(0.88, 0.626, 0.78, 0.60, 0.19, 0.12),
    ];

    public static readonly (double X, double Y, double W, double H)[] DetailPriceRegions =
    [
        (0.57, 0.73, 0.16, 0.09),
        (0.59, 0.75, 0.14, 0.08),
        (0.61, 0.76, 0.12, 0.08),
        (0.55, 0.72, 0.18, 0.10),
        (0.63, 0.74, 0.10, 0.08),
        (0.58, 0.78, 0.14, 0.07),
    ];

    public static readonly (double X, double Y, double W, double H)[] DetailTitleRegions =
    [
        (0.58, 0.24, 0.18, 0.10),
        (0.60, 0.26, 0.16, 0.09),
        (0.56, 0.23, 0.20, 0.12),
        (0.52, 0.25, 0.28, 0.12),
        (0.54, 0.27, 0.24, 0.10),
        (0.50, 0.23, 0.30, 0.14),
    ];

    public static readonly (double X, double Y, double W, double H)[] EffectRegions =
    [
        (0.49, 0.625, 0.23, 0.11),
        (0.48, 0.60, 0.25, 0.13),
        (0.50, 0.64, 0.21, 0.09),
    ];
    public const double EffectCheckX = 0.48;
    public const double EffectCheckY = 0.60;
    public const double EffectCheckW = 0.25;
    public const double EffectCheckH = 0.13;

    public static readonly (double X, double Y, double W, double H)[] BuyButtonRegions =
    [
        (0.68, 0.80, 0.28, 0.16),
        (0.64, 0.76, 0.32, 0.20),
        (0.72, 0.84, 0.24, 0.12),
    ];
    public static readonly (double X, double Y)[] BuyButtonClickPoints =
    [
        (0.82, 0.88),
        (0.80, 0.86),
        (0.84, 0.90),
        (0.89, 0.89),
        (0.86, 0.89),
        (0.91, 0.85),
    ];
    public static readonly (double X, double Y, double W, double H)[] ConfirmRegions =
    [
        (0.36, 0.68, 0.28, 0.22),
        (0.30, 0.62, 0.40, 0.30),
    ];

    public static int ReadCurrentMoney(Mat screenshot)
    {
        bool ok = HudMoneyOcr.TryReadMoney(screenshot, out int money, out string summary);
        Logger.Log($"[Race:Trade] Trade executor: money OCR regions => {summary}");
        return ok ? money : 0;
    }

    public static string ClipForLog(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Replace('\n', ' ').Replace('\r', ' ');
        return text.Length <= max ? text : text.Substring(0, max) + "…";
    }

    /// <summary>
    /// 优先详情卡标题，失败回退到右侧列表槽位
    /// </summary>
    public static string ReadSlotText(Mat screenshot, int slotIndex)
    {
        string detailTitle = ReadDetailTitleText(screenshot);
        if (IsReliableSlotText(detailTitle))
            return detailTitle;
        return ReadRowSlotText(screenshot, slotIndex);
    }

    public static string ReadRowSlotText(Mat screenshot, int slotIndex)
    {
        string best = "";
        int bestScore = int.MinValue;
        foreach (var r in GetSlotTextRegions(slotIndex))
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H).GetAwaiter().GetResult();
            string text = TradeStageOcr.NormalizeOcr(raw);
            int score = ScoreSlotText(text);
            if (score > bestScore || (score == bestScore && text.Length > best.Length))
            {
                best = text;
                bestScore = score;
            }
        }

        return best;
    }

    public static string ReadDetailTitleText(Mat screenshot)
    {
        string best = "";
        int bestScore = int.MinValue;
        foreach (var r in DetailTitleRegions)
        {
            foreach (string text in ReadDetailTitleCandidates(screenshot, r.X, r.Y, r.W, r.H))
            {
                int score = ScoreSlotText(text);
                if (score > bestScore || (score == bestScore && IsPreferredTitleCandidate(text, best)))
                {
                    best = text;
                    bestScore = score;
                }
            }
        }

        return best;
    }

    private static IEnumerable<string> ReadDetailTitleCandidates(Mat screenshot, double xPct, double yPct, double wPct, double hPct)
    {
        var rect = ToPixelRect(screenshot, xPct, yPct, wPct, hPct);
        using var roi = new Mat(screenshot, rect);
        using var roiClone = roi.Clone();
        yield return TradeStageOcr.NormalizeOcr(OcrHelper.RecognizeMat(roiClone).GetAwaiter().GetResult());

        using var enlarged = new Mat();
        Cv2.Resize(roi, enlarged, new OpenCvSharp.Size(), 3.0, 3.0, InterpolationFlags.Cubic);
        yield return TradeStageOcr.NormalizeOcr(OcrHelper.RecognizeMat(enlarged).GetAwaiter().GetResult());

        foreach (bool invert in new[] { false, true })
        {
            using var gray = new Mat();
            using var binary = new Mat();
            using var binaryBgr = new Mat();
            Cv2.CvtColor(enlarged, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, binary, 0, 255,
                (invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary) | ThresholdTypes.Otsu);
            Cv2.CvtColor(binary, binaryBgr, ColorConversionCodes.GRAY2BGR);
            yield return TradeStageOcr.NormalizeOcr(OcrHelper.RecognizeMat(binaryBgr).GetAwaiter().GetResult());
        }
    }

    public static int ScoreSlotText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return -10;

        int zhCount = TradeStageOcr.CountChineseChars(text);
        int score = zhCount * 2;
        int price = ExtractPrice(text);
        bool hasAsciiWord = Regex.IsMatch(text, @"[A-Za-z]{3,}");

        if (Regex.IsMatch(text, @"\d+"))
            score += 8;
        if (price > 0 && price < 10000)
            score += 6;
        if (TradePurchasePolicy.ContainsTradeItemKeyword(text))
            score += 12;
        if (text.Contains("蛋糕", StringComparison.Ordinal)) score += 4;
        if (text.Contains("鸡排", StringComparison.Ordinal)) score += 4;
        if (text.Contains("义大利", StringComparison.Ordinal) || text.Contains("意大利", StringComparison.Ordinal)) score += 4;
        if (hasAsciiWord)
            score -= 16;
        if (text.Contains("Journey", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Trading", StringComparison.OrdinalIgnoreCase))
            score -= 18;
        if (zhCount >= 2 && zhCount <= 12)
            score += 6;
        if (zhCount >= 2 && zhCount <= 8 && !Regex.IsMatch(text, @"[A-Za-z]"))
            score += 8;
        if (text.Length <= 18)
            score += 6;
        if (text.Length > 24)
            score -= 10;
        if (Regex.Matches(text, @"\d+").Count > 2)
            score -= 6;
        if (LooksLikeContaminatedSlotText(text))
            score -= 18;
        score += Math.Min(12, text.Length / 2);
        return score;
    }

    private static bool IsPreferredTitleCandidate(string candidate, string currentBest)
    {
        if (string.IsNullOrEmpty(currentBest))
            return true;

        int candidateZh = TradeStageOcr.CountChineseChars(candidate);
        int currentZh = TradeStageOcr.CountChineseChars(currentBest);
        if (candidateZh != currentZh)
            return candidateZh > currentZh;

        bool candidateHasAscii = Regex.IsMatch(candidate, @"[A-Za-z]");
        bool currentHasAscii = Regex.IsMatch(currentBest, @"[A-Za-z]");
        if (candidateHasAscii != currentHasAscii)
            return !candidateHasAscii;

        return candidate.Length < currentBest.Length;
    }

    public static IEnumerable<(double X, double Y, double W, double H)> GetSlotTextRegions(int slotIndex)
    {
        var slot = OfferSlots[slotIndex];
        double centerY = slot.TextY + slot.TextH / 2.0;
        double safeTop = Math.Max(0.20, slot.TextY - 0.01);
        double safeBottom = Math.Min(0.78, slot.TextY + slot.TextH + 0.02);

        if (slotIndex > 0)
        {
            var prev = OfferSlots[slotIndex - 1];
            double prevCenterY = prev.TextY + prev.TextH / 2.0;
            safeTop = Math.Max(safeTop, ((prevCenterY + centerY) / 2.0) - 0.01);
        }

        if (slotIndex < OfferSlots.Length - 1)
        {
            var next = OfferSlots[slotIndex + 1];
            double nextCenterY = next.TextY + next.TextH / 2.0;
            safeBottom = Math.Min(safeBottom, ((centerY + nextCenterY) / 2.0) + 0.01);
        }

        yield return ClampSlotTextRegion(slot.TextX, slot.TextY, slot.TextW, slot.TextH, safeTop, safeBottom);
        yield return ClampSlotTextRegion(slot.TextX, slot.TextY - 0.01, slot.TextW, slot.TextH + 0.02, safeTop, safeBottom);
        yield return ClampSlotTextRegion(slot.TextX, slot.TextY + 0.01, slot.TextW, slot.TextH, safeTop, safeBottom);
        yield return ClampSlotTextRegion(slot.TextX + 0.01, slot.TextY, Math.Max(0.24, slot.TextW - 0.02), slot.TextH, safeTop, safeBottom);
        yield return ClampSlotTextRegion(Math.Max(0.56, slot.TextX - 0.01), slot.TextY, slot.TextW + 0.02, slot.TextH, safeTop, safeBottom);
    }

    public static bool IsRowMarkedSoldOut(Mat screenshot, int slotIndex)
    {
        foreach (var r in GetRowSoldOutRegions(slotIndex))
        {
            var rect = ToPixelRect(screenshot, r.X, r.Y, r.W, r.H);
            using var roi = new Mat(screenshot, rect);
            if (LooksLikeSoldOutStamp(roi))
                return true;

            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H).GetAwaiter().GetResult();
            string text = TradeStageOcr.NormalizeOcr(raw);
            if (text.Contains("SOLD", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("OUT", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("售罄", StringComparison.Ordinal) ||
                text.Contains("已售", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasRowSoldOutStamp(Mat screenshot, int slotIndex)
    {
        foreach (var r in GetRowSoldOutRegions(slotIndex))
        {
            var rect = ToPixelRect(screenshot, r.X, r.Y, r.W, r.H);
            using var roi = new Mat(screenshot, rect);
            if (LooksLikeSoldOutStamp(roi))
                return true;
        }

        return false;
    }

    private static IEnumerable<(double X, double Y, double W, double H)> GetRowSoldOutRegions(int slotIndex)
    {
        var slot = OfferSlots[slotIndex];
        double centerY = slot.ClickY;
        yield return (0.74, Math.Clamp(centerY - 0.05, 0.20, 0.82), 0.14, 0.12);
        yield return (0.72, Math.Clamp(centerY - 0.06, 0.20, 0.82), 0.18, 0.14);
    }

    private static bool LooksLikeSoldOutStamp(Mat roi)
    {
        if (roi.Empty())
            return false;

        var channels = roi.Split();
        using var redMask = new Mat();
        using var greenScaled = new Mat();
        using var blueScaled = new Mat();
        using var greenOk = new Mat();
        using var blueOk = new Mat();
        using var combined = new Mat();

        Cv2.Multiply(channels[1], 1.2, greenScaled);
        Cv2.Multiply(channels[0], 1.2, blueScaled);
        Cv2.Threshold(channels[2], redMask, 120, 255, ThresholdTypes.Binary);
        Cv2.Compare(channels[2], greenScaled, greenOk, CmpTypes.GT);
        Cv2.Compare(channels[2], blueScaled, blueOk, CmpTypes.GT);
        Cv2.BitwiseAnd(redMask, greenOk, combined);
        Cv2.BitwiseAnd(combined, blueOk, combined);

        double redRatio = Cv2.CountNonZero(combined) / (double)(roi.Rows * roi.Cols);
        bool hasStampShape = HasStampSizedRedComponent(combined, roi.Cols, roi.Rows);
        foreach (var channel in channels)
            channel.Dispose();
        return redRatio >= 0.12 || hasStampShape;
    }

    private static bool HasStampSizedRedComponent(Mat redMask, int width, int height)
    {
        using var closed = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(9, 3));
        Cv2.MorphologyEx(redMask, closed, MorphTypes.Close, kernel);
        Cv2.FindContours(closed, out OpenCvSharp.Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            Rect rect = Cv2.BoundingRect(contour);
            if (rect.Width >= width * 0.30 &&
                rect.Height >= height * 0.18)
            {
                return true;
            }
        }

        return false;
    }

    public static (double X, double Y, double W, double H) ClampSlotTextRegion(
        double x, double y, double w, double h, double safeTop, double safeBottom)
    {
        double clampedX = Math.Clamp(x, 0.50, 0.90);
        double clampedW = Math.Clamp(w, 0.18, 0.40);
        double top = Math.Clamp(y, safeTop, safeBottom - 0.04);
        double bottom = Math.Clamp(y + h, top + 0.04, safeBottom);
        return (clampedX, top, clampedW, bottom - top);
    }

    public static bool IsReliableSlotText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        string normalized = TradePurchasePolicy.NormalizeTradeSignalText(text);
        if (TradeStageOcr.CountChineseChars(normalized) < 2)
            return false;
        if (LooksLikeContaminatedSlotText(normalized))
            return false;

        bool hasTradeItemHint = TradePurchasePolicy.ContainsTradeItemKeyword(normalized);
        bool hasPrice = ExtractPrice(normalized) > 0;
        bool shortEnough = normalized.Length <= 20;
        bool hasEnoughItemChars = TradeStageOcr.CountChineseChars(normalized) >= 4;
        return shortEnough && (hasTradeItemHint || hasPrice || hasEnoughItemChars);
    }

    public static bool LooksLikeContaminatedSlotText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        bool hasSentencePunctuation = text.Contains('。') ||
                                      text.Contains('，') ||
                                      text.Contains(',') ||
                                      text.Contains('！') ||
                                      text.Contains('？');
        bool hasStrongNarrativeKeyword = text.Contains("虽然", StringComparison.Ordinal) ||
                                         text.Contains("滋味", StringComparison.Ordinal) ||
                                         text.Contains("始终", StringComparison.Ordinal) ||
                                         text.Contains("队长专用", StringComparison.Ordinal) ||
                                         text.Contains("每回合", StringComparison.Ordinal) ||
                                         text.Contains("开始时", StringComparison.Ordinal) ||
                                         text.Contains("最多", StringComparison.Ordinal) ||
                                         text.Contains("叠加", StringComparison.Ordinal);
        bool plainAsNarrative = text.Contains("普通", StringComparison.Ordinal) && hasSentencePunctuation;
        bool hasEffectKeyword = text.Contains("效果", StringComparison.Ordinal) ||
                                text.Contains("增加", StringComparison.Ordinal) ||
                                text.Contains("提升", StringComparison.Ordinal) ||
                                text.Contains("恢复", StringComparison.Ordinal) ||
                                text.Contains("暴击", StringComparison.Ordinal);
        bool tooLong = TradeStageOcr.CountChineseChars(text) >= 14 || text.Length >= 24;
        return (hasSentencePunctuation && tooLong) || hasStrongNarrativeKeyword || plainAsNarrative || hasEffectKeyword;
    }

    /// <summary>
    /// 优先从详情卡价格区读价；失败回退到槽位文本里的数字
    /// </summary>
    public static int ReadOfferPrice(Mat screenshot, int slotIndex, string slotText)
    {
        if (TryReadTrustedRowPrice(slotText, out int trustedRowPrice))
        {
            Logger.Log($"[Race:Trade] Trade price[{slotIndex + 1}]: {trustedRowPrice} from row-fast text='{slotText}'");
            return trustedRowPrice;
        }

        PriceCandidate? bestCandidate = null;
        var detailSamples = new List<string>();
        var slotPriceSamples = new List<string>();

        foreach (var r in DetailPriceRegions)
        {
            foreach (string text in ReadPriceRegionCandidates(screenshot, r.X, r.Y, r.W, r.H))
            {
                if (detailSamples.Count < 8)
                    detailSamples.Add(text);
                int price = ExtractPriceValue(text);
                if (price <= 0)
                    continue;
                bestCandidate = PickBetterPriceCandidate(bestCandidate, new PriceCandidate(price, "detail", text));
            }
        }

        foreach (var slotPriceRegion in GetSlotPriceRegions(slotIndex))
        {
            foreach (string text in ReadPriceRegionCandidates(screenshot, slotPriceRegion.X, slotPriceRegion.Y, slotPriceRegion.W, slotPriceRegion.H))
            {
                if (slotPriceSamples.Count < 8)
                    slotPriceSamples.Add(text);
                int price = ExtractPriceValue(text);
                if (price <= 0)
                    continue;
                bestCandidate = PickBetterPriceCandidate(bestCandidate, new PriceCandidate(price, "slot-price", text));
            }
        }

        if (bestCandidate == null)
        {
            int fallbackPrice = ExtractPrice(slotText);
            if (fallbackPrice > 0)
                bestCandidate = new PriceCandidate(fallbackPrice, "slot-fallback", slotText);
        }

        if (bestCandidate != null)
        {
            if (bestCandidate.Value.Source == "slot-fallback")
            {
                Logger.Log($"[Race:Trade] Trade price[{slotIndex + 1}] fallback detail-cands=[{string.Join(", ", detailSamples.Select(t => $"'{t}'"))}] slot-cands=[{string.Join(", ", slotPriceSamples.Select(t => $"'{t}'"))}]");
            }
            Logger.Log($"[Race:Trade] Trade price[{slotIndex + 1}]: {bestCandidate.Value.Value} from {bestCandidate.Value.Source} text='{bestCandidate.Value.Text}'");
        }
        else
        {
            Logger.Log($"[Race:Trade] Trade price[{slotIndex + 1}] detail-cands=[{string.Join(", ", detailSamples.Select(t => $"'{t}'"))}] slot-cands=[{string.Join(", ", slotPriceSamples.Select(t => $"'{t}'"))}]");
            Logger.Log($"[Race:Trade] Trade price[{slotIndex + 1}]: miss (slotText='{slotText}')");
        }

        return bestCandidate?.Value ?? 0;
    }

    public static bool TryReadTrustedRowPrice(string rowText, out int price)
    {
        price = 0;
        string normalized = TradeStageOcr.NormalizeOcr(rowText);
        if (!IsReliableSlotText(normalized) || LooksLikeContaminatedSlotText(normalized))
            return false;

        int candidate = ExtractPrice(normalized);
        if (candidate < 10 || candidate > 199)
            return false;

        price = candidate;
        return true;
    }

    public static PriceCandidate? PickBetterPriceCandidate(PriceCandidate? current, PriceCandidate candidate)
    {
        if (candidate.Value <= 0)
            return current;

        int candidateScore = ScorePriceCandidate(candidate);
        if (current == null)
            return candidate with { Score = candidateScore };

        int currentScore = current.Value.Score;
        if (candidateScore > currentScore)
            return candidate with { Score = candidateScore };
        if (candidateScore == currentScore && candidate.Value < current.Value.Value)
            return candidate with { Score = candidateScore };
        return current;
    }

    public static int ScorePriceCandidate(PriceCandidate candidate)
    {
        int value = candidate.Value;
        string text = candidate.Text ?? "";
        string source = candidate.Source ?? "";

        int score = source switch
        {
            "detail" => 50,
            "slot-price" => 40,
            _ => 10
        };

        int digits = value.ToString().Length;
        if (digits == 2) score += 18;
        else if (digits == 1) score += 10;
        else if (digits == 3) score += 4;
        else score -= 8;

        if (value >= 10 && value <= 99) score += 18;
        else if (value >= 100 && value <= 199) score += 6;
        else if (value >= 200) score -= 6;

        if (source == "slot-fallback")
        {
            if (LooksLikeContaminatedSlotText(text))
                score -= 12;
            if (text.Length > 10)
                score -= 4;
            if (digits >= 3 && Regex.IsMatch(text, @"[^\d]0{2}$"))
                score -= 10;
        }

        if (source != "slot-fallback" && text.Length <= 6)
            score += 6;

        return score;
    }

    public static IEnumerable<string> ReadPriceRegionCandidates(Mat screenshot, double xPct, double yPct, double wPct, double hPct)
    {
        var rect = ToPixelRect(screenshot, xPct, yPct, wPct, hPct);
        using var roi = new Mat(screenshot, rect);
        foreach (string candidate in ReadPriceRegionCandidates(roi))
            yield return candidate;
    }

    private static IEnumerable<string> ReadPriceRegionCandidates(Mat roi)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string text in RecognizePriceVariants(roi))
        {
            string normalized = TradeStageOcr.NormalizeOcr(text);
            if (string.IsNullOrEmpty(normalized))
                continue;
            if (seen.Add(normalized))
                yield return normalized;
        }
    }

    private static IEnumerable<string> RecognizePriceVariants(Mat roi)
    {
        using (var full = roi.Clone())
        {
            yield return OcrHelper.RecognizeMat(full).GetAwaiter().GetResult();
            yield return OcrHelper.RecognizeMat(full, "en-US").GetAwaiter().GetResult();
        }

        using (var enlarged = new Mat())
        {
            Cv2.Resize(roi, enlarged, new OpenCvSharp.Size(), 4.0, 4.0, InterpolationFlags.Cubic);
            yield return OcrHelper.RecognizeMat(enlarged).GetAwaiter().GetResult();
            yield return OcrHelper.RecognizeMat(enlarged, "en-US").GetAwaiter().GetResult();

            foreach (bool invert in new[] { false, true })
            {
                using var gray = new Mat();
                using var binary = new Mat();
                using var binaryBgr = new Mat();
                Cv2.CvtColor(enlarged, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.Threshold(gray, binary, 0, 255,
                    (invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary) | ThresholdTypes.Otsu);
                Cv2.CvtColor(binary, binaryBgr, ColorConversionCodes.GRAY2BGR);
                yield return OcrHelper.RecognizeMat(binaryBgr).GetAwaiter().GetResult();
                yield return OcrHelper.RecognizeMat(binaryBgr, "en-US").GetAwaiter().GetResult();
            }
        }

        foreach (var subRect in BuildPriceSubRects(roi))
        {
            using var sub = new Mat(roi, subRect);
            using var enlargedSub = new Mat();
            Cv2.Resize(sub, enlargedSub, new OpenCvSharp.Size(), 5.0, 5.0, InterpolationFlags.Cubic);
            yield return OcrHelper.RecognizeMat(enlargedSub).GetAwaiter().GetResult();
            yield return OcrHelper.RecognizeMat(enlargedSub, "en-US").GetAwaiter().GetResult();

            foreach (bool invert in new[] { false, true })
            {
                using var gray = new Mat();
                using var binary = new Mat();
                using var binaryBgr = new Mat();
                Cv2.CvtColor(enlargedSub, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.Threshold(gray, binary, 0, 255,
                    (invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary) | ThresholdTypes.Otsu);
                Cv2.CvtColor(binary, binaryBgr, ColorConversionCodes.GRAY2BGR);
                yield return OcrHelper.RecognizeMat(binaryBgr).GetAwaiter().GetResult();
                yield return OcrHelper.RecognizeMat(binaryBgr, "en-US").GetAwaiter().GetResult();
            }
        }
    }

    private static IEnumerable<Rect> BuildPriceSubRects(Mat roi)
    {
        int w = roi.Width;
        int h = roi.Height;
        yield return new Rect((int)(w * 0.35), 0, Math.Max(1, (int)(w * 0.55)), h);
        yield return new Rect((int)(w * 0.50), 0, Math.Max(1, (int)(w * 0.40)), h);
        yield return new Rect((int)(w * 0.60), 0, Math.Max(1, (int)(w * 0.30)), h);
    }

    public static IEnumerable<(double X, double Y, double W, double H)> GetSlotPriceRegions(int slotIndex)
    {
        var slot = OfferSlots[slotIndex];
        double rowY = Math.Clamp(slot.ClickY - 0.03, 0.20, 0.82);
        yield return (0.86, rowY, 0.10, 0.07);
        yield return (0.88, rowY, 0.08, 0.07);
        yield return (0.84, rowY - 0.01, 0.12, 0.08);
        yield return (0.82, rowY, 0.14, 0.08);
    }

    public static string ReadEffectText(Mat screenshot)
    {
        string best = "";
        int bestScore = int.MinValue;
        foreach (var r in EffectRegions)
        {
            foreach (string text in ReadEffectRegionCandidates(screenshot, r.X, r.Y, r.W, r.H))
            {
                int score = ScoreEffectText(text);
                if (score > bestScore || (score == bestScore && text.Length > best.Length))
                {
                    best = text;
                    bestScore = score;
                }
            }
        }
        return best;
    }

    public static IEnumerable<string> ReadEffectRegionCandidates(Mat screenshot, double xPct, double yPct, double wPct, double hPct)
    {
        var rect = ToPixelRect(screenshot, xPct, yPct, wPct, hPct);
        using var roi = new Mat(screenshot, rect);
        using var roiClone = roi.Clone();
        yield return TradeStageOcr.NormalizeOcr(OcrHelper.RecognizeMat(roiClone).GetAwaiter().GetResult());

        using var enlarged = new Mat();
        Cv2.Resize(roi, enlarged, new OpenCvSharp.Size(), 3.0, 3.0, InterpolationFlags.Cubic);
        yield return TradeStageOcr.NormalizeOcr(OcrHelper.RecognizeMat(enlarged).GetAwaiter().GetResult());

        using var gray = new Mat();
        using var binary = new Mat();
        using var binaryBgr = new Mat();
        Cv2.CvtColor(enlarged, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        Cv2.CvtColor(binary, binaryBgr, ColorConversionCodes.GRAY2BGR);
        yield return TradeStageOcr.NormalizeOcr(OcrHelper.RecognizeMat(binaryBgr).GetAwaiter().GetResult());
    }

    public static int ScoreEffectText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return -10;

        int score = 0;
        if (text.Contains("力量", StringComparison.Ordinal)) score += 6;
        if (text.Contains("体力", StringComparison.Ordinal)) score += 6;
        if (text.Contains("韧性", StringComparison.Ordinal)) score += 6;
        if (text.Contains("专注", StringComparison.Ordinal)) score += 6;
        if (text.Contains("保护", StringComparison.Ordinal)) score += 6;
        if (text.Contains("潜质", StringComparison.Ordinal)) score += 8;
        if (text.Contains("效果", StringComparison.Ordinal)) score += 3;
        if (text.Contains("增加", StringComparison.Ordinal) || text.Contains("提升", StringComparison.Ordinal) || text.Contains("恢复", StringComparison.Ordinal)) score += 4;
        if (Regex.IsMatch(text, @"\d+")) score += 2;
        if (text.Contains("1250", StringComparison.Ordinal)) score -= 6;
        return score;
    }

    public static Rect ToPixelRect(Mat screenshot, double xPct, double yPct, double wPct, double hPct)
    {
        int x = Math.Clamp((int)(screenshot.Width * xPct), 0, screenshot.Width - 1);
        int y = Math.Clamp((int)(screenshot.Height * yPct), 0, screenshot.Height - 1);
        int w = Math.Min(Math.Max(1, (int)(screenshot.Width * wPct)), screenshot.Width - x);
        int h = Math.Min(Math.Max(1, (int)(screenshot.Height * hPct)), screenshot.Height - y);
        return new Rect(x, y, w, h);
    }

    /// <summary>
    /// 从槽位文本中提取价格（末尾数字优先）
    /// </summary>
    public static int ExtractPriceValue(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var matches = Regex.Matches(text, @"\d+");
        if (matches.Count == 0)
            return 0;

        int bestValue = 0;
        int bestScore = int.MinValue;
        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (!match.Success || !int.TryParse(match.Value, out int value))
                continue;
            if (value <= 0 || value >= 10000)
                continue;

            int score = 0;
            int digitCount = match.Value.Length;
            if (digitCount == 2) score += 24;
            else if (digitCount == 3) score += 14;
            else if (digitCount == 1) score -= 8;

            if (value >= 10 && value <= 99) score += 18;
            else if (value >= 100 && value <= 199) score += 6;
            else if (value >= 200) score -= 8;

            if (i == matches.Count - 1) score += 16;
            if (matches.Count >= 2 && i == matches.Count - 1 && value < ParseMatchValue(matches[0]))
                score += 12;
            if (Regex.IsMatch(text, @"\d+\D+\d+")) score += 4;

            if (score > bestScore || (score == bestScore && value < bestValue))
            {
                bestScore = score;
                bestValue = value;
            }
        }

        return bestScore == int.MinValue ? 0 : bestValue;
    }

    public static int ExtractPrice(string slotText)
    {
        if (string.IsNullOrEmpty(slotText))
            return 0;

        var matches = Regex.Matches(slotText, @"\d+");
        if (matches.Count == 0)
            return 0;

        PriceCandidate? best = null;
        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Value, out int value) || value <= 0 || value >= 10000)
                continue;

            var rawCandidate = CreateSlotFallbackPriceCandidate(slotText, value, match.Index, match.Value.Length);
            if (best == null || rawCandidate.Score > best.Value.Score ||
                (rawCandidate.Score == best.Value.Score && rawCandidate.Value < best.Value.Value))
            {
                best = rawCandidate;
            }

            // OCR 常把 20/40/80 读成 200/400/800，slot-fallback 时额外尝试去末尾一个 0
            if (value >= 100 && value % 10 == 0)
            {
                int trimmed = value / 10;
                if (trimmed > 0)
                {
                    var trimmedCandidate = CreateSlotFallbackPriceCandidate(slotText, trimmed, match.Index, Math.Max(1, match.Value.Length - 1));
                    trimmedCandidate = trimmedCandidate with { Score = trimmedCandidate.Score + 12 };
                    if (best == null || trimmedCandidate.Score > best.Value.Score ||
                        (trimmedCandidate.Score == best.Value.Score && trimmedCandidate.Value < best.Value.Value))
                    {
                        best = trimmedCandidate;
                    }
                }
            }
        }

        return best?.Value ?? ExtractPriceValue(slotText);
    }

    public static PriceCandidate CreateSlotFallbackPriceCandidate(string slotText, int value, int matchIndex, int digitCount)
    {
        int score = 0;
        if (digitCount == 2) score += 18;
        else if (digitCount == 1) score -= 12;
        else if (digitCount == 3) score += 2;
        if (value >= 10 && value <= 99) score += 18;
        else if (value >= 100 && value <= 199) score += 4;
        else if (value >= 200) score -= 8;
        else if (value < 10) score -= 16;

        if (matchIndex >= slotText.Length - 4) score += 8;
        if (Regex.IsMatch(slotText, @"[^\d]0{2}$") && value >= 100) score -= 10;
        if (LooksLikeContaminatedSlotText(slotText) && value >= 100) score -= 8;
        if (LooksLikeContaminatedSlotText(slotText) && value < 10) score -= 20;
        return new PriceCandidate(value, "slot-fallback", slotText, score);
    }

    private static int ParseMatchValue(Match match)
    {
        return int.TryParse(match.Value, out int value) ? value : 0;
    }

    public readonly record struct PriceCandidate(int Value, string Source, string Text, int Score = 0);

    public readonly struct OfferSlotRegion
    {
        public readonly double ClickX;
        public readonly double ClickY;
        public readonly double TextX;
        public readonly double TextY;
        public readonly double TextW;
        public readonly double TextH;

        public OfferSlotRegion(double clickX, double clickY, double textX, double textY, double textW, double textH)
        {
            ClickX = clickX;
            ClickY = clickY;
            TextX = textX;
            TextY = textY;
            TextW = textW;
            TextH = textH;
        }
    }
}

/// <summary>
/// 单个交易商品的全部决策维度（OCR + 推断结果）
/// </summary>
internal sealed class TradeOffer
{
    public int SlotIndex { get; set; }
    public string SlotText { get; set; } = "";
    public bool HasReliableSlotText { get; set; }
    public int Price { get; set; }
    public string EffectText { get; set; } = "";
    public int StrengthGain { get; set; }
    public bool IsStrengthIncrease { get; set; }
    public bool AffectsStrengthStat { get; set; }
    public bool IsMustBuy { get; set; }
    public bool IsPotentialPoint { get; set; }
    public int StaminaRecover { get; set; }
    public bool IsStaminaRecover { get; set; }
    public bool AffectsStaminaStat { get; set; }
    public bool HasBuyButtonVisible { get; set; }
    public bool IsBuyDisabled { get; set; }
    public bool IsRowSoldOut { get; set; }
}
