using System.Reflection;
using System.Text.Json;
using SleepRunner.Automation.Race.Policy.Training;
using Xunit;

namespace SleepRunner.Tests.Automation.Race.Policy.Training;

public class TrainingRuleLoaderRoundTripTests
{
    [Fact]
    public void SaveToJson_round_trips_rule_cards_through_LoadFromJson()
    {
        var profile = new TrainingRuleProfile
        {
            SourcePath = "source.json",
        };
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "disabled_focus_gate",
            Field = TrainingRuleField.FocusIcons,
            Operator = TrainingRuleOperator.LessThan,
            Value = 4,
            Action = TrainingDecisionAction.TrainFocus,
            Enabled = false,
            IsFallback = false,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "strength_gate",
            Field = TrainingRuleField.StrengthIcons,
            Operator = TrainingRuleOperator.GreaterThanOrEqual,
            Value = 12,
            Action = TrainingDecisionAction.TrainStrength,
            Enabled = true,
            IsFallback = false,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rest_fallback",
            Action = TrainingDecisionAction.Rest,
            Enabled = true,
            IsFallback = true,
        });

        MethodInfo? saveToJson = typeof(TrainingRuleLoader).GetMethod(
            "SaveToJson",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(TrainingRuleProfile) },
            modifiers: null);

        Assert.NotNull(saveToJson);

        string json = Assert.IsType<string>(saveToJson!.Invoke(null, new object[] { profile }));

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("legacy_strategy", out _));
        JsonElement rules = document.RootElement.GetProperty("rules");
        Assert.Equal(3, rules.GetArrayLength());

        JsonElement savedDisabled = rules[0];
        Assert.Equal("disabled_focus_gate", savedDisabled.GetProperty("id").GetString());
        Assert.Equal("focus_icons", savedDisabled.GetProperty("field").GetString());
        Assert.Equal("<", savedDisabled.GetProperty("operator").GetString());
        Assert.Equal(4, savedDisabled.GetProperty("value").GetInt32());
        Assert.Equal("train_focus", savedDisabled.GetProperty("action").GetString());
        Assert.False(savedDisabled.GetProperty("enabled").GetBoolean());

        JsonElement savedFallback = rules[2];
        Assert.Equal("rest_fallback", savedFallback.GetProperty("id").GetString());
        Assert.Equal("rest", savedFallback.GetProperty("action").GetString());
        Assert.True(savedFallback.GetProperty("enabled").GetBoolean());
        Assert.False(savedFallback.TryGetProperty("field", out _));
        Assert.False(savedFallback.TryGetProperty("operator", out _));
        Assert.False(savedFallback.TryGetProperty("value", out _));

        TrainingRuleProfile roundTripped = TrainingRuleLoader.LoadFromJson(json, "round-trip.json");

        Assert.Equal("round-trip.json", roundTripped.SourcePath);
        Assert.Collection(
            roundTripped.Rules,
            disabled =>
            {
                Assert.Equal("disabled_focus_gate", disabled.Id);
                Assert.False(disabled.IsFallback);
                Assert.Equal(TrainingRuleField.FocusIcons, disabled.Field);
                Assert.Equal(TrainingRuleOperator.LessThan, disabled.Operator);
                Assert.Equal(4, disabled.Value);
                Assert.Equal(TrainingDecisionAction.TrainFocus, disabled.Action);
                Assert.False(disabled.Enabled);
            },
            first =>
            {
                Assert.Equal("strength_gate", first.Id);
                Assert.False(first.IsFallback);
                Assert.Equal(TrainingRuleField.StrengthIcons, first.Field);
                Assert.Equal(TrainingRuleOperator.GreaterThanOrEqual, first.Operator);
                Assert.Equal(12, first.Value);
                Assert.Equal(TrainingDecisionAction.TrainStrength, first.Action);
                Assert.True(first.Enabled);
            },
            fallback =>
            {
                Assert.Equal("rest_fallback", fallback.Id);
                Assert.True(fallback.IsFallback);
                Assert.Null(fallback.Field);
                Assert.Null(fallback.Operator);
                Assert.Null(fallback.Value);
                Assert.Equal(TrainingDecisionAction.Rest, fallback.Action);
                Assert.True(fallback.Enabled);
            });
    }

    [Fact]
    public void SaveToJson_rejects_duplicate_explicit_rule_ids()
    {
        var profile = new TrainingRuleProfile
        {
            SourcePath = "duplicate-save.json",
            Rules =
            {
                new TrainingRuleCard
                {
                    Id = "focus_first",
                    Field = TrainingRuleField.FocusIcons,
                    Operator = TrainingRuleOperator.GreaterThan,
                    Value = 3,
                    Action = TrainingDecisionAction.TrainFocus,
                    Enabled = true,
                    IsFallback = false,
                },
                new TrainingRuleCard
                {
                    Id = "focus_first",
                    Field = TrainingRuleField.StrengthIcons,
                    Operator = TrainingRuleOperator.GreaterThanOrEqual,
                    Value = 6,
                    Action = TrainingDecisionAction.TrainStrength,
                    Enabled = true,
                    IsFallback = false,
                },
                new TrainingRuleCard
                {
                    Id = "fallback_rule",
                    Action = TrainingDecisionAction.BuiltinDefault,
                    Enabled = true,
                    IsFallback = true,
                },
            },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TrainingRuleLoader.SaveToJson(profile));

        Assert.Contains("Duplicate training rule id", ex.Message);
    }

    [Fact]
    public void SaveToJson_omits_legacy_strategy_when_profile_does_not_use_builtin_default()
    {
        var profile = new TrainingRuleProfile
        {
            SourcePath = "pure-rules.json",
        };
        profile.LegacyStrategy.BuildDirection = SleepRunner.Automation.Race.BuildDirection.Survival;
        profile.LegacyStrategy.FailRateThreshold = 28;
        profile.LegacyStrategy.RushThreshold = 520;
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rest_high_fail",
            Field = TrainingRuleField.AnyFailRate,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 30,
            Action = TrainingDecisionAction.Rest,
            Enabled = true,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "fallback_strength",
            Action = TrainingDecisionAction.TrainStrength,
            Enabled = true,
            IsFallback = true,
        });

        string json = TrainingRuleLoader.SaveToJson(profile);

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("legacy_strategy", out _));
    }

    [Fact]
    public void SaveToJson_preserves_two_condition_rules_loaded_from_json()
    {
        var sourceJson = """
        {
          "rules": [
            {
              "id": "safe_strength",
              "conditions": [
                {
                  "field": "strength_icons",
                  "operator": ">=",
                  "value": 3
                },
                {
                  "field": "strength_fail_rate",
                  "operator": "<",
                  "value": 40
                }
              ],
              "action": "train_strength",
              "enabled": true
            },
            {
              "id": "fallback_rest",
              "action": "rest",
              "enabled": true
            }
          ]
        }
        """;

        TrainingRuleProfile profile = TrainingRuleLoader.LoadFromJson(sourceJson, "two-conditions.json");

        string savedJson = TrainingRuleLoader.SaveToJson(profile);

        using var document = JsonDocument.Parse(savedJson);
        JsonElement savedRule = document.RootElement.GetProperty("rules")[0];
        JsonElement conditions = savedRule.GetProperty("conditions");
        Assert.Equal(2, conditions.GetArrayLength());
        Assert.Equal("strength_icons", conditions[0].GetProperty("field").GetString());
        Assert.Equal(">=", conditions[0].GetProperty("operator").GetString());
        Assert.Equal(3, conditions[0].GetProperty("value").GetInt32());
        Assert.Equal("strength_fail_rate", conditions[1].GetProperty("field").GetString());
        Assert.Equal("<", conditions[1].GetProperty("operator").GetString());
        Assert.Equal(40, conditions[1].GetProperty("value").GetInt32());
    }
}
