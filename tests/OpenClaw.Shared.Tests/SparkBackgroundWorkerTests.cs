using OpenClaw.Shared.Background;
using OpenClaw.Shared.Cron.Autonomous;
using OpenClaw.Shared.Orchestration;
using OpenClaw.Shared.Security.Approvals;
using OpenClaw.Shared.Security.Sentinel;
using OpenClaw.Shared.Tools;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkBackgroundWorkerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SparkTaskStateManager _stateManager;
    private readonly SparkTaskPlanner _planner;
    private readonly SparkToolRegistry _toolRegistry;
    private readonly SparkApprovalManager _approvalManager;
    private readonly SentinelReviewer _sentinel;
    private readonly SparkAgentOrchestrator _orchestrator;
    private readonly SparkAutonomousScheduler _scheduler;
    private readonly SparkBackgroundWorker _worker;

    public SparkBackgroundWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SparkBgWorkerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _stateManager = new SparkTaskStateManager(Path.Combine(_tempDir, "Tasks"));
        _toolRegistry = new SparkToolRegistry();
        _planner = new SparkTaskPlanner(new SparkTaskDecomposer(), _toolRegistry);
        _approvalManager = new SparkApprovalManager(Path.Combine(_tempDir, "Approvals"));
        _sentinel = new SentinelReviewer();
        _orchestrator = new SparkAgentOrchestrator(_stateManager, _planner, _toolRegistry, _approvalManager, _sentinel);
        _scheduler = new SparkAutonomousScheduler(Path.Combine(_tempDir, "Cron"));
        _worker = new SparkBackgroundWorker(_stateManager, _orchestrator, _scheduler);
    }

    public void Dispose()
    {
        _worker.Dispose();
        _scheduler.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task BackgroundWorker_StartAndStop_LifecycleWorks()
    {
        Assert.False(_worker.GetStatus().IsRunning);

        await _worker.StartAsync();
        var status = _worker.GetStatus();
        Assert.True(status.IsRunning);
        Assert.NotNull(status.StartedAt);

        await _worker.StopAsync();
        Assert.False(_worker.GetStatus().IsRunning);
    }

    [Fact]
    public async Task BackgroundWorker_Startup_RecoversInterruptedTasks()
    {
        // Simulate a task that was left running during crash
        var task = await _stateManager.CreateTaskAsync("Interrupted background task");
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Interpreting);
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Planning);
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Ready);
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Executing);

        // Starting worker automatically runs recovery
        await _worker.StartAsync();
        await _worker.StopAsync();

        var refreshed = await _stateManager.GetTaskAsync(task.TaskId);
        Assert.Equal(SparkTaskState.RequiresUserIntervention, refreshed!.Status);

        var status = _worker.GetStatus();
        Assert.Contains(status.RecentAlerts, a => a.Contains("Recovered"));
    }

    [Fact]
    public async Task BackgroundWorker_PerformWorkerCycle_ExecutesReadyTasks()
    {
        // Register a safe test tool
        _toolRegistry.RegisterTool(new TestMockTool("notes_create", "Creates notes"));

        var task = await _stateManager.CreateTaskAsync("Write a report");
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Interpreting);
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Planning);

        // Formulate plan
        await _planner.CreatePlanAsync(task);
        await _stateManager.SaveTaskAsync(task);
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Ready);

        // Perform one worker cycle
        var processed = await _worker.PerformWorkerCycleAsync();
        Assert.Equal(1, processed);

        var executedTask = await _stateManager.GetTaskAsync(task.TaskId);
        Assert.Equal(SparkTaskState.Completed, executedTask!.Status);

        var status = _worker.GetStatus();
        Assert.Equal(1, status.TotalTasksExecuted);
        Assert.True(status.ConsecutiveHeartbeats > 0);
    }

    [Fact]
    public async Task BackgroundWorker_Config_QuietHoursCalculation()
    {
        var config = new SparkBackgroundWorkerConfig
        {
            EnableQuietHours = true,
            QuietHoursStart = new TimeSpan(0, 0, 0), // 12:00 AM
            QuietHoursEnd = new TimeSpan(23, 59, 59)  // 11:59 PM (always quiet)
        };

        await _worker.UpdateConfigAsync(config);
        var status = _worker.GetStatus();
        Assert.True(status.IsInQuietHours);
    }

    private sealed class TestMockTool : ISparkTool
    {
        public SparkToolMetadata Metadata { get; }

        public TestMockTool(string id, string name)
        {
            Metadata = new SparkToolMetadata
            {
                Id = id,
                Name = name,
                Category = SparkToolCategory.ExternalConnector,
                RiskLevel = SparkToolRiskLevel.Low,
                RequiredPermissions = []
            };
        }

        public Task<SparkToolExecutionResponse> ExecuteAsync(SparkToolExecutionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SparkToolExecutionResponse
            {
                AuditId = "audit-bg-1",
                Success = true,
                Result = "Note created in background",
                ExecutedAt = DateTimeOffset.UtcNow
            });
        }
    }
}
