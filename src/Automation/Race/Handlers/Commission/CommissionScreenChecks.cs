using OpenCvSharp;
using SleepRunner.Recognition;

namespace SleepRunner.Automation.Race.Handlers.Commission;

internal static class CommissionScreenChecks
{
    private static readonly string[] CommissionTierKeywords =
    [
        "低阶委托",
        "初阶委托",
        "中阶委托",
        "高阶委托",
    ];

    private static readonly string[] CommissionTierRomanKeywords =
    [
        "I低阶委托",
        "I初阶委托",
        "II中阶委托",
        "III高阶委托",
    ];

    public static bool IsBattleResultScreen(Mat screenshot)
    {
        string title = CommissionOcrRegions.NormalizeOcr(OcrHelper.RecognizeRegion(
                screenshot,
                CommissionOcrRegions.VictoryTitleX,
                CommissionOcrRegions.VictoryTitleY,
                CommissionOcrRegions.VictoryTitleW,
                CommissionOcrRegions.VictoryTitleH)
            .GetAwaiter()
            .GetResult());
        if (title.ToUpperInvariant().Contains("VICTORY", StringComparison.Ordinal) ||
            title.Contains("回合以内获胜", StringComparison.Ordinal) ||
            title.Contains("达成度", StringComparison.Ordinal))
            return true;

        string leaveText = CommissionOcrRegions.NormalizeOcr(OcrHelper.RecognizeRegion(
                screenshot,
                CommissionOcrRegions.LeaveBtnTextX,
                CommissionOcrRegions.LeaveBtnTextY,
                CommissionOcrRegions.LeaveBtnTextW,
                CommissionOcrRegions.LeaveBtnTextH)
            .GetAwaiter()
            .GetResult());
        return leaveText.Contains("离开", StringComparison.Ordinal) ||
               leaveText.Contains("離開", StringComparison.Ordinal);
    }

    public static bool IsAppraiseAcceptDetailScreen(Mat screenshot)
    {
        string titleText = CommissionOcrRegions.ReadBestText(screenshot, CommissionOcrRegions.AppraiseTitleRegions);
        string detailText = CommissionOcrRegions.ReadBestText(screenshot, CommissionOcrRegions.AppraiseDetailRegions);
        string acceptText = CommissionOcrRegions.ReadBestText(screenshot, CommissionOcrRegions.AppraiseAcceptTextRegions);
        return IsAppraiseAcceptDetailText(titleText, detailText, acceptText);
    }

    internal static bool IsAppraiseAcceptDetailText(string titleText, string detailText, string acceptText)
    {
        bool hasAppraiseTitle = titleText.Contains("评鉴战", StringComparison.Ordinal) ||
                                detailText.Contains("评鉴战", StringComparison.Ordinal);
        bool hasAccept = acceptText.Contains("接受", StringComparison.Ordinal) ||
                         detailText.Contains("接受", StringComparison.Ordinal);
        bool hasPrepareInstruction = IsAppraisePrepareInstructionText(detailText);
        bool hasDetailSheet = detailText.Contains("建议综合等级", StringComparison.Ordinal) ||
                              detailText.Contains("登场敌人", StringComparison.Ordinal) ||
                              detailText.Contains("额外奖励", StringComparison.Ordinal) ||
                              detailText.Contains("队伍编制", StringComparison.Ordinal);
        bool hasPopupAction = detailText.Contains("开始委托", StringComparison.Ordinal) ||
                              detailText.Contains("跳过战斗", StringComparison.Ordinal) ||
                              acceptText.Contains("跳过", StringComparison.Ordinal);
        bool tierSelection = IsCommissionTierSelectionText(detailText);
        return hasAppraiseTitle && (hasAccept || hasPrepareInstruction) && hasDetailSheet && !hasPopupAction && !tierSelection;
    }

    private static bool IsAppraisePrepareInstructionText(string detailText)
    {
        if (string.IsNullOrEmpty(detailText))
            return false;

        bool hasPrepare = detailText.Contains("战前准备", StringComparison.Ordinal) ||
                          detailText.Contains("即将开始", StringComparison.Ordinal);
        bool hasBattleSheet = detailText.Contains("建议综合等级", StringComparison.Ordinal) ||
                              detailText.Contains("登场敌人", StringComparison.Ordinal) ||
                              detailText.Contains("可获得奖励", StringComparison.Ordinal);
        return hasPrepare && hasBattleSheet;
    }

    internal static bool IsCommissionTierSelectionText(string detailText)
    {
        if (string.IsNullOrEmpty(detailText))
            return false;

        int namedTierCount = CountContains(detailText, CommissionTierKeywords);
        if (namedTierCount >= 2)
            return true;

        int romanTierCount = CountContains(detailText, CommissionTierRomanKeywords);
        return romanTierCount >= 2;
    }

    public static bool IsCommissionAcceptDetailScreen(Mat screenshot)
    {
        string detailText = CommissionOcrRegions.ReadBestText(screenshot, CommissionOcrRegions.AppraiseDetailRegions);
        string acceptText = CommissionOcrRegions.ReadBestText(screenshot, CommissionOcrRegions.AppraiseAcceptTextRegions);

        bool hasAccept = acceptText.Contains("接受", StringComparison.Ordinal) ||
                         detailText.Contains("接受", StringComparison.Ordinal);
        bool hasDetailSheet = detailText.Contains("建议综合等级", StringComparison.Ordinal) ||
                              detailText.Contains("登场敌人", StringComparison.Ordinal) ||
                              detailText.Contains("讨伐委托", StringComparison.Ordinal) ||
                              detailText.Contains("高阶委托", StringComparison.Ordinal) ||
                              detailText.Contains("中阶委托", StringComparison.Ordinal) ||
                              detailText.Contains("初阶委托", StringComparison.Ordinal) ||
                              detailText.Contains("低阶委托", StringComparison.Ordinal);
        bool hasPopupAction = detailText.Contains("开始委托", StringComparison.Ordinal) ||
                              detailText.Contains("跳过战斗", StringComparison.Ordinal);
        bool targetSelectionReady = IsThirdCommissionSelectionReady(screenshot);
        return hasAccept && hasDetailSheet && !hasPopupAction && targetSelectionReady;
    }

    public static bool IsThirdCommissionSelectionReady(Mat screenshot)
    {
        string titleText = CommissionOcrRegions.ReadBestText(screenshot, CommissionOcrRegions.CommissionCurrentTitleRegions);
        string tierText = CommissionOcrRegions.ReadBestText(screenshot, CommissionOcrRegions.CommissionCurrentTierRegions);
        string combined = $"{titleText}|{tierText}";

        bool hasHighTier = combined.Contains("高阶委托", StringComparison.Ordinal);
        bool hasThirdRoman = combined.Contains("III", StringComparison.Ordinal);
        return hasHighTier || hasThirdRoman;
    }

    public static bool IsCommissionListText(string commissionText)
    {
        if (string.IsNullOrEmpty(commissionText))
            return false;

        return commissionText.Contains("讨伐委托", StringComparison.Ordinal) ||
               commissionText.Contains("受理委托", StringComparison.Ordinal) ||
               commissionText.Contains("受理讨伐委托", StringComparison.Ordinal) ||
               commissionText.Contains("高阶委托", StringComparison.Ordinal) ||
               commissionText.Contains("中阶委托", StringComparison.Ordinal) ||
               commissionText.Contains("初阶委托", StringComparison.Ordinal) ||
               commissionText.Contains("低阶委托", StringComparison.Ordinal);
    }

    private static int CountContains(string text, IEnumerable<string> keywords)
    {
        int count = 0;
        foreach (string keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.Ordinal))
                count++;
        }

        return count;
    }
}
