namespace SleepRunner.Automation.Race.Policy.Training;

public sealed class TrainingDecisionResult
{
    public string MatchedRuleId { get; init; } = string.Empty;

    public TrainingDecisionAction Action { get; init; }

    public int? TargetRowIndex { get; init; }

    public bool UsedBuiltinDefault { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class TrainingRuleProbeResult
{
    public TrainingDecisionResult? Decision { get; init; }

    public TrainingRuleField? MissingField { get; init; }
}
