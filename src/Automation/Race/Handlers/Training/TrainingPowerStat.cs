using System.Text.RegularExpressions;
using OpenCvSharp;
using SleepRunner.Recognition;
using SleepRunner.Utils;

namespace SleepRunner.Automation.Race.Handlers.Training;

/// <summary>
/// 左侧属性面板单项数值 OCR（力量 / 体力 通用）
///
/// 拆分意图：
/// - rush fast-path（攻击=力量、生存=体力）共用同一套两段式 OCR 策略
/// - 两段：标准多区域 → 聚焦区域 + 放大 + 二值化兜底
/// - 通过 statName 参数切换匹配关键字与排除规则；其余像素布局完全一致
/// </summary>
internal static class TrainingPowerStat
{
    private static readonly (double X, double Y, double W, double H)[] PowerStatRegions =
    [
        (0.01, 0.12, 0.30, 0.25),
        (0.00, 0.08, 0.35, 0.45),
        (0.02, 0.27, 0.15, 0.12),
    ];

    // 聚焦力量首行数值区域：避免上方 RANK 头部被一起裁进来
    private static readonly (double X, double Y, double W, double H)[] PowerValueFocusedRegions =
    [
        (0.045, 0.335, 0.17, 0.10),
        (0.050, 0.345, 0.16, 0.09),
        (0.060, 0.360, 0.15, 0.07),
    ];

    /// <summary>力量值 OCR；兼容旧调用，等价于 ReadStatAsync(shot, "力量")</summary>
    public static Task<int> ReadPowerStatAsync(Mat? shot) => ReadStatAsync(shot, "力量");

    /// <summary>体力值 OCR；Survival 基调下用于 stamina rush 触发判断</summary>
    public static Task<int> ReadStaminaStatAsync(Mat? shot) => ReadStatAsync(shot, "体力");

    /// <summary>
    /// 通用属性 OCR：根据 statName 切换匹配关键字与排除规则；
    /// 区域百分比沿用力量探测的最佳实践（左侧属性面板上半段）
    /// </summary>
    public static async Task<int> ReadStatAsync(Mat? shot, string statName)
    {
        if (shot == null || shot.Empty())
        {
            Logger.Log($"[Race:TrainingSelect] {statName} stat read: capture empty");
            return -1;
        }

        // Phase 1: 标准多区域 OCR，匹配 "<statName>XXX" 格式
        foreach (var r in PowerStatRegions)
        {
            string raw = await OcrHelper.RecognizeRegion(shot, r.X, r.Y, r.W, r.H);
            string text = TrainingFailRateOcr.NormalizeOcrText(raw);
            if (string.IsNullOrEmpty(text)) continue;

            int value = ParseStatValue(text, statName);
            Logger.Log($"[Race:TrainingSelect] {statName} stat OCR ({r.X:F2},{r.Y:F2},{r.W:F2},{r.H:F2}): '{text}' => {(value >= 0 ? value.ToString() : "N/A")}");
            if (value >= 0) return value;
        }

        // Phase 2: 聚焦数值区域 + 放大/二值化
        Logger.Log($"[Race:TrainingSelect] {statName} stat: standard OCR missed, trying focused regions with preprocessing...");
        foreach (var r in PowerValueFocusedRegions)
        {
            var variants = await RecognizePowerValueVariants(shot, r.X, r.Y, r.W, r.H);
            foreach (string text in variants)
            {
                if (string.IsNullOrEmpty(text)) continue;
                if (TryParseStatValueFocused(text, statName, out int val, out string reason))
                {
                    Logger.Log($"[Race:TrainingSelect] {statName} stat focused ({r.X:F3},{r.Y:F3},{r.W:F3},{r.H:F3}): '{text}' => {val} ({reason})");
                    return val;
                }
                Logger.Log($"[Race:TrainingSelect] {statName} stat focused ({r.X:F3},{r.Y:F3},{r.W:F3},{r.H:F3}): reject '{text}'");
            }
        }

        Logger.Log($"[Race:TrainingSelect] {statName} stat read: no valid value found in any region");
        return -1;
    }

    private static async Task<List<string>> RecognizePowerValueVariants(Mat screenshot, double x, double y, double w, double h)
    {
        int px = Math.Clamp((int)(screenshot.Width * x), 0, screenshot.Width - 1);
        int py = Math.Clamp((int)(screenshot.Height * y), 0, screenshot.Height - 1);
        int pw = Math.Min(Math.Max(1, (int)(screenshot.Width * w)), screenshot.Width - px);
        int ph = Math.Min(Math.Max(1, (int)(screenshot.Height * h)), screenshot.Height - py);
        if (pw <= 1 || ph <= 1) return [];

        using var region = new Mat(screenshot, new Rect(px, py, pw, ph));
        var results = new List<string>();

        results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(region)));

        using (var up = new Mat())
        {
            Cv2.Resize(region, up, new OpenCvSharp.Size(region.Width * 3, region.Height * 3), 0, 0, InterpolationFlags.Cubic);
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(up)));
        }

        using (var gray = new Mat())
        using (var bin = new Mat())
        using (var upBin = new Mat())
        {
            Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            Cv2.Resize(bin, upBin, new OpenCvSharp.Size(bin.Width * 3, bin.Height * 3), 0, 0, InterpolationFlags.Nearest);
            Cv2.CvtColor(upBin, upBin, ColorConversionCodes.GRAY2BGR);
            results.Add(TrainingFailRateOcr.NormalizeOcrText(await OcrHelper.RecognizeMat(upBin)));
        }

        return results;
    }

    private static int ParseStatValue(string text, string statName)
    {
        if (string.IsNullOrEmpty(text)) return -1;
        if (LooksLikeRankText(text)) return -1;

        // 排除 "/" 后的上限值 1250
        var match = Regex.Match(text, $@"{Regex.Escape(statName)}[^\d/]{{0,5}}(\d{{1,4}})\s*/?\s*(?:1250)?");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int val) && val >= 0 && val <= 1300)
            return val;

        match = Regex.Match(text, @"(\d{1,4})\s*/\s*1250");
        if (match.Success && int.TryParse(match.Groups[1].Value, out val) && val >= 0 && val <= 1300)
            return val;

        return -1;
    }

    private static bool TryParseStatValueFocused(string text, string statName, out int value, out string reason)
    {
        value = -1;
        reason = "no-match";
        if (string.IsNullOrEmpty(text)) return false;

        if (LooksLikeRankText(text)) { reason = "rank-like"; return false; }
        if (LooksLikeOtherStatText(text, statName)) { reason = "other-stat-like"; return false; }

        if (LooksLikePrefixedBonusNoise(text))
        {
            reason = "prefixed-bonus-noise";
            return false;
        }

        if (Regex.IsMatch(text, @"^[^\d]*[+＋]\d{3,4}\s*/\s*1250"))
        {
            reason = "prefixed-bonus-noise";
            return false;
        }

        if (Regex.IsMatch(text, @"^[^\d]*(?:/|\uFF0F)\s*1250\D*$"))
        {
            reason = "max-denominator-only";
            return false;
        }

        var match = Regex.Match(text, @"(\d{1,4})\s*/\s*1250");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int slashVal) && slashVal >= 0 && slashVal <= 1300)
        { value = slashVal; reason = "current/max"; return true; }

        match = Regex.Match(text, $@"{Regex.Escape(statName)}[^\d/]{{0,5}}(\d{{1,4}})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int labelVal) && labelVal >= 0 && labelVal <= 1300)
        { value = labelVal; reason = "label+digits"; return true; }

        match = Regex.Match(text, @"^\D*(\d{3,4})\D*$");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int plainVal) && plainVal >= 100 && plainVal <= 1300)
        { value = plainVal; reason = "plain-digits"; return true; }

        reason = "digits-not-trustworthy";
        return false;
    }

    private static bool LooksLikeRankText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("RANK", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(text, @"^R\w*ANK?\d{2,4}$", RegexOptions.IgnoreCase);
    }

    private static bool LooksLikePrefixedBonusNoise(string text)
    {
        return Regex.IsMatch(text, @"^[^\d]*(?:[+＋]|锛媇)\s*\d{1,4}\s*/\s*1250");
    }

    /// <summary>
    /// 排除非目标属性的 OCR 串扰；statName 自身不算"其他属性"
    /// </summary>
    private static bool LooksLikeOtherStatText(string text, string statName)
    {
        if (string.IsNullOrEmpty(text)) return false;
        string[] allStats = ["力量", "体力", "韧性", "专注", "保护", "集中"];
        foreach (var s in allStats)
        {
            if (s == statName) continue;
            if (text.Contains(s, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
