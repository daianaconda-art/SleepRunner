using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race.Handlers.Battle;

/// <summary>
/// 战斗页指纹识别（BURST 文本）
/// </summary>
internal static class BattleScreenChecks
{
    // BURST 文字可能因分辨率/动画抖动出现偏移，使用多个候选区域提高识别稳定性
    private static readonly (double X, double Y, double W, double H)[] BurstRegions =
    [
        (0.62, 0.76, 0.34, 0.20),
        (0.56, 0.74, 0.40, 0.24),
        (0.68, 0.80, 0.26, 0.16),
    ];

    /// <summary>
    /// 顺序扫描所有 BURST 候选区域，命中即返回；都没命中时返回首个区域文本（用于日志诊断）
    /// </summary>
    public static string ReadBurstText(Mat screenshot)
    {
        foreach (var region in BurstRegions)
        {
            string raw = OcrHelper.RecognizeRegion(
                    screenshot,
                    region.X,
                    region.Y,
                    region.W,
                    region.H)
                .GetAwaiter()
                .GetResult();

            string normalized = NormalizeOcr(raw);
            if (IsBurstText(normalized))
                return normalized;
        }

        // 返回首个候选区域文本用于日志观察（便于后续继续微调）
        var first = BurstRegions[0];
        string fallbackRaw = OcrHelper.RecognizeRegion(
                screenshot,
                first.X,
                first.Y,
                first.W,
                first.H)
            .GetAwaiter()
            .GetResult();
        return NormalizeOcr(fallbackRaw);
    }

    /// <summary>
    /// BURST 文字兼容匹配：处理 OCR 常见误差（空格、感叹号、数字替代）
    /// </summary>
    public static bool IsBurstText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        string upper = text.ToUpperInvariant();
        if (upper.Contains("BURST", StringComparison.Ordinal))
            return true;

        // 常见 OCR 误识别：U->V、S->5、T->7
        upper = upper
            .Replace('V', 'U')
            .Replace('5', 'S')
            .Replace('7', 'T');
        if (upper.Contains("BURST", StringComparison.Ordinal))
            return true;

        // OCR 常见极端误读：BURST -> "B皿釘"（仅保留首字母 B）
        // 约束为"以 B 开头且长度较短"，避免过宽误判
        return upper.StartsWith("B", StringComparison.Ordinal) && upper.Length <= 5;
    }

    private static string NormalizeOcr(string raw)
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
