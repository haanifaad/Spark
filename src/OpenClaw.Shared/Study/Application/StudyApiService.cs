using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Application;

/// <summary>
/// Implements backend API handling for /api/v1/study endpoints.
/// </summary>
public sealed class StudyApiService : IStudyApiService
{
    private readonly IStudyDataStore _dataStore;
    private readonly ISparkModelRouter? _modelRouter;
    private readonly IStudyEventBus? _eventBus;

    public StudyApiService(
        IStudyDataStore dataStore,
        ISparkModelRouter? modelRouter = null,
        IStudyEventBus? eventBus = null)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _modelRouter = modelRouter;
        _eventBus = eventBus;
    }

    public async Task<StudyApiResponse<StudyHealthReport>> GetHealthAsync(string? correlationId = null, CancellationToken ct = default)
    {
        try
        {
            var sessions = await _dataStore.ListSessionsAsync("default", null, ct);
            var activeCount = sessions.Count(s => s.Status == StudySessionStatus.Active);
            var report = new StudyHealthReport(
                Status: "Healthy",
                SchemaVersion: _dataStore.SchemaVersion,
                DatabaseStatus: "Connected",
                ModelRouterStatus: _modelRouter != null ? "Available" : "NotConfigured",
                ActiveSessions: activeCount);

            return StudyApiResponse<StudyHealthReport>.Ok(report, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudyHealthReport>.Fail("HEALTH_CHECK_FAILED", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudyPagedResult<StudyCourse>>> GetCoursesAsync(
        string userId = "default",
        EducationLevel? level = null,
        int pageNumber = 1,
        int pageSize = 20,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;

            var allCourses = await _dataStore.ListCoursesAsync(userId, ct);
            if (level.HasValue)
            {
                allCourses = allCourses.Where(c => c.Level == level.Value).ToList();
            }

            var total = allCourses.Count;
            var paged = allCourses.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            var result = new StudyPagedResult<StudyCourse>(paged, pageNumber, pageSize, total);

            return StudyApiResponse<StudyPagedResult<StudyCourse>>.Ok(result, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudyPagedResult<StudyCourse>>.Fail("COURSES_QUERY_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudyCourse>> GetCourseByIdAsync(
        string courseId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(courseId))
            {
                return StudyApiResponse<StudyCourse>.Fail("INVALID_ARGUMENT", "Course ID cannot be empty.", correlationId);
            }

            var course = await _dataStore.GetCourseAsync(courseId, userId, ct);
            if (course == null)
            {
                return StudyApiResponse<StudyCourse>.Fail("NOT_FOUND", $"Course with ID '{courseId}' was not found.", correlationId);
            }

            return StudyApiResponse<StudyCourse>.Ok(course, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudyCourse>.Fail("COURSE_LOOKUP_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudyCourse>> CreateCourseAsync(
        CreateCourseRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return StudyApiResponse<StudyCourse>.Fail("VALIDATION_ERROR", "Course title is required.", correlationId);
            }

            var course = new StudyCourse
            {
                UserId = userId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                Level = request.Level,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            if (!string.IsNullOrWhiteSpace(request.Subject))
            {
                course.Subjects.Add(new StudySubject
                {
                    CourseId = course.Id,
                    Name = request.Subject.Trim(),
                    Description = $"Primary subject for {course.Title}"
                });
            }

            var saved = await _dataStore.SaveCourseAsync(course, userId, ct);
            return StudyApiResponse<StudyCourse>.Ok(saved, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudyCourse>.Fail("CREATE_COURSE_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<IReadOnlyList<StudySession>>> GetSessionsAsync(
        string userId = "default",
        string? courseId = null,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            var sessions = await _dataStore.ListSessionsAsync(userId, courseId, ct);
            return StudyApiResponse<IReadOnlyList<StudySession>>.Ok(sessions, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<IReadOnlyList<StudySession>>.Fail("SESSIONS_QUERY_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudySession>> CreateSessionAsync(
        CreateSessionRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.CourseId))
            {
                return StudyApiResponse<StudySession>.Fail("VALIDATION_ERROR", "Course ID is required to start a session.", correlationId);
            }

            var session = new StudySession
            {
                UserId = userId,
                CourseId = request.CourseId,
                LessonId = request.LessonId,
                Title = request.Title ?? "Interactive Learning Session",
                StartedAtUtc = DateTime.UtcNow,
                Status = StudySessionStatus.Active
            };

            var saved = await _dataStore.SaveSessionAsync(session, userId, ct);

            if (_eventBus != null)
            {
                await _eventBus.PublishAsync(new StudySessionStartedEvent(saved.Id, saved.CourseId, saved.LessonId), ct);
            }

            return StudyApiResponse<StudySession>.Ok(saved, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudySession>.Fail("CREATE_SESSION_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudyProgress>> GetProgressAsync(
        string courseId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(courseId))
            {
                return StudyApiResponse<StudyProgress>.Fail("INVALID_ARGUMENT", "Course ID cannot be empty.", correlationId);
            }

            var progress = await _dataStore.GetProgressAsync(courseId, userId, ct);
            return StudyApiResponse<StudyProgress>.Ok(progress, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudyProgress>.Fail("PROGRESS_LOOKUP_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudyProgress>> RecordProgressAsync(
        RecordProgressRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.CourseId) || string.IsNullOrWhiteSpace(request.LessonId))
            {
                return StudyApiResponse<StudyProgress>.Fail("VALIDATION_ERROR", "Course ID and Lesson ID are required.", correlationId);
            }

            var progress = await _dataStore.GetProgressAsync(request.CourseId, userId, ct);
            progress.CompletedLessonIds.Add(request.LessonId);
            progress.MasteryTier = request.Mastery;
            progress.OverallScore = request.Score;

            var saved = await _dataStore.SaveProgressAsync(progress, userId, ct);

            if (_eventBus != null)
            {
                await _eventBus.PublishAsync(new StudyLessonCompletedEvent(request.CourseId, request.LessonId, request.Score), ct);
                await _eventBus.PublishAsync(new StudyProgressUpdatedEvent(request.CourseId, request.Mastery.ToString(), saved.CompletedLessonIds.Count), ct);
            }

            return StudyApiResponse<StudyProgress>.Ok(saved, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudyProgress>.Fail("RECORD_PROGRESS_ERROR", ex.Message, correlationId);
        }
    }
}
