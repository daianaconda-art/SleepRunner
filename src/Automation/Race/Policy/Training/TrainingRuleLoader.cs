using System.Text;
using System.Text.Json;
using SleepRunner.Automation.Race;

namespace SleepRunner.Automation.Race.Policy.Training;

public static class TrainingRuleLoader
{
    public static string SaveToJson(TrainingRuleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        IReadOnlyList<TrainingRuleCard> normalizedRules = NormalizeRulesForSave(profile);
        bool usesBuiltinDefault = normalizedRules.Any(rule => rule.Action == TrainingDecisionAction.BuiltinDefault);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            if (usesBuiltinDefault)
            {
                WriteLegacyStrategy(writer, profile.LegacyStrategy);
            }
            writer.WritePropertyName("rules");
            writer.WriteStartArray();

            foreach (var rule in normalizedRules)
            {
                WriteRule(writer, rule);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void SaveToPath(TrainingRuleProfile profile, string path)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, SaveToJson(profile), Encoding.UTF8);
    }

    public static TrainingRuleProfile LoadFromJson(string json, string sourcePath)
    {
        using var document = JsonDocument.Parse(json);

        var profile = new TrainingRuleProfile
        {
            SourcePath = sourcePath,
        };

        if (TryGetProperty(document.RootElement, "legacy_strategy", out var legacyStrategyElement) &&
            legacyStrategyElement.ValueKind == JsonValueKind.Object)
        {
            LoadLegacyStrategy(legacyStrategyElement, profile.LegacyStrategy);
        }

        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasFallbackRule = false;

        if (TryGetProperty(document.RootElement, "rules", out var rulesElement) &&
            rulesElement.ValueKind == JsonValueKind.Array)
        {
            var ruleIndex = 0;
            foreach (var ruleElement in rulesElement.EnumerateArray())
            {
                ruleIndex++;

                if (hasFallbackRule)
                {
                    throw new JsonException("Fallback rules must be the last rule in a training profile.");
                }

                var hasField = TryGetProperty(ruleElement, "field", out var fieldElement);
                var hasOperator = TryGetProperty(ruleElement, "operator", out var operatorElement);
                var hasValue = TryGetProperty(ruleElement, "value", out var valueElement);
                var hasConditions = TryGetProperty(ruleElement, "conditions", out var conditionsElement);
                var hasConditionFieldCount = (hasField ? 1 : 0) + (hasOperator ? 1 : 0) + (hasValue ? 1 : 0);
                var hasLegacyCondition = hasConditionFieldCount > 0;

                if (hasConditions && hasLegacyCondition)
                {
                    throw new JsonException("Training rules cannot mix 'conditions' with legacy field/operator/value properties.");
                }

                if (hasLegacyCondition && hasConditionFieldCount != 3)
                {
                    throw new JsonException("Training rules must be either fully conditional or structurally fallback.");
                }

                List<TrainingRuleCondition> conditions = hasConditions
                    ? ParseConditions(conditionsElement)
                    : hasLegacyCondition
                        ? [ParseCondition(fieldElement, operatorElement, valueElement)]
                        : [];
                var isFallback = conditions.Count == 0;

                string id = ResolveRuleId(
                    ruleElement,
                    isFallback,
                    conditions.Count > 0 ? ToToken(conditions[0].Field) : null,
                    conditions.Count > 0 ? ToToken(conditions[0].Operator) : null,
                    conditions.Count > 0 ? conditions[0].Value : null,
                    ruleIndex,
                    usedIds);

                var rule = new TrainingRuleCard
                {
                    Id = id,
                    Enabled = TryGetBoolean(ruleElement, "enabled", defaultValue: true),
                    IsFallback = isFallback,
                };

                if (rule.IsFallback)
                {
                    hasFallbackRule = true;
                }

                ApplyConditions(rule, conditions);

                if (TryGetProperty(ruleElement, "action", out var actionElement))
                {
                    rule.Action = ParseAction(actionElement.GetString());
                }
                else if (rule.IsFallback)
                {
                    rule.Action = TrainingDecisionAction.BuiltinDefault;
                }
                else
                {
                    throw new JsonException("Training rules require an action.");
                }

                profile.Rules.Add(rule);
            }
        }

        if (!hasFallbackRule)
        {
            string fallbackId = EnsureUniqueId("fallback", usedIds);
            profile.Rules.Add(new TrainingRuleCard
            {
                Id = fallbackId,
                Action = TrainingDecisionAction.BuiltinDefault,
                Enabled = true,
                IsFallback = true,
            });
        }

        return profile;
    }

    private static List<TrainingRuleCondition> ParseConditions(JsonElement conditionsElement)
    {
        if (conditionsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Training rule conditions must be an array.");
        }

        var conditions = new List<TrainingRuleCondition>();
        foreach (var conditionElement in conditionsElement.EnumerateArray())
        {
            if (conditionElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Training rule conditions must be objects.");
            }

            if (!TryGetProperty(conditionElement, "field", out var fieldElement) ||
                !TryGetProperty(conditionElement, "operator", out var operatorElement) ||
                !TryGetProperty(conditionElement, "value", out var valueElement))
            {
                throw new JsonException("Training rule conditions require field, operator, and value.");
            }

            conditions.Add(ParseCondition(fieldElement, operatorElement, valueElement));
        }

        if (conditions.Count is < 1 or > 2)
        {
            throw new JsonException("Training rules support one or two conditions.");
        }

        return conditions;
    }

    private static TrainingRuleCondition ParseCondition(JsonElement fieldElement, JsonElement operatorElement, JsonElement valueElement)
    {
        return new TrainingRuleCondition
        {
            Field = ParseField(fieldElement.GetString()),
            Operator = ParseOperator(operatorElement.GetString()),
            Value = valueElement.GetInt32(),
        };
    }

    private static void ApplyConditions(TrainingRuleCard rule, IReadOnlyList<TrainingRuleCondition> conditions)
    {
        rule.Conditions.Clear();

        foreach (TrainingRuleCondition condition in conditions)
        {
            rule.Conditions.Add(CloneCondition(condition));
        }

        TrainingRuleCondition? first = conditions.FirstOrDefault();
        rule.Field = first?.Field;
        rule.Operator = first?.Operator;
        rule.Value = first?.Value;
    }

    private static TrainingRuleField ParseField(string? token) => token?.ToLowerInvariant() switch
    {
        "strength_icons" => TrainingRuleField.StrengthIcons,
        "stamina_icons" => TrainingRuleField.StaminaIcons,
        "agility_icons" => TrainingRuleField.AgilityIcons,
        "focus_icons" => TrainingRuleField.FocusIcons,
        "guard_icons" => TrainingRuleField.GuardIcons,
        "strength_fail_rate" => TrainingRuleField.StrengthFailRate,
        "stamina_fail_rate" => TrainingRuleField.StaminaFailRate,
        "agility_fail_rate" => TrainingRuleField.AgilityFailRate,
        "focus_fail_rate" => TrainingRuleField.FocusFailRate,
        "guard_fail_rate" => TrainingRuleField.GuardFailRate,
        "any_fail_rate" => TrainingRuleField.AnyFailRate,
        "strength_stat" => TrainingRuleField.StrengthStat,
        "stamina_stat" => TrainingRuleField.StaminaStat,
        _ => throw new JsonException($"Unknown training rule field '{token}'."),
    };

    private static TrainingRuleOperator ParseOperator(string? token) => token switch
    {
        ">" => TrainingRuleOperator.GreaterThan,
        ">=" => TrainingRuleOperator.GreaterThanOrEqual,
        "<" => TrainingRuleOperator.LessThan,
        "<=" => TrainingRuleOperator.LessThanOrEqual,
        _ => throw new JsonException($"Unknown training rule operator '{token}'."),
    };

    private static TrainingDecisionAction ParseAction(string? token) => token?.ToLowerInvariant() switch
    {
        "train_strength" => TrainingDecisionAction.TrainStrength,
        "train_stamina" => TrainingDecisionAction.TrainStamina,
        "train_agility" => TrainingDecisionAction.TrainAgility,
        "train_focus" => TrainingDecisionAction.TrainFocus,
        "train_guard" => TrainingDecisionAction.TrainGuard,
        "rest" => TrainingDecisionAction.Rest,
        "builtin_default" => TrainingDecisionAction.BuiltinDefault,
        _ => throw new JsonException($"Unknown training decision action '{token}'."),
    };

    private static bool TryGetBoolean(JsonElement element, string propertyName, bool defaultValue)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return defaultValue;
        }

        return property.GetBoolean();
    }

    private static void LoadLegacyStrategy(JsonElement element, TrainingLegacyStrategy strategy)
    {
        if (TryGetProperty(element, "build_direction", out var buildDirectionElement))
        {
            strategy.BuildDirection = ParseBuildDirection(buildDirectionElement.GetString());
        }

        if (TryGetProperty(element, "fail_rate_threshold", out var failRateThresholdElement))
        {
            strategy.FailRateThreshold = Math.Clamp(failRateThresholdElement.GetInt32(), 0, 100);
        }

        if (TryGetProperty(element, "rush_threshold", out var rushThresholdElement))
        {
            strategy.RushThreshold = Math.Clamp(rushThresholdElement.GetInt32(), 100, 1200);
        }
    }

    private static BuildDirection ParseBuildDirection(string? token) => token?.ToLowerInvariant() switch
    {
        "attack" => BuildDirection.Attack,
        "survival" => BuildDirection.Survival,
        _ => throw new JsonException($"Unknown build direction '{token}'."),
    };

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement propertyValue)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                propertyValue = property.Value;
                return true;
            }
        }

        propertyValue = default;
        return false;
    }

    private static string ResolveRuleId(
        JsonElement ruleElement,
        bool isFallback,
        string? fieldToken,
        string? operatorToken,
        int? valueToken,
        int ruleIndex,
        HashSet<string> usedIds)
    {
        if (TryGetProperty(ruleElement, "id", out var idElement))
        {
            string? explicitId = idElement.GetString();
            if (!string.IsNullOrWhiteSpace(explicitId))
            {
                string normalized = explicitId.Trim();
                if (usedIds.Contains(normalized))
                {
                    throw new JsonException($"Duplicate training rule id '{normalized}'.");
                }

                usedIds.Add(normalized);
                return normalized;
            }
        }

        if (isFallback)
        {
            return EnsureUniqueId("fallback", usedIds);
        }

        string derivedId = BuildDerivedRuleId(fieldToken, operatorToken, valueToken, ruleIndex);
        return EnsureUniqueId(derivedId, usedIds);
    }

    private static string BuildDerivedRuleId(string? fieldToken, string? operatorToken, int? valueToken, int ruleIndex)
    {
        string fieldPart = NormalizeIdPart(fieldToken) ?? $"rule{ruleIndex}";
        string operatorPart = NormalizeIdPart(operatorToken) ?? "cond";
        string valuePart = valueToken?.ToString() ?? "value";
        return $"{fieldPart}_{operatorPart}_{valuePart}";
    }

    private static string? NormalizeIdPart(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return token.Trim().ToLowerInvariant();
    }

    private static string EnsureUniqueId(string id, HashSet<string> usedIds)
    {
        string candidate = id;
        int suffix = 2;

        while (!usedIds.Add(candidate))
        {
            candidate = $"{id}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static IReadOnlyList<TrainingRuleCard> NormalizeRulesForSave(TrainingRuleProfile profile)
    {
        var normalRules = new List<TrainingRuleCard>();
        TrainingRuleCard? fallbackRule = null;
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in profile.Rules)
        {
            ValidateExplicitId(rule.Id, usedIds);

            if (rule.IsFallback)
            {
                if (fallbackRule is not null)
                {
                    throw new InvalidOperationException("Training profiles can only contain one fallback rule.");
                }

                fallbackRule = rule;
                continue;
            }

            ValidateNormalRule(rule);
            normalRules.Add(rule);
        }

        fallbackRule ??= new TrainingRuleCard
        {
            Id = "fallback",
            Action = TrainingDecisionAction.BuiltinDefault,
            Enabled = true,
            IsFallback = true,
        };

        normalRules.Add(fallbackRule);
        return normalRules;
    }

    private static void ValidateExplicitId(string? id, HashSet<string> usedIds)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        string normalized = id.Trim();
        if (!usedIds.Add(normalized))
        {
            throw new InvalidOperationException($"Duplicate training rule id '{normalized}'.");
        }
    }

    private static void ValidateNormalRule(TrainingRuleCard rule)
    {
        IReadOnlyList<TrainingRuleCondition> conditions = GetConditions(rule);
        if (conditions.Count is < 1 or > 2)
        {
            throw new InvalidOperationException("Training rules must have one or two conditions.");
        }
    }

    private static void WriteRule(Utf8JsonWriter writer, TrainingRuleCard rule)
    {
        writer.WriteStartObject();

        if (!string.IsNullOrWhiteSpace(rule.Id))
        {
            writer.WriteString("id", rule.Id);
        }

        if (!rule.IsFallback)
        {
            IReadOnlyList<TrainingRuleCondition> conditions = GetConditions(rule);
            if (conditions.Count == 1)
            {
                WriteLegacyCondition(writer, conditions[0]);
            }
            else
            {
                writer.WritePropertyName("conditions");
                writer.WriteStartArray();
                foreach (TrainingRuleCondition condition in conditions)
                {
                    writer.WriteStartObject();
                    WriteLegacyCondition(writer, condition);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
        }

        writer.WriteString("action", ToToken(rule.Action));
        writer.WriteBoolean("enabled", rule.Enabled);
        writer.WriteEndObject();
    }

    private static IReadOnlyList<TrainingRuleCondition> GetConditions(TrainingRuleCard rule)
    {
        if (rule.Conditions.Count > 0)
        {
            return rule.Conditions;
        }

        if (rule.Field is null || rule.Operator is null || rule.Value is null)
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

    private static TrainingRuleCondition CloneCondition(TrainingRuleCondition condition)
    {
        return new TrainingRuleCondition
        {
            Field = condition.Field,
            Operator = condition.Operator,
            Value = condition.Value,
        };
    }

    private static void WriteLegacyCondition(Utf8JsonWriter writer, TrainingRuleCondition condition)
    {
        writer.WriteString("field", ToToken(condition.Field));
        writer.WriteString("operator", ToToken(condition.Operator));
        writer.WriteNumber("value", condition.Value);
    }

    private static void WriteLegacyStrategy(Utf8JsonWriter writer, TrainingLegacyStrategy strategy)
    {
        writer.WritePropertyName("legacy_strategy");
        writer.WriteStartObject();
        writer.WriteString("build_direction", ToToken(strategy.BuildDirection));
        writer.WriteNumber("fail_rate_threshold", Math.Clamp(strategy.FailRateThreshold, 0, 100));
        writer.WriteNumber("rush_threshold", Math.Clamp(strategy.RushThreshold, 100, 1200));
        writer.WriteEndObject();
    }

    private static string ToToken(TrainingRuleField field) => field switch
    {
        TrainingRuleField.StrengthIcons => "strength_icons",
        TrainingRuleField.StaminaIcons => "stamina_icons",
        TrainingRuleField.AgilityIcons => "agility_icons",
        TrainingRuleField.FocusIcons => "focus_icons",
        TrainingRuleField.GuardIcons => "guard_icons",
        TrainingRuleField.StrengthFailRate => "strength_fail_rate",
        TrainingRuleField.StaminaFailRate => "stamina_fail_rate",
        TrainingRuleField.AgilityFailRate => "agility_fail_rate",
        TrainingRuleField.FocusFailRate => "focus_fail_rate",
        TrainingRuleField.GuardFailRate => "guard_fail_rate",
        TrainingRuleField.AnyFailRate => "any_fail_rate",
        TrainingRuleField.StrengthStat => "strength_stat",
        TrainingRuleField.StaminaStat => "stamina_stat",
        _ => throw new InvalidOperationException($"Unknown training rule field '{field}'."),
    };

    private static string ToToken(TrainingRuleOperator op) => op switch
    {
        TrainingRuleOperator.GreaterThan => ">",
        TrainingRuleOperator.GreaterThanOrEqual => ">=",
        TrainingRuleOperator.LessThan => "<",
        TrainingRuleOperator.LessThanOrEqual => "<=",
        _ => throw new InvalidOperationException($"Unknown training rule operator '{op}'."),
    };

    private static string ToToken(TrainingDecisionAction action) => action switch
    {
        TrainingDecisionAction.TrainStrength => "train_strength",
        TrainingDecisionAction.TrainStamina => "train_stamina",
        TrainingDecisionAction.TrainAgility => "train_agility",
        TrainingDecisionAction.TrainFocus => "train_focus",
        TrainingDecisionAction.TrainGuard => "train_guard",
        TrainingDecisionAction.Rest => "rest",
        TrainingDecisionAction.BuiltinDefault => "builtin_default",
        _ => throw new InvalidOperationException($"Unknown training decision action '{action}'."),
    };

    private static string ToToken(BuildDirection direction) => direction switch
    {
        BuildDirection.Attack => "attack",
        BuildDirection.Survival => "survival",
        _ => throw new InvalidOperationException($"Unknown build direction '{direction}'."),
    };
}
