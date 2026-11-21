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
}
