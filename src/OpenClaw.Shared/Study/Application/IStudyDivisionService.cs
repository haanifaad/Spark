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
    IStudyApiService Api { get; }
    IStudyPermissionManager PermissionManager { get; }
    IStudyEventBus EventBus { get; }

    StudyCourse? ActiveCourse { get; }
    StudySession? ActiveSession { get; }

    event EventHandler<StudySessionMessage>? MessageReceived;
    event EventHandler<StudyProgress>? ProgressUpdated;
    event EventHandler<StudyCourse?>? ActiveCourseChanged;

    Task InitializeAsync(CancellationToken ct = default);

    Task SetActiveCourseAsync(string courseId, string userId = "default", CancellationToken ct = default);

    Task<StudySession> StartSessionAsync(string courseId, string? lessonId = null, string userId = "default", CancellationToken ct = default);

    Task<StudyInteractionResult> SendMessageAsync(string prompt, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<StudyCourse>> LoadCoursesAsync(string userId = "default", CancellationToken ct = default);
}
