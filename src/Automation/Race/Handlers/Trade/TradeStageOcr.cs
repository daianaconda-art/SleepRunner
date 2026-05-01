using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race.Handlers.Trade;

/// <summary>
/// 分流页（评鉴战出击前 准备 / 委托 / 交易 / 商店）OCR + 文本判定
///
/// 拆分意图：
/// - TradeAndAppraiseHandler 的 CanHandle/Branch 逻辑大量依赖"右侧菜单是否含交易/委托关键词"
/// - 集中后调阈值/关键词只动这里，不会牵连交易详情页的 OCR
/// </summary>
internal static class TradeStageOcr
{
    public const double MenuTextX = 0.66;
    public const double MenuTextY = 0.36;
    public const double MenuTextW = 0.32;
    public const double MenuTextH = 0.44;

    public static readonly (double X, double Y, double W, double H)[] StageTitleRegions =
    [
        (0.01, 0.07, 0.22, 0.12),
        (0.00, 0.04, 0.26, 0.16),
    ];

    public static readonly (double X, double Y, double W, double H)[] MenuTextRegions =
    [
        (0.66, 0.36, 0.32, 0.44),
        (0.58, 0.32, 0.38, 0.48),
        (0.55, 0.30, 0.42, 0.52),
    ];

    public const double TradeBuyKeywordX = 0.68;
    public const double TradeBuyKeywordY = 0.80;
    public const double TradeBuyKeywordW = 0.28;
    public const double TradeBuyKeywordH = 0.16;

    public static string ReadMenuText(Mat screenshot)
    {
        string best = "";
        foreach (var r in MenuTextRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H).GetAwaiter().GetResult();
            string text = NormalizeOcr(raw);
            if (text.Length > best.Length)
                best = text;
            if (ContainsTradeKeyword(text) || ContainsTradeStageHint(text) || ContainsCommissionKeyword(text))
                return text;
        }
        return best;
    }

    public static string ReadStageTitleText(Mat screenshot)
    {
        string best = "";
        foreach (var r in StageTitleRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H).GetAwaiter().GetResult();
            string text = NormalizeOcr(raw);
            if (text.Length > best.Length)
                best = text;
            if (ContainsTradeStageTitleKeyword(text))
                return text;
        }
        return best;
    }

    public static bool ContainsAppraiseKeyword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("评鉴战", StringComparison.Ordinal) ||
               text.Contains("评鉴", StringComparison.Ordinal) ||
               text.Contains("目标评鉴战", StringComparison.Ordinal) ||
               text.Contains("D-DAY", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsTradeStageTitleKeyword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        bool hasDDay = text.Contains("D-DAY", StringComparison.OrdinalIgnoreCase);
        bool hasPrepare = text.Contains("准备", StringComparison.Ordinal);
        bool hasAppraise = text.Contains("评鉴战", StringComparison.Ordinal) ||
                           text.Contains("目标评鉴战", StringComparison.Ordinal);
        bool isVictoryProgressTitle = text.Contains("评鉴战胜利", StringComparison.Ordinal) ||
                                      text.Contains("距离目标评鉴战胜利", StringComparison.Ordinal);
        return !isVictoryProgressTitle && (hasDDay || (hasAppraise && hasPrepare));
    }

    public static bool ContainsTradeKeyword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("交易", StringComparison.Ordinal) ||
               text.Contains("商店", StringComparison.Ordinal);
    }

    public static bool ContainsTradeStageHint(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("全新商品到货", StringComparison.Ordinal) ||
               text.Contains("商品到货", StringComparison.Ordinal) ||
               text.Contains("到货", StringComparison.Ordinal);
    }

    public static bool ContainsCommissionKeyword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("委托", StringComparison.Ordinal) ||
               text.Contains("讨伐", StringComparison.Ordinal) ||
               text.Contains("受理", StringComparison.Ordinal);
    }

    public static bool ContainsProgressBranchKeyword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return ContainsAppraiseKeyword(text) || ContainsCommissionKeyword(text);
    }

    public static bool ContainsMainMenuKeyword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("训练", StringComparison.Ordinal) ||
               text.Contains("休息", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断是否为评鉴战"出击前准备/挑战确认"页文案，反向排除二选一菜单的假阳性
    /// </summary>
    public static bool ContainsPrepDetailKeyword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("前准备", StringComparison.Ordinal) ||
               text.Contains("出击", StringComparison.Ordinal) ||
               text.Contains("挑战", StringComparison.Ordinal) ||
               text.Contains("开始战斗", StringComparison.Ordinal);
    }

    /// <summary>
    /// OCR 归一化：去空白、换行、全角空格（Trade 全模块统一通过这个版本）
    /// </summary>
    public static string NormalizeOcr(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return Regex.Replace(raw, @"[\s\u3000]+", "").Trim();
    }

    /// <summary>
    /// 判断是否处于交易详情页（"购买"按钮可见）
    /// </summary>
    public static bool IsTradeDetailScreen(Mat screenshot)
    {
        string buyText = OcrHelper.RecognizeRegion(
                screenshot, TradeBuyKeywordX, TradeBuyKeywordY, TradeBuyKeywordW, TradeBuyKeywordH)
            .GetAwaiter()
            .GetResult();
        string text = NormalizeOcr(buyText);
        return text.Contains("购买", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断是否处于交易相关界面（详情页或列表页）
    /// </summary>
    public static bool IsTradeScreen(Mat screenshot)
    {
        if (IsTradeDetailScreen(screenshot))
            return true;

        if (HasSoldOutRowSignal(screenshot))
            return true;

        var regions = new (double X, double Y, double W, double H)[]
        {
            (0.60, 0.30, 0.36, 0.46),
            (0.56, 0.34, 0.40, 0.40),
            (0.62, 0.40, 0.34, 0.34),
        };
        foreach (var r in regions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H).GetAwaiter().GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text))
                continue;
            if (LooksLikeTradeListText(text) ||
                text.Contains("购买", StringComparison.Ordinal) ||
                text.Contains("OPEN", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("PEN", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasSoldOutRowSignal(Mat screenshot)
    {
        for (int i = 0; i < TradeDetailOcr.OfferSlots.Length; i++)
        {
            if (TradeDetailOcr.IsRowMarkedSoldOut(screenshot, i))
                return true;
        }

        return false;
    }

    public static bool IsCommissionLike(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, 0.56, 0.22, 0.40, 0.56).GetAwaiter().GetResult();
        string text = NormalizeOcr(raw);
        if (string.IsNullOrEmpty(text))
            return false;
        return text.Contains("委托", StringComparison.Ordinal) ||
               text.Contains("讨伐", StringComparison.Ordinal) ||
               text.Contains("受理", StringComparison.Ordinal) ||
               text.Contains("开始委托", StringComparison.Ordinal);
    }

    public static bool LooksLikeTradeListText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        if (ContainsTradeListItemKeyword(text))
            return true;

        if (text.Contains("SOLD", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("OUT", StringComparison.OrdinalIgnoreCase))
            return true;

        return CountPriceLikeNumbers(text) >= 2 && text.Length >= 8;
    }

    public static bool ContainsTradeListItemKeyword(string text)
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
               text.Contains("炖菜", StringComparison.Ordinal);
    }

    public static int CountPriceLikeNumbers(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        return Regex.Matches(text, @"\d{1,3}").Count;
    }

    public static int CountChineseChars(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        int count = 0;
        foreach (char c in text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF)
                count++;
        }
        return count;
    }

    public static int ExtractLargestNumber(string text)
    {
        int max = 0;
        foreach (Match m in Regex.Matches(text, @"\d+"))
        {
            if (int.TryParse(m.Value, out int v) && v > max)
                max = v;
        }
        return max;
    }
}
