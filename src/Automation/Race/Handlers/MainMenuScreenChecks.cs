using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race.Handlers;

internal static class MainMenuScreenChecks
{
    private const double MenuDiamondX = 0.91;
    private const double TrainDiamondY = 0.40;
    private const double CommissionDiamondY = 0.56;
    private const double RestDiamondY = 0.626;

    private const string TrainText = "\u8bad\u7ec3";
    private const string CommissionText = "\u59d4\u6258";
    private const string RaidText = "\u8ba8\u4f10";
    private const string RestText = "\u4f11\u606f";
    private const string AcceptText = "\u53d7\u7406";

    private static readonly (double ClickY, double TextX, double TextY, double TextW, double TextH)[] MenuRowProbes =
    [
        (TrainDiamondY, 0.74, 0.34, 0.22, 0.10),
        (CommissionDiamondY, 0.74, 0.50, 0.22, 0.10),
        (RestDiamondY, 0.74, 0.58, 0.22, 0.10),
    ];

    public static bool IsMainMenuScreen(Mat screenshot, out string summary)
    {
        var rows = ReadRows(screenshot);
        summary = string.Join(" | ", rows.Select(row => $"y={row.ClickY:F3},text='{row.Text}'"));

        bool hasTrain = rows.Any(row => row.Text.Contains(TrainText, StringComparison.Ordinal));
        bool hasCommission = rows.Any(row => row.Text.Contains(CommissionText, StringComparison.Ordinal) ||
                                             row.Text.Contains(RaidText, StringComparison.Ordinal));
        bool hasRest = rows.Any(row => row.Text.Contains(RestText, StringComparison.Ordinal));

        int expectedHits = 0;
        if (rows.Length > 0 && rows[0].Text.Contains(TrainText, StringComparison.Ordinal)) expectedHits++;
        if (rows.Length > 1 && (rows[1].Text.Contains(CommissionText, StringComparison.Ordinal) ||
                                rows[1].Text.Contains(RaidText, StringComparison.Ordinal))) expectedHits++;
        if (rows.Length > 2 && rows[2].Text.Contains(RestText, StringComparison.Ordinal)) expectedHits++;

        return expectedHits >= 2 ||
               (hasTrain && hasRest) ||
               (hasTrain && hasCommission) ||
               (hasCommission && hasRest);
    }

    public static (double X, double Y) ResolveMenuClickPoint(Mat? screenshot, bool preferCommission)
    {
        return ResolveMenuClickPoint(
            screenshot,
            preferCommission ? MenuTarget.Commission : MenuTarget.Training);
    }

    public static (double X, double Y) ResolveRestMenuClickPoint(Mat? screenshot)
    {
        return ResolveMenuClickPoint(screenshot, MenuTarget.Rest);
    }

    private static (double X, double Y) ResolveMenuClickPoint(Mat? screenshot, MenuTarget target)
    {
        double fallbackY = target switch
        {
            MenuTarget.Commission => CommissionDiamondY,
            MenuTarget.Rest => RestDiamondY,
            _ => TrainDiamondY,
        };

        if (screenshot == null || screenshot.Empty())
            return (MenuDiamondX, fallbackY);

        int bestScore = int.MinValue;
        double bestY = fallbackY;
        foreach (var row in ReadRows(screenshot))
        {
            int score = ScoreRow(row, target);
            if (score > bestScore)
            {
                bestScore = score;
                bestY = row.ClickY;
            }
        }

        return (MenuDiamondX, bestY);
    }

    private static int ScoreRow((double ClickY, string Text) row, MenuTarget target)
    {
        if (target == MenuTarget.Commission)
        {
            int score = Math.Abs(row.ClickY - CommissionDiamondY) < 0.01 ? 2 : 0;
            if (row.Text.Contains(CommissionText, StringComparison.Ordinal)) score += 6;
            if (row.Text.Contains(RaidText, StringComparison.Ordinal) || row.Text.Contains(AcceptText, StringComparison.Ordinal)) score += 4;
            if (row.Text.Contains(TrainText, StringComparison.Ordinal) || row.Text.Contains(RestText, StringComparison.Ordinal)) score -= 2;
            return score;
        }

        if (target == MenuTarget.Rest)
        {
            int score = Math.Abs(row.ClickY - RestDiamondY) < 0.01 ? 2 : 0;
            if (row.Text.Contains(RestText, StringComparison.Ordinal)) score += 6;
            if (row.Text.Contains(TrainText, StringComparison.Ordinal) || row.Text.Contains(CommissionText, StringComparison.Ordinal)) score -= 2;
            return score;
        }

        {
            int score = Math.Abs(row.ClickY - TrainDiamondY) < 0.01 ? 2 : 0;
            if (row.Text.Contains(TrainText, StringComparison.Ordinal)) score += 6;
            if (row.Text.Contains(CommissionText, StringComparison.Ordinal) || row.Text.Contains(RestText, StringComparison.Ordinal)) score -= 2;
            return score;
        }
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

    private enum MenuTarget
    {
        Training,
        Commission,
        Rest,
    }
}
