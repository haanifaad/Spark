using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Application;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Exposes the backend API foundation for the Study Division under /api/v1/study.
/// </summary>
public interface IStudyApiService
{
    Task<StudyApiResponse<StudyHealthReport>> GetHealthAsync(string? correlationId = null, CancellationToken ct = default);

    Task<StudyApiResponse<StudyPagedResult<StudyCourse>>> GetCoursesAsync(
        string userId = "default",
        EducationLevel? level = null,
        int pageNumber = 1,
        int pageSize = 20,
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudyCourse>> GetCourseByIdAsync(
        string courseId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudyCourse>> CreateCourseAsync(
        CreateCourseRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<IReadOnlyList<StudySession>>> GetSessionsAsync(
        string userId = "default",
        string? courseId = null,
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudySession>> GetSessionByIdAsync(
        string sessionId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudySession>> CreateSessionAsync(
        CreateSessionRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudySession>> ResumeSessionAsync(
        string sessionId,
        ResumeSessionRequest? request = null,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudySession>> UpdateSessionPreferencesAsync(
        string sessionId,
        UpdateSessionPreferencesRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudySessionMessage>> AddSessionMessageAsync(
        string sessionId,
        PostSessionMessageRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudyProgress>> GetProgressAsync(
        string courseId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudyProgress>> RecordProgressAsync(
        RecordProgressRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<IReadOnlyList<LearningPath>>> GetLearningPathsAsync(
        EducationLevel? level = null,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<LearningPath>> GetLearningPathByIdAsync(
        string pathId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<IReadOnlyList<LearningObjective>>> GetLessonObjectivesAsync(
        string lessonId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<bool>> MarkObjectiveMasteredAsync(
        string objectiveId,
        bool isMastered = true,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    // ══════════════════════════════════════════════════════════════
    // Phase 3: Skills, Tools & Slash Commands Endpoints
    // ══════════════════════════════════════════════════════════════
    Task<StudyApiResponse<IReadOnlyList<StudySkillDefinition>>> GetSkillsAsync(
        SkillCategory? category = null,
        bool includeDisabled = false,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudySkillDefinition>> GetSkillByIdAsync(
        string skillId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<bool>> ToggleSkillAsync(
        string skillId,
        ToggleSkillRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<IReadOnlyList<StudyToolDefinition>>> GetToolsAsync(
        ToolCategory? category = null,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudyToolDefinition>> GetToolByIdAsync(
        string toolId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudyToolExecutionResult>> ExecuteToolAsync(
        ExecuteToolApiRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudyToolExecutionResult>> ApproveExecutionAsync(
        string requestId,
        ApproveExecutionRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudyToolExecutionResult>> RejectExecutionAsync(
        string requestId,
        RejectExecutionRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<bool>> CancelExecutionAsync(
        string requestId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<IReadOnlyList<StudyCommandDefinition>>> GetCommandsAsync(
        CommandCategory? category = null,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<ParsedStudyCommand>> ResolveCommandAsync(
        ResolveCommandRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<PipelineExecutionResult>> ExecuteCommandAsync(
        ExecuteCommandApiRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<IReadOnlyList<CommandAutocompleteItem>>> GetCommandAutocompleteAsync(
        string partialInput,
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<PipelineExecutionResult>> ProcessPipelineAsync(
        ExecutePipelineApiRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default);

    Task<StudyApiResponse<StudyExecutionHistoryReport>> GetExecutionHistoryAsync(
        string userId = "default",
        int limit = 50,
        string? correlationId = null,
        CancellationToken ct = default);
}
