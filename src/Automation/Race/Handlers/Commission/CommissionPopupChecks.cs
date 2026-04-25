using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race.Handlers.Commission;

/// <summary>
/// 委托弹窗类型枚举：用于区分 start-only 单按钮 vs 困难判定二选一
/// </summary>
internal enum PopupDecisionMode
{
    None = 0,
    DifficultyBased = 1,
    StartOnly = 2,
}

/// <summary>
/// 委托二次弹窗的文本指纹与决策模式分类
/// </summary>
internal static class CommissionPopupChecks
{
    /// <summary>
    /// 在所有候选区域里 OCR 一遍，命中任一关键词即返回 true
    /// </summary>
    public static bool DetectDecisionPopup(Mat screenshot, out string normalizedText)
    {
        var hits = new List<string>();
        foreach (var region in CommissionOcrRegions.PopupDetectRegions)
        {
            string raw = OcrHelper.RecognizeRegion(screenshot, region.X, region.Y, region.W, region.H)
                .GetAwaiter()
                .GetResult();
            string text = CommissionOcrRegions.NormalizeOcr(raw);
            if (string.IsNullOrEmpty(text))
                continue;

            hits.Add(text);
            if (ContainsPopupDecisionKeywords(text))
            {
                normalizedText = string.Join("|", hits.Distinct());
                return true;
            }
        }

        normalizedText = string.Join("|", hits.Distinct());
        return ContainsPopupDecisionKeywords(normalizedText);
    }

    /// <summary>
    /// 弹窗关键词命中：开始委托 / 跳过战斗 / 评鉴战询问 / 战斗委托询问
    /// </summary>
    public static bool ContainsPopupDecisionKeywords(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        bool startHit = text.Contains("开始委托", StringComparison.Ordinal) ||
                        (text.Contains("开始", StringComparison.Ordinal) &&
                         text.Contains("委托", StringComparison.Ordinal));
        bool skipHit = text.Contains("跳过战斗", StringComparison.Ordinal) ||
                       (text.Contains("跳过", StringComparison.Ordinal) &&
                        text.Contains("战斗", StringComparison.Ordinal));
        bool battleCommissionQuestionHit = IsBattleCommissionQuestionPopup(text);
        bool appraiseQuestionHit = IsAppraiseQuestionPopup(text);
        return startHit || skipHit || battleCommissionQuestionHit || appraiseQuestionHit;
    }

    /// <summary>
    /// 评鉴战询问页：标题 + “是否要进行评鉴战”问句
    /// 这类页统一走“困难才开始，否则跳过”的通用规则，不并入 start-only
    /// </summary>
    public static bool IsAppraiseQuestionPopup(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        bool hasAppraiseTitle =
            text.Contains("最终评鉴战", StringComparison.Ordinal) ||
            text.Contains("讨伐评鉴战", StringComparison.Ordinal) ||
            text.Contains("虚空评鉴战", StringComparison.Ordinal) ||
            text.Contains("虚空评鉴占", StringComparison.Ordinal) ||
            text.Contains("评鉴战", StringComparison.Ordinal) ||
            text.Contains("评鉴占", StringComparison.Ordinal);
        bool hasQuestion =
            text.Contains("是否要进行评鉴战", StringComparison.Ordinal) ||
            text.Contains("是否进行评鉴战", StringComparison.Ordinal) ||
            (text.Contains("是否", StringComparison.Ordinal) &&
             (text.Contains("评鉴战", StringComparison.Ordinal) ||
              text.Contains("评鉴占", StringComparison.Ordinal)));
        return hasAppraiseTitle && hasQuestion;
    }

    /// <summary>
    /// 战斗委托询问页：是否要进行战斗委托
    /// </summary>
    public static bool IsBattleCommissionQuestionPopup(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        return text.Contains("是否要进行战斗委托", StringComparison.Ordinal) ||
               text.Contains("进行战斗委托", StringComparison.Ordinal) ||
               (text.Contains("战斗委托", StringComparison.Ordinal) &&
                text.Contains("是否", StringComparison.Ordinal));
    }

    /// <summary>
    /// 弹窗模式分类：评鉴战/战斗委托问句走困难判定，单按钮（仅“开始委托”）直接 start-only
    /// </summary>
    public static PopupDecisionMode ClassifyPopupDecisionMode(string popupText)
    {
        if (string.IsNullOrEmpty(popupText))
            return PopupDecisionMode.None;

        if (IsAppraiseQuestionPopup(popupText) || IsBattleCommissionQuestionPopup(popupText))
            return PopupDecisionMode.DifficultyBased;

        bool hasStart = popupText.Contains("开始委托", StringComparison.Ordinal) ||
                        (popupText.Contains("开始", StringComparison.Ordinal) &&
                         popupText.Contains("委托", StringComparison.Ordinal));
        bool hasSkip = popupText.Contains("跳过战斗", StringComparison.Ordinal) ||
                       (popupText.Contains("跳过", StringComparison.Ordinal) &&
                        popupText.Contains("战斗", StringComparison.Ordinal));
        if (hasStart && !hasSkip)
            return PopupDecisionMode.StartOnly;

        return PopupDecisionMode.DifficultyBased;
    }

    /// <summary>
    /// 红色困难判定：关键词“困难” + 红色像素占比（占比仅作日志参考，业务规则只看关键词）
    /// </summary>
    public static bool DetectRedDifficult(
        Mat screenshot,
        string? popupTextHint,
        out double redRatio,
        out bool hasDifficultKeyword)
    {
        string raw = OcrHelper.RecognizeRegion(
                screenshot,
                CommissionOcrRegions.DifficultyRegionX,
                CommissionOcrRegions.DifficultyRegionY,
                CommissionOcrRegions.DifficultyRegionW,
                CommissionOcrRegions.DifficultyRegionH)
            .GetAwaiter()
            .GetResult();
        string text = CommissionOcrRegions.NormalizeOcr(raw);
        bool regionKeyword = text.Contains("困难", StringComparison.Ordinal);
        bool popupKeyword = !string.IsNullOrEmpty(popupTextHint) &&
                           popupTextHint.Contains("困难", StringComparison.Ordinal);
        hasDifficultKeyword = regionKeyword || popupKeyword;

        using var hsv = new Mat();
        Cv2.CvtColor(screenshot, hsv, ColorConversionCodes.BGR2HSV);
        int w = screenshot.Width;
        int h = screenshot.Height;
        int x = (int)(w * CommissionOcrRegions.DifficultyRegionX);
        int y = (int)(h * CommissionOcrRegions.DifficultyRegionY);
        int rw = Math.Max(1, (int)(w * CommissionOcrRegions.DifficultyRegionW));
        int rh = Math.Max(1, (int)(h * CommissionOcrRegions.DifficultyRegionH));
        x = Math.Clamp(x, 0, w - 1);
        y = Math.Clamp(y, 0, h - 1);
        rw = Math.Min(rw, w - x);
        rh = Math.Min(rh, h - y);

        using var region = new Mat(hsv, new Rect(x, y, rw, rh));
        using var mask1 = new Mat();
        using var mask2 = new Mat();
        // 红色 HSV 双区间
        Cv2.InRange(region, new Scalar(0, 80, 80), new Scalar(10, 255, 255), mask1);
        Cv2.InRange(region, new Scalar(160, 80, 80), new Scalar(180, 255, 255), mask2);
        using var mask = new Mat();
        Cv2.BitwiseOr(mask1, mask2, mask);

        int redPixels = Cv2.CountNonZero(mask);
        int totalPixels = rw * rh;
        redRatio = totalPixels > 0 ? (double)redPixels / totalPixels : 0;

        // 业务规则：只要识别到“困难”就按困难处理（直接开始委托）
        return hasDifficultKeyword;
    }
}
