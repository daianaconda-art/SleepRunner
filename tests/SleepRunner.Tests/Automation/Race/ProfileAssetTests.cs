using Xunit;

namespace SleepRunner.Tests.Automation.Race;

public class ProfileAssetTests
{
    public static TheoryData<string> ProfileDirectories => new()
    {
        "events",
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
}
