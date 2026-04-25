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

        return GetCommonPrefixLength(normalizedRow, normalizedDetail) >= 4;
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
        return normalized;
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
}
