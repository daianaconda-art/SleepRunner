using System.Text.Json;
using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class ProfileAssetTests
{
    public static TheoryData<string> ProfileDirectories => new()
    {
        "cards",
        "trade",
        "training",
    };

    [Theory]
    [MemberData(nameof(ProfileDirectories))]
    public void Speed_profile_is_seeded_from_attack_profile(string directory)
    {
        string assetsDir = Path.Combine(AppContext.BaseDirectory, "assets", directory);
        string attackPath = Path.Combine(assetsDir, "attack.json");
        string speedPath = Path.Combine(assetsDir, "speed.json");

        Assert.True(File.Exists(speedPath), $"Missing speed profile at {speedPath}");
        Assert.Equal(File.ReadAllText(attackPath), File.ReadAllText(speedPath));
    }

    [Fact]
    public void Ambush_event_strategy_is_profile_driven()
    {
        string assetsDir = Path.Combine(AppContext.BaseDirectory, "assets", "events");

        AssertAmbushDecision(Path.Combine(assetsDir, "speed.json"), recommendedOption: 1, fallbackOption: null);
        AssertAmbushDecision(Path.Combine(assetsDir, "attack.json"), recommendedOption: 2, fallbackOption: 1);
        AssertAmbushDecision(Path.Combine(assetsDir, "default.json"), recommendedOption: 2, fallbackOption: 1);
        AssertAmbushDecision(Path.Combine(assetsDir, "survival.json"), recommendedOption: 2, fallbackOption: 1);
    }

    [Fact]
    public void Fei_ambush_event_tries_option_two_then_falls_back_to_option_three()
    {
        string assetsDir = Path.Combine(AppContext.BaseDirectory, "assets", "events");

        AssertAmbushDecision(Path.Combine(assetsDir, "fei.json"), recommendedOption: 2, fallbackOption: 3);
    }

    private static void AssertAmbushDecision(string profilePath, int recommendedOption, int? fallbackOption)
    {
        Assert.True(File.Exists(profilePath), $"Missing events profile at {profilePath}");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(profilePath));
        JsonElement events = doc.RootElement.GetProperty("events");
        JsonElement ambush = events.EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == "ambush");

        Assert.Equal(recommendedOption, ambush.GetProperty("recommended_option").GetInt32());

        if (fallbackOption == null)
        {
            Assert.False(ambush.TryGetProperty("fallback_option", out _));
        }
        else
        {
            Assert.True(ambush.TryGetProperty("fallback_option", out JsonElement fallback));
            Assert.Equal(fallbackOption.Value, fallback.GetInt32());
        }
    }
}
