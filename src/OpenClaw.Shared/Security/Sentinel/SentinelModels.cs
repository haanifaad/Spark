using OpenClaw.Shared.Tools;

namespace OpenClaw.Shared.Security.Sentinel;

/// <summary>
/// Structured outcome from the independent Sentinel security review.
/// </summary>
public enum SentinelDecisionKind
{
    Approved = 0,
    Rejected = 1,
    RequiresUserApproval = 2,
    RequiresMoreInformation = 3
}

/// <summary>
/// Contextual request passed to Sentinel for independent second-order security evaluation.
/// </summary>
public sealed class SentinelEvaluationRequest
{
    public required string TaskId { get; init; }
    public string? StepId { get; init; }
    public required string ProposedToolName { get; init; }
    public Dictionary<string, object?> OperationArguments { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string CallerIdentity { get; init; } = "SPARK-Primary";
    public string? TargetDestination { get; init; }
    public decimal? FinancialAmount { get; init; }
    public string Currency { get; init; } = "INR";
    public required string TaskOriginalInstruction { get; init; }
    public string? DataPayloadText { get; init; }
    public bool IsAutonomousBackgroundExecution { get; init; }
    public HashSet<string> GrantedPermissions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Detailed evaluation verdict produced by the Sentinel security engine.
/// </summary>
public sealed class SentinelReviewResult
{
    public required SentinelDecisionKind Decision { get; init; }
    public required SparkToolRiskLevel RiskLevel { get; init; }
    public List<string> Reasons { get; init; } = [];
    public List<string> Evidence { get; init; } = [];
    public List<string> PolicyReferences { get; init; } = [];
    public string? RequiredUserAction { get; init; }
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string EvaluatorSignature { get; init; } = string.Empty;

    public bool IsBlocked => Decision is SentinelDecisionKind.Rejected or SentinelDecisionKind.RequiresUserApproval or SentinelDecisionKind.RequiresMoreInformation;
}
