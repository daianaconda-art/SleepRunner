using SleepRunner.Automation.Race;
using SleepRunner.Automation.Race.Policy.Training;
using Xunit;

namespace SleepRunner.Tests.Automation.Race.Policy.Training;

public class TrainingRuleEngineTests
{
    [Fact]
    public void Evaluate_skips_strength_actions_when_strength_is_capped()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rush_strength",
            Field = TrainingRuleField.StrengthStat,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 450,
            Action = TrainingDecisionAction.TrainStrength,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "focus_icons_3",
            Field = TrainingRuleField.FocusIcons,
            Operator = TrainingRuleOperator.GreaterThanOrEqual,
            Value = 3,
            Action = TrainingDecisionAction.TrainFocus,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "strength-cap.json",
            IconCounts = [0, 0, 0, 4, 0],
            FailRates = [5, 5, 5, 5, 5],
            StrengthStat = 1250,
            StaminaStat = 300,
            LegacyFailRateThreshold = 30,
            LegacyRushThreshold = 450,
        };

        var result = TrainingRuleEngine.Evaluate(context, profile);

        Assert.Equal("focus_icons_3", result.MatchedRuleId);
        Assert.Equal(TrainingDecisionAction.TrainFocus, result.Action);
    }

    [Fact]
    public void Probe_requests_next_rule_after_strength_action_is_vetoed_by_cap()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rush_strength",
            Field = TrainingRuleField.StrengthStat,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 450,
            Action = TrainingDecisionAction.TrainStrength,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "focus_icons_3",
            Field = TrainingRuleField.FocusIcons,
            Operator = TrainingRuleOperator.GreaterThanOrEqual,
            Value = 3,
            Action = TrainingDecisionAction.TrainFocus,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "strength-cap.json",
            FailRates = [5, 5, 5, 5, 5],
            KnownFailRateMask = 0b11111,
            StrengthStat = 1250,
            StaminaStat = 300,
            KnownIconMask = 0,
            LegacyFailRateThreshold = 30,
            LegacyRushThreshold = 450,
        };

        var probe = TrainingRuleEngine.Probe(context, profile);

        Assert.Null(probe.Decision);
        Assert.Equal(TrainingRuleField.FocusIcons, probe.MissingField);
    }

    [Fact]
    public void Probe_requests_strength_fail_rate_before_falling_through_to_later_strength_rules()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rest_on_strength_fail",
            Field = TrainingRuleField.StrengthFailRate,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 25,
            Action = TrainingDecisionAction.Rest,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rush_strength",
            Field = TrainingRuleField.StrengthStat,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 450,
            Action = TrainingDecisionAction.TrainStrength,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "fallback_strength",
            Action = TrainingDecisionAction.TrainStrength,
            IsFallback = true,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "lazy-strength.json",
            FailRates = [0, 0, 0, 0, 0],
            KnownFailRateMask = 0,
            StrengthStat = 900,
        };

        var probe = TrainingRuleEngine.Probe(context, profile);

        Assert.Null(probe.Decision);
        Assert.Equal(TrainingRuleField.StrengthFailRate, probe.MissingField);
    }

    [Fact]
    public void Probe_returns_later_strength_rule_once_strength_fail_rate_is_known_safe()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rest_on_strength_fail",
            Field = TrainingRuleField.StrengthFailRate,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 25,
            Action = TrainingDecisionAction.Rest,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rush_strength",
            Field = TrainingRuleField.StrengthStat,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 450,
            Action = TrainingDecisionAction.TrainStrength,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "fallback_strength",
            Action = TrainingDecisionAction.TrainStrength,
            IsFallback = true,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "lazy-strength.json",
            FailRates = [20, 0, 0, 0, 0],
            KnownFailRateMask = 0b00001,
            StrengthStat = 900,
        };

        var probe = TrainingRuleEngine.Probe(context, profile);

        Assert.NotNull(probe.Decision);
        Assert.Null(probe.MissingField);
        Assert.Equal("rush_strength", probe.Decision!.MatchedRuleId);
        Assert.Equal(TrainingDecisionAction.TrainStrength, probe.Decision.Action);
    }

    [Fact]
    public void Probe_returns_later_strength_icons_rule_when_strength_stat_was_attempted_but_unavailable()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rest_on_strength_fail",
            Field = TrainingRuleField.StrengthFailRate,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 25,
            Action = TrainingDecisionAction.Rest,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rush_strength",
            Field = TrainingRuleField.StrengthStat,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 450,
            Action = TrainingDecisionAction.TrainStrength,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "strength_icons_3",
            Field = TrainingRuleField.StrengthIcons,
            Operator = TrainingRuleOperator.GreaterThanOrEqual,
            Value = 3,
            Action = TrainingDecisionAction.TrainStrength,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "lazy-strength.json",
            IconCounts = [3, 0, 0, 0, 0],
            KnownIconMask = 0b00001,
            FailRates = [0, 0, 0, 0, 0],
            KnownFailRateMask = 0b00001,
        };
        SetUnavailableFields(context, TrainingRuleField.StrengthStat);

        var probe = TrainingRuleEngine.Probe(context, profile);

        Assert.NotNull(probe.Decision);
        Assert.Null(probe.MissingField);
        Assert.Equal("strength_icons_3", probe.Decision!.MatchedRuleId);
        Assert.Equal(TrainingDecisionAction.TrainStrength, probe.Decision.Action);
    }

    [Fact]
    public void Probe_still_requests_strength_stat_before_later_strength_icons_when_not_attempted_yet()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rush_strength",
            Field = TrainingRuleField.StrengthStat,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 450,
            Action = TrainingDecisionAction.TrainStrength,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "strength_icons_3",
            Field = TrainingRuleField.StrengthIcons,
            Operator = TrainingRuleOperator.GreaterThanOrEqual,
            Value = 3,
            Action = TrainingDecisionAction.TrainStrength,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "lazy-strength.json",
            IconCounts = [3, 0, 0, 0, 0],
            KnownIconMask = 0b00001,
            FailRates = [0, 0, 0, 0, 0],
            KnownFailRateMask = 0b00001,
        };

        var probe = TrainingRuleEngine.Probe(context, profile);

        Assert.Null(probe.Decision);
        Assert.Equal(TrainingRuleField.StrengthStat, probe.MissingField);
    }

    [Fact]
    public void Probe_does_not_skip_any_fail_rate_rules_when_other_rows_are_still_unknown()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rest_high_fail",
            Field = TrainingRuleField.AnyFailRate,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 30,
            Action = TrainingDecisionAction.Rest,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "rush_strength",
            Field = TrainingRuleField.StrengthStat,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 450,
            Action = TrainingDecisionAction.TrainStrength,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "fallback_strength",
            Action = TrainingDecisionAction.TrainStrength,
            IsFallback = true,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "lazy-any-fail.json",
            FailRates = [10, 0, 0, 0, 0],
            KnownFailRateMask = 0b00001,
            StrengthStat = 900,
        };

        var probe = TrainingRuleEngine.Probe(context, profile);

        Assert.Null(probe.Decision);
        Assert.Equal(TrainingRuleField.AnyFailRate, probe.MissingField);
    }

    [Fact]
    public void Probe_builtin_default_can_resolve_without_icon_counts_when_rush_stat_is_already_enough()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "fallback",
            Action = TrainingDecisionAction.BuiltinDefault,
            IsFallback = true,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "builtin-default.json",
            FailRates = [5, 5, 5, 5, 5],
            KnownFailRateMask = 0b11111,
            StrengthStat = 900,
            LegacyFailRateThreshold = 30,
            LegacyRushThreshold = 450,
        };

        var probe = TrainingRuleEngine.Probe(context, profile);

        Assert.NotNull(probe.Decision);
        Assert.Null(probe.MissingField);
        Assert.Equal(TrainingDecisionAction.TrainStrength, probe.Decision!.Action);
        Assert.Equal(0, probe.Decision.TargetRowIndex);
    }

    [Fact]
    public void Probe_builtin_default_requests_icons_when_rush_stat_was_attempted_but_unavailable()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "fallback",
            Action = TrainingDecisionAction.BuiltinDefault,
            IsFallback = true,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "builtin-default.json",
            FailRates = [5, 5, 5, 5, 5],
            KnownFailRateMask = 0b11111,
            LegacyFailRateThreshold = 30,
            LegacyRushThreshold = 450,
        };
        SetUnavailableFields(context, TrainingRuleField.StrengthStat);

        var probe = TrainingRuleEngine.Probe(context, profile);

        Assert.Null(probe.Decision);
        Assert.Equal(TrainingRuleField.StrengthIcons, probe.MissingField);
    }

    [Fact]
    public void Evaluate_returns_the_first_matching_rule()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "focus_first",
            Field = TrainingRuleField.FocusIcons,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 10,
            Action = TrainingDecisionAction.TrainFocus,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "need_stamina_stat",
            Field = TrainingRuleField.StaminaStat,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 999,
            Action = TrainingDecisionAction.TrainStamina,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "first-match.json",
            IconCounts = [0, 0, 0, 12, 0],
            FailRates = [5, 5, 5, 5, 5],
            StrengthStat = 300,
            StaminaStat = 200,
            BuildDirection = BuildDirection.Attack,
            LegacyFailRateThreshold = 30,
            LegacyRushThreshold = 450,
        };

        var result = TrainingRuleEngine.Evaluate(context, profile);

        Assert.Equal("focus_first", result.MatchedRuleId);
        Assert.Equal(TrainingDecisionAction.TrainFocus, result.Action);
        Assert.Equal(3, result.TargetRowIndex);
        Assert.False(result.UsedBuiltinDefault);
    }

    [Fact]
    public void Evaluate_requires_every_condition_in_a_two_condition_rule_to_match()
    {
        var profile = TrainingRuleLoader.LoadFromJson("""
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
        """, "two-conditions.json");

        var context = new TrainingDecisionContext
        {
            ProfileName = "two-conditions.json",
            IconCounts = [4, 0, 0, 0, 0],
            FailRates = [45, 0, 0, 0, 0],
            StrengthStat = 300,
            StaminaStat = 200,
        };

        var result = TrainingRuleEngine.Evaluate(context, profile);

        Assert.Equal("fallback_rest", result.MatchedRuleId);
        Assert.Equal(TrainingDecisionAction.Rest, result.Action);
    }

    [Fact]
    public void Probe_requests_the_second_condition_field_after_the_first_condition_matches()
    {
        var profile = TrainingRuleLoader.LoadFromJson("""
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
        """, "two-conditions.json");

        var context = new TrainingDecisionContext
        {
            ProfileName = "two-conditions.json",
            IconCounts = [4, 0, 0, 0, 0],
            KnownIconMask = 0b00001,
            FailRates = [0, 0, 0, 0, 0],
            KnownFailRateMask = 0,
            StrengthStat = 300,
        };

        var probe = TrainingRuleEngine.Probe(context, profile);

        Assert.Null(probe.Decision);
        Assert.Equal(TrainingRuleField.StrengthFailRate, probe.MissingField);
    }

    [Fact]
    public void Evaluate_preserves_the_fallback_rule_id_when_it_delegates_to_builtin_default()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "need_stamina_stat",
            Field = TrainingRuleField.StaminaStat,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 99,
            Action = TrainingDecisionAction.TrainStamina,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "builtin-default.json",
            IconCounts = [10, 0, 0, 0, 0],
            FailRates = [10, 10, 10, 10, 10],
            StrengthStat = 200,
            StaminaStat = 20,
            BuildDirection = BuildDirection.Attack,
            LegacyFailRateThreshold = 30,
            LegacyRushThreshold = 450,
        };

        var result = TrainingRuleEngine.Evaluate(context, profile);

        Assert.Equal("builtin_default", result.MatchedRuleId);
        Assert.Equal(TrainingDecisionAction.TrainStrength, result.Action);
        Assert.Equal(0, result.TargetRowIndex);
        Assert.True(result.UsedBuiltinDefault);
    }

    [Fact]
    public void Evaluate_uses_builtin_default_when_no_rule_matches()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "need_stamina_stat",
            Field = TrainingRuleField.StaminaStat,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 99,
            Action = TrainingDecisionAction.TrainStamina,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "fallback",
            Action = TrainingDecisionAction.BuiltinDefault,
            IsFallback = true,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "builtin-default.json",
            IconCounts = [5, 2, 3, 1, 1],
            FailRates = [45, 10, 10, 10, 10],
            StrengthStat = 200,
            StaminaStat = 20,
            BuildDirection = BuildDirection.Attack,
            LegacyFailRateThreshold = 30,
            LegacyRushThreshold = 450,
        };

        var result = TrainingRuleEngine.Evaluate(context, profile);

        Assert.Equal("fallback", result.MatchedRuleId);
        Assert.Equal(TrainingDecisionAction.Rest, result.Action);
        Assert.Null(result.TargetRowIndex);
        Assert.True(result.UsedBuiltinDefault);
    }

    [Fact]
    public void Evaluate_skips_a_rule_when_a_requested_field_is_unavailable()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "need_stamina_stat",
            Field = TrainingRuleField.StaminaFailRate,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 5,
            Action = TrainingDecisionAction.TrainStamina,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "focus_first",
            Field = TrainingRuleField.FocusIcons,
            Operator = TrainingRuleOperator.GreaterThan,
            Value = 10,
            Action = TrainingDecisionAction.TrainFocus,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "skip-unavailable.json",
            IconCounts = [0, 0, 0, 12, 0],
            FailRates = [0],
            StrengthStat = 300,
            StaminaStat = 200,
            BuildDirection = BuildDirection.Attack,
            LegacyFailRateThreshold = 30,
            LegacyRushThreshold = 450,
        };

        var result = TrainingRuleEngine.Evaluate(context, profile);

        Assert.Equal("focus_first", result.MatchedRuleId);
        Assert.Equal(TrainingDecisionAction.TrainFocus, result.Action);
        Assert.Equal(3, result.TargetRowIndex);
        Assert.False(result.UsedBuiltinDefault);
    }

    [Fact]
    public void Evaluate_uses_explicit_fallback_without_delegating_to_builtin_default()
    {
        var profile = new TrainingRuleProfile();
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "strength_icons_3",
            Field = TrainingRuleField.StrengthIcons,
            Operator = TrainingRuleOperator.GreaterThanOrEqual,
            Value = 3,
            Action = TrainingDecisionAction.TrainStrength,
        });
        profile.Rules.Add(new TrainingRuleCard
        {
            Id = "fallback_strength",
            Action = TrainingDecisionAction.TrainStrength,
            IsFallback = true,
        });

        var context = new TrainingDecisionContext
        {
            ProfileName = "pure-rules.json",
            IconCounts = [1, 1, 1, 1, 1],
            FailRates = [5, 5, 5, 5, 5],
            StrengthStat = 120,
            StaminaStat = 120,
        };

        var result = TrainingRuleEngine.Evaluate(context, profile);

        Assert.Equal("fallback_strength", result.MatchedRuleId);
        Assert.Equal(TrainingDecisionAction.TrainStrength, result.Action);
        Assert.Equal(0, result.TargetRowIndex);
        Assert.False(result.UsedBuiltinDefault);
    }

    private static void SetUnavailableFields(
        TrainingDecisionContext context,
        params TrainingRuleField[] fields)
    {
        var property = typeof(TrainingDecisionContext).GetProperty("UnavailableFields")
            ?? throw new Xunit.Sdk.XunitException("TrainingDecisionContext.UnavailableFields was not found.");
        property.SetValue(context, fields);
    }
}
