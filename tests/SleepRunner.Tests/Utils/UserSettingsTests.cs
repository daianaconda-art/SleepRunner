using SleepRunner.Automation.Race.Policy;
using SleepRunner.Automation.Race.Policy.Training;
using SleepRunner.Utils;
using Xunit;

namespace SleepRunner.Tests.Utils;

public class UserSettingsTests
{
    [Fact]
    public void New_settings_default_to_speed_profiles_and_not_topmost()
    {
        var settings = new UserSettings();

        Assert.False(settings.TopMost);
        Assert.Equal("speed", settings.EventsProfile);
        Assert.Equal("speed", settings.CardsProfile);
        Assert.Equal("speed", settings.TradeProfile);
        Assert.Equal("speed", settings.TrainingProfile);
        Assert.Equal("speed", RaceProfileManager.DefaultProfileName);
        Assert.Equal("speed", TrainingRuleProfileManager.DefaultProfileName);
    }

    [Fact]
    public void Apply_promotes_legacy_default_events_profile_to_training_profile()
    {
        string originalEventsProfile = RaceProfileManager.CurrentEventsProfile;
        string originalCardsProfile = RaceProfileManager.CurrentCardsProfile;
        string originalTradeProfile = RaceProfileManager.CurrentTradeProfile;
        string originalTrainingProfile = TrainingRuleProfileManager.CurrentProfile;

        try
        {
            var settings = new UserSettings
            {
                EventsProfile = "speed",
                CardsProfile = "survival",
                TradeProfile = "survival",
                TrainingProfile = "survival",
            };

            settings.ApplyToRaceConfig();

            Assert.Equal("survival", RaceProfileManager.CurrentEventsProfile);
            Assert.Equal("survival", RaceProfileManager.CurrentCardsProfile);
            Assert.Equal("survival", RaceProfileManager.CurrentTradeProfile);
            Assert.Equal("survival", TrainingRuleProfileManager.CurrentProfile);
        }
        finally
        {
            RaceProfileManager.SetEventsProfile(originalEventsProfile);
            RaceProfileManager.SetCardsProfile(originalCardsProfile);
            RaceProfileManager.SetTradeProfile(originalTradeProfile);
            TrainingRuleProfileManager.SetCurrentProfile(originalTrainingProfile);
        }
    }
}
