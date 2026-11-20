using System.Text.Json.Serialization;

namespace OpenClaw.Shared.Cron.Autonomous;

/// <summary>
/// Categories of autonomous tasks handled by the SPARK Autonomous Scheduler.
/// </summary>
public enum SparkTaskCategory
{
    InboxMonitor,
    DailySummary,
    TaskReminder,
    ProjectHealthMonitor,
    ScheduledResearch,
    Custom
}

/// <summary>
/// Status lifecycle of an autonomous task.
/// </summary>
public enum SparkTaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Paused,
    Cancelled
}

/// <summary>
/// Explicit, bounded permissions granted to an autonomous background task.
/// </summary>
public enum SparkTaskPermission
{
    FilesystemRead,
    FilesystemWrite,
    ProcessExecution,
    NetworkAccess,
    McpToolAccess,
    ExternalApiAccess
}

/// <summary>
/// Exponential backoff retry configuration for autonomous tasks.
/// </summary>
public sealed class SparkRetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan MaxInterval { get; set; } = TimeSpan.FromMinutes(5);
    public double BackoffMultiplier { get; set; } = 2.0;

    public TimeSpan CalculateDelay(int attempt)
    {
        if (attempt <= 0) return TimeSpan.Zero;
        if (attempt > MaxRetries) return MaxInterval;

        var delayTicks = (long)(InitialInterval.Ticks * Math.Pow(BackoffMultiplier, attempt - 1));
        var delay = TimeSpan.FromTicks(delayTicks);
        return delay > MaxInterval ? MaxInterval : delay;
    }
}

/// <summary>
/// Immutable record of a single autonomous task run execution.
/// </summary>
public sealed record SparkTaskExecutionRecord
{
    public required string ExecutionId { get; init; }
    public required string TaskId { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
    public TimeSpan Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : TimeSpan.Zero;
    public SparkTaskStatus Status { get; init; } = SparkTaskStatus.Pending;
    public int AttemptNumber { get; init; } = 1;
    public string? OutputSummary { get; init; }
    public string? ErrorMessage { get; init; }
    public string? DiagnosticsJson { get; init; }
}

/// <summary>
/// Full definition and runtime state of an autonomous SPARK task.
/// </summary>
public sealed class SparkAutonomousTask
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public SparkTaskCategory Category { get; set; } = SparkTaskCategory.Custom;
    public string OwnerAgentId { get; set; } = "main";
    public bool Enabled { get; set; } = true;

    // Scheduling
    public TimeSpan? Interval { get; set; }
    public string? CronExpression { get; set; }
    public DateTimeOffset? OneTimeDueAt { get; set; }

    // Bounded Execution & Security Policies
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(3);
    public HashSet<SparkTaskPermission> Permissions { get; set; } = new();
    public bool RequireApprovalForHighImpact { get; set; } = true;
    public SparkRetryPolicy RetryPolicy { get; set; } = new();

    // State Tracking
    public SparkTaskStatus CurrentStatus { get; set; } = SparkTaskStatus.Pending;
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public int RunCount { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? LastError { get; set; }

    // Execution Payload
    public string PayloadPrompt { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Audit and History
    public List<SparkTaskExecutionRecord> ExecutionHistory { get; set; } = new();

    public void RecordExecution(SparkTaskExecutionRecord record, int maxHistoryToRetain = 100)
    {
        ExecutionHistory.Insert(0, record);
        if (ExecutionHistory.Count > maxHistoryToRetain)
        {
            ExecutionHistory.RemoveRange(maxHistoryToRetain, ExecutionHistory.Count - maxHistoryToRetain);
        }
    }
}

/// <summary>
/// Status telemetry produced by periodic heartbeat evaluations.
/// </summary>
public sealed record SparkHeartbeatReport
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public bool IsHealthy { get; init; } = true;
    public int ActiveTaskCount { get; init; }
    public int RunningTaskCount { get; init; }
    public long ConsecutiveHeartbeatTicks { get; init; }
    public double LastTickDurationMs { get; init; }
    public int StalledTasksRecovered { get; init; }
    public List<string> SystemAlerts { get; init; } = new();
}
