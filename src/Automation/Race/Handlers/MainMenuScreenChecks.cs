using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race.Handlers;

internal static class MainMenuScreenChecks
{
    private static readonly (double ClickY, double TextX, double TextY, double TextW, double TextH)[] MenuRowProbes =
    [
        (0.40, 0.74, 0.34, 0.22, 0.10),
        (0.56, 0.74, 0.50, 0.22, 0.10),
        (0.72, 0.74, 0.66, 0.22, 0.10),
    ];

    public static bool IsMainMenuScreen(Mat screenshot, out string summary)
    {
        var rows = ReadRows(screenshot);
        summary = string.Join(" | ", rows.Select(row => $"y={row.ClickY:F3},text='{row.Text}'"));

        bool hasTrain = rows.Any(row => row.Text.Contains("训练", StringComparison.Ordinal));
        bool hasCommission = rows.Any(row => row.Text.Contains("委托", StringComparison.Ordinal) ||
                                             row.Text.Contains("讨伐", StringComparison.Ordinal));
        bool hasRest = rows.Any(row => row.Text.Contains("休息", StringComparison.Ordinal));

        int expectedHits = 0;
        if (rows.Length > 0 && rows[0].Text.Contains("训练", StringComparison.Ordinal)) expectedHits++;
        if (rows.Length > 1 && (rows[1].Text.Contains("委托", StringComparison.Ordinal) ||
                                rows[1].Text.Contains("讨伐", StringComparison.Ordinal))) expectedHits++;
        if (rows.Length > 2 && rows[2].Text.Contains("休息", StringComparison.Ordinal)) expectedHits++;

        return expectedHits >= 2 ||
               (hasTrain && hasRest) ||
               (hasTrain && hasCommission) ||
               (hasCommission && hasRest);
    }

    public static (double X, double Y) ResolveMenuClickPoint(Mat? screenshot, bool preferCommission)
    {
        const double menuDiamondX = 0.91;
        const double trainDiamondY = 0.40;
        const double commissionDiamondY = 0.56;

        if (screenshot == null || screenshot.Empty())
            return (menuDiamondX, preferCommission ? commissionDiamondY : trainDiamondY);

        int bestScore = int.MinValue;
        double bestY = preferCommission ? commissionDiamondY : trainDiamondY;
        foreach (var row in ReadRows(screenshot))
        {
            int score = 0;
            if (preferCommission)
            {
                if (row.Text.Contains("委托", StringComparison.Ordinal)) score += 6;
                if (row.Text.Contains("讨伐", StringComparison.Ordinal) || row.Text.Contains("受理", StringComparison.Ordinal)) score += 4;
                if (Math.Abs(row.ClickY - commissionDiamondY) < 0.01) score += 2;
                if (row.Text.Contains("训练", StringComparison.Ordinal) || row.Text.Contains("休息", StringComparison.Ordinal)) score -= 2;
            }
            else
            {
                if (row.Text.Contains("训练", StringComparison.Ordinal)) score += 6;
                if (Math.Abs(row.ClickY - trainDiamondY) < 0.01) score += 2;
                if (row.Text.Contains("委托", StringComparison.Ordinal) || row.Text.Contains("休息", StringComparison.Ordinal)) score -= 2;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestY = row.ClickY;
            }
        }

        return (menuDiamondX, bestY);
    }

    private static (double ClickY, string Text)[] ReadRows(Mat screenshot)
    {
        var rows = new List<(double ClickY, string Text)>();
        foreach (var row in MenuRowProbes)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, row.TextX, row.TextY, row.TextW, row.TextH)
                .GetAwaiter()
                .GetResult();
            rows.Add((row.ClickY, Normalize(raw)));
        }

        return [.. rows];
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Trim();
    }
}
