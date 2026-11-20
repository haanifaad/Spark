namespace OpenClaw.Shared.Cron.Autonomous;

/// <summary>
/// Contract for the unified SPARK Autonomous Scheduler and Heartbeat Monitor.
/// Extends existing cron infrastructure with bounded autonomous task execution,
/// proactive health ticks, exponential backoff retries, and comprehensive audit history.
/// </summary>
public interface ISparkAutonomousScheduler : IDisposable
{
    /// <summary>
    /// Gets whether the autonomous background heartbeat loop is currently running.
    /// </summary>
    bool IsHeartbeatRunning { get; }

    /// <summary>
    /// Starts the autonomous heartbeat loop.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the autonomous heartbeat loop.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers or updates an autonomous task in the persistent schedule.
    /// </summary>
    Task<SparkAutonomousTask> RegisterTaskAsync(SparkAutonomousTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a task from the schedule.
    /// </summary>
    Task<bool> UnregisterTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an active task.
    /// </summary>
    Task<bool> PauseTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused task.
    /// </summary>
    Task<bool> ResumeTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers an immediate execution of a scheduled task.
    /// </summary>
    Task<SparkTaskExecutionRecord> TriggerTaskNowAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves filtered scheduled tasks.
    /// </summary>
    Task<IReadOnlyList<SparkAutonomousTask>> GetTasksAsync(
        SparkTaskCategory? category = null,
        SparkTaskStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific task by its unique ID.
    /// </summary>
    Task<SparkAutonomousTask?> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves execution run history for a specific task.
    /// </summary>
    Task<IReadOnlyList<SparkTaskExecutionRecord>> GetExecutionHistoryAsync(
        string taskId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs an explicit heartbeat evaluation tick (scans for due tasks, detects stalls, updates health report).
    /// </summary>
    Task<SparkHeartbeatReport> PerformHeartbeatTickAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised after each heartbeat tick finishes.
    /// </summary>
    event EventHandler<SparkHeartbeatReport>? HeartbeatTickCompleted;

    /// <summary>
    /// Event raised when a task run finishes execution.
    /// </summary>
    event EventHandler<SparkTaskExecutionRecord>? TaskExecutionCompleted;
}
