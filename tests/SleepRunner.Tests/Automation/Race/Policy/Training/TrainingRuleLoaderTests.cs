using System.Reflection;
using SleepRunner.Automation.Race.Policy.Training;
using Xunit;

namespace SleepRunner.Tests.Automation.Race.Policy.Training;

public class TrainingRuleLoaderTests
{
    [Fact]
    public void LoadFromJson_parses_a_normal_rule_and_a_fallback_rule()
    {
        var json = """
        {
          "legacy_strategy": {
            "build_direction": "survival",
            "fail_rate_threshold": 33,
            "rush_threshold": 480
          },
          "rules": [
            {
              "id": "focus_first",
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
        """;

        var profile = TrainingRuleLoader.LoadFromJson(json, "profile.json");

        Assert.Equal("profile.json", profile.SourcePath);
        Assert.Equal(SleepRunner.Automation.Race.BuildDirection.Survival, profile.LegacyStrategy.BuildDirection);
        Assert.Equal(33, profile.LegacyStrategy.FailRateThreshold);
        Assert.Equal(480, profile.LegacyStrategy.RushThreshold);
        Assert.Equal(2, profile.Rules.Count);

        var first = profile.Rules[0];
        Assert.Equal("focus_first", first.Id);
        Assert.False(first.IsFallback);
        Assert.Equal(TrainingRuleField.StrengthIcons, first.Field);
        Assert.Equal(TrainingRuleOperator.GreaterThan, first.Operator);
        Assert.Equal(50, first.Value);
        Assert.Equal(TrainingDecisionAction.TrainStrength, first.Action);
        Assert.True(first.Enabled);

        var second = profile.Rules[1];
        Assert.Equal("fallback", second.Id);
        Assert.True(second.IsFallback);
        Assert.Null(second.Field);
        Assert.Null(second.Operator);
        Assert.Null(second.Value);
        Assert.Equal(TrainingDecisionAction.Rest, second.Action);
        Assert.True(second.Enabled);
    }

    [Fact]
    public void LoadFromJson_appends_a_builtin_fallback_rule_when_one_is_missing()
    {
        var json = """
        {
          "rules": [
            {
              "field": "stamina_icons",
              "operator": ">=",
              "value": 10,
              "action": "train_stamina",
              "enabled": true
            }
          ]
        }
        """;

        var profile = TrainingRuleLoader.LoadFromJson(json, "missing-fallback.json");

        Assert.Equal(2, profile.Rules.Count);

        var builtinFallback = profile.Rules[1];
        Assert.Equal("fallback", builtinFallback.Id);
        Assert.True(builtinFallback.IsFallback);
        Assert.Null(builtinFallback.Field);
        Assert.Null(builtinFallback.Operator);
        Assert.Null(builtinFallback.Value);
        Assert.Equal(TrainingDecisionAction.BuiltinDefault, builtinFallback.Action);
        Assert.True(builtinFallback.Enabled);
    }

    [Fact]
    public void LoadFromJson_parses_a_two_condition_rule_as_a_normal_rule()
    {
        var json = """
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
              "action": "rest",
              "enabled": true
            }
          ]
        }
        """;

        var profile = TrainingRuleLoader.LoadFromJson(json, "two-conditions.json");

        var first = profile.Rules[0];
        Assert.Equal("safe_strength", first.Id);
        Assert.False(first.IsFallback);
        Assert.Equal(TrainingRuleField.StrengthIcons, first.Field);
        Assert.Equal(TrainingRuleOperator.GreaterThanOrEqual, first.Operator);
        Assert.Equal(3, first.Value);
        Assert.Equal(TrainingDecisionAction.TrainStrength, first.Action);
        Assert.True(first.Enabled);
    }

    [Fact]
    public void LoadFromJson_rejects_a_rule_with_only_a_field()
    {
        var json = """
        {
          "rules": [
            {
              "field": "strength_icons",
              "action": "train_strength",
              "enabled": true
            }
          ]
        }
        """;

        Assert.Throws<System.Text.Json.JsonException>(() =>
            TrainingRuleLoader.LoadFromJson(json, "partial-field.json"));
    }

    [Fact]
    public void LoadFromJson_rejects_a_rule_with_only_field_and_operator()
    {
        var json = """
        {
          "rules": [
            {
              "field": "strength_icons",
              "operator": ">",
              "action": "train_strength",
              "enabled": true
            }
          ]
        }
        """;

        Assert.Throws<System.Text.Json.JsonException>(() =>
            TrainingRuleLoader.LoadFromJson(json, "partial-operator.json"));
    }

    [Fact]
    public void LoadFromJson_preserves_disabled_rules_and_enabled_flags()
    {
        var json = """
        {
          "rules": [
            {
              "id": "agility_gate",
              "field": "agility_icons",
              "operator": ">",
              "value": 7,
              "action": "train_agility",
              "enabled": false
            },
            {
              "id": "focus_first",
              "field": "focus_icons",
              "operator": ">=",
              "value": 12,
              "action": "train_focus",
              "enabled": true
            }
          ]
        }
        """;

        var profile = TrainingRuleLoader.LoadFromJson(json, "disabled.json");

        Assert.Equal(3, profile.Rules.Count);
        Assert.Collection(profile.Rules,
            rule =>
            {
                Assert.False(rule.IsFallback);
                Assert.Equal("agility_gate", rule.Id);
                Assert.Equal(TrainingRuleField.AgilityIcons, rule.Field);
                Assert.Equal(TrainingRuleOperator.GreaterThan, rule.Operator);
                Assert.Equal(7, rule.Value);
                Assert.Equal(TrainingDecisionAction.TrainAgility, rule.Action);
                Assert.False(rule.Enabled);
            },
            rule =>
            {
                Assert.False(rule.IsFallback);
                Assert.Equal("focus_first", rule.Id);
                Assert.Equal(TrainingRuleField.FocusIcons, rule.Field);
                Assert.Equal(TrainingRuleOperator.GreaterThanOrEqual, rule.Operator);
                Assert.Equal(12, rule.Value);
                Assert.Equal(TrainingDecisionAction.TrainFocus, rule.Action);
                Assert.True(rule.Enabled);
            },
            rule =>
            {
                Assert.Equal("fallback", rule.Id);
                Assert.True(rule.IsFallback);
                Assert.Null(rule.Field);
                Assert.Null(rule.Operator);
                Assert.Null(rule.Value);
                Assert.Equal(TrainingDecisionAction.BuiltinDefault, rule.Action);
                Assert.True(rule.Enabled);
            });
    }

    [Fact]
    public void LoadFromJson_rejects_a_fallback_rule_before_a_later_enabled_rule()
    {
        var json = """
        {
          "rules": [
            {
              "field": "strength_icons",
              "operator": ">",
              "value": 5,
              "action": "train_strength",
              "enabled": true
            },
            {
              "action": "rest",
              "enabled": true
            },
            {
              "field": "focus_icons",
              "operator": ">=",
              "value": 10,
              "action": "train_focus",
              "enabled": true
            }
          ]
        }
        """;

        Assert.Throws<System.Text.Json.JsonException>(() =>
            TrainingRuleLoader.LoadFromJson(json, "fallback-middle.json"));
    }

    [Fact]
    public void LoadFromJson_rejects_duplicate_explicit_rule_ids()
    {
        var json = """
        {
          "rules": [
            {
              "id": "focus_first",
              "field": "focus_icons",
              "operator": ">",
              "value": 7,
              "action": "train_focus",
              "enabled": true
            },
            {
              "id": "focus_first",
              "field": "strength_icons",
              "operator": ">",
              "value": 9,
              "action": "train_strength",
              "enabled": true
            }
          ]
        }
        """;

        Assert.Throws<System.Text.Json.JsonException>(() =>
            TrainingRuleLoader.LoadFromJson(json, "duplicate-ids.json"));
    }

    [Fact]
    public void TrainingRuleCardControl_throws_when_a_rule_value_is_missing_from_editor_options()
    {
        Type? controlType = typeof(TrainingRuleLoader).Assembly.GetType("SleepRunner.Forms.TrainingRules.TrainingRuleCardControl");
        Assert.NotNull(controlType);

        var rule = new TrainingRuleCard
        {
            Id = "invalid_action",
            Field = TrainingRuleField.StrengthIcons,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 1,
            Action = (TrainingDecisionAction)999,
            Enabled = true,
            IsFallback = false,
        };

        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() =>
            Activator.CreateInstance(controlType!, new object[] { rule }));

        InvalidOperationException inner = Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("editor option", inner.Message, StringComparison.OrdinalIgnoreCase);
    }
}
