using System;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding;
using OpenClaw.Shared.Creative;
using OpenClaw.Shared.Orchestration;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Divisions;

/// <summary>
/// Cross-division orchestration contract enabling safe, synchronized delegation and context
/// sharing across all five SPARK divisions (Chat, Work, Code, Study, Creative).
/// </summary>
public interface ISparkDivisionCoordinator
{
    /// <summary>
    /// Delegates a natural language task from Chat to the Work division for planning and execution.
    /// </summary>
    Task<SparkTask> DelegateChatToWorkAsync(
        string instruction,
        string? title = null,
        SparkTaskPriority priority = SparkTaskPriority.Normal,
        string userId = "default",
        CancellationToken ct = default);

    /// <summary>
    /// Delegates a coding problem or repository inquiry from Chat to the Code division.
    /// </summary>
    Task<CodingPlan?> DelegateChatToCodeAsync(
        string instruction,
        string workspacePath,
        CancellationToken ct = default);

    /// <summary>
    /// Transfers a learning topic, explanation request, or educational question from Chat to Study division.
    /// </summary>
    Task<StudySessionMessage> DelegateChatToStudyAsync(
        string topicOrQuestion,
        string? courseId = null,
        string userId = "default",
        CancellationToken ct = default);

    /// <summary>
    /// Delegates an asset generation prompt from Chat to the Creative division.
    /// </summary>
    Task<CreativeAgentResult> DelegateChatToCreativeAsync(
        string prompt,
        string? targetApplicationId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a code modification or build/test validation step on behalf of a Work task.
    /// </summary>
    Task<bool> DelegateWorkToCodeAsync(
        string taskId,
        string stepId,
        string instruction,
        string workspacePath,
        CancellationToken ct = default);

    /// <summary>
    /// Delegates research, document analysis, or educational workflow execution from Work to Study.
    /// </summary>
    Task<StudySessionMessage> DelegateWorkToStudyAsync(
        string taskId,
        string query,
        string? courseId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Delegates creative asset creation from an autonomous Work task to the Creative division.
    /// </summary>
    Task<CreativeAgentResult> DelegateWorkToCreativeAsync(
        string taskId,
        string creativePrompt,
        string? targetApplicationId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Reports execution progress or findings from Code back to a parent Work task.
    /// </summary>
    Task ReportCodeProgressToWorkAsync(
        string taskId,
        string stepId,
        bool success,
        string outputOrError,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves authorized learning summaries or subject context from Study to enrich Chat conversations.
    /// </summary>
    Task<string> ShareStudyContextToChatAsync(
        string subjectOrTopic,
        CancellationToken ct = default);
}
