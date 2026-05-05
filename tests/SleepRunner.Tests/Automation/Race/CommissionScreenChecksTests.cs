using System.Reflection;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class CommissionScreenChecksTests
{
    [Fact]
    public void IsAppraiseAcceptDetailText_rejects_commission_tier_selection_sheet()
    {
        bool matched = InvokeIsAppraiseAcceptDetailText(
            "距离目标评鉴战06月下旬",
            "额外奖励队伍编制男爵讨伐委托I低阶委托传闻有贵族男爵在横行霸道。建议综合等级RANK24登场敌人可获得奖励男爵讨伐委托II中阶委托男爵讨伐委托III高阶委托",
            "接受");

        Assert.False(matched);
    }

    [Fact]
    public void IsAppraiseAcceptDetailText_accepts_single_appraise_prepare_sheet()
    {
        bool matched = InvokeIsAppraiseAcceptDetailText(
            "距离目标评鉴战06月下旬",
            "额外奖励队伍编制建议综合等级RANK24登场敌人可获得奖励",
            "接受");

        Assert.True(matched);
    }

    [Fact]
    public void IsAppraiseAcceptDetailText_accepts_prepare_sheet_when_accept_button_ocr_is_empty()
    {
        bool matched = InvokeIsAppraiseAcceptDetailText(
            "目标评鉴战胜利0评鉴战D-DAY0",
            "额外奖励队伍编制远征评鉴战评鉴战即将开始，请完成战前准备。建议综合等级RANK35登场敌人000可获得奖励060远征评鉴战",
            "");

        Assert.True(matched);
    }

    [Fact]
    public void IsCommissionTierSelectionText_detects_multiple_commission_tiers()
    {
        bool matched = InvokeIsCommissionTierSelectionText(
            "男爵讨伐委托I低阶委托男爵讨伐委托II中阶委托男爵讨伐委托III高阶委托");

        Assert.True(matched);
    }

    [Fact]
    public void IsCommissionEntryText_uses_tier_fallback_when_list_text_is_empty()
    {
        bool matched = InvokeIsCommissionEntryText(
            "",
            "男爵讨伐委托I低阶委托男爵讨伐委托II中阶委托男爵讨伐委托III高阶委托");

        Assert.True(matched);
    }

    [Fact]
    public void IsCommissionEntryText_rejects_appraise_prepare_fallback_without_tiers()
    {
        bool matched = InvokeIsCommissionEntryText(
            "",
            "额外奖励队伍编制远征评鉴战评鉴战即将开始，请完成战前准备。建议综合等级RANK35登场敌人000可获得奖励060远征评鉴战");

        Assert.False(matched);
    }

    private static bool InvokeIsAppraiseAcceptDetailText(string titleText, string detailText, string acceptText)
    {
        Type checksType = GetCommissionScreenChecksType();
        MethodInfo method = checksType.GetMethod(
                                "IsAppraiseAcceptDetailText",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("CommissionScreenChecks.IsAppraiseAcceptDetailText was not found.");

        return (bool)method.Invoke(null, [titleText, detailText, acceptText])!;
    }

    private static bool InvokeIsCommissionTierSelectionText(string detailText)
    {
        Type checksType = GetCommissionScreenChecksType();
        MethodInfo method = checksType.GetMethod(
                                "IsCommissionTierSelectionText",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("CommissionScreenChecks.IsCommissionTierSelectionText was not found.");

        return (bool)method.Invoke(null, [detailText])!;
    }

    private static bool InvokeIsCommissionEntryText(string listText, string fallbackText)
    {
        Type checksType = GetCommissionScreenChecksType();
        MethodInfo method = checksType.GetMethod(
                                "IsCommissionEntryText",
                                BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new Xunit.Sdk.XunitException("CommissionScreenChecks.IsCommissionEntryText was not found.");

        return (bool)method.Invoke(null, [listText, fallbackText])!;
    }

    private static Type GetCommissionScreenChecksType()
    {
        return Type.GetType("SleepRunner.Automation.Race.Handlers.Commission.CommissionScreenChecks, SleepRunner")
               ?? throw new Xunit.Sdk.XunitException("CommissionScreenChecks type was not found.");
    }
}
