using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Study.Commands;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Orchestration;
using OpenClaw.Shared.Study.Skills;
using OpenClaw.Shared.Study.Tools;

namespace OpenClaw.Shared.Study.Application;

/// <summary>
/// Implements backend API handling for /api/v1/study endpoints.
/// </summary>
public sealed class StudyApiService : IStudyApiService
{
    private readonly IStudyDataStore _dataStore;
    private readonly ISparkModelRouter? _modelRouter;
    private readonly IStudyEventBus? _eventBus;
    private readonly IStudyTutorService _tutorService;
    private readonly IStudySkillRegistry _skillRegistry;
    private readonly IStudyToolRegistry _toolRegistry;
    private readonly IStudyCommandRegistry _commandRegistry;
    private readonly IStudyCommandParser _commandParser;
    private readonly IStudyExecutionPipeline _executionPipeline;
    private readonly IStudyPermissionManager _permissionManager;

    public StudyApiService(
        IStudyDataStore dataStore,
        ISparkModelRouter? modelRouter = null,
        IStudyEventBus? eventBus = null,
        IStudyTutorService? tutorService = null,
        IStudySkillRegistry? skillRegistry = null,
        IStudyToolRegistry? toolRegistry = null,
        IStudyCommandRegistry? commandRegistry = null,
        IStudyCommandParser? commandParser = null,
        IStudyExecutionPipeline? executionPipeline = null,
        IStudyPermissionManager? permissionManager = null)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _modelRouter = modelRouter;
        _eventBus = eventBus;
        _tutorService = tutorService ?? new StudyTutorService(modelRouter);

        _permissionManager = permissionManager ?? new StudyPermissionManager();
        _skillRegistry = skillRegistry ?? new StudySkillRegistry(_dataStore);
        _toolRegistry = toolRegistry ?? new StudyToolRegistry(_permissionManager, _dataStore);
        _commandParser = commandParser ?? new StudyCommandParser();
        _commandRegistry = commandRegistry ?? new StudyCommandRegistry(_commandParser, _dataStore);
        _executionPipeline = executionPipeline ?? new StudyExecutionPipeline(
            _commandParser,
            _commandRegistry,
            _skillRegistry,
            _toolRegistry,
            new StudyIntentRouter(),
            _permissionManager,
            _tutorService,
            _modelRouter);
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

    public async Task<StudyApiResponse<StudySession>> GetSessionByIdAsync(
        string sessionId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return StudyApiResponse<StudySession>.Fail("INVALID_ARGUMENT", "Session ID cannot be empty.", correlationId);
            }

            var session = await _dataStore.GetSessionAsync(sessionId, userId, ct);
            if (session == null)
            {
                return StudyApiResponse<StudySession>.Fail("NOT_FOUND", $"Session '{sessionId}' not found.", correlationId);
            }

            return StudyApiResponse<StudySession>.Ok(session, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudySession>.Fail("SESSION_LOOKUP_ERROR", ex.Message, correlationId);
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
                SubjectId = request.SubjectId,
                CurrentTopic = request.Topic,
                TeachingMode = request.TeachingMode,
                Language = request.Language,
                Level = request.Level,
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

    public async Task<StudyApiResponse<StudySession>> ResumeSessionAsync(
        string sessionId,
        ResumeSessionRequest? request = null,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return StudyApiResponse<StudySession>.Fail("INVALID_ARGUMENT", "Session ID cannot be empty.", correlationId);
            }

            var session = await _dataStore.GetSessionAsync(sessionId, userId, ct);
            if (session == null)
            {
                return StudyApiResponse<StudySession>.Fail("NOT_FOUND", $"Session '{sessionId}' not found.", correlationId);
            }

            // User isolation check
            if (!string.Equals(session.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return StudyApiResponse<StudySession>.Fail("FORBIDDEN", "Access denied: session belongs to another user.", correlationId);
            }

            session.Status = StudySessionStatus.Active;
            session.ResumedAtUtc = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(request?.Notes))
            {
                session.Notes = string.IsNullOrWhiteSpace(session.Notes)
                    ? request.Notes
                    : $"{session.Notes}\n[Resumed {DateTime.UtcNow:g}]: {request.Notes}";
            }

            var saved = await _dataStore.SaveSessionAsync(session, userId, ct);
            return StudyApiResponse<StudySession>.Ok(saved, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudySession>.Fail("RESUME_SESSION_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudySession>> UpdateSessionPreferencesAsync(
        string sessionId,
        UpdateSessionPreferencesRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return StudyApiResponse<StudySession>.Fail("INVALID_ARGUMENT", "Session ID cannot be empty.", correlationId);
            }

            var session = await _dataStore.GetSessionAsync(sessionId, userId, ct);
            if (session == null)
            {
                return StudyApiResponse<StudySession>.Fail("NOT_FOUND", $"Session '{sessionId}' not found.", correlationId);
            }

            if (!string.Equals(session.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return StudyApiResponse<StudySession>.Fail("FORBIDDEN", "Access denied: session belongs to another user.", correlationId);
            }

            if (request.TeachingMode.HasValue)
            {
                session.TeachingMode = request.TeachingMode.Value;
            }
            if (request.Language.HasValue)
            {
                session.Language = request.Language.Value;
            }
            if (request.Level.HasValue)
            {
                session.Level = request.Level.Value;
            }
            if (!string.IsNullOrWhiteSpace(request.CurrentTopic))
            {
                session.CurrentTopic = request.CurrentTopic.Trim();
            }

            var saved = await _dataStore.SaveSessionAsync(session, userId, ct);
            return StudyApiResponse<StudySession>.Ok(saved, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudySession>.Fail("UPDATE_SESSION_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudySessionMessage>> AddSessionMessageAsync(
        string sessionId,
        PostSessionMessageRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return StudyApiResponse<StudySessionMessage>.Fail("VALIDATION_ERROR", "Message content cannot be empty.", correlationId);
            }

            var session = await _dataStore.GetSessionAsync(sessionId, userId, ct);
            if (session == null)
            {
                return StudyApiResponse<StudySessionMessage>.Fail("NOT_FOUND", $"Session '{sessionId}' not found.", correlationId);
            }

            if (!string.Equals(session.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return StudyApiResponse<StudySessionMessage>.Fail("FORBIDDEN", "Access denied: session belongs to another user.", correlationId);
            }

            // Apply overrides if specified
            if (request.OverrideMode.HasValue)
            {
                session.TeachingMode = request.OverrideMode.Value;
            }
            if (request.OverrideLanguage.HasValue)
            {
                session.Language = request.OverrideLanguage.Value;
            }

            // 1. Record Learner message
            var userMsg = new StudySessionMessage(
                Id: Guid.NewGuid().ToString("N"),
                Sender: StudyMessageSender.User,
                Content: request.Content.Trim(),
                TimestampUtc: DateTime.UtcNow,
                Language: session.Language,
                TeachingMode: session.TeachingMode);

            session.Messages.Add(userMsg);

            // 2. Load relevant learning objectives if lesson is set
            IReadOnlyList<LearningObjective>? objectives = null;
            if (!string.IsNullOrWhiteSpace(session.LessonId))
            {
                objectives = await _dataStore.ListObjectivesAsync(session.LessonId, userId, ct);
            }

            // 3. Generate pedagogical AI Tutor turn
            var tutorMsg = await _tutorService.GenerateTutorTurnAsync(session, userMsg.Content, objectives, ct);
            session.Messages.Add(tutorMsg);

            // 4. Save session atomically
            await _dataStore.SaveSessionAsync(session, userId, ct);

            // 5. Publish events
            if (_eventBus != null)
            {
                await _eventBus.PublishAsync(new StudyMessageReceivedEvent(session.Id, userMsg.Content, "User"), ct);
                await _eventBus.PublishAsync(new StudyMessageReceivedEvent(session.Id, tutorMsg.Content, "Tutor"), ct);
            }

            return StudyApiResponse<StudySessionMessage>.Ok(tutorMsg, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudySessionMessage>.Fail("ADD_MESSAGE_ERROR", ex.Message, correlationId);
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

    public async Task<StudyApiResponse<IReadOnlyList<LearningPath>>> GetLearningPathsAsync(
        EducationLevel? level = null,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            var paths = await _dataStore.ListLearningPathsAsync(level, userId, ct);
            return StudyApiResponse<IReadOnlyList<LearningPath>>.Ok(paths, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<IReadOnlyList<LearningPath>>.Fail("LEARNING_PATHS_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<LearningPath>> GetLearningPathByIdAsync(
        string pathId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pathId))
            {
                return StudyApiResponse<LearningPath>.Fail("INVALID_ARGUMENT", "Path ID cannot be empty.", correlationId);
            }

            var path = await _dataStore.GetLearningPathAsync(pathId, userId, ct);
            if (path == null)
            {
                return StudyApiResponse<LearningPath>.Fail("NOT_FOUND", $"Learning path '{pathId}' not found.", correlationId);
            }

            return StudyApiResponse<LearningPath>.Ok(path, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<LearningPath>.Fail("LEARNING_PATH_LOOKUP_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<IReadOnlyList<LearningObjective>>> GetLessonObjectivesAsync(
        string lessonId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(lessonId))
            {
                return StudyApiResponse<IReadOnlyList<LearningObjective>>.Fail("INVALID_ARGUMENT", "Lesson ID cannot be empty.", correlationId);
            }

            var objectives = await _dataStore.ListObjectivesAsync(lessonId, userId, ct);
            return StudyApiResponse<IReadOnlyList<LearningObjective>>.Ok(objectives, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<IReadOnlyList<LearningObjective>>.Fail("OBJECTIVES_LOOKUP_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<bool>> MarkObjectiveMasteredAsync(
        string objectiveId,
        bool isMastered = true,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                return StudyApiResponse<bool>.Fail("INVALID_ARGUMENT", "Objective ID cannot be empty.", correlationId);
            }

            var success = await _dataStore.MarkObjectiveMasteredAsync(objectiveId, isMastered, userId, ct);
            return StudyApiResponse<bool>.Ok(success, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<bool>.Fail("MARK_OBJECTIVE_ERROR", ex.Message, correlationId);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Phase 3: Skills, Tools & Slash Commands Endpoints Implementation
    // ══════════════════════════════════════════════════════════════

    public async Task<StudyApiResponse<IReadOnlyList<StudySkillDefinition>>> GetSkillsAsync(
        SkillCategory? category = null,
        bool includeDisabled = false,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            var skills = await _skillRegistry.ListSkillsAsync(category, includeDisabled, userId, ct);
            return StudyApiResponse<IReadOnlyList<StudySkillDefinition>>.Ok(skills, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<IReadOnlyList<StudySkillDefinition>>.Fail("SKILLS_LOOKUP_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudySkillDefinition>> GetSkillByIdAsync(
        string skillId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return StudyApiResponse<StudySkillDefinition>.Fail("INVALID_ARGUMENT", "Skill ID cannot be empty.", correlationId);
            }

            var skill = await _skillRegistry.GetSkillAsync(skillId, userId, ct);
            if (skill == null)
            {
                return StudyApiResponse<StudySkillDefinition>.Fail("NOT_FOUND", $"Skill '{skillId}' not found.", correlationId);
            }

            return StudyApiResponse<StudySkillDefinition>.Ok(skill, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudySkillDefinition>.Fail("SKILL_LOOKUP_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<bool>> ToggleSkillAsync(
        string skillId,
        ToggleSkillRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return StudyApiResponse<bool>.Fail("INVALID_ARGUMENT", "Skill ID cannot be empty.", correlationId);
            }

            var success = await _skillRegistry.SetSkillEnabledAsync(skillId, request.IsEnabled, userId, ct);
            if (!success)
            {
                return StudyApiResponse<bool>.Fail("NOT_FOUND", $"Skill '{skillId}' not found to toggle.", correlationId);
            }

            return StudyApiResponse<bool>.Ok(true, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<bool>.Fail("SKILL_TOGGLE_ERROR", ex.Message, correlationId);
        }
    }

    public Task<StudyApiResponse<IReadOnlyList<StudyToolDefinition>>> GetToolsAsync(
        ToolCategory? category = null,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            var tools = _toolRegistry.ListTools(category);
            return Task.FromResult(StudyApiResponse<IReadOnlyList<StudyToolDefinition>>.Ok(tools, correlationId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(StudyApiResponse<IReadOnlyList<StudyToolDefinition>>.Fail("TOOLS_LOOKUP_ERROR", ex.Message, correlationId));
        }
    }

    public Task<StudyApiResponse<StudyToolDefinition>> GetToolByIdAsync(
        string toolId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(toolId))
            {
                return Task.FromResult(StudyApiResponse<StudyToolDefinition>.Fail("INVALID_ARGUMENT", "Tool ID cannot be empty.", correlationId));
            }

            var tool = _toolRegistry.GetTool(toolId);
            if (tool == null)
            {
                return Task.FromResult(StudyApiResponse<StudyToolDefinition>.Fail("NOT_FOUND", $"Tool '{toolId}' not found.", correlationId));
            }

            return Task.FromResult(StudyApiResponse<StudyToolDefinition>.Ok(tool, correlationId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(StudyApiResponse<StudyToolDefinition>.Fail("TOOL_LOOKUP_ERROR", ex.Message, correlationId));
        }
    }

    public async Task<StudyApiResponse<StudyToolExecutionResult>> ExecuteToolAsync(
        ExecuteToolApiRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ToolId))
            {
                return StudyApiResponse<StudyToolExecutionResult>.Fail("INVALID_ARGUMENT", "Tool ID must be specified.", correlationId);
            }

            var execReq = new StudyToolExecutionRequest(
                request.ToolId,
                request.Arguments,
                userId,
                request.SessionId);

            var result = await _toolRegistry.ExecuteToolAsync(execReq, ct);
            return StudyApiResponse<StudyToolExecutionResult>.Ok(result, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudyToolExecutionResult>.Fail("TOOL_EXECUTION_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudyToolExecutionResult>> ApproveExecutionAsync(
        string requestId,
        ApproveExecutionRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return StudyApiResponse<StudyToolExecutionResult>.Fail("INVALID_ARGUMENT", "Request ID cannot be empty.", correlationId);
            }

            var approvedBy = string.IsNullOrWhiteSpace(request?.ApprovedBy) ? userId : request.ApprovedBy;
            var result = await _toolRegistry.ApproveAndExecutePendingAsync(requestId, approvedBy, userId, ct);
            return StudyApiResponse<StudyToolExecutionResult>.Ok(result, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudyToolExecutionResult>.Fail("APPROVAL_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudyToolExecutionResult>> RejectExecutionAsync(
        string requestId,
        RejectExecutionRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return StudyApiResponse<StudyToolExecutionResult>.Fail("INVALID_ARGUMENT", "Request ID cannot be empty.", correlationId);
            }

            var reason = request?.Reason ?? "Execution rejected by user.";
            var rejectedBy = string.IsNullOrWhiteSpace(request?.RejectedBy) ? userId : request.RejectedBy;

            var result = await _toolRegistry.RejectPendingAsync(requestId, reason, rejectedBy, userId, ct);
            return StudyApiResponse<StudyToolExecutionResult>.Ok(result, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudyToolExecutionResult>.Fail("REJECTION_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<bool>> CancelExecutionAsync(
        string requestId,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return StudyApiResponse<bool>.Fail("INVALID_ARGUMENT", "Request ID cannot be empty.", correlationId);
            }

            var cancelled = await _toolRegistry.CancelExecutionAsync(requestId, userId, ct);
            return StudyApiResponse<bool>.Ok(cancelled, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<bool>.Fail("CANCEL_ERROR", ex.Message, correlationId);
        }
    }

    public Task<StudyApiResponse<IReadOnlyList<StudyCommandDefinition>>> GetCommandsAsync(
        CommandCategory? category = null,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            var commands = _commandRegistry.ListCommands(category);
            return Task.FromResult(StudyApiResponse<IReadOnlyList<StudyCommandDefinition>>.Ok(commands, correlationId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(StudyApiResponse<IReadOnlyList<StudyCommandDefinition>>.Fail("COMMANDS_LOOKUP_ERROR", ex.Message, correlationId));
        }
    }

    public Task<StudyApiResponse<ParsedStudyCommand>> ResolveCommandAsync(
        ResolveCommandRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RawInput))
            {
                return Task.FromResult(StudyApiResponse<ParsedStudyCommand>.Fail("INVALID_ARGUMENT", "Command input must be specified.", correlationId));
            }

            var parsed = _commandParser.Parse(request.RawInput, _commandRegistry.ListCommands());
            return Task.FromResult(StudyApiResponse<ParsedStudyCommand>.Ok(parsed, correlationId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(StudyApiResponse<ParsedStudyCommand>.Fail("COMMAND_PARSE_ERROR", ex.Message, correlationId));
        }
    }

    public async Task<StudyApiResponse<PipelineExecutionResult>> ExecuteCommandAsync(
        ExecuteCommandApiRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CommandInput))
            {
                return StudyApiResponse<PipelineExecutionResult>.Fail("INVALID_ARGUMENT", "Command input must be specified.", correlationId);
            }

            var pipelineReq = new PipelineExecutionRequest(
                request.CommandInput,
                userId,
                request.SessionId);

            var result = await _executionPipeline.ProcessInputAsync(pipelineReq, ct);
            return StudyApiResponse<PipelineExecutionResult>.Ok(result, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<PipelineExecutionResult>.Fail("COMMAND_EXECUTION_ERROR", ex.Message, correlationId);
        }
    }

    public Task<StudyApiResponse<IReadOnlyList<CommandAutocompleteItem>>> GetCommandAutocompleteAsync(
        string partialInput,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            var suggestions = _commandRegistry.GetAutocompleteSuggestions(partialInput);
            return Task.FromResult(StudyApiResponse<IReadOnlyList<CommandAutocompleteItem>>.Ok(suggestions, correlationId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(StudyApiResponse<IReadOnlyList<CommandAutocompleteItem>>.Fail("AUTOCOMPLETE_ERROR", ex.Message, correlationId));
        }
    }

    public async Task<StudyApiResponse<PipelineExecutionResult>> ProcessPipelineAsync(
        ExecutePipelineApiRequest request,
        string userId = "default",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UserInput))
            {
                return StudyApiResponse<PipelineExecutionResult>.Fail("INVALID_ARGUMENT", "User input cannot be empty.", correlationId);
            }

            var pipelineReq = new PipelineExecutionRequest(
                request.UserInput,
                userId,
                request.SessionId,
                request.CourseId,
                null,
                request.ForceSkillId,
                request.TeachingMode,
                request.Language,
                request.Level);

            var result = await _executionPipeline.ProcessInputAsync(pipelineReq, ct);
            return StudyApiResponse<PipelineExecutionResult>.Ok(result, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<PipelineExecutionResult>.Fail("PIPELINE_ERROR", ex.Message, correlationId);
        }
    }

    public async Task<StudyApiResponse<StudyExecutionHistoryReport>> GetExecutionHistoryAsync(
        string userId = "default",
        int limit = 50,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            var skillHist = await _skillRegistry.GetSkillExecutionHistoryAsync(null, userId, limit, ct);
            var toolHist = await _toolRegistry.GetToolExecutionHistoryAsync(null, userId, limit, ct);
            var cmdHist = await _commandRegistry.GetCommandExecutionHistoryAsync(null, userId, limit, ct);

            var report = new StudyExecutionHistoryReport(skillHist, toolHist, cmdHist);
            return StudyApiResponse<StudyExecutionHistoryReport>.Ok(report, correlationId);
        }
        catch (Exception ex)
        {
            return StudyApiResponse<StudyExecutionHistoryReport>.Fail("HISTORY_LOOKUP_ERROR", ex.Message, correlationId);
        }
    }
}
