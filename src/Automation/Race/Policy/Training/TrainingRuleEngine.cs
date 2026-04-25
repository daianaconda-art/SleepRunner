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

            if (rule.Field is null || rule.Operator is null || !rule.Value.HasValue)
            {
                continue;
            }

            if (!context.TryGetMetric(rule.Field.Value, out int metric))
            {
                return new TrainingRuleProbeResult
                {
                    MissingField = rule.Field.Value,
                };
            }

            if (!Matches(metric, rule.Operator.Value, rule.Value.Value))
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

            if (rule.Field is null || rule.Operator is null || !rule.Value.HasValue)
            {
                continue;
            }

            if (!context.TryGetMetric(rule.Field.Value, out int metric))
            {
                continue;
            }

            if (!Matches(metric, rule.Operator.Value, rule.Value.Value))
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
