namespace SleepRunner.Automation.BuiltInRace;

public enum BuiltInRaceStep
{
    OpenAutoJourney,
    StartAutoJourney,
    ConfirmEntry,
    JourneyCompleteContinue,
    InheritJourneyContinue,
    OpenPotential,
}

public readonly record struct BuiltInRaceAction(
    BuiltInRaceStep Step,
    double XPct,
    double YPct,
    string Description);

public readonly record struct BuiltInRaceScreenSnapshot(
    string JourneyTitleText,
    string BottomRightText,
    string BottomJourneyText,
    string DialogTitleText,
    string DialogBodyText);
