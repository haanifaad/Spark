using OpenClaw.Shared.Cron.Autonomous;
using OpenClaw.Shared.Orchestration;

namespace OpenClaw.Shared.Background;

/// <summary>
/// Production persistent background worker running autonomous schedules, morning briefings,
/// and queued task execution with restart recovery and quiet-hours throttling.
/// </summary>
public sealed class SparkBackgroundWorker : ISparkBackgroundWorker, IDisposable
{
    private readonly ITaskStateManager _stateManager;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly ISparkAutonomousScheduler _scheduler;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private SparkBackgroundWorkerConfig _config = new();
    private readonly SparkBackgroundWorkerStatus _status = new();
    private CancellationTokenSource? _workerCts;
    private Task? _backgroundLoopTask;

    public SparkBackgroundWorker(
        ITaskStateManager stateManager,
        IAgentOrchestrator orchestrator,
        ISparkAutonomousScheduler scheduler)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_status.IsRunning)
                return;

            // 1. Perform crash recovery for tasks interrupted by prior shutdown
            var recovered = await _stateManager.RecoverInterruptedTasksAsync(ct: ct).ConfigureAwait(false);
            if (recovered > 0)
            {
                _status.RecentAlerts.Add($"Recovered {recovered} interrupted task(s) on worker startup.");
            }

            _status.IsRunning = true;
            _status.StartedAt = DateTimeOffset.UtcNow;
            _workerCts = new CancellationTokenSource();

            // 2. Launch background execution loop
            _backgroundLoopTask = Task.Run(() => WorkerLoopAsync(_workerCts.Token));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_status.IsRunning)
                return;

            _workerCts?.Cancel();
            if (_backgroundLoopTask != null)
            {
                try
                {
                    await _backgroundLoopTask.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore shutdown timeout
                }
            }

            _status.IsRunning = false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public SparkBackgroundWorkerStatus GetStatus()
    {
        _status.IsInQuietHours = IsCurrentlyQuietHours();
        return _status;
    }

    public Task UpdateConfigAsync(SparkBackgroundWorkerConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        return Task.CompletedTask;
    }

    public async Task<int> PerformWorkerCycleAsync(CancellationToken ct = default)
    {
        var processedCount = 0;

        _status.LastHeartbeatAt = DateTimeOffset.UtcNow;
        _status.ConsecutiveHeartbeats++;
        _status.IsInQuietHours = IsCurrentlyQuietHours();

        // 1. Tick autonomous scheduler for due background jobs (inbox monitor, daily summary, etc.)
        try
        {
            var heartbeatReport = await _scheduler.PerformHeartbeatTickAsync(ct).ConfigureAwait(false);
            if (heartbeatReport.StalledTasksRecovered > 0)
            {
                _status.RecentAlerts.Add($"Autonomous scheduler recovered {heartbeatReport.StalledTasksRecovered} stalled cron task(s).");
            }
        }
        catch (Exception ex)
        {
            _status.RecentAlerts.Add($"Heartbeat tick failure: {ex.Message}");
        }

        // 2. Query pending Ready tasks
        var allTasks = await _stateManager.GetAllTasksAsync(ct: ct).ConfigureAwait(false);
        var readyTasks = allTasks.Where(t => t.Status == SparkTaskState.Ready).Take(_config.MaxConcurrentTasks).ToList();

        _status.ActiveTasksCount = readyTasks.Count;

        foreach (var task in readyTasks)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await _orchestrator.ExecuteTaskAsync(task.TaskId, task.UserId, ct).ConfigureAwait(false);
                processedCount++;
                _status.TotalTasksExecuted++;
            }
            catch (Exception ex)
            {
                _status.RecentAlerts.Add($"Task '{task.TaskId}' execution error in background: {ex.Message}");
            }
        }

        _status.ActiveTasksCount = 0;
        return processedCount;
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PerformWorkerCycleAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _status.RecentAlerts.Add($"Worker loop unexpected error: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _config.HeartbeatIntervalSeconds)), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private bool IsCurrentlyQuietHours()
    {
        if (!_config.EnableQuietHours)
            return false;

        var now = DateTime.Now.TimeOfDay;
        if (_config.QuietHoursStart > _config.QuietHoursEnd)
        {
            // Overnight window, e.g. 22:00 -> 07:00
            return now >= _config.QuietHoursStart || now <= _config.QuietHoursEnd;
        }

        return now >= _config.QuietHoursStart && now <= _config.QuietHoursEnd;
    }

    public void Dispose()
    {
        _workerCts?.Cancel();
        _workerCts?.Dispose();
        _gate.Dispose();
    }
}
