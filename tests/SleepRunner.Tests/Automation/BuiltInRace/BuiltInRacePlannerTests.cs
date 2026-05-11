using SleepRunner.Automation.BuiltInRace;
using Xunit;

namespace SleepRunner.Tests.Automation.BuiltInRace;

public class BuiltInRacePlannerTests
{
    [Fact]
    public void Decide_clicks_select_on_character_list()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "旅程起点",
            BottomRightText: "选择",
            BottomJourneyText: "",
            DialogTitleText: "",
            DialogBodyText: ""));

        Assert.NotNull(action);
        Assert.Equal(BuiltInRaceStep.SelectStartingCharacter, action.Value.Step);
        Assert.InRange(action.Value.XPct, 0.84, 0.92);
        Assert.InRange(action.Value.YPct, 0.92, 0.98);
    }

    [Fact]
    public void Decide_clicks_confirm_after_character_selected()
    {
        BuiltInRaceAction? action = BuiltInRacePlanner.Decide(new BuiltInRaceScreenSnapshot(
            JourneyTitleText: "旅程起点",
            BottomRightText: "确认",
            BottomJourneyText: "",
            DialogTitleText: "",
            DialogBodyText: ""));

        Assert.NotNull(action);
        Assert.Equal(BuiltInRaceStep.ConfirmStartingCharacter, action.Value.Step);
        Assert.InRange(action.Value.XPct, 0.84, 0.92);
        Assert.InRange(action.Value.YPct, 0.90, 0.97);
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
        Assert.InRange(action.Value.YPct, 0.83, 0.91);
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
}
