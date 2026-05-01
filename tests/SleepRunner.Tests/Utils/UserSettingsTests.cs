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
}
