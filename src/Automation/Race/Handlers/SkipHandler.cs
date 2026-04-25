using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 检测右上角 SKIP 按钮并点击
/// </summary>
public class SkipHandler : IRaceHandler
{
    public string Name => "SKIP";
    public int Priority => 0;

    private static readonly (double X, double Y, double W, double H)[] SkipTextRegions =
    [
        (0.90, 0.00, 0.10, 0.08),
        (0.86, 0.00, 0.14, 0.10),
    ];

    public bool CanHandle(FrameContext frame)
    {
        string text = ReadSkipText(frame.Screenshot);
        bool hit = IsSkipText(text);
        if (hit)
            Log.Log($"Skip marker hit: '{text}'");
        return hit;
    }

    public string DescribeDecision(FrameContext frame)
    {
        return "Skip: click skip button to fast-forward";
    }

    public async Task HandleAsync(GameContext ctx)
    {
        Log.Log("SKIP detected, clicking fixed top-right skip point...");
        await ctx.ClickAtPercent(0.96, 0.04);
        await ctx.Wait(500);
    }

    private static string ReadSkipText(OpenCvSharp.Mat screenshot)
    {
        string best = "";
        foreach (var region in SkipTextRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = Normalize(raw);
            if (IsSkipText(text))
                return text;
            if (text.Length > best.Length)
                best = text;
        }

        return best;
    }

    private static bool IsSkipText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("SKIP", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("跳过", StringComparison.Ordinal) ||
               text.Contains("跳過", StringComparison.Ordinal);
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
    private static readonly LogScope Log = new("Race:Skip");
}
