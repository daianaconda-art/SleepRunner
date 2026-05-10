using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Policy;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class EventProfileSettingsTests
{
    [Fact]
    public void Move_platform_option_index_follows_events_profile()
    {
        string originalEventsProfile = RaceProfileManager.CurrentEventsProfile;
        string originalTradeProfile = RaceProfileManager.CurrentTradeProfile;
        string tempProfile = $"platform_test_{Guid.NewGuid():N}";
        string tempEventsPath = RaceProfileManager.ResolveEventsPath(tempProfile);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempEventsPath)!);
            File.WriteAllText(tempEventsPath, """
            {
              "move_platform_option_index": 2,
              "events": []
            }
            """);

            RaceProfileManager.SetTradeProfile("speed");
            EventProfileSettings.ForceReload();
            Assert.Equal(1, EventProfileSettings.MovePlatformOptionIndex);

            RaceProfileManager.SetEventsProfile(tempProfile);

            Assert.Equal(2, EventProfileSettings.MovePlatformOptionIndex);
        }
        finally
        {
            RaceProfileManager.SetEventsProfile(originalEventsProfile);
            RaceProfileManager.SetTradeProfile(originalTradeProfile);
            EventProfileSettings.ForceReload();

            if (File.Exists(tempEventsPath))
                File.Delete(tempEventsPath);
        }
    }

    [Theory]
    [InlineData("training_direction", 2)]
    [InlineData("mysterious_statue", 2)]
    public void Survival_event_profile_records_survival_choices_in_recommended_option(string eventId, int expectedOption)
    {
        string path = RaceProfileManager.ResolveEventsPath("survival");
        string json = File.ReadAllText(path);
        var data = System.Text.Json.JsonSerializer.Deserialize<EventDecisionData>(json);

        Assert.NotNull(data);
        var evt = Assert.Single(data!.Events, e => e.Id == eventId);
        Assert.Equal(expectedOption, evt.GetRecommendedOption());
    }
}
