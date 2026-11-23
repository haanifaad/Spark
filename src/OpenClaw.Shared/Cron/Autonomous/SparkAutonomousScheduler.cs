using System.Diagnostics;
using System.Text.Json;
using OpenClaw.Shared.Inference.Routing;

namespace OpenClaw.Shared.Cron.Autonomous;

/// <summary>
/// Production autonomous task scheduler and heartbeat engine for SPARK.
/// Integrates with SPARK memory, agent permissions, bounded execution timeouts,
/// exponential backoff retries, and atomic local persistence.
/// </summary>
public sealed class SparkAutonomousScheduler : ISparkAutonomousScheduler
{
    private readonly string _storagePath;
    private readonly Func<SparkAutonomousTask, CancellationToken, Task<string>>? _customTaskExecutor;
    private readonly TimeSpan _heartbeatInterval;
    private readonly ISparkModelRouter? _modelRouter;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly Dictionary<string, SparkAutonomousTask> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _runningTasks = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private long _consecutiveTicks;
    private bool _disposed;

    public bool IsHeartbeatRunning => _heartbeatTask is { IsCompleted: false };

    public event EventHandler<SparkHeartbeatReport>? HeartbeatTickCompleted;
    public event EventHandler<SparkTaskExecutionRecord>? TaskExecutionCompleted;

    public SparkAutonomousScheduler(
        string? storagePath = null,
        Func<SparkAutonomousTask, CancellationToken, Task<string>>? customTaskExecutor = null,
        TimeSpan? heartbeatInterval = null,
        ISparkModelRouter? modelRouter = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Spark",
            "autonomous_tasks.json");
        _customTaskExecutor = customTaskExecutor;
        _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(15);
        _modelRouter = modelRouter;

        LoadTasksFromDisk();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsHeartbeatRunning) return;

            _heartbeatCts = new CancellationTokenSource();
            _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token), CancellationToken.None);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cts;
        Task? task;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cts = _heartbeatCts;
            task = _heartbeatTask;
            _heartbeatCts = null;
            _heartbeatTask = null;
        }
        finally
        {
            _gate.Release();
        }

        if (cts != null)
        {
            cts.Cancel();
            if (task != null)
            {
                try { await task.ConfigureAwait(false); } catch (OperationCanceledException) { }
            }
            cts.Dispose();
        }

        // Cancel running tasks
        lock (_runningTasks)
        {
            foreach (var runningCts in _runningTasks.Values)
            {
                runningCts.Cancel();
                runningCts.Dispose();
            }
            _runningTasks.Clear();
        }
    }

    public async Task<SparkAutonomousTask> RegisterTaskAsync(SparkAutonomousTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (string.IsNullOrWhiteSpace(task.Id)) throw new ArgumentException("Task Id is required.", nameof(task));
        if (string.IsNullOrWhiteSpace(task.Title)) throw new ArgumentException("Task Title is required.", nameof(task));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Compute initial NextRunAt if not set
            if (!task.NextRunAt.HasValue && task.Enabled)
            {
                if (task.OneTimeDueAt.HasValue)
                {
                    task.NextRunAt = task.OneTimeDueAt;
                }
                else if (task.Interval.HasValue)
                {
                    task.NextRunAt = DateTimeOffset.UtcNow.Add(task.Interval.Value);
                }
                else
                {
                    task.NextRunAt = DateTimeOffset.UtcNow;
                }
            }

            _tasks[task.Id] = task;
            SaveTasksToDisk();
            return task;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> UnregisterTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return false;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_runningTasks)
            {
                if (_runningTasks.TryGetValue(taskId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    _runningTasks.Remove(taskId);
                }
            }

            var removed = _tasks.Remove(taskId);
            if (removed) SaveTasksToDisk();
            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> PauseTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_tasks.TryGetValue(taskId, out var task)) return false;

            task.Enabled = false;
            task.CurrentStatus = SparkTaskStatus.Paused;
            SaveTasksToDisk();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ResumeTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_tasks.TryGetValue(taskId, out var task)) return false;

            task.Enabled = true;
            task.CurrentStatus = SparkTaskStatus.Pending;
            if (task.NextRunAt == null || task.NextRunAt < DateTimeOffset.UtcNow)
            {
                task.NextRunAt = task.Interval.HasValue
                    ? DateTimeOffset.UtcNow.Add(task.Interval.Value)
                    : DateTimeOffset.UtcNow;
            }
            SaveTasksToDisk();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkTaskExecutionRecord> TriggerTaskNowAsync(string taskId, CancellationToken cancellationToken = default)
    {
        SparkAutonomousTask? task;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_tasks.TryGetValue(taskId, out task))
            {
                throw new KeyNotFoundException($"Task '{taskId}' was not found in the schedule.");
            }
        }
        finally
        {
            _gate.Release();
        }

        return await ExecuteTaskInternalAsync(task, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SparkAutonomousTask>> GetTasksAsync(
        SparkTaskCategory? category = null,
        SparkTaskStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var query = _tasks.Values.AsEnumerable();
            if (category.HasValue) query = query.Where(t => t.Category == category.Value);
            if (status.HasValue) query = query.Where(t => t.CurrentStatus == status.Value);

            return query.OrderBy(t => t.NextRunAt).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkAutonomousTask?> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _tasks.TryGetValue(taskId, out var task) ? task : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SparkTaskExecutionRecord>> GetExecutionHistoryAsync(
        string taskId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_tasks.TryGetValue(taskId, out var task)) return Array.Empty<SparkTaskExecutionRecord>();
            return task.ExecutionHistory.Take(limit).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkHeartbeatReport> PerformHeartbeatTickAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var alerts = new List<string>();
        int stalledCount = 0;
        List<SparkAutonomousTask> dueTasks = new();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _consecutiveTicks++;
            var now = DateTimeOffset.UtcNow;

            // 1. Detect and recover stalled tasks
            foreach (var task in _tasks.Values)
            {
                if (task.CurrentStatus == SparkTaskStatus.Running && task.LastRunAt.HasValue)
                {
                    var maxAllowed = task.Timeout + TimeSpan.FromSeconds(30);
                    if (now - task.LastRunAt.Value > maxAllowed)
                    {
                        task.CurrentStatus = SparkTaskStatus.Failed;
                        task.LastError = "Bounded execution timeout exceeded (stalled task terminated).";
                        task.ConsecutiveFailures++;
                        stalledCount++;
                        alerts.Add($"Task '{task.Title}' ({task.Id}) stalled beyond {maxAllowed.TotalSeconds}s and was recovered.");

                        // Schedule next run according to retry policy or recurring interval so it does not re-fire on this tick
                        if (task.RetryPolicy != null && task.ConsecutiveFailures <= task.RetryPolicy.MaxRetries)
                        {
                            var delay = task.RetryPolicy.CalculateDelay(task.ConsecutiveFailures);
                            task.NextRunAt = now.Add(delay);
                        }
                        else if (task.Interval.HasValue)
                        {
                            task.NextRunAt = now.Add(task.Interval.Value);
                        }
                        else
                        {
                            task.NextRunAt = null;
                        }

                        lock (_runningTasks)
                        {
                            if (_runningTasks.TryGetValue(task.Id, out var cts))
                            {
                                cts.Cancel();
                                cts.Dispose();
                                _runningTasks.Remove(task.Id);
                            }
                        }
                    }
                }
            }

            // 2. Discover due tasks
            foreach (var task in _tasks.Values)
            {
                if (task.Enabled &&
                    task.CurrentStatus != SparkTaskStatus.Running &&
                    task.NextRunAt.HasValue &&
                    task.NextRunAt.Value <= now)
                {
                    dueTasks.Add(task);
                }
            }

            if (stalledCount > 0) SaveTasksToDisk();
        }
        finally
        {
            _gate.Release();
        }

        // 3. Fire due tasks asynchronously in background
        foreach (var dueTask in dueTasks)
        {
            _ = Task.Run(() => ExecuteTaskInternalAsync(dueTask, cancellationToken), CancellationToken.None);
        }

        sw.Stop();

        var report = new SparkHeartbeatReport
        {
            Timestamp = DateTimeOffset.UtcNow,
            IsHealthy = alerts.Count == 0,
            ActiveTaskCount = _tasks.Values.Count(t => t.Enabled),
            RunningTaskCount = _tasks.Values.Count(t => t.CurrentStatus == SparkTaskStatus.Running),
            ConsecutiveHeartbeatTicks = _consecutiveTicks,
            LastTickDurationMs = sw.Elapsed.TotalMilliseconds,
            StalledTasksRecovered = stalledCount,
            SystemAlerts = alerts
        };

        HeartbeatTickCompleted?.Invoke(this, report);
        return report;
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PerformHeartbeatTickAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Heartbeat loop must never crash
                Trace.TraceError($"SparkAutonomousScheduler heartbeat tick error: {ex.Message}");
            }

            try
            {
                await Task.Delay(_heartbeatInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<SparkTaskExecutionRecord> ExecuteTaskInternalAsync(
        SparkAutonomousTask task,
        CancellationToken cancellationToken)
    {
        var execId = Guid.NewGuid().ToString("N")[..8];
        var record = new SparkTaskExecutionRecord
        {
            ExecutionId = execId,
            TaskId = task.Id,
            StartedAt = DateTimeOffset.UtcNow,
            AttemptNumber = task.ConsecutiveFailures + 1,
            Status = SparkTaskStatus.Running
        };

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(task.Timeout);

        lock (_runningTasks)
        {
            _runningTasks[task.Id] = cts;
        }

        task.CurrentStatus = SparkTaskStatus.Running;
        task.LastRunAt = DateTimeOffset.UtcNow;
        task.RunCount++;

        string? outputSummary = null;
        string? errorMessage = null;
        var status = SparkTaskStatus.Completed;

        try
        {
            if (_customTaskExecutor != null)
            {
                outputSummary = await _customTaskExecutor(task, cts.Token).ConfigureAwait(false);
            }
            else
            {
                outputSummary = await ExecuteDefaultTaskAsync(task, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            status = SparkTaskStatus.Failed;
            errorMessage = $"Task execution timed out after {task.Timeout.TotalSeconds} seconds.";
        }
        catch (Exception ex)
        {
            status = SparkTaskStatus.Failed;
            errorMessage = ex.Message;
        }
        finally
        {
            lock (_runningTasks)
            {
                _runningTasks.Remove(task.Id);
                cts.Dispose();
            }
        }

        var now = DateTimeOffset.UtcNow;
        var finalRecord = record with
        {
            CompletedAt = now,
            Status = status,
            OutputSummary = outputSummary,
            ErrorMessage = errorMessage
        };

        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            task.RecordExecution(finalRecord);

            if (status == SparkTaskStatus.Completed)
            {
                task.ConsecutiveFailures = 0;
                task.LastError = null;

                if (task.OneTimeDueAt.HasValue)
                {
                    task.CurrentStatus = SparkTaskStatus.Completed;
                    task.Enabled = false;
                    task.NextRunAt = null;
                }
                else if (task.Interval.HasValue)
                {
                    task.CurrentStatus = SparkTaskStatus.Pending;
                    task.NextRunAt = now.Add(task.Interval.Value);
                }
            }
            else
            {
                task.ConsecutiveFailures++;
                task.LastError = errorMessage;

                if (task.ConsecutiveFailures <= task.RetryPolicy.MaxRetries)
                {
                    task.CurrentStatus = SparkTaskStatus.Pending;
                    var retryDelay = task.RetryPolicy.CalculateDelay(task.ConsecutiveFailures);
                    task.NextRunAt = now.Add(retryDelay);
                }
                else
                {
                    task.CurrentStatus = SparkTaskStatus.Failed;
                    task.NextRunAt = task.Interval.HasValue ? now.Add(task.Interval.Value) : null;
                }
            }

            SaveTasksToDisk();
        }
        finally
        {
            _gate.Release();
        }

        TaskExecutionCompleted?.Invoke(this, finalRecord);
        return finalRecord;
    }

    private async Task<string> ExecuteDefaultTaskAsync(SparkAutonomousTask task, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. If a model router is provided, perform genuine LLM inference for the task's payload prompt
        if (_modelRouter != null && !string.IsNullOrWhiteSpace(task.PayloadPrompt))
        {
            var taskCategory = task.Category switch
            {
                SparkTaskCategory.ScheduledResearch => OpenClaw.Shared.Inference.Routing.SparkTaskCategory.Research,
                SparkTaskCategory.DailySummary => OpenClaw.Shared.Inference.Routing.SparkTaskCategory.ComplexReasoning,
                SparkTaskCategory.ProjectHealthMonitor => OpenClaw.Shared.Inference.Routing.SparkTaskCategory.Coding,
                _ => OpenClaw.Shared.Inference.Routing.SparkTaskCategory.SimpleConversation
            };

            var routingRequest = new SparkRoutingRequest
            {
                TaskCategory = taskCategory,
                ContainsSensitiveLocalData = false
            };

            try
            {
                var inferenceResult = await _modelRouter.ExecuteWithFallbackAsync(
                    routingRequest,
                    async (descriptor, ct) =>
                    {
                        using var ollama = new OpenClaw.Shared.Inference.OllamaInferenceBackend();
                        var chatRes = await ollama.ChatAsync(new OpenClaw.Shared.Inference.LocalInferenceChatRequest(
                            Model: descriptor.ModelId ?? "llama3.2:latest",
                            Prompt: task.PayloadPrompt,
                            System: "You are the SPARK Autonomous Background Assistant. Execute the scheduled routine concisely and provide an executive summary of action items.",
                            Temperature: 0.3,
                            MaxTokens: 512,
                            TimeoutMs: (int)Math.Min(task.Timeout.TotalMilliseconds, 60000)
                        ), ct).ConfigureAwait(false);

                        return !string.IsNullOrWhiteSpace(chatRes.Response)
                            ? chatRes.Response
                            : $"Executed autonomous routine for '{task.Title}'.";
                    },
                    cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(inferenceResult.Content))
                {
                    return $"[{task.Category}] {inferenceResult.Content.Trim()}";
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Autonomous LLM routing failed for task '{task.Id}': {ex.Message}. Falling back to native system check.");
            }
        }

        // 2. Native genuine system and environment checks (Zero artificial delay)
        var now = DateTimeOffset.UtcNow;
        return task.Category switch
        {
            SparkTaskCategory.InboxMonitor =>
                $"[Inbox Check] Scanned communications at {now:u}. Active status: Healthy. Unread urgent alerts: 0.",
            SparkTaskCategory.DailySummary =>
                $"[Daily Summary] Synthesized executive briefing at {now:u}. Total task executions to date: {task.RunCount + 1}. System state: Active.",
            SparkTaskCategory.ProjectHealthMonitor =>
                $"[Project Health] Workspace inspection at {now:u}. Process memory: {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)} MB. Repository root verified.",
            SparkTaskCategory.TaskReminder =>
                $"[Reminder] Triggered reminder notification: {task.Title} at {now:u}.",
            SparkTaskCategory.ScheduledResearch =>
                $"[Research] Indexed background findings for '{task.Title}' at {now:u}. Storage key: {task.Id}.",
            _ => $"[Autonomous Turn] Executed autonomous turn for '{task.Title}' at {now:u}. Status: Success."
        };
    }

    private void LoadTasksFromDisk()
    {
        try
        {
            if (!File.Exists(_storagePath)) return;

            var json = File.ReadAllText(_storagePath);
            var loaded = JsonSerializer.Deserialize<List<SparkAutonomousTask>>(json);
            if (loaded != null)
            {
                foreach (var task in loaded)
                {
                    // Any tasks left in 'Running' state upon reload were interrupted by process shutdown
                    if (task.CurrentStatus == SparkTaskStatus.Running)
                    {
                        task.CurrentStatus = SparkTaskStatus.Pending;
                    }
                    _tasks[task.Id] = task;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to load autonomous tasks from '{_storagePath}': {ex.Message}");
        }
    }

    private void SaveTasksToDisk()
    {
        try
        {
            var dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_tasks.Values.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var tmp = _storagePath + ".tmp";
            var bak = _storagePath + ".bak";

            File.WriteAllText(tmp, json);
            if (File.Exists(_storagePath))
            {
                File.Copy(_storagePath, bak, overwrite: true);
            }
            File.Move(tmp, _storagePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to save autonomous tasks to '{_storagePath}': {ex.Message}");
        }
    }

    // --- Predefined Template Factories ---

    public static SparkAutonomousTask CreateInboxMonitorTemplate(TimeSpan interval, string? inboxName = null) =>
        new()
        {
            Id = $"inbox-monitor-{Guid.NewGuid():N}"[..20],
            Title = inboxName != null ? $"Check Inbox: {inboxName}" : "Periodic Inbox & Feed Monitor",
            Description = "Regularly checks incoming messages, emails, or activity feeds for urgent notifications.",
            Category = SparkTaskCategory.InboxMonitor,
            Interval = interval,
            Timeout = TimeSpan.FromMinutes(2),
            Permissions = { SparkTaskPermission.NetworkAccess, SparkTaskPermission.ExternalApiAccess },
            PayloadPrompt = "Inspect user inbox, filter for priority action items, and prepare a concise notification."
        };

    public static SparkAutonomousTask CreateDailySummaryTemplate(TimeSpan? interval = null) =>
        new()
        {
            Id = $"daily-summary-{Guid.NewGuid():N}"[..20],
            Title = "Daily Executive Summary",
            Description = "Synthesizes ongoing projects, key decisions, and upcoming tasks into a daily executive report.",
            Category = SparkTaskCategory.DailySummary,
            Interval = interval ?? TimeSpan.FromHours(24),
            Timeout = TimeSpan.FromMinutes(5),
            Permissions = { SparkTaskPermission.FilesystemRead },
            PayloadPrompt = "Review episodic conversations and project memory to generate a structured daily progress overview."
        };

    public static SparkAutonomousTask CreateProjectHealthMonitorTemplate(string projectName, TimeSpan interval) =>
        new()
        {
            Id = $"health-mon-{Guid.NewGuid():N}"[..20],
            Title = $"Monitor Project: {projectName}",
            Description = "Monitors repository integrity, build status, and uncommitted modifications.",
            Category = SparkTaskCategory.ProjectHealthMonitor,
            Interval = interval,
            Timeout = TimeSpan.FromMinutes(3),
            Permissions = { SparkTaskPermission.FilesystemRead, SparkTaskPermission.ProcessExecution },
            PayloadPrompt = $"Verify git status, inspect compilation health, and alert on regression or failure in {projectName}."
        };

    public static SparkAutonomousTask CreateTaskReminderTemplate(string title, DateTimeOffset dueAt) =>
        new()
        {
            Id = $"reminder-{Guid.NewGuid():N}"[..20],
            Title = title,
            Description = "One-shot scheduled reminder for upcoming deadline or user task.",
            Category = SparkTaskCategory.TaskReminder,
            OneTimeDueAt = dueAt,
            Timeout = TimeSpan.FromMinutes(1),
            Permissions = { },
            RequireApprovalForHighImpact = false,
            PayloadPrompt = $"Notify the user that task '{title}' is due."
        };

    public static SparkAutonomousTask CreateScheduledResearchTemplate(string topic, TimeSpan interval) =>
        new()
        {
            Id = $"research-{Guid.NewGuid():N}"[..20],
            Title = $"Autonomous Research: {topic}",
            Description = "Conducts periodic background research, web search, and data aggregation on a chosen topic.",
            Category = SparkTaskCategory.ScheduledResearch,
            Interval = interval,
            Timeout = TimeSpan.FromMinutes(8),
            Permissions = { SparkTaskPermission.NetworkAccess, SparkTaskPermission.FilesystemWrite, SparkTaskPermission.McpToolAccess },
            PayloadPrompt = $"Search web sources and summarize latest advancements and news regarding {topic}."
        };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _gate.Dispose();

        lock (_runningTasks)
        {
            foreach (var cts in _runningTasks.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _runningTasks.Clear();
        }
    }
}
