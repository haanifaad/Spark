using System.Collections.Concurrent;
using OpenClaw.Shared.Memory;
using OpenClaw.Shared.Security.Approvals;
using OpenClaw.Shared.Security.Sentinel;
using OpenClaw.Shared.Tools;
using OpenClaw.Shared.Watchdog;

namespace OpenClaw.Shared.Orchestration;

/// <summary>
/// Production implementation of the central SPARK Autonomous Agent Orchestrator.
/// </summary>
public sealed class SparkAgentOrchestrator : IAgentOrchestrator
{
    private readonly ITaskStateManager _stateManager;
    private readonly ITaskPlanner _planner;
    private readonly ISparkToolRegistry _toolRegistry;
    private readonly ISparkApprovalManager _approvalManager;
    private readonly ISentinelReviewer? _sentinel;
    private readonly ISparkMemoryStore? _memoryStore;
    private readonly TurnWatchdogMonitor? _watchdog;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTasks = new();
    private readonly ConcurrentDictionary<string, Task<SparkTask>> _activeExecutionTasks = new();

    public SparkAgentOrchestrator(
        ITaskStateManager stateManager,
        ITaskPlanner planner,
        ISparkToolRegistry toolRegistry,
        ISparkApprovalManager approvalManager,
        ISentinelReviewer? sentinel = null,
        ISparkMemoryStore? memoryStore = null,
        TurnWatchdogMonitor? watchdog = null)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _approvalManager = approvalManager ?? throw new ArgumentNullException(nameof(approvalManager));
        _sentinel = sentinel;
        _memoryStore = memoryStore;
        _watchdog = watchdog;
    }

    public async Task<SparkTask> SubmitTaskAsync(string instruction, string? title = null, SparkTaskPriority priority = SparkTaskPriority.Normal, string userId = "default", CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);

        // 1. Create task in Received state
        var task = await _stateManager.CreateTaskAsync(instruction, title, priority, userId, ct).ConfigureAwait(false);

        // 2. Transition Received -> Interpreting
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Interpreting, "Analyzing user intent and objectives", userId, ct).ConfigureAwait(false);

        // 3. Transition Interpreting -> Planning
        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Planning, "Formulating task execution plan", userId, ct).ConfigureAwait(false);

        // 4. Formulate validated plan
        var plan = await _planner.CreatePlanAsync(task, _memoryStore, ct).ConfigureAwait(false);
        await _stateManager.SaveTaskAsync(task, ct).ConfigureAwait(false);

        // 5. Sentinel Independent Security Review
        if (_sentinel != null)
        {
            var firstStep = plan.PlannedSteps.FirstOrDefault();
            var sentinelReq = new SentinelEvaluationRequest
            {
                TaskId = task.TaskId,
                ProposedToolName = firstStep?.ToolName ?? "agent_general_reasoning",
                TaskOriginalInstruction = task.UserInstruction,
                FinancialAmount = plan.EstimatedTotalCost > 0 ? plan.EstimatedTotalCost : null
            };

            var sentinelVerdict = await _sentinel.ReviewOperationAsync(sentinelReq, ct).ConfigureAwait(false);
            if (sentinelVerdict.Decision == SentinelDecisionKind.Rejected)
            {
                task.OverallRiskLevel = sentinelVerdict.RiskLevel;
                task = await _stateManager.UpdateTaskStateAsync(task.TaskId, SparkTaskState.Failed, $"Rejected by Sentinel independent security review: {string.Join("; ", sentinelVerdict.Reasons)}", userId, ct).ConfigureAwait(false);
                return task;
            }

            if (sentinelVerdict.Decision == SentinelDecisionKind.RequiresUserApproval)
            {
                plan.PlannedSteps.ForEach(s => s.RequiresApproval = true);
            }
        }

        // 6. Determine next state: WaitingForApproval if any step requires approval, otherwise Ready
        var nextState = plan.HasApprovalGatedSteps ? SparkTaskState.WaitingForApproval : SparkTaskState.Ready;
        var reason = plan.HasApprovalGatedSteps 
            ? "Task plan formulated; human approval required for high-risk operations." 
            : "Task plan formulated and verified; ready for execution.";

        task = await _stateManager.UpdateTaskStateAsync(task.TaskId, nextState, reason, userId, ct).ConfigureAwait(false);

        // 7. If Ready, begin execution automatically in background
        if (nextState == SparkTaskState.Ready)
        {
            _ = ExecuteTaskAsync(task.TaskId, userId, CancellationToken.None);
        }

        return task;
    }

    public async Task<SparkTask> ExecuteTaskAsync(string taskId, string userId = "default", CancellationToken ct = default)
    {
        while (true)
        {
            if (_activeExecutionTasks.TryGetValue(taskId, out var existingTask))
            {
                if (!existingTask.IsCompleted)
                {
                    return await existingTask.WaitAsync(ct).ConfigureAwait(false);
                }
                _activeExecutionTasks.TryRemove(taskId, out _);
            }

            var tcs = new TaskCompletionSource<SparkTask>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_activeExecutionTasks.TryAdd(taskId, tcs.Task))
            {
                try
                {
                    var result = await ExecuteTaskCoreAsync(taskId, userId, ct).ConfigureAwait(false);
                    tcs.TrySetResult(result);
                    return result;
                }
                catch (OperationCanceledException oce)
                {
                    tcs.TrySetCanceled(oce.CancellationToken);
                    throw;
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    throw;
                }
                finally
                {
                    _activeExecutionTasks.TryRemove(taskId, out _);
                    _runningTasks.TryRemove(taskId, out _);
                }
            }
        }
    }

    private async Task<SparkTask> ExecuteTaskCoreAsync(string taskId, string userId, CancellationToken ct)
    {
        var task = await _stateManager.GetTaskAsync(taskId, userId, ct).ConfigureAwait(false)
                   ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");

        if (task.Status == SparkTaskState.Completed)
        {
            return task;
        }

        if (task.Status is not (SparkTaskState.Ready or SparkTaskState.WaitingForApproval or SparkTaskState.Paused or SparkTaskState.Executing))
        {
            throw new InvalidOperationException($"Cannot execute task in state '{task.Status}'.");
        }

        // Setup linked cancellation token for task execution
        var cts = new CancellationTokenSource();
        _runningTasks[taskId] = cts;
        var combinedCt = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct).Token;

        try
        {
            task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Executing, "Executing steps", userId, combinedCt).ConfigureAwait(false);

            // Execute each step in sequence
            for (var i = 0; i < task.Steps.Count; i++)
            {
                combinedCt.ThrowIfCancellationRequested();

                var step = task.Steps[i];

                // If step is already completed, skip
                if (step.Status == SparkStepState.Completed)
                    continue;

                // Check for User Takeover
                if (task.IsUserTakeoverActive)
                {
                    task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Paused, "Automated execution paused due to active user takeover.", userId, combinedCt).ConfigureAwait(false);
                    return task;
                }

                // Independent Sentinel step review before execution
                if (_sentinel != null)
                {
                    var sentinelStepReq = new SentinelEvaluationRequest
                    {
                        TaskId = taskId,
                        StepId = step.StepId,
                        ProposedToolName = step.ToolName,
                        OperationArguments = step.InputArguments,
                        TaskOriginalInstruction = task.UserInstruction
                    };

                    var sentinelVerdict = await _sentinel.ReviewOperationAsync(sentinelStepReq, combinedCt).ConfigureAwait(false);
                    if (sentinelVerdict.Decision == SentinelDecisionKind.Rejected)
                    {
                        step.Status = SparkStepState.Failed;
                        step.ErrorInfo = $"Blocked by Sentinel independent safety review: {string.Join("; ", sentinelVerdict.Reasons)}";
                        await _stateManager.UpdateTaskStepAsync(taskId, step, userId, combinedCt).ConfigureAwait(false);

                        task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Failed, step.ErrorInfo, userId, combinedCt).ConfigureAwait(false);
                        return task;
                    }

                    if (sentinelVerdict.Decision == SentinelDecisionKind.RequiresUserApproval)
                    {
                        step.RequiresApproval = true;
                    }
                }

                // If step requires approval and has not been approved
                if (step.RequiresApproval && string.IsNullOrEmpty(step.ApprovalId))
                {
                    var details = step.InputArguments.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty);
                    var approvalReq = await _approvalManager.RequestApprovalAsync(
                        SparkHighImpactOperationType.ExecutePrivilegedCommand,
                        $"Execute step {step.Index}: {step.Description} using tool {step.ToolName}",
                        details,
                        requestedBy: task.ActiveAgentIdentity ?? "SPARK",
                        cancellationToken: combinedCt).ConfigureAwait(false);

                    step.ApprovalId = approvalReq.ApprovalId;
                    step.Status = SparkStepState.WaitingForApproval;
                    await _stateManager.UpdateTaskStepAsync(taskId, step, userId, combinedCt).ConfigureAwait(false);

                    task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.WaitingForApproval, $"Approval required for step {step.Index}: {step.Description}", userId, combinedCt).ConfigureAwait(false);
                    return task;
                }

                // Execute the tool
                var turnId = $"turn_{taskId}_{step.StepId}";
                _watchdog?.RegisterTurn(new TurnWatchdogMonitor.WatchedTurn
                {
                    TurnId = turnId,
                    SessionId = taskId,
                    ToolName = step.ToolName,
                    StartedAt = DateTimeOffset.UtcNow
                });

                step.StartedAt = DateTimeOffset.UtcNow;
                step.Status = SparkStepState.Executing;
                await _stateManager.UpdateTaskStepAsync(taskId, step, userId, combinedCt).ConfigureAwait(false);

                SparkToolExecutionResponse toolResponse;
                try
                {
                    var tool = _toolRegistry.GetTool(step.ToolName);
                    var permissions = tool != null
                        ? new HashSet<string>(tool.Metadata.RequiredPermissions, StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    var toolRequest = new SparkToolExecutionRequest
                    {
                        ToolId = step.ToolName,
                        Arguments = step.InputArguments,
                        GrantedPermissions = permissions,
                        CallerIdentity = "SPARK-Orchestrator",
                        SessionId = taskId,
                        IsApproved = !string.IsNullOrEmpty(step.ApprovalId),
                        ApprovalId = step.ApprovalId
                    };

                    toolResponse = await _toolRegistry.ExecuteToolAsync(toolRequest, combinedCt).ConfigureAwait(false);
                }
                finally
                {
                    _watchdog?.EndTurn(turnId);
                }

                step.CompletedAt = DateTimeOffset.UtcNow;
                if (toolResponse.Success)
                {
                    step.Status = SparkStepState.Completed;
                    step.OutputResult = toolResponse.Result?.ToString();
                    step.VerificationPassed = true;
                    step.VerificationDetails = "Tool executed successfully with valid response.";
                }
                else
                {
                    step.Status = SparkStepState.Failed;
                    step.ErrorInfo = toolResponse.Error;
                    step.VerificationPassed = false;
                    step.VerificationDetails = $"Execution failed: {toolResponse.Error}";

                    await _stateManager.UpdateTaskStepAsync(taskId, step, userId, combinedCt).ConfigureAwait(false);
                    task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Failed, $"Step {step.Index} failed: {toolResponse.Error}", userId, combinedCt).ConfigureAwait(false);
                    return task;
                }

                await _stateManager.UpdateTaskStepAsync(taskId, step, userId, combinedCt).ConfigureAwait(false);
            }

            // Transition Executing -> Reviewing
            task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Reviewing, "Reviewing execution outputs and constraints", userId, combinedCt).ConfigureAwait(false);

            // Transition Reviewing -> Verifying
            task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Verifying, "Verifying real-world execution results", userId, combinedCt).ConfigureAwait(false);

            // Verify all steps completed successfully
            var allSucceeded = task.Steps.All(s => s.Status == SparkStepState.Completed);
            if (allSucceeded)
            {
                var summary = $"Successfully completed all {task.Steps.Count} steps.";
                task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Completed, summary, userId, combinedCt).ConfigureAwait(false);
            }
            else
            {
                task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Failed, "Not all steps succeeded verification.", userId, combinedCt).ConfigureAwait(false);
            }

            return task;
        }
        catch (OperationCanceledException)
        {
            task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Paused, "Execution paused or cancelled.", userId, CancellationToken.None).ConfigureAwait(false);
            return task;
        }
        catch (Exception ex)
        {
            task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Failed, $"Unexpected error during execution: {ex.Message}", userId, CancellationToken.None).ConfigureAwait(false);
            return task;
        }
        finally
        {
            _runningTasks.TryRemove(taskId, out _);
        }
    }

    public async Task<SparkTask> PauseTaskAsync(string taskId, string userId = "default", CancellationToken ct = default)
    {
        if (_runningTasks.TryRemove(taskId, out var cts))
        {
            cts.Cancel();
        }

        return await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Paused, "Task paused by user", userId, ct).ConfigureAwait(false);
    }

    public async Task<SparkTask> ResumeTaskAsync(string taskId, string userId = "default", CancellationToken ct = default)
    {
        var task = await _stateManager.GetTaskAsync(taskId, userId, ct).ConfigureAwait(false)
                   ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");

        if (task.Status != SparkTaskState.Paused)
        {
            throw new InvalidOperationException($"Cannot resume task in state '{task.Status}'.");
        }

        task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Ready, "Resumed by user", userId, ct).ConfigureAwait(false);
        _ = ExecuteTaskAsync(taskId, userId, CancellationToken.None);
        return task;
    }

    public async Task<SparkTask> CancelTaskAsync(string taskId, string reason = "User cancelled", string userId = "default", CancellationToken ct = default)
    {
        if (_runningTasks.TryRemove(taskId, out var cts))
        {
            cts.Cancel();
        }

        return await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Cancelled, reason, userId, ct).ConfigureAwait(false);
    }

    public async Task<SparkTask> ModifyTaskAsync(string taskId, string newInstruction, string reason = "User edit", string userId = "default", CancellationToken ct = default)
    {
        // 1. Pause active execution if running
        if (_runningTasks.TryRemove(taskId, out var cts))
        {
            cts.Cancel();
        }

        // 2. Modify instruction in state manager (transitions active task to Planning)
        var task = await _stateManager.ModifyTaskInstructionAsync(taskId, newInstruction, reason, userId, ct).ConfigureAwait(false);

        // 3. Replan remaining steps
        var replan = await _planner.ReplanAsync(task, reason, _memoryStore, ct).ConfigureAwait(false);
        await _stateManager.SaveTaskAsync(task, ct).ConfigureAwait(false);

        // 4. Update status based on approval requirements
        var nextState = replan.HasApprovalGatedSteps ? SparkTaskState.WaitingForApproval : SparkTaskState.Ready;
        task = await _stateManager.UpdateTaskStateAsync(taskId, nextState, "Task replanned following user instruction modification.", userId, ct).ConfigureAwait(false);

        if (nextState == SparkTaskState.Ready)
        {
            _ = ExecuteTaskAsync(taskId, userId, CancellationToken.None);
        }

        return task;
    }

    public async Task<SparkTask> SetUserTakeoverAsync(string taskId, bool isTakeoverActive, string userId = "default", CancellationToken ct = default)
    {
        var task = await _stateManager.GetTaskAsync(taskId, userId, ct).ConfigureAwait(false)
                   ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");

        task.IsUserTakeoverActive = isTakeoverActive;

        if (isTakeoverActive)
        {
            if (_runningTasks.TryRemove(taskId, out var cts))
            {
                cts.Cancel();
            }
            task.Status = SparkTaskState.Paused;
            task.Metadata["UserTakeoverActive"] = "True";
            task.Metadata["UserTakeoverStartedAt"] = DateTimeOffset.UtcNow.ToString("O");
        }
        else
        {
            task.Metadata["UserTakeoverActive"] = "False";
            task.Metadata["UserTakeoverEndedAt"] = DateTimeOffset.UtcNow.ToString("O");
            task.Status = SparkTaskState.Ready;
        }

        await _stateManager.SaveTaskAsync(task, ct).ConfigureAwait(false);

        if (!isTakeoverActive)
        {
            _ = ExecuteTaskAsync(taskId, userId, CancellationToken.None);
        }

        return task;
    }

    public async Task<SparkTask> ApproveStepAsync(string taskId, string stepId, string approverIdentity, string expectedBindingSignature, string userId = "default", CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approverIdentity);

        var task = await _stateManager.GetTaskAsync(taskId, userId, ct).ConfigureAwait(false)
                   ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");

        var step = task.Steps.FirstOrDefault(s => s.StepId == stepId)
                   ?? throw new KeyNotFoundException($"Step '{stepId}' was not found in task '{taskId}'.");

        if (string.IsNullOrEmpty(step.ApprovalId))
        {
            throw new InvalidOperationException("Step does not have a pending approval request.");
        }

        // Verify with human approval manager (enforces human identity & parameter binding signature)
        var approvalResult = await _approvalManager.ApproveAsync(step.ApprovalId, approverIdentity, expectedBindingSignature, cancellationToken: ct).ConfigureAwait(false);

        step.Status = SparkStepState.Ready;
        await _stateManager.UpdateTaskStepAsync(taskId, step, userId, ct).ConfigureAwait(false);

        // If no more steps are waiting for approval, transition task back to Ready and resume execution
        if (task.Steps.All(s => s.Status != SparkStepState.WaitingForApproval))
        {
            task = await _stateManager.UpdateTaskStateAsync(taskId, SparkTaskState.Ready, "Step approved; resuming task execution.", userId, ct).ConfigureAwait(false);
            _ = ExecuteTaskAsync(taskId, userId, CancellationToken.None);
        }

        return task;
    }
}
