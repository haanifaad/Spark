using System.Text.Json.Serialization;
using OpenClaw.Shared.Tools;

namespace OpenClaw.Shared.Orchestration;

/// <summary>
/// Strict 14-state lifecycle for SPARK autonomous tasks as mandated by master architecture.
/// </summary>
public enum SparkTaskState
{
    Received = 0,
    Interpreting = 1,
    Planning = 2,
    WaitingForApproval = 3,
    Ready = 4,
    Executing = 5,
    WaitingForExternalInput = 6,
    Paused = 7,
    Reviewing = 8,
    Verifying = 9,
    Completed = 10,
    Failed = 11,
    Cancelled = 12,
    RequiresUserIntervention = 13
}

/// <summary>
/// Execution state of an individual step within a task plan.
/// </summary>
public enum SparkStepState
{
    Pending = 0,
    Ready = 1,
    WaitingForApproval = 2,
    Executing = 3,
    Completed = 4,
    Failed = 5,
    Skipped = 6,
    RequiresIntervention = 7
}

/// <summary>
/// Scheduling priority for autonomous tasks.
/// </summary>
public enum SparkTaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>
/// Audit record of user modification made to an active or pending task.
/// </summary>
public sealed class SparkTaskModificationRecord
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required string PreviousInstruction { get; init; }
    public required string NewInstruction { get; init; }
    public string? Reason { get; init; }
    public string ModifiedBy { get; init; } = "User";
}

/// <summary>
/// Individual operation step within a decomposed task.
/// </summary>
public sealed class SparkTaskStep
{
    public string StepId { get; init; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; init; } = string.Empty;
    public int Index { get; set; }
    public required string Description { get; set; }
    public required string ToolName { get; set; }
    public Dictionary<string, object?> InputArguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? OutputResult { get; set; }
    public SparkStepState Status { get; set; } = SparkStepState.Pending;
    public SparkToolRiskLevel RiskClassification { get; set; } = SparkToolRiskLevel.Low;
    public bool RequiresApproval { get; set; }
    public string? ApprovalId { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public bool? VerificationPassed { get; set; }
    public string? VerificationDetails { get; set; }
    public string? ErrorInfo { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan? ExecutionDuration => (CompletedAt.HasValue && StartedAt.HasValue) ? CompletedAt.Value - StartedAt.Value : null;
}

/// <summary>
/// Complete autonomous agent task with 14-state lifecycle, steps, and provenance.
/// </summary>
public sealed class SparkTask
{
    public string TaskId { get; init; } = Guid.NewGuid().ToString("N");
    public string UserId { get; init; } = "default";
    public required string Title { get; set; }
    public required string UserInstruction { get; set; }
    public string DetectedLanguage { get; set; } = "English"; // English, Malayalam, Mixed
    public SparkTaskState Status { get; set; } = SparkTaskState.Received;
    public SparkTaskPriority Priority { get; set; } = SparkTaskPriority.Normal;
    public SparkToolRiskLevel OverallRiskLevel { get; set; } = SparkToolRiskLevel.Low;
    public List<SparkTaskStep> Steps { get; set; } = [];
    public List<SparkTaskModificationRecord> ModificationHistory { get; set; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan? EstimatedDuration { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FinalSummary { get; set; }
    public string? ActiveAgentIdentity { get; set; } = "SPARK-Primary";
    public bool IsUserTakeoverActive { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsActive => Status is SparkTaskState.Executing 
                            or SparkTaskState.Planning 
                            or SparkTaskState.Interpreting 
                            or SparkTaskState.WaitingForApproval 
                            or SparkTaskState.WaitingForExternalInput 
                            or SparkTaskState.Reviewing 
                            or SparkTaskState.Verifying;

    [JsonIgnore]
    public bool IsTerminal => Status is SparkTaskState.Completed or SparkTaskState.Failed or SparkTaskState.Cancelled;
}

/// <summary>
/// Validated plan produced by the TaskPlanner before execution.
/// </summary>
public sealed class SparkTaskPlan
{
    public required string TaskId { get; init; }
    public required string HighLevelObjective { get; init; }
    public List<SparkTaskStep> PlannedSteps { get; init; } = [];
    public SparkToolRiskLevel HighestRiskLevel { get; init; } = SparkToolRiskLevel.Low;
    public bool HasApprovalGatedSteps => PlannedSteps.Any(s => s.RequiresApproval);
    public TimeSpan EstimatedTotalDuration { get; init; } = TimeSpan.FromSeconds(30);
    public decimal EstimatedTotalCost { get; init; } = 0.0m;
    public List<string> RequiredPermissions { get; init; } = [];
    public List<string> MissingInformation { get; init; } = [];
}
