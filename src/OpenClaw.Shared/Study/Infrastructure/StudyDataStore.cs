using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Infrastructure;

/// <summary>
/// Thread-safe, ACID-atomic, user-isolated JSON persistence store for the Study Division.
/// Uses .tmp writes followed by atomic replace to guarantee data integrity across crashes.
/// </summary>
public sealed class StudyDataStore : IStudyDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _baseStoragePath;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _userLocks = new(StringComparer.OrdinalIgnoreCase);

    public string SchemaVersion => "1.1.0";

    public StudyDataStore(string? baseStoragePath = null)
    {
        if (!string.IsNullOrWhiteSpace(baseStoragePath))
        {
            _baseStoragePath = baseStoragePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baseStoragePath = Path.Combine(appData, "Spark", "Study");
        }

        Directory.CreateDirectory(_baseStoragePath);
    }

    private SemaphoreSlim GetUserGate(string userId) =>
        _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

    private string GetUserDirectory(string userId)
    {
        var clean = string.IsNullOrWhiteSpace(userId) ? "default" : userId.Trim();
        var dir = Path.Combine(_baseStoragePath, clean);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string GetCoursesPath(string userId) => Path.Combine(GetUserDirectory(userId), "courses.json");
    private string GetSessionsPath(string userId) => Path.Combine(GetUserDirectory(userId), "sessions.json");
    private string GetProgressPath(string userId) => Path.Combine(GetUserDirectory(userId), "progress.json");
    private string GetObjectivesPath(string userId) => Path.Combine(GetUserDirectory(userId), "learning_objectives.json");
    private string GetLearningPathsPath(string userId) => Path.Combine(GetUserDirectory(userId), "learning_paths.json");

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var migrationRunner = new StudyDatabaseMigrations(_baseStoragePath);
        await migrationRunner.ApplyMigrationsAsync(ct);
    }

    public async Task<IReadOnlyList<StudyCourse>> ListCoursesAsync(string userId = "default", CancellationToken ct = default)
    {
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            return await ReadCollectionAsync<StudyCourse>(GetCoursesPath(userId), ct);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<StudyCourse?> GetCourseAsync(string courseId, string userId = "default", CancellationToken ct = default)
    {
        var courses = await ListCoursesAsync(userId, ct);
        return courses.FirstOrDefault(c => string.Equals(c.Id, courseId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<StudyCourse> SaveCourseAsync(StudyCourse course, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(course);
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var path = GetCoursesPath(userId);
            var courses = (await ReadCollectionAsync<StudyCourse>(path, ct)).ToList();

            var index = courses.FindIndex(c => string.Equals(c.Id, course.Id, StringComparison.OrdinalIgnoreCase));
            course.UpdatedAtUtc = DateTime.UtcNow;

            if (index >= 0)
            {
                courses[index] = course;
            }
            else
            {
                courses.Add(course);
            }

            await WriteCollectionAtomicAsync(path, courses, ct);
            return course;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> DeleteCourseAsync(string courseId, string userId = "default", CancellationToken ct = default)
    {
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var path = GetCoursesPath(userId);
            var courses = (await ReadCollectionAsync<StudyCourse>(path, ct)).ToList();
            var removed = courses.RemoveAll(c => string.Equals(c.Id, courseId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await WriteCollectionAtomicAsync(path, courses, ct);
                return true;
            }
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<StudySession>> ListSessionsAsync(string userId = "default", string? courseId = null, CancellationToken ct = default)
    {
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var sessions = await ReadCollectionAsync<StudySession>(GetSessionsPath(userId), ct);
            if (!string.IsNullOrWhiteSpace(courseId))
            {
                return sessions.Where(s => string.Equals(s.CourseId, courseId, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            return sessions;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<StudySession?> GetSessionAsync(string sessionId, string userId = "default", CancellationToken ct = default)
    {
        var sessions = await ListSessionsAsync(userId, null, ct);
        return sessions.FirstOrDefault(s => string.Equals(s.Id, sessionId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<StudySession> SaveSessionAsync(StudySession session, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var path = GetSessionsPath(userId);
            var sessions = (await ReadCollectionAsync<StudySession>(path, ct)).ToList();

            var index = sessions.FindIndex(s => string.Equals(s.Id, session.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                sessions[index] = session;
            }
            else
            {
                sessions.Add(session);
            }

            await WriteCollectionAtomicAsync(path, sessions, ct);
            return session;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<StudyProgress> GetProgressAsync(string courseId, string userId = "default", CancellationToken ct = default)
    {
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var progressList = await ReadCollectionAsync<StudyProgress>(GetProgressPath(userId), ct);
            var existing = progressList.FirstOrDefault(p => string.Equals(p.CourseId, courseId, StringComparison.OrdinalIgnoreCase));
            return existing ?? new StudyProgress { UserId = userId, CourseId = courseId };
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<StudyProgress> SaveProgressAsync(StudyProgress progress, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var path = GetProgressPath(userId);
            var list = (await ReadCollectionAsync<StudyProgress>(path, ct)).ToList();
            var index = list.FindIndex(p => string.Equals(p.CourseId, progress.CourseId, StringComparison.OrdinalIgnoreCase));
            progress.LastStudiedUtc = DateTime.UtcNow;
            if (index >= 0)
            {
                list[index] = progress;
            }
            else
            {
                list.Add(progress);
            }

            await WriteCollectionAtomicAsync(path, list, ct);
            return progress;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<LearningObjective>> ListObjectivesAsync(string? lessonId = null, string userId = "default", CancellationToken ct = default)
    {
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var objectives = await ReadCollectionAsync<LearningObjective>(GetObjectivesPath(userId), ct);
            if (!string.IsNullOrWhiteSpace(lessonId))
            {
                return objectives.Where(o => string.Equals(o.LessonId, lessonId, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            return objectives;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<LearningObjective?> GetObjectiveAsync(string objectiveId, string userId = "default", CancellationToken ct = default)
    {
        var objectives = await ListObjectivesAsync(null, userId, ct);
        return objectives.FirstOrDefault(o => string.Equals(o.Id, objectiveId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<LearningObjective> SaveObjectiveAsync(LearningObjective objective, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(objective);
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var path = GetObjectivesPath(userId);
            var objectives = (await ReadCollectionAsync<LearningObjective>(path, ct)).ToList();
            var index = objectives.FindIndex(o => string.Equals(o.Id, objective.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                objectives[index] = objective;
            }
            else
            {
                objectives.Add(objective);
            }

            await WriteCollectionAtomicAsync(path, objectives, ct);
            return objective;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> MarkObjectiveMasteredAsync(string objectiveId, bool isMastered = true, string userId = "default", CancellationToken ct = default)
    {
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var path = GetObjectivesPath(userId);
            var objectives = (await ReadCollectionAsync<LearningObjective>(path, ct)).ToList();
            var objective = objectives.FirstOrDefault(o => string.Equals(o.Id, objectiveId, StringComparison.OrdinalIgnoreCase));
            if (objective == null)
            {
                return false;
            }

            objective.IsMastered = isMastered;
            await WriteCollectionAtomicAsync(path, objectives, ct);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<LearningPath>> ListLearningPathsAsync(EducationLevel? level = null, string userId = "default", CancellationToken ct = default)
    {
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var paths = await ReadCollectionAsync<LearningPath>(GetLearningPathsPath(userId), ct);
            if (level.HasValue)
            {
                return paths.Where(p => p.Level == level.Value).ToList();
            }
            return paths;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<LearningPath?> GetLearningPathAsync(string pathId, string userId = "default", CancellationToken ct = default)
    {
        var paths = await ListLearningPathsAsync(null, userId, ct);
        return paths.FirstOrDefault(p => string.Equals(p.Id, pathId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<LearningPath> SaveLearningPathAsync(LearningPath path, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct);
        try
        {
            var filePath = GetLearningPathsPath(userId);
            var paths = (await ReadCollectionAsync<LearningPath>(filePath, ct)).ToList();
            var index = paths.FindIndex(p => string.Equals(p.Id, path.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                paths[index] = path;
            }
            else
            {
                paths.Add(path);
            }

            await WriteCollectionAtomicAsync(filePath, paths, ct);
            return path;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<List<T>> ReadCollectionAsync<T>(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, ct);
            return items ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static async Task WriteCollectionAtomicAsync<T>(string filePath, List<T> data, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = filePath + ".tmp." + Guid.NewGuid().ToString("N");
        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, data, JsonOptions, ct);
            await stream.FlushAsync(ct);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }
}
