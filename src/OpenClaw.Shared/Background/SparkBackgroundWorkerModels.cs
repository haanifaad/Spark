namespace OpenClaw.Shared.Background;

/// <summary>
/// Operational configuration for the unattended background autonomous worker.
/// </summary>
public sealed class SparkBackgroundWorkerConfig
{
    public int HeartbeatIntervalSeconds { get; set; } = 60;
    public int MaxConcurrentTasks { get; set; } = 4;
    public bool EnableQuietHours { get; set; } = true;
    public TimeSpan QuietHoursStart { get; set; } = new(22, 0, 0); // 10:00 PM
    public TimeSpan QuietHoursEnd { get; set; } = new(7, 0, 0);   // 07:00 AM
    public bool EnableMorningBriefing { get; set; } = true;
    public TimeSpan MorningBriefingTime { get; set; } = new(8, 0, 0); // 08:00 AM
    public bool EnableInboxMonitoring { get; set; } = true;
    public int InboxMonitoringIntervalMinutes { get; set; } = 15;
}

/// <summary>
/// Live telemetry state of the autonomous background worker.
/// </summary>
public sealed class SparkBackgroundWorkerStatus
{
    public bool IsRunning { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public int ConsecutiveHeartbeats { get; set; }
    public int ActiveTasksCount { get; set; }
    public int TotalTasksExecuted { get; set; }
    public bool IsInQuietHours { get; set; }
    public List<string> RecentAlerts { get; set; } = [];
}
