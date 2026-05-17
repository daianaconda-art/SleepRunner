using SleepRunner.Automation.BuiltInRace;
using Xunit;

namespace SleepRunner.Tests.Automation.BuiltInRace;

public class BuiltInRacePlannerTests
{
    [Fact]
    public void Decide_ignores_select_on_character_list()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "旅程起点",
            BottomRightText: "选择",
            BottomJourneyText: "",
            DialogTitleText: "",
            DialogBodyText: ""));

        Assert.Null(action);
    }

    [Fact]
    public void Decide_ignores_journey_start_detail_when_auto_journey_is_not_detected()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "旅程起点",
            BottomRightText: "梦贝塔艾莉莎莱希艾黛贝尔莉丝",
            BottomJourneyText: "旅程初始信息",
            DialogTitleText: "",
            DialogBodyText: ""));

        Assert.Null(action);
    }

    [Fact]
    public void Decide_ignores_confirm_after_character_selected()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "旅程起点",
            BottomRightText: "确认",
            BottomJourneyText: "",
            DialogTitleText: "",
            DialogBodyText: ""));

        Assert.Null(action);
    }

    [Fact]
    public void Decide_clicks_auto_journey_on_team_screen()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "旅程起点",
            BottomRightText: "旅程起点",
            BottomJourneyText: "自动旅程",
            DialogTitleText: "",
            DialogBodyText: ""));

        Assert.NotNull(action);
        Assert.Equal(BuiltInRaceStep.OpenAutoJourney, action.Value.Step);
        Assert.InRange(action.Value.XPct, 0.62, 0.76);
        Assert.InRange(action.Value.YPct, 0.88, 0.98);
    }

    [Fact]
    public void Decide_clicks_auto_journey_when_bottom_button_is_detected_without_title()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "",
            BottomRightText: "",
            BottomJourneyText: "自动旅程",
            DialogTitleText: "",
            DialogBodyText: ""));

        Assert.NotNull(action);
        Assert.Equal(BuiltInRaceStep.OpenAutoJourney, action.Value.Step);
        Assert.InRange(action.Value.XPct, 0.62, 0.76);
        Assert.InRange(action.Value.YPct, 0.88, 0.98);
    }

    [Fact]
    public void Decide_clicks_start_journey_in_auto_journey_modal()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "",
            BottomRightText: "",
            BottomJourneyText: "开始旅程",
            DialogTitleText: "自动旅程",
            DialogBodyText: "设定在自动旅程中要进行的训练频率与选项"));

        Assert.NotNull(action);
        Assert.Equal(BuiltInRaceStep.StartAutoJourney, action.Value.Step);
        Assert.InRange(action.Value.XPct, 0.46, 0.56);
        Assert.InRange(action.Value.YPct, 0.76, 0.82);
    }

    [Fact]
    public void Decide_clicks_confirm_in_entry_confirmation_modal()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "",
            BottomRightText: "确认",
            BottomJourneyText: "",
            DialogTitleText: "入场确认",
            DialogBodyText: "是否要进行旅程"));

        Assert.NotNull(action);
        Assert.Equal(BuiltInRaceStep.ConfirmEntry, action.Value.Step);
        Assert.InRange(action.Value.XPct, 0.52, 0.64);
        Assert.InRange(action.Value.YPct, 0.64, 0.74);
    }

    [Fact]
    public void Decide_clicks_continue_on_journey_complete_screen()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "",
            BottomRightText: "",
            BottomJourneyText: "",
            DialogTitleText: "",
            DialogBodyText: "JOURNEY COMPLETE 救援者完成了旅程 点击以继续"));

        Assert.NotNull(action);
        Assert.Equal(BuiltInRaceStep.JourneyCompleteContinue, action.Value.Step);
        Assert.InRange(action.Value.XPct, 0.45, 0.55);
        Assert.InRange(action.Value.YPct, 0.86, 0.96);
    }

    [Fact]
    public void Decide_clicks_continue_when_journey_complete_ocr_only_sees_continue_prompt()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "",
            BottomRightText: "",
            BottomJourneyText: "",
            DialogTitleText: "",
            DialogBodyText: "点击以继续"));

        Assert.NotNull(action);
        Assert.Equal(BuiltInRaceStep.JourneyCompleteContinue, action.Value.Step);
        Assert.InRange(action.Value.XPct, 0.45, 0.55);
        Assert.InRange(action.Value.YPct, 0.86, 0.96);
    }

    [Fact]
    public void Decide_clicks_continue_on_inherit_journey_screen()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "",
            BottomRightText: "",
            BottomJourneyText: "",
            DialogTitleText: "继承旅程",
            DialogBodyText: "是时候为旅程画下句号，向救援者告别了。剩余的古币与护符将退还并换取奖励。点击以继续"));

        Assert.NotNull(action);
        Assert.Equal(BuiltInRaceStep.InheritJourneyContinue, action.Value.Step);
        Assert.InRange(action.Value.XPct, 0.45, 0.55);
        Assert.InRange(action.Value.YPct, 0.82, 0.92);
    }

    [Fact]
    public void Decide_clicks_potential_on_journey_end_screen()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "",
            BottomRightText: "旅程结束",
            BottomJourneyText: "潜质",
            DialogTitleText: "",
            DialogBodyText: ""));

        Assert.NotNull(action);
        Assert.Equal(BuiltInRaceStep.OpenPotential, action.Value.Step);
        Assert.InRange(action.Value.XPct, 0.68, 0.77);
        Assert.InRange(action.Value.YPct, 0.92, 0.99);
    }

    [Fact]
    public void ShouldStopAfterAction_stops_only_after_opening_potential()
    {
        Assert.False(BuiltInRacePlanner.ShouldStopAfterAction(BuiltInRaceStep.ConfirmEntry));
        Assert.False(BuiltInRacePlanner.ShouldStopAfterAction(BuiltInRaceStep.JourneyCompleteContinue));
        Assert.False(BuiltInRacePlanner.ShouldStopAfterAction(BuiltInRaceStep.InheritJourneyContinue));
        Assert.True(BuiltInRacePlanner.ShouldStopAfterAction(BuiltInRaceStep.OpenPotential));
    }
}
