using OpenCvSharp;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Trade;

/// <summary>
/// 分流页右侧二选一菜单"按行扫描 + 评分 + 点击前校验"几何与点击辅助
/// </summary>
internal static class TradeStageGeometry
{
    public const double CommissionX = 0.88;
    public const double CommissionY = 0.50;
    public const double TradeX = 0.88;
    public const double TradeY = 0.64;
    public const double OptionLineScanX = 0.72;
    public const double OptionLineScanW = 0.24;
    public const double OptionLineScanH = 0.09;
    public const double OptionLineStep = 0.06;
    public const double OptionVerifyX = 0.66;
    public const double OptionVerifyW = 0.30;
    public const double OptionVerifyH = 0.10;

    public readonly record struct OptionProbe(double Y, int Score, string Text);

    /// <summary>
    /// 在右侧菜单区域动态扫描目标选项行，返回更稳定的点击 Y 坐标
    /// </summary>
    public static double DetectOptionClickY(Mat screenshot, bool isTrade, double fallbackY)
    {
        int lines = 6;
        for (int i = 0; i < lines; i++)
        {
            double y = TradeStageOcr.MenuTextY + i * OptionLineStep;
            if (y + OptionLineScanH > 0.92) break;

            string raw = OcrHelper.RecognizeRegion(
                    screenshot, OptionLineScanX, y, OptionLineScanW, OptionLineScanH)
                .GetAwaiter()
                .GetResult();
            string text = TradeStageOcr.NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text)) continue;

            if (isTrade && TradeStageOcr.ContainsTradeKeyword(text))
                return y + OptionLineScanH / 2;
            if (!isTrade && TradeStageOcr.ContainsProgressBranchKeyword(text))
                return y + OptionLineScanH / 2;
        }

        return fallbackY;
    }

    /// <summary>
    /// 生成候选点击行并按匹配得分排序，确保点击前位置符合预期
    /// </summary>
    public static List<OptionProbe> BuildOptionProbes(Mat? screenshot, bool isTrade, double preferredY, double fallbackY)
    {
        var probes = new List<OptionProbe>();
        var yCandidates = new List<double>
        {
            preferredY,
            fallbackY,
            Math.Clamp(preferredY - 0.03, 0.40, 0.78),
            Math.Clamp(preferredY + 0.03, 0.40, 0.78),
        };
        for (int i = 0; i < 6; i++)
            yCandidates.Add(TradeStageOcr.MenuTextY + i * OptionLineStep + OptionLineScanH / 2);

        var seen = new HashSet<int>();
        foreach (double y in yCandidates)
        {
            int key = (int)Math.Round(y * 1000);
            if (!seen.Add(key))
                continue;
            string text = screenshot == null || screenshot.Empty()
                ? ""
                : ReadOptionTextAtY(screenshot, y);
            int score = ScoreOptionText(text, isTrade);
            probes.Add(new OptionProbe(y, score, ClipText(text)));
        }

        probes.Sort((a, b) =>
        {
            int byScore = b.Score.CompareTo(a.Score);
            if (byScore != 0) return byScore;
            return Math.Abs(a.Y - preferredY).CompareTo(Math.Abs(b.Y - preferredY));
        });

        return probes.Take(4).ToList();
    }

    public static string ReadOptionTextAtY(Mat screenshot, double yCenter)
    {
        double y = Math.Clamp(yCenter - OptionVerifyH / 2, 0.25, 0.82);
        string raw = OcrHelper.RecognizeRegion(screenshot, OptionVerifyX, y, OptionVerifyW, OptionVerifyH)
            .GetAwaiter()
            .GetResult();
        return TradeStageOcr.NormalizeOcr(raw);
    }

    public static int ScoreOptionText(string text, bool isTrade)
    {
        if (string.IsNullOrEmpty(text))
            return -2;

        int score = 0;
        bool hasTrade = TradeStageOcr.ContainsTradeKeyword(text);
        bool hasProgress = TradeStageOcr.ContainsProgressBranchKeyword(text);
        if (isTrade)
        {
            if (hasTrade) score += 8;
            if (hasProgress) score -= 5;
        }
        else
        {
            if (hasProgress) score += 8;
            if (hasTrade) score -= 5;
        }

        if (text.Contains("购买", StringComparison.Ordinal) ||
            text.Contains("药水", StringComparison.Ordinal) ||
            text.Contains("抽奖券", StringComparison.Ordinal))
            score += isTrade ? 4 : -2;

        return score;
    }

    /// <summary>
    /// 点击前二次确认：移动到候选点后重新 OCR 校验该行文本
    /// </summary>
    public static async Task<bool> VerifyTargetBeforeClickAsync(GameContext ctx, double xPct, double yPct, bool expectTrade)
    {
        using var beforeMove = ctx.CaptureScreen();
        if (beforeMove == null || beforeMove.Empty())
            return false;

        int px = (int)(beforeMove.Width * xPct);
        int py = (int)(beforeMove.Height * yPct);
        await MouseSimulator.MoveToClient(ctx.WindowHandle, px, py);
        await ctx.Wait(120);

        using var afterMove = ctx.CaptureScreen();
        if (afterMove == null || afterMove.Empty())
            return false;

        string text = ReadOptionTextAtY(afterMove, yPct);
        int score = ScoreOptionText(text, expectTrade);
        string target = expectTrade ? "trade" : "appraise";
        Logger.Log($"[Race:Trade] Pre-click verify ({target}): y={yPct:F3}, score={score}, text='{ClipText(text)}'");
        return expectTrade ? score >= 3 : score >= 0;
    }

    public static string ClipText(string text, int maxLen = 60)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        if (text.Length <= maxLen)
            return text;
        return text[..maxLen] + "...";
    }
}
