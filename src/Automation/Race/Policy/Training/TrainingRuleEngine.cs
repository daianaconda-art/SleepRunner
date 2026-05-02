using SleepRunner.Automation.Race.Policy;

namespace SleepRunner.Automation.Race.Policy.Training;

public static class TrainingRuleEngine
{
    public static TrainingRuleProbeResult Probe(TrainingDecisionContext context, TrainingRuleProfile profile)
    {
        for (int i = 0; i < profile.Rules.Count; i++)
        {
            var rule = profile.Rules[i];
            if (!rule.Enabled)
            {
                continue;
            }

            if (rule.IsFallback)
            {
                if (!TryProbeActionAgainstCap(context, rule.Action, out TrainingRuleField? missingCapField))
                {
                    if (missingCapField.HasValue)
                    {
                        return new TrainingRuleProbeResult
                        {
                            MissingField = missingCapField.Value,
                        };
                    }

                    continue;
                }

                return ResolveProbe(context, rule, rule.Action);
            }

            if (!TryProbeConditions(context, rule, out bool conditionsMatch, out TrainingRuleField? missingConditionField))
            {
                return new TrainingRuleProbeResult
                {
                    MissingField = missingConditionField,
                };
            }

            if (!conditionsMatch)
            {
                continue;
            }

            if (!TryProbeActionAgainstCap(context, rule.Action, out TrainingRuleField? missingProbeCapField))
            {
                if (missingProbeCapField.HasValue)
                {
                    return new TrainingRuleProbeResult
                    {
                        MissingField = missingProbeCapField.Value,
                    };
                }

                continue;
            }

            return ResolveProbe(context, rule, rule.Action);
        }

        return TrainingBuiltInDecision.Probe(context, "builtin_default");
    }

    public static TrainingDecisionResult Evaluate(TrainingDecisionContext context, TrainingRuleProfile profile)
    {
        for (int i = 0; i < profile.Rules.Count; i++)
        {
            var rule = profile.Rules[i];
            if (!rule.Enabled)
            {
                continue;
            }

            if (rule.IsFallback)
            {
                if (IsActionBlockedByKnownCap(context, rule.Action))
                {
                    continue;
                }

                return ResolveResult(context, rule, rule.Action);
            }

            if (!EvaluateConditions(context, rule))
            {
                continue;
            }

            if (IsActionBlockedByKnownCap(context, rule.Action))
            {
                continue;
            }

            return ResolveResult(context, rule, rule.Action);
        }

        return TrainingBuiltInDecision.Evaluate(context, "builtin_default");
    }

    private static TrainingRuleProbeResult ResolveProbe(TrainingDecisionContext context, TrainingRuleCard rule, TrainingDecisionAction action)
    {
        if (action == TrainingDecisionAction.BuiltinDefault)
        {
            return TrainingBuiltInDecision.Probe(context, rule.Id);
        }

        return new TrainingRuleProbeResult
        {
            Decision = ResolveResult(context, rule, action),
        };
    }

    private static TrainingDecisionResult ResolveResult(TrainingDecisionContext context, TrainingRuleCard rule, TrainingDecisionAction action)
    {
        if (action == TrainingDecisionAction.BuiltinDefault)
        {
            return TrainingBuiltInDecision.Evaluate(context, rule.Id);
        }

        int? targetRowIndex = ActionToRowIndex(action);
        return new TrainingDecisionResult
        {
            MatchedRuleId = rule.Id,
            Action = action,
            TargetRowIndex = targetRowIndex,
            UsedBuiltinDefault = false,
            Summary = $"profile={context.ProfileName}, matched={rule.Id}, action={action}, target={targetRowIndex}, builtin_default=False",
        };
    }

    private static bool TryProbeConditions(
        TrainingDecisionContext context,
        TrainingRuleCard rule,
        out bool matches,
        out TrainingRuleField? missingField)
    {
        matches = false;
        missingField = null;

        IReadOnlyList<TrainingRuleCondition> conditions = GetConditions(rule);
        if (conditions.Count == 0)
        {
            return true;
        }

        foreach (TrainingRuleCondition condition in conditions)
        {
            if (!context.TryGetMetric(condition.Field, out int metric))
            {
                missingField = condition.Field;
                return false;
            }

            if (!Matches(metric, condition.Operator, condition.Value))
            {
                return true;
            }
        }

        matches = true;
        return true;
    }

    private static bool EvaluateConditions(TrainingDecisionContext context, TrainingRuleCard rule)
    {
        IReadOnlyList<TrainingRuleCondition> conditions = GetConditions(rule);
        if (conditions.Count == 0)
        {
            return false;
        }

        foreach (TrainingRuleCondition condition in conditions)
        {
            if (!context.TryGetMetric(condition.Field, out int metric))
            {
                return false;
            }

            if (!Matches(metric, condition.Operator, condition.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<TrainingRuleCondition> GetConditions(TrainingRuleCard rule)
    {
        if (rule.Conditions.Count > 0)
        {
            return rule.Conditions;
        }

        if (rule.Field is null || rule.Operator is null || !rule.Value.HasValue)
        {
            return [];
        }

        return
        [
            new TrainingRuleCondition
            {
                Field = rule.Field.Value,
                Operator = rule.Operator.Value,
                Value = rule.Value.Value,
            },
        ];
    }

    private static bool Matches(int metric, TrainingRuleOperator op, int value) => op switch
    {
        TrainingRuleOperator.GreaterThan => metric > value,
        TrainingRuleOperator.GreaterThanOrEqual => metric >= value,
        TrainingRuleOperator.LessThan => metric < value,
        TrainingRuleOperator.LessThanOrEqual => metric <= value,
        _ => false,
    };

    private static int? ActionToRowIndex(TrainingDecisionAction action) => action switch
    {
        TrainingDecisionAction.TrainStrength => 0,
        TrainingDecisionAction.TrainStamina => 1,
        TrainingDecisionAction.TrainAgility => 2,
        TrainingDecisionAction.TrainFocus => 3,
        TrainingDecisionAction.TrainGuard => 4,
        _ => null,
    };

    private static bool TryProbeActionAgainstCap(
        TrainingDecisionContext context,
        TrainingDecisionAction action,
        out TrainingRuleField? missingField)
    {
        missingField = GetCapMetricField(action);
        if (!missingField.HasValue)
        {
            return true;
        }

        if (!context.TryGetMetric(missingField.Value, out int stat))
        {
            return false;
        }

        missingField = null;
        return !IsActionBlockedByCap(action, stat);
    }

    private static bool IsActionBlockedByKnownCap(TrainingDecisionContext context, TrainingDecisionAction action)
    {
        TrainingRuleField? field = GetCapMetricField(action);
        if (!field.HasValue || !context.TryGetMetric(field.Value, out int stat))
        {
            return false;
        }

        return IsActionBlockedByCap(action, stat);
    }

    private static TrainingRuleField? GetCapMetricField(TrainingDecisionAction action) => action switch
    {
        TrainingDecisionAction.TrainStrength => TrainingRuleField.StrengthStat,
        TrainingDecisionAction.TrainStamina => TrainingRuleField.StaminaStat,
        _ => null,
    };

    private static bool IsActionBlockedByCap(TrainingDecisionAction action, int stat) => action switch
    {
        TrainingDecisionAction.TrainStrength => RaceStatCapPolicy.IsStrengthCapped(stat),
        TrainingDecisionAction.TrainStamina => RaceStatCapPolicy.IsStaminaCapped(stat),
        _ => false,
    };
}
