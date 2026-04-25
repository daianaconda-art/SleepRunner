using OpenCvSharp;
using SleepRunner.Recognition;
using SleepRunner.Utils;
using SleepRunner.Vision;

namespace SleepRunner.Automation.Race.Handlers;

/// <summary>
/// 旅程结束页：右侧出现“旅程结束”时直接结束脚本，不再继续主菜单决策。
/// </summary>
public sealed class JourneyEndHandler : IRaceHandler
{
    public string Name => "旅程结束";
    public int Priority => 19;

    private static readonly (double X, double Y, double W, double H)[] JourneyEndRegions =
    [
        (0.74, 0.50, 0.22, 0.10),
        (0.72, 0.48, 0.24, 0.14),
        (0.70, 0.46, 0.28, 0.18),
        (0.78, 0.58, 0.20, 0.16),
        (0.74, 0.54, 0.24, 0.20),
        (0.80, 0.60, 0.18, 0.14),
    ];

    public bool CanHandle(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string text = ReadJourneyEndText(screenshot);
        bool hit = IsJourneyEndText(text);
        if (hit)
            Log.Log($"Journey-end hit: '{text}'");
        return hit;
    }

    public string DescribeDecision(FrameContext frame)
    {
        var screenshot = frame.Screenshot;
        string text = ReadJourneyEndText(screenshot);
        return $"Journey end detected ('{text}') -> stop race script";
    }

    public Task HandleAsync(GameContext ctx)
    {
        throw new RaceTaskCompletedException("Journey end detected.");
    }

    private static string ReadJourneyEndText(Mat screenshot)
    {
        string best = "";
        foreach (var region in JourneyEndRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text))
                continue;
            if (IsJourneyEndText(text))
                return text;
            if (text.Length > best.Length)
                best = text;
        }

        return best;
    }

    private static bool IsJourneyEndText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        if (text.Contains("旅程结束", StringComparison.Ordinal))
            return true;

        return text.Contains("旅程", StringComparison.Ordinal) &&
               text.Contains("结束", StringComparison.Ordinal);
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

    private static readonly LogScope Log = new("Race:JourneyEnd");
}
