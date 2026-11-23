using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding;
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.Creative;
using OpenClaw.Shared.Memory;
using OpenClaw.Shared.Orchestration;
using OpenClaw.Shared.Study.Application;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Divisions;

/// <summary>
/// Central coordinator implementing cross-division delegation, state sharing, and task synchronization
/// across Chat, Work, Code, Study, and Creative divisions.
/// </summary>
public sealed class SparkDivisionCoordinator : ISparkDivisionCoordinator
{
    private readonly IDivisionManager _divisionManager;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly ITaskStateManager _stateManager;
    private readonly ICodingDivisionService? _codingService;
    private readonly IStudyDivisionService? _studyService;
    private readonly ISparkMemoryStore? _memoryStore;

    public SparkDivisionCoordinator(
        IDivisionManager divisionManager,
        IAgentOrchestrator orchestrator,
        ITaskStateManager stateManager,
        ICodingDivisionService? codingService = null,
        IStudyDivisionService? studyService = null,
        ISparkMemoryStore? memoryStore = null)
    {
        _divisionManager = divisionManager ?? throw new ArgumentNullException(nameof(divisionManager));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _codingService = codingService;
        _studyService = studyService;
        _memoryStore = memoryStore;
    }

    /// <inheritdoc />
    public async Task<SparkTask> DelegateChatToWorkAsync(
        string instruction,
        string? title = null,
        SparkTaskPriority priority = SparkTaskPriority.Normal,
        string userId = "default",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);

        var task = await _orchestrator.SubmitTaskAsync(instruction, title, priority, userId, ct).ConfigureAwait(false);
        _divisionManager.SetDivisionState(SparkDivision.Work, task);

        if (_memoryStore != null)
        {
            await _memoryStore.RememberAsync(new MemoryRecord
            {
                Id = $"work_task_{task.TaskId}",
                Scope = MemoryScope.UserProfile,
                Content = $"Task created from Chat: '{instruction}'. Status: {task.Status}",
                UserId = userId,
                Tags = ["cross_division", "chat_to_work", task.TaskId]
            }, ct).ConfigureAwait(false);
        }

        return task;
    }

    /// <inheritdoc />
    public async Task<CodingPlan?> DelegateChatToCodeAsync(
        string instruction,
        string workspacePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        if (_codingService == null)
        {
            return null;
        }

        await _codingService.OpenWorkspaceAsync(workspacePath, ct).ConfigureAwait(false);
        var message = await _codingService.SendPromptAsync(instruction, ct: ct).ConfigureAwait(false);

        var plan = _codingService.CurrentOrchestrator?.CurrentPlan;
        if (plan != null)
        {
            _divisionManager.SetDivisionState(SparkDivision.Code, plan);
        }

        return plan;
    }

    /// <inheritdoc />
    public async Task<StudySessionMessage> DelegateChatToStudyAsync(
        string topicOrQuestion,
        string? courseId = null,
        string userId = "default",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicOrQuestion);

        if (_studyService == null)
        {
            return new StudySessionMessage(
                Guid.NewGuid().ToString("N"),
                StudyMessageSender.Tutor,
                "Study division service is unconfigured.",
                DateTime.UtcNow);
        }

        if (!string.IsNullOrWhiteSpace(courseId))
        {
            await _studyService.SetActiveCourseAsync(courseId, userId, ct).ConfigureAwait(false);
        }

        var interaction = await _studyService.SendMessageAsync(topicOrQuestion, userId, ct).ConfigureAwait(false);

        var responseMessage = new StudySessionMessage(
            Guid.NewGuid().ToString("N"),
            StudyMessageSender.Tutor,
            interaction.ResponseText,
            DateTime.UtcNow);

        _divisionManager.SetDivisionState(SparkDivision.Study, responseMessage);

        if (_memoryStore != null)
        {
            await _memoryStore.RememberAsync(new MemoryRecord
            {
                Id = $"study_topic_{Guid.NewGuid():N}",
                Scope = MemoryScope.UserProfile,
                Content = $"Learner topic: '{topicOrQuestion}'\nSummary: {interaction.ResponseText}",
                UserId = userId,
                Tags = ["cross_division", "chat_to_study"]
            }, ct).ConfigureAwait(false);
        }

        return responseMessage;
    }

    /// <inheritdoc />
    public async Task<CreativeAgentResult> DelegateChatToCreativeAsync(
        string prompt,
        string? targetApplicationId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var result = await CreativeAgent.Instance.ExecutePromptAsync(prompt, targetApplicationId, ct).ConfigureAwait(false);
        _divisionManager.SetDivisionState(SparkDivision.Creative, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> DelegateWorkToCodeAsync(
        string taskId,
        string stepId,
        string instruction,
        string workspacePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);

        if (_codingService == null)
        {
            await ReportCodeProgressToWorkAsync(taskId, stepId, false, "Coding division service is unavailable.", ct).ConfigureAwait(false);
            return false;
        }

        try
        {
            if (_codingService.CurrentWorkspaceRoot != workspacePath)
            {
                await _codingService.OpenWorkspaceAsync(workspacePath, ct).ConfigureAwait(false);
            }

            var response = await _codingService.SendPromptAsync(instruction, ct: ct).ConfigureAwait(false);
            bool success = !string.IsNullOrWhiteSpace(response.Content);

            await ReportCodeProgressToWorkAsync(taskId, stepId, success, response.Content, ct).ConfigureAwait(false);
            return success;
        }
        catch (Exception ex)
        {
            await ReportCodeProgressToWorkAsync(taskId, stepId, false, ex.Message, ct).ConfigureAwait(false);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<StudySessionMessage> DelegateWorkToStudyAsync(
        string taskId,
        string query,
        string? courseId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        return await DelegateChatToStudyAsync(query, courseId, "system_work", ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CreativeAgentResult> DelegateWorkToCreativeAsync(
        string taskId,
        string creativePrompt,
        string? targetApplicationId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(creativePrompt);

        return await DelegateChatToCreativeAsync(creativePrompt, targetApplicationId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReportCodeProgressToWorkAsync(
        string taskId,
        string stepId,
        bool success,
        string outputOrError,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var task = await _stateManager.GetTaskAsync(taskId, "default", ct).ConfigureAwait(false);
        if (task == null) return;

        var step = task.Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step != null)
        {
            step.CompletedAt = DateTimeOffset.UtcNow;
            step.Status = success ? SparkStepState.Completed : SparkStepState.Failed;
            step.OutputResult = success ? outputOrError : null;
            step.ErrorInfo = success ? null : outputOrError;
            step.VerificationPassed = success;
            step.VerificationDetails = success ? "Code execution completed successfully." : $"Code execution failed: {outputOrError}";

            await _stateManager.UpdateTaskStepAsync(taskId, step, "default", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<string> ShareStudyContextToChatAsync(
        string subjectOrTopic,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectOrTopic);

        if (_memoryStore != null)
        {
            var memories = await _memoryStore.SearchAsync(subjectOrTopic, limit: 3, ct: ct).ConfigureAwait(false);
            if (memories.Count > 0)
            {
                return string.Join("\n---\n", memories.Select(m => m.Record.Content));
            }
        }

        return $"No active study notes found for '{subjectOrTopic}'.";
    }
}
