using OpenClaw.Shared.Cron.Autonomous;

namespace OpenClaw.Shared.Tests;

public sealed class SparkAutonomousSchedulerTests : IDisposable
{
    private readonly string _testStoragePath;

    public SparkAutonomousSchedulerTests()
    {
        _testStoragePath = Path.Combine(
            Path.GetTempPath(),
            $"spark_test_scheduler_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testStoragePath)) File.Delete(_testStoragePath);
            if (File.Exists(_testStoragePath + ".tmp")) File.Delete(_testStoragePath + ".tmp");
            if (File.Exists(_testStoragePath + ".bak")) File.Delete(_testStoragePath + ".bak");
        }
        catch { }
    }

    [Fact]
    public async Task RegisterAndGetTask_PersistsAndRetrievesTaskCorrectly()
    {
        using var scheduler = new SparkAutonomousScheduler(_testStoragePath);

        var task = new SparkAutonomousTask
        {
            Id = "task-1",
            Title = "Test Task 1",
            Category = SparkTaskCategory.InboxMonitor,
            Interval = TimeSpan.FromMinutes(10),
            Permissions = { SparkTaskPermission.NetworkAccess }
        };

        var registered = await scheduler.RegisterTaskAsync(task);
        Assert.NotNull(registered);
        Assert.Equal("task-1", registered.Id);
        Assert.True(registered.NextRunAt.HasValue);

        var fetched = await scheduler.GetTaskByIdAsync("task-1");
        Assert.NotNull(fetched);
        Assert.Equal("Test Task 1", fetched.Title);
        Assert.Contains(SparkTaskPermission.NetworkAccess, fetched.Permissions);

        var all = await scheduler.GetTasksAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task PauseAndResumeTask_UpdatesStateAndScheduling()
    {
        using var scheduler = new SparkAutonomousScheduler(_testStoragePath);

        var task = new SparkAutonomousTask
        {
            Id = "task-pause",
            Title = "Pausable Task",
            Interval = TimeSpan.FromHours(1)
        };

        await scheduler.RegisterTaskAsync(task);

        var paused = await scheduler.PauseTaskAsync("task-pause");
        Assert.True(paused);

        var taskPaused = await scheduler.GetTaskByIdAsync("task-pause");
        Assert.NotNull(taskPaused);
        Assert.False(taskPaused.Enabled);
        Assert.Equal(SparkTaskStatus.Paused, taskPaused.CurrentStatus);

        var resumed = await scheduler.ResumeTaskAsync("task-pause");
        Assert.True(resumed);

        var taskResumed = await scheduler.GetTaskByIdAsync("task-pause");
        Assert.NotNull(taskResumed);
        Assert.True(taskResumed.Enabled);
        Assert.Equal(SparkTaskStatus.Pending, taskResumed.CurrentStatus);
    }

    [Fact]
    public async Task TriggerTaskNowAsync_ExecutesTask_AndRecordsHistory()
    {
        using var scheduler = new SparkAutonomousScheduler(
            _testStoragePath,
            customTaskExecutor: (t, ct) => Task.FromResult($"Custom executed: {t.Title}"));

        var task = new SparkAutonomousTask
        {
            Id = "task-exec",
            Title = "Execution Check",
            Interval = TimeSpan.FromMinutes(30)
        };

        await scheduler.RegisterTaskAsync(task);

        var record = await scheduler.TriggerTaskNowAsync("task-exec");
        Assert.NotNull(record);
        Assert.Equal(SparkTaskStatus.Completed, record.Status);
        Assert.Equal("Custom executed: Execution Check", record.OutputSummary);
        Assert.True(record.Duration >= TimeSpan.Zero);

        var history = await scheduler.GetExecutionHistoryAsync("task-exec");
        Assert.Single(history);
        Assert.Equal("Custom executed: Execution Check", history[0].OutputSummary);

        var updated = await scheduler.GetTaskByIdAsync("task-exec");
        Assert.NotNull(updated);
        Assert.Equal(1, updated.RunCount);
        Assert.Equal(0, updated.ConsecutiveFailures);
    }

    [Fact]
    public void RetryPolicy_ExponentialBackoff_CalculatesExpectedDelays()
    {
        var policy = new SparkRetryPolicy
        {
            MaxRetries = 3,
            InitialInterval = TimeSpan.FromSeconds(2),
            BackoffMultiplier = 2.0,
            MaxInterval = TimeSpan.FromSeconds(30)
        };

        Assert.Equal(TimeSpan.Zero, policy.CalculateDelay(0));
        Assert.Equal(TimeSpan.FromSeconds(2), policy.CalculateDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.CalculateDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(8), policy.CalculateDelay(3));
        Assert.Equal(TimeSpan.FromSeconds(30), policy.CalculateDelay(4)); // Exceeds max retries -> capped at MaxInterval
    }

    [Fact]
    public async Task HeartbeatTick_ExecutesDueTasks_AndProducesReport()
    {
        var executionCount = 0;
        using var scheduler = new SparkAutonomousScheduler(
            _testStoragePath,
            customTaskExecutor: (t, ct) =>
            {
                Interlocked.Increment(ref executionCount);
                return Task.FromResult("Heartbeat executed");
            });

        var dueTask = new SparkAutonomousTask
        {
            Id = "task-due",
            Title = "Due Task",
            Interval = TimeSpan.FromMinutes(1),
            NextRunAt = DateTimeOffset.UtcNow.AddSeconds(-5) // Past due
        };

        await scheduler.RegisterTaskAsync(dueTask);

        var report = await scheduler.PerformHeartbeatTickAsync();
        Assert.NotNull(report);
        Assert.True(report.IsHealthy);
        Assert.Equal(1, report.ActiveTaskCount);
        Assert.True(report.ConsecutiveHeartbeatTicks >= 1);

        // Allow background execution to complete
        await Task.Delay(200);

        Assert.True(executionCount >= 1);
    }

    [Fact]
    public async Task HeartbeatTick_DetectsAndRecoversStalledTasks()
    {
        using var scheduler = new SparkAutonomousScheduler(_testStoragePath);

        var stalledTask = new SparkAutonomousTask
        {
            Id = "task-stalled",
            Title = "Stalled Task",
            Timeout = TimeSpan.FromMilliseconds(50),
            CurrentStatus = SparkTaskStatus.Running,
            LastRunAt = DateTimeOffset.UtcNow.AddMinutes(-5) // Started long ago
        };

        await scheduler.RegisterTaskAsync(stalledTask);

        var report = await scheduler.PerformHeartbeatTickAsync();
        Assert.NotNull(report);
        Assert.Equal(1, report.StalledTasksRecovered);
        Assert.False(report.IsHealthy);
        Assert.NotEmpty(report.SystemAlerts);

        var recovered = await scheduler.GetTaskByIdAsync("task-stalled");
        Assert.NotNull(recovered);
        Assert.Equal(SparkTaskStatus.Failed, recovered.CurrentStatus);
        Assert.Contains("stalled task terminated", recovered.LastError);
    }

    [Fact]
    public async Task TaskTimeout_EnforcesBoundedExecution()
    {
        using var scheduler = new SparkAutonomousScheduler(
            _testStoragePath,
            customTaskExecutor: async (t, ct) =>
            {
                // Deliberately sleep longer than task timeout
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return "Should not finish";
            });

        var task = new SparkAutonomousTask
        {
            Id = "task-timeout",
            Title = "Timeout Task",
            Timeout = TimeSpan.FromMilliseconds(100) // very short timeout
        };

        await scheduler.RegisterTaskAsync(task);

        var record = await scheduler.TriggerTaskNowAsync("task-timeout");
        Assert.NotNull(record);
        Assert.Equal(SparkTaskStatus.Failed, record.Status);
        Assert.Contains("timed out", record.ErrorMessage);

        var updated = await scheduler.GetTaskByIdAsync("task-timeout");
        Assert.NotNull(updated);
        Assert.Equal(1, updated.ConsecutiveFailures);
    }

    [Fact]
    public void PredefinedTemplates_ExposeValidSecurityPoliciesAndPermissions()
    {
        var inbox = SparkAutonomousScheduler.CreateInboxMonitorTemplate(TimeSpan.FromMinutes(15), "Work");
        Assert.Equal(SparkTaskCategory.InboxMonitor, inbox.Category);
        Assert.Contains(SparkTaskPermission.NetworkAccess, inbox.Permissions);

        var summary = SparkAutonomousScheduler.CreateDailySummaryTemplate();
        Assert.Equal(SparkTaskCategory.DailySummary, summary.Category);
        Assert.Contains(SparkTaskPermission.FilesystemRead, summary.Permissions);

        var health = SparkAutonomousScheduler.CreateProjectHealthMonitorTemplate("SPARK", TimeSpan.FromMinutes(30));
        Assert.Equal(SparkTaskCategory.ProjectHealthMonitor, health.Category);
        Assert.Contains(SparkTaskPermission.ProcessExecution, health.Permissions);

        var reminder = SparkAutonomousScheduler.CreateTaskReminderTemplate("Review PR", DateTimeOffset.UtcNow.AddHours(2));
        Assert.Equal(SparkTaskCategory.TaskReminder, reminder.Category);
        Assert.False(reminder.RequireApprovalForHighImpact);

        var research = SparkAutonomousScheduler.CreateScheduledResearchTemplate("Quantum AI", TimeSpan.FromDays(1));
        Assert.Equal(SparkTaskCategory.ScheduledResearch, research.Category);
        Assert.Contains(SparkTaskPermission.McpToolAccess, research.Permissions);
    }
}
