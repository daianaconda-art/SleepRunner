using SleepRunner.Automation.Race;

namespace SleepRunner.Automation.Race.Policy.Training;

public enum TrainingRuleField
{
    StrengthIcons,
    StaminaIcons,
    AgilityIcons,
    FocusIcons,
    GuardIcons,
    StrengthFailRate,
    StaminaFailRate,
    AgilityFailRate,
    FocusFailRate,
    GuardFailRate,
    AnyFailRate,
    StrengthStat,
    StaminaStat,
}

public enum TrainingRuleOperator
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
}

public enum TrainingDecisionAction
{
    TrainStrength,
    TrainStamina,
    TrainAgility,
    TrainFocus,
    TrainGuard,
    Rest,
    BuiltinDefault,
}

public sealed class TrainingRuleCondition
{
    public TrainingRuleField Field { get; set; }

    public TrainingRuleOperator Operator { get; set; }

    public int Value { get; set; }
}

public sealed class TrainingRuleCard
{
    public string Id { get; set; } = string.Empty;

    public List<TrainingRuleCondition> Conditions { get; } = new();

    public TrainingRuleField? Field { get; set; }

    public TrainingRuleOperator? Operator { get; set; }

    public int? Value { get; set; }

    public TrainingDecisionAction Action { get; set; }

    public bool Enabled { get; set; } = true;

    public bool IsFallback { get; set; }
}

public sealed class TrainingRuleProfile
{
    public string SourcePath { get; init; } = string.Empty;

    public TrainingLegacyStrategy LegacyStrategy { get; } = new();

    public List<TrainingRuleCard> Rules { get; } = new();
}

public sealed class TrainingLegacyStrategy
{
    public BuildDirection BuildDirection { get; set; } = BuildDirection.Attack;

    public int FailRateThreshold { get; set; } = 30;

    public int RushThreshold { get; set; } = 450;
}
