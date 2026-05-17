using SleepRunner.Automation.Race.Policy.Training;
using System.Runtime.CompilerServices;
using Xunit;

namespace SleepRunner.Tests.Automation.Race.Policy.Training;

public class TrainingRuleStoreTests
{
    [Fact]
    public void LoadFromPath_returns_builtin_default_when_file_is_missing()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json");

        var profile = TrainingRuleStore.LoadFromPath(missingPath);

        Assert.False(string.IsNullOrWhiteSpace(profile.SourcePath));
        Assert.Single(profile.Rules);
        Assert.Equal(30, profile.LegacyStrategy.FailRateThreshold);
        Assert.Equal(450, profile.LegacyStrategy.RushThreshold);
        var fallback = profile.Rules[0];
        Assert.True(fallback.IsFallback);
        Assert.Equal(TrainingDecisionAction.BuiltinDefault, fallback.Action);
        Assert.True(fallback.Enabled);
    }

    [Fact]
    public void LoadFromPath_loads_json_when_file_exists()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string path = Path.Combine(tempDir, "custom.json");
        File.WriteAllText(path, """
        {
          "legacy_strategy": {
            "build_direction": "survival",
            "fail_rate_threshold": 34,
            "rush_threshold": 510
          },
          "rules": [
            {
              "id": "train_strength_first",
              "field": "strength_icons",
              "operator": ">",
              "value": 50,
              "action": "train_strength",
              "enabled": true
            },
            {
              "action": "rest",
              "enabled": true
            }
          ]
        }
        """);

        try
        {
            var profile = TrainingRuleStore.LoadFromPath(path);

            Assert.Equal(path, profile.SourcePath);
            Assert.Equal(SleepRunner.Automation.Race.BuildDirection.Survival, profile.LegacyStrategy.BuildDirection);
            Assert.Equal(34, profile.LegacyStrategy.FailRateThreshold);
            Assert.Equal(510, profile.LegacyStrategy.RushThreshold);
            Assert.Equal(2, profile.Rules.Count);
            Assert.Equal("train_strength_first", profile.Rules[0].Id);
            Assert.False(profile.Rules[0].IsFallback);
            Assert.Equal(TrainingDecisionAction.TrainStrength, profile.Rules[0].Action);
            Assert.True(profile.Rules[1].IsFallback);
            Assert.Equal(TrainingDecisionAction.Rest, profile.Rules[1].Action);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void CurrentProfile_returns_a_defensive_copy_of_the_cached_profile()
    {
        string profileName = $"copy_{Guid.NewGuid():N}";
        string profilePath = TrainingRuleProfileManager.ResolvePath(profileName);
        Directory.CreateDirectory(TrainingRuleProfileManager.TrainingDir);
        File.WriteAllText(profilePath, """
        {
          "rules": [
            {
              "id": "original_rule",
              "field": "strength_icons",
              "operator": ">",
              "value": 12,
              "action": "train_strength",
              "enabled": true
            }
          ]
        }
        """);

        try
        {
            TrainingRuleProfileManager.SetCurrentProfile(profileName);
            var firstRead = TrainingRuleStore.CurrentProfile;
            firstRead.Rules[0].Id = "mutated_rule";
            firstRead.Rules.Add(new TrainingRuleCard { Id = "extra_rule", Action = TrainingDecisionAction.Rest, Enabled = true });

            var secondRead = TrainingRuleStore.CurrentProfile;

            Assert.Equal("original_rule", secondRead.Rules[0].Id);
            Assert.Equal(2, secondRead.Rules.Count);
            Assert.NotSame(firstRead, secondRead);
            Assert.NotSame(firstRead.Rules[0], secondRead.Rules[0]);
        }
        finally
        {
            if (File.Exists(profilePath))
            {
                File.Delete(profilePath);
            }

            TrainingRuleProfileManager.SetCurrentProfile(TrainingRuleProfileManager.DefaultProfileName);
            TrainingRuleStore.ForceReload();
        }
    }

    [Fact]
    public void Throwing_profile_changed_subscriber_does_not_block_later_subscribers_or_reload()
    {
        string profileName = $"reload_{Guid.NewGuid():N}";
        string profilePath = TrainingRuleProfileManager.ResolvePath(profileName);
        Directory.CreateDirectory(TrainingRuleProfileManager.TrainingDir);

        File.WriteAllText(profilePath, """
        {
          "rules": [
            {
              "id": "version_one",
              "field": "strength_icons",
              "operator": ">",
              "value": 10,
              "action": "train_strength",
              "enabled": true
            }
          ]
        }
        """);

        bool laterSubscriberRan = false;
        Action<string> throwingSubscriber = _ => throw new InvalidOperationException("subscriber boom");
        Action<string> laterSubscriber = _ => laterSubscriberRan = true;

        try
        {
            TrainingRuleProfileManager.TrainingProfileChanged += throwingSubscriber;
            TrainingRuleProfileManager.TrainingProfileChanged += laterSubscriber;

            RuntimeHelpers.RunClassConstructor(typeof(TrainingRuleStore).TypeHandle);

            TrainingRuleProfileManager.SetCurrentProfile(profileName);
            var firstRead = TrainingRuleStore.CurrentProfile;
            Assert.Equal("version_one", firstRead.Rules[0].Id);

            File.WriteAllText(profilePath, """
            {
              "rules": [
                {
                  "id": "version_two",
                  "field": "strength_icons",
                  "operator": ">",
                  "value": 10,
                  "action": "train_strength",
                  "enabled": true
                }
              ]
            }
            """);

            TrainingRuleProfileManager.SetCurrentProfile(profileName.ToUpperInvariant());
            var secondRead = TrainingRuleStore.CurrentProfile;

            Assert.True(laterSubscriberRan);
            Assert.Equal("version_two", secondRead.Rules[0].Id);
        }
        finally
        {
            TrainingRuleProfileManager.TrainingProfileChanged -= laterSubscriber;
            TrainingRuleProfileManager.TrainingProfileChanged -= throwingSubscriber;

            if (File.Exists(profilePath))
            {
                File.Delete(profilePath);
            }

            TrainingRuleProfileManager.SetCurrentProfile(TrainingRuleProfileManager.DefaultProfileName);
            TrainingRuleStore.ForceReload();
        }
    }

    [Fact]
    public void Survival_asset_uses_survival_metadata_and_builtin_fallback()
    {
        string profilePath = TrainingRuleProfileManager.ResolvePath("survival");

        var profile = TrainingRuleStore.LoadFromPath(profilePath);

        Assert.Equal(SleepRunner.Automation.Race.BuildDirection.Survival, profile.LegacyStrategy.BuildDirection);
        var fallback = Assert.Single(profile.Rules, rule => rule.IsFallback);
        Assert.Equal("fallback_survival_builtin", fallback.Id);
        Assert.Equal(TrainingDecisionAction.BuiltinDefault, fallback.Action);

        var highFailDecision = TrainingRuleEngine.Evaluate(new TrainingDecisionContext
        {
            ProfileName = "survival",
            IconCounts = [2, 1, 2, 1, 0],
            FailRates = [0, 0, 0, 100, 0],
            KnownIconMask = 0b11111,
            KnownFailRateMask = 0b11111,
            StrengthStat = 114,
            StaminaStat = 193,
            BuildDirection = profile.LegacyStrategy.BuildDirection,
            LegacyFailRateThreshold = profile.LegacyStrategy.FailRateThreshold,
            LegacyRushThreshold = profile.LegacyStrategy.RushThreshold,
        }, profile);
        Assert.Equal(TrainingDecisionAction.Rest, highFailDecision.Action);

        var fallbackDecision = TrainingRuleEngine.Evaluate(new TrainingDecisionContext
        {
            ProfileName = "survival",
            IconCounts = [2, 1, 1, 2, 2],
            FailRates = [0, 0, 0, 0, 0],
            KnownIconMask = 0b11111,
            KnownFailRateMask = 0b11111,
            StrengthStat = 114,
            StaminaStat = 286,
            BuildDirection = profile.LegacyStrategy.BuildDirection,
            LegacyFailRateThreshold = profile.LegacyStrategy.FailRateThreshold,
            LegacyRushThreshold = profile.LegacyStrategy.RushThreshold,
        }, profile);
        Assert.Equal("fallback_survival_builtin", fallbackDecision.MatchedRuleId);
        Assert.True(fallbackDecision.UsedBuiltinDefault);
        Assert.NotEqual(TrainingDecisionAction.TrainStamina, fallbackDecision.Action);
    }
}
