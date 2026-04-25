using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race.Handlers.Training;

/// <summary>
/// 训练页/分支区域 OCR 与文本判定（与"主菜单/委托/交易"分流相关）
/// </summary>
internal static class TrainingScreenChecks
{
    /// <summary>读取右侧分支区域文字，用于训练/委托分流</summary>
    public static string ReadBranchText(Mat screenshot)
    {
        string raw = OcrHelper.RecognizeRegion(screenshot, 0.68, 0.40, 0.30, 0.40)
            .GetAwaiter().GetResult();
        return Normalize(raw);
    }

    public static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw
            .Replace(" ", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\u3000", "")
            .Trim();
    }

    /// <summary>
    /// 训练详细页指纹：≥2 个 "XX训练" / "训练Lv.N" / 继续训练等
    /// </summary>
    public static bool IsTrainingDetailText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        int trainingHits = 0;
        if (text.Contains("力量训练", StringComparison.Ordinal)) trainingHits++;
        if (text.Contains("体力训练", StringComparison.Ordinal)) trainingHits++;
        if (text.Contains("韧性训练", StringComparison.Ordinal)) trainingHits++;
        if (text.Contains("集中训练", StringComparison.Ordinal) || text.Contains("专注训练", StringComparison.Ordinal)) trainingHits++;
        if (text.Contains("保护训练", StringComparison.Ordinal)) trainingHits++;
        if (trainingHits >= 2)
            return true;

        if (Regex.IsMatch(text, @"(力量|体力|韧性|集中|专注|保护)训练L?V?\.?\d", RegexOptions.IgnoreCase))
            return true;

        if ((text.Contains("继续训练", StringComparison.Ordinal) ||
             text.Contains("返回住处", StringComparison.Ordinal)) &&
            text.Contains("训练", StringComparison.Ordinal))
            return true;

        if (Regex.Matches(text, @"训练L?V?\.?\d").Count >= 2)
            return true;
        return false;
    }

    /// <summary>
    /// 交易商品文本指纹（避免训练 handler 误吃交易页）
    /// </summary>
    public static bool IsTradeItemText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        int tradeHits = 0;
        if (text.Contains("\u8d2d\u4e70", StringComparison.Ordinal)) tradeHits += 2;
        if (text.Contains("\u7981\u4e66", StringComparison.Ordinal)) tradeHits++;
        if (text.Contains("\u79d8\u7b08", StringComparison.Ordinal)) tradeHits++;
        if (text.Contains("\u5546\u54c1", StringComparison.Ordinal)) tradeHits++;
        if (text.Contains("\u8bc4\u9274\u6218", StringComparison.Ordinal)) tradeHits++;

        if (tradeHits >= 2)
            return true;
        return tradeHits > 0 && Regex.IsMatch(text, @"\d{1,4}");
    }
}
