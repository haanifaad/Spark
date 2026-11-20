using OpenClaw.Shared.Memory;
using OpenClaw.Shared.Orchestration;
using OpenClaw.Shared.Security.Approvals;
using OpenClaw.Shared.Tools;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkAutonomousAgentOrchestratorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SparkTaskStateManager _stateManager;
    private readonly SparkTaskDecomposer _decomposer;
    private readonly SparkToolRegistry _toolRegistry;
    private readonly SparkApprovalManager _approvalManager;
    private readonly SparkTaskPlanner _planner;
    private readonly SparkAgentOrchestrator _orchestrator;

    public SparkAutonomousAgentOrchestratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SparkOrchestratorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _stateManager = new SparkTaskStateManager(Path.Combine(_tempDir, "Tasks"));
        _decomposer = new SparkTaskDecomposer();
        _toolRegistry = new SparkToolRegistry();
        _approvalManager = new SparkApprovalManager(Path.Combine(_tempDir, "Approvals"));
        _planner = new SparkTaskPlanner(_decomposer, _toolRegistry);
        _orchestrator = new SparkAgentOrchestrator(_stateManager, _planner, _toolRegistry, _approvalManager);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Ignore temp cleanup errors
        }
    }

    [Fact]
    public async Task StateManager_EnforcesStrict14StateTransitions_AndPersists()
    {
        var task = await _stateManager.CreateTaskAsync("Generate quarterly report");
        Assert.Equal(SparkTaskState.Received, task.Status);

        // Valid transitions: Received -> Interpreting -> Planning -> Ready
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Interpreting);
        Assert.Equal(SparkTaskState.Interpreting, task.Status);

        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Planning);
        Assert.Equal(SparkTaskState.Planning, task.Status);

        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Ready);
        Assert.Equal(SparkTaskState.Ready, task.Status);

        // Invalid jump: Ready -> Completed directly without Executing/Reviewing/Verifying should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Completed));

        // Valid path to completion: Ready -> Executing -> Reviewing -> Verifying -> Completed
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Executing);
        Assert.NotNull(task.StartedAt);

        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Reviewing);
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Verifying);
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Completed, "All requirements met.");
        Assert.Equal(SparkTaskState.Completed, task.Status);
        Assert.NotNull(task.CompletedAt);
    }

    [Fact]
    public async Task Decomposer_ParsesComplexMalayalamAndEnglishInstructions()
    {
        // Malayalam instruction with 3 distinct sub-intents:
        // 1. Check emails and prepare reply (email_draft)
        // 2. Find good train options to Kochi tomorrow (travel_search_trains)
        // 3. Prepare evening report (notes_create)
        var malayalamPrompt = "എന്റെ പ്രധാനപ്പെട്ട ഇമെയിലുകൾ പരിശോധിച്ച് മറുപടി തയ്യാറാക്കൂ. നാളെ കൊച്ചിയിലേക്ക് പോകാൻ നല്ല ട്രെയിൻ ഓപ്ഷനുകൾ കണ്ടെത്തൂ. വൈകുന്നേരം എനിക്ക് ഒരു റിപ്പോർട്ടും തയ്യാറാക്കണം.";

        var steps = await _decomposer.DecomposeInstructionAsync("task-1", malayalamPrompt, "Malayalam");

        Assert.True(steps.Count >= 3, $"Expected at least 3 decomposed steps, got {steps.Count}");
        Assert.Contains(steps, s => s.ToolName == "email_draft" || s.ToolName == "email_search");
        Assert.Contains(steps, s => s.ToolName == "travel_search_trains" || s.ToolName.StartsWith("travel_"));
        Assert.Contains(steps, s => s.ToolName == "notes_create");
    }

    [Fact]
    public async Task Planner_RevisesPlanOnFly_WhenInstructionIsModified()
    {
        var task = await _stateManager.CreateTaskAsync("Search for cheap flights to Delhi");
        var initialPlan = await _planner.CreatePlanAsync(task);

        Assert.Single(task.Steps);
        Assert.Equal("travel_search_flights", task.Steps[0].ToolName);

        // Modify instruction on the fly
        var modifiedTask = await _stateManager.ModifyTaskInstructionAsync(
            task.TaskId, 
            "Search for flights to Delhi and then send an email report to my manager", 
            "Scope expanded by user");

        Assert.Single(modifiedTask.ModificationHistory);
        Assert.Equal(SparkTaskState.Planning, modifiedTask.Status);

        var replan = await _planner.ReplanAsync(modifiedTask);
        Assert.True(replan.PlannedSteps.Count >= 2);
        Assert.Contains(replan.PlannedSteps, s => s.ToolName == "email_send");
        Assert.Equal(SparkToolRiskLevel.High, replan.HighestRiskLevel);
    }

    [Fact]
    public async Task Orchestrator_SubmitAndExecute_EndToEndFlow()
    {
        // Register a safe test tool
        _toolRegistry.RegisterTool(new TestMockTool("notes_create", "Creates a markdown note"));

        var task = await _orchestrator.SubmitTaskAsync("Write a summary report for today");

        // Should be planned and transition to Ready or Completed
        Assert.NotNull(task);
        Assert.True(task.Steps.Count > 0);

        // Execute explicitly to verify state progression
        var executed = await _orchestrator.ExecuteTaskAsync(task.TaskId);

        Assert.Equal(SparkTaskState.Completed, executed.Status);
        Assert.All(executed.Steps, s => Assert.Equal(SparkStepState.Completed, s.Status));
        Assert.All(executed.Steps, s => Assert.True(s.VerificationPassed));
    }

    [Fact]
    public async Task Orchestrator_UserTakeover_SafelyPausesExecution()
    {
        var task = await _stateManager.CreateTaskAsync("Complex interactive web automation");
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Interpreting);
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Planning);
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Ready);

        // User initiates takeover
        var takeoverTask = await _orchestrator.SetUserTakeoverAsync(task.TaskId, isTakeoverActive: true);

        Assert.True(takeoverTask.IsUserTakeoverActive);
        Assert.Equal(SparkTaskState.Paused, takeoverTask.Status);
        Assert.Equal("True", takeoverTask.Metadata["UserTakeoverActive"]);

        // User releases takeover
        var resumedTask = await _orchestrator.SetUserTakeoverAsync(task.TaskId, isTakeoverActive: false);

        Assert.False(resumedTask.IsUserTakeoverActive);
        Assert.Equal(SparkTaskState.Ready, resumedTask.Status);
    }

    [Fact]
    public async Task StateManager_RecoverInterruptedTasks_MarksExecutingTasksForIntervention()
    {
        var task1 = await _stateManager.CreateTaskAsync("Task in flight during power loss");
        task1 = await _stateManager.UpdateTaskStateAsync(task1.TaskId, SparkTaskState.Interpreting);
        task1 = await _stateManager.UpdateTaskStateAsync(task1.TaskId, SparkTaskState.Planning);
        task1 = await _stateManager.UpdateTaskStateAsync(task1.TaskId, SparkTaskState.Ready);
        task1 = await _stateManager.UpdateTaskStateAsync(task1.TaskId, SparkTaskState.Executing);

        var task2 = await _stateManager.CreateTaskAsync("Already completed task");
        task2 = await _stateManager.UpdateTaskStateAsync(task2.TaskId, SparkTaskState.Interpreting);
        task2 = await _stateManager.UpdateTaskStateAsync(task2.TaskId, SparkTaskState.Planning);
        task2 = await _stateManager.UpdateTaskStateAsync(task2.TaskId, SparkTaskState.Ready);
        task2 = await _stateManager.UpdateTaskStateAsync(task2.TaskId, SparkTaskState.Executing);
        task2 = await _stateManager.UpdateTaskStateAsync(task2.TaskId, SparkTaskState.Reviewing);
        task2 = await _stateManager.UpdateTaskStateAsync(task2.TaskId, SparkTaskState.Verifying);
        task2 = await _stateManager.UpdateTaskStateAsync(task2.TaskId, SparkTaskState.Completed, "Done");

        // Simulate application restart
        var recovered = await _stateManager.RecoverInterruptedTasksAsync();

        Assert.Equal(1, recovered);

        var refreshedTask1 = await _stateManager.GetTaskAsync(task1.TaskId);
        Assert.Equal(SparkTaskState.RequiresUserIntervention, refreshedTask1!.Status);
        Assert.Contains("interrupted by unexpected application restart", refreshedTask1.ErrorMessage);

        var refreshedTask2 = await _stateManager.GetTaskAsync(task2.TaskId);
        Assert.Equal(SparkTaskState.Completed, refreshedTask2!.Status);
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
                AuditId = "audit-1",
                Success = true,
                Result = "Note created at reports/summary.md",
                ExecutedAt = DateTimeOffset.UtcNow
            });
        }
    }
}
