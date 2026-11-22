using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Orchestration;

namespace OpenClaw.Shared.Study.Application;

/// <summary>
/// Primary coordinator service facade for the SPARK Study Division.
/// </summary>
public interface IStudyDivisionService
{
    IStudyDataStore DataStore { get; }
    IStudyOrchestrator Orchestrator { get; }
    IStudyTutorService TutorService { get; }
    IStudyApiService Api { get; }
    IStudyPermissionManager PermissionManager { get; }
    IStudyEventBus EventBus { get; }
    IStudySkillRegistry SkillRegistry { get; }
    IStudyToolRegistry ToolRegistry { get; }
    IStudyCommandRegistry CommandRegistry { get; }
    IStudyCommandParser CommandParser { get; }
    IStudyExecutionPipeline ExecutionPipeline { get; }

    StudyCourse? ActiveCourse { get; }
    StudySession? ActiveSession { get; }

    event EventHandler<StudySessionMessage>? MessageReceived;
    event EventHandler<StudyProgress>? ProgressUpdated;
    event EventHandler<StudyCourse?>? ActiveCourseChanged;
    event EventHandler<StudySession?>? ActiveSessionChanged;

    Task InitializeAsync(CancellationToken ct = default);

    Task SetActiveCourseAsync(string courseId, string userId = "default", CancellationToken ct = default);

    Task<StudySession> StartSessionAsync(
        string courseId,
        string? lessonId = null,
        TeachingMode mode = TeachingMode.Standard,
        LearningLanguage language = LearningLanguage.English,
        EducationLevel level = EducationLevel.University,
        string? topic = null,
        string userId = "default",
        CancellationToken ct = default);

    Task<StudySession> ResumeSessionAsync(string sessionId, string userId = "default", CancellationToken ct = default);

    Task<StudySession> UpdateActiveSessionPreferencesAsync(
        TeachingMode? mode,
        LearningLanguage? language,
        EducationLevel? level,
        string? topic,
        string userId = "default",
        CancellationToken ct = default);

    Task<StudySessionMessage> SendLearnerMessageAsync(
        string prompt,
        TeachingMode? overrideMode = null,
        LearningLanguage? overrideLanguage = null,
        string userId = "default",
        CancellationToken ct = default);

    Task<StudyInteractionResult> SendMessageAsync(string prompt, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<StudyCourse>> LoadCoursesAsync(string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<LearningPath>> GetLearningPathsAsync(EducationLevel? level = null, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<LearningObjective>> GetActiveLessonObjectivesAsync(string userId = "default", CancellationToken ct = default);

    Task<PipelineExecutionResult> ExecutePipelineAsync(
        string input,
        TeachingMode? mode = null,
        LearningLanguage? language = null,
        string userId = "default",
        CancellationToken ct = default);
}
