using OpenCvSharp;
using SleepRunner.Input;
using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// Esc 误触后出现的系统菜单覆盖层：检测到后点击右侧遮罩区关闭。
/// </summary>
public class OverlayMenuHandler : IRaceHandler
{
    public string Name => "覆盖层关闭";
    public int Priority => 2;

    private static readonly (double X, double Y, double W, double H)[] MenuTextRegions =
    [
        (0.26, 0.35, 0.50, 0.14),
        (0.26, 0.50, 0.52, 0.28),
        (0.24, 0.34, 0.56, 0.38),
    ];

    // 按用户建议，优先点击菜单右侧遮罩空白区关闭覆盖层
    private const double DismissClickX = 0.90;
    private const double DismissClickY = 0.46;

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string text = ReadMenuOverlayText(screenshot);
        bool hit = IsOverlayMenuText(text);
        if (hit)
            Log.Log($"Overlay menu hit: '{text}'");
        return hit;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string text = ReadMenuOverlayText(screenshot);
        return $"Overlay menu: dismiss by click right-side mask ('{text}')";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        Log.Log($"Overlay menu: click dismiss mask at ({DismissClickX:F2},{DismissClickY:F2})");
        await ctx.ClickAtPercent(DismissClickX, DismissClickY);
        await ctx.Wait(700);
    }

    public async Task ProbeAsync(GameContext ctx)
    {
        using var shot = ctx.CaptureScreen();
        if (shot == null || shot.Empty())
            return;

        int x = (int)(shot.Width * DismissClickX);
        int y = (int)(shot.Height * DismissClickY);
        Log.Log($"Overlay menu probe: move dismiss point=({DismissClickX:F3},{DismissClickY:F3}) => ({x},{y})");
        await MouseSimulator.MoveToClient(ctx.WindowHandle, x, y);
        await ctx.Wait(200);
    }

    private static string ReadMenuOverlayText(Mat screenshot)
    {
        string best = "";
        int bestScore = int.MinValue;
        foreach (var r in MenuTextRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, r.X, r.Y, r.W, r.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text))
                continue;

            int score = 0;
            if (text.Contains("菜单", StringComparison.Ordinal)) score += 12;
            if (text.Contains("指南", StringComparison.Ordinal)) score += 8;
            if (text.Contains("选项", StringComparison.Ordinal)) score += 8;
            if (text.Contains("编制信息", StringComparison.Ordinal)) score += 8;
            if (text.Contains("观测结束", StringComparison.Ordinal)) score += 10;
            if (text.Contains("重新观测", StringComparison.Ordinal)) score += 10;
            if (text.Contains("储存后前往大厅", StringComparison.Ordinal) ||
                text.Contains("存后前往大厅", StringComparison.Ordinal))
                score += 12;
            score += Math.Min(8, text.Length / 8);

            if (score > bestScore || (score == bestScore && text.Length > best.Length))
            {
                best = text;
                bestScore = score;
            }
        }
        return best;
    }

    private static bool IsOverlayMenuText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        bool hasMenuTitle = text.Contains("菜单", StringComparison.Ordinal);
        int optionHits = 0;
        if (text.Contains("指南", StringComparison.Ordinal)) optionHits++;
        if (text.Contains("选项", StringComparison.Ordinal)) optionHits++;
        if (text.Contains("编制信息", StringComparison.Ordinal)) optionHits++;
        if (text.Contains("观测结束", StringComparison.Ordinal)) optionHits++;
        if (text.Contains("重新观测", StringComparison.Ordinal)) optionHits++;
        if (text.Contains("储存后前往大厅", StringComparison.Ordinal) ||
            text.Contains("存后前往大厅", StringComparison.Ordinal))
            optionHits++;

        return hasMenuTitle && optionHits >= 2;
    }

    private static string NormalizeOcr(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        return raw
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Trim();
    }
    private static readonly LogScope Log = new("Race:Overlay");
}
