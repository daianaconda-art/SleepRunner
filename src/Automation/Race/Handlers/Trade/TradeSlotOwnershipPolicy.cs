using System.Text.RegularExpressions;

namespace SleepRunner.Automation.Race.Handlers.Trade;

internal static class TradeSlotOwnershipPolicy
{
    internal static bool BelongsToSlot(string rowText, string detailTitle)
    {
        string normalizedRow = NormalizeItemText(rowText);
        string normalizedDetail = NormalizeItemText(detailTitle);

        if (string.IsNullOrEmpty(normalizedRow) || string.IsNullOrEmpty(normalizedDetail))
            return false;

        if (normalizedRow.Contains(normalizedDetail, StringComparison.Ordinal) ||
            normalizedDetail.Contains(normalizedRow, StringComparison.Ordinal))
        {
            return true;
        }

        if (GetCommonPrefixLength(normalizedRow, normalizedDetail) >= 4)
            return true;

        if (GetLongestCommonSubstringLength(normalizedRow, normalizedDetail) >= 5)
            return true;

        string rowCore = RemoveGenericCategoryWords(normalizedRow);
        string detailCore = RemoveGenericCategoryWords(normalizedDetail);
        return rowCore.Length >= 3 &&
               detailCore.Length >= 3 &&
               GetLongestCommonSubstringLength(rowCore, detailCore) >= 3;
    }

    private static string NormalizeItemText(string text)
    {
        string normalized = TradePurchasePolicy.NormalizeTradeSignalText(text);
        normalized = Regex.Replace(normalized, @"\d+", "");
        normalized = Regex.Replace(normalized, @"\s+", "");
        normalized = normalized.Replace("(", "", StringComparison.Ordinal)
                               .Replace(")", "", StringComparison.Ordinal)
                               .Replace("（", "", StringComparison.Ordinal)
                               .Replace("）", "", StringComparison.Ordinal)
                               .Trim();
        normalized = Regex.Replace(normalized, @"[^\u4e00-\u9fff]", "");
        normalized = NormalizeCommonOcrNoise(normalized);
        normalized = StripKnownDetailSuffix(normalized);
        return normalized;
    }

    private static string NormalizeCommonOcrNoise(string text)
    {
        text = text.Replace("哥级", "高级", StringComparison.Ordinal)
                   .Replace("川练", "训练", StringComparison.Ordinal)
                   .Replace("文大利", "义大利", StringComparison.Ordinal)
                   .Replace("义呔利面", "义大利面", StringComparison.Ordinal)
                   .Replace("戈大利面", "义大利面", StringComparison.Ordinal)
                   .Replace("利面组", "义大利面组", StringComparison.Ordinal)
                   .Replace("咖丨啡", "咖啡", StringComparison.Ordinal)
                   .Replace("日非", "咖啡", StringComparison.Ordinal)
                   .Replace("山义大利面", "奶油义大利面", StringComparison.Ordinal)
                   .Replace("鸡捕三", "鸡排", StringComparison.Ordinal)
                   .Replace("鸡捕", "鸡排", StringComparison.Ordinal)
                   .Replace("丨", "", StringComparison.Ordinal)
                   .Replace("乪", "", StringComparison.Ordinal)
                   .Replace("丿", "", StringComparison.Ordinal);
        return Regex.Replace(text, "皇家奶(?!油)", "皇家奶油");
    }

    private static string RemoveGenericCategoryWords(string text)
    {
        foreach (string word in new[] { "料理食物", "训练书籍", "义大利面", "意大利面", "食物", "书籍" })
        {
            text = text.Replace(word, "", StringComparison.Ordinal);
        }

        return text;
    }

    private static string StripKnownDetailSuffix(string text)
    {
        foreach (string suffix in new[] { "料理食物", "训练书籍", "食物", "书籍" })
        {
            if (text.EndsWith(suffix, StringComparison.Ordinal) &&
                text.Length > suffix.Length + 2)
            {
                return text[..^suffix.Length];
            }
        }

        return text;
    }

    private static int GetCommonPrefixLength(string left, string right)
    {
        int max = Math.Min(left.Length, right.Length);
        int count = 0;
        for (int i = 0; i < max; i++)
        {
            if (left[i] != right[i])
                break;
            count++;
        }

        return count;
    }

    private static int GetLongestCommonSubstringLength(string left, string right)
    {
        int best = 0;
        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];

        for (int i = 1; i <= left.Length; i++)
        {
            for (int j = 1; j <= right.Length; j++)
            {
                current[j] = left[i - 1] == right[j - 1]
                    ? previous[j - 1] + 1
                    : 0;
                best = Math.Max(best, current[j]);
            }

            Array.Clear(previous);
            (previous, current) = (current, previous);
        }

        return best;
    }
}
