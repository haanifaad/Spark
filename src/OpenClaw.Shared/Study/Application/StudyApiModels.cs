using System;
using System.Collections.Generic;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Application;

public sealed record StudyApiError(
    string Code,
    string Message,
    IReadOnlyList<string>? Details = null);

public sealed record StudyApiResponse<T>(
    bool Success,
    T? Data,
    StudyApiError? Error,
    string CorrelationId,
    DateTime TimestampUtc)
{
    public static StudyApiResponse<T> Ok(T data, string? correlationId = null) =>
        new(true, data, null, correlationId ?? Guid.NewGuid().ToString("N"), DateTime.UtcNow);

    public static StudyApiResponse<T> Fail(string code, string message, string? correlationId = null, IReadOnlyList<string>? details = null) =>
        new(false, default, new StudyApiError(code, message, details), correlationId ?? Guid.NewGuid().ToString("N"), DateTime.UtcNow);
}

public sealed record StudyPagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}

public sealed record StudyHealthReport(
    string Status,
    string SchemaVersion,
    string DatabaseStatus,
    string ModelRouterStatus,
    int ActiveSessions);

public sealed record CreateCourseRequest(
    string Title,
    string Description,
    EducationLevel Level,
    string? Subject = null);

public sealed record CreateSessionRequest(
    string CourseId,
    string? LessonId = null,
    string Title = "Interactive Learning Session",
    TeachingMode TeachingMode = TeachingMode.Standard,
    LearningLanguage Language = LearningLanguage.English,
    EducationLevel Level = EducationLevel.University,
    string? SubjectId = null,
    string? Topic = null);

public sealed record RecordProgressRequest(
    string CourseId,
    string LessonId,
    double Score,
    MasteryTier Mastery = MasteryTier.Competent);

public sealed record ResumeSessionRequest(
    string? Notes = null);

public sealed record UpdateSessionPreferencesRequest(
    TeachingMode? TeachingMode = null,
    LearningLanguage? Language = null,
    EducationLevel? Level = null,
    string? CurrentTopic = null);

public sealed record PostSessionMessageRequest(
    string Content,
    TeachingMode? OverrideMode = null,
    LearningLanguage? OverrideLanguage = null);

public sealed record CreateLearningPathRequest(
    string Title,
    string Description,
    EducationLevel Level,
    string TargetAudience,
    List<string> OrderedCourseIds,
    List<string> OrderedLessonIds,
    int EstimatedTotalHours);

public sealed record CreateLearningObjectiveRequest(
    string LessonId,
    string Description,
    TaxonomyLevel TaxonomyLevel,
    List<string>? Prerequisites = null);

// ══════════════════════════════════════════════════════════════
// Phase 3: Skills, Tools & Slash Commands API Models
// ══════════════════════════════════════════════════════════════
public sealed record ToggleSkillRequest(
    bool IsEnabled);

public sealed record ExecuteToolApiRequest(
    string ToolId,
    Dictionary<string, object?>? Arguments = null,
    string? SessionId = null);

public sealed record ApproveExecutionRequest(
    string ApprovedBy,
    string? Notes = null);

public sealed record RejectExecutionRequest(
    string Reason,
    string RejectedBy);

public sealed record ResolveCommandRequest(
    string RawInput);

public sealed record ExecuteCommandApiRequest(
    string CommandInput,
    string? SessionId = null);

public sealed record ExecutePipelineApiRequest(
    string UserInput,
    string? SessionId = null,
    string? CourseId = null,
    string? ForceSkillId = null,
    TeachingMode? TeachingMode = null,
    LearningLanguage? Language = null,
    EducationLevel? Level = null);

public sealed record StudyExecutionHistoryReport(
    IReadOnlyList<SkillExecutionRecord> SkillExecutions,
    IReadOnlyList<StudyToolExecutionResult> ToolExecutions,
    IReadOnlyList<CommandExecutionRecord> CommandExecutions);
