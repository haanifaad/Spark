using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Data persistence contract for the Study Division.
/// Guarantees thread-safe, isolated, atomic reads and writes.
/// </summary>
public interface IStudyDataStore
{
    string SchemaVersion { get; }

    Task InitializeAsync(CancellationToken ct = default);

    Task<IReadOnlyList<StudyCourse>> ListCoursesAsync(string userId = "default", CancellationToken ct = default);

    Task<StudyCourse?> GetCourseAsync(string courseId, string userId = "default", CancellationToken ct = default);

    Task<StudyCourse> SaveCourseAsync(StudyCourse course, string userId = "default", CancellationToken ct = default);

    Task<bool> DeleteCourseAsync(string courseId, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<StudySession>> ListSessionsAsync(string userId = "default", string? courseId = null, CancellationToken ct = default);

    Task<StudySession?> GetSessionAsync(string sessionId, string userId = "default", CancellationToken ct = default);

    Task<StudySession> SaveSessionAsync(StudySession session, string userId = "default", CancellationToken ct = default);

    Task<StudyProgress> GetProgressAsync(string courseId, string userId = "default", CancellationToken ct = default);

    Task<StudyProgress> SaveProgressAsync(StudyProgress progress, string userId = "default", CancellationToken ct = default);

    // Phase 2: Learning Objectives & Learning Paths
    Task<IReadOnlyList<LearningObjective>> ListObjectivesAsync(string? lessonId = null, string userId = "default", CancellationToken ct = default);

    Task<LearningObjective?> GetObjectiveAsync(string objectiveId, string userId = "default", CancellationToken ct = default);

    Task<LearningObjective> SaveObjectiveAsync(LearningObjective objective, string userId = "default", CancellationToken ct = default);

    Task<bool> MarkObjectiveMasteredAsync(string objectiveId, bool isMastered = true, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<LearningPath>> ListLearningPathsAsync(EducationLevel? level = null, string userId = "default", CancellationToken ct = default);

    Task<LearningPath?> GetLearningPathAsync(string pathId, string userId = "default", CancellationToken ct = default);

    Task<LearningPath> SaveLearningPathAsync(LearningPath path, string userId = "default", CancellationToken ct = default);
}
