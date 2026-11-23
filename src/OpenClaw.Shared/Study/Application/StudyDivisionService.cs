using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Memory;
using OpenClaw.Shared.Security.Approvals;
using OpenClaw.Shared.Study.Commands;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Orchestration;
using OpenClaw.Shared.Study.Skills;
using OpenClaw.Shared.Study.Tools;

namespace OpenClaw.Shared.Study.Application;

/// <summary>
/// Primary coordinator service facade for the SPARK Study Division.
/// Seamlessly wires data access, migrations, event dispatch, AI orchestration,
/// skills, tools, slash commands, and desktop user interaction.
/// </summary>
public sealed class StudyDivisionService : IStudyDivisionService, IDisposable
{
    private readonly IStudyDataStore _dataStore;
    private readonly IStudyOrchestrator _orchestrator;
    private readonly IStudyTutorService _tutorService;
    private readonly IStudyApiService _api;
    private readonly IStudyPermissionManager _permissionManager;
    private readonly IStudyEventBus _eventBus;
    private readonly IStudySkillRegistry _skillRegistry;
    private readonly IStudyToolRegistry _toolRegistry;
    private readonly IStudyCommandRegistry _commandRegistry;
    private readonly IStudyCommandParser _commandParser;
    private readonly IStudyExecutionPipeline _executionPipeline;
    private readonly IStudyKnowledgeService _knowledge;
    private readonly IStudyDocumentIngestionService _ingestion;
    private readonly IStudySearchService _search;
    private readonly IStudyResearchService _research;
    private readonly OpenClaw.Shared.Study.Knowledge.StudyGroundedAnswerSynthesizer _groundedAI;
    private readonly IDisposable? _messageSubscription;
    private readonly IDisposable? _progressSubscription;

    private StudyCourse? _activeCourse;
    private StudySession? _activeSession;

    public IStudyDataStore DataStore => _dataStore;
    public IStudyOrchestrator Orchestrator => _orchestrator;
    public IStudyTutorService TutorService => _tutorService;
    public IStudyApiService Api => _api;
    public IStudyPermissionManager PermissionManager => _permissionManager;
    public IStudyEventBus EventBus => _eventBus;
    public IStudySkillRegistry SkillRegistry => _skillRegistry;
    public IStudyToolRegistry ToolRegistry => _toolRegistry;
    public IStudyCommandRegistry CommandRegistry => _commandRegistry;
    public IStudyCommandParser CommandParser => _commandParser;
    public IStudyExecutionPipeline ExecutionPipeline => _executionPipeline;

    public IStudyKnowledgeService Knowledge => _knowledge;
    public IStudyDocumentIngestionService Ingestion => _ingestion;
    public IStudySearchService Search => _search;
    public IStudyResearchService Research => _research;
    public OpenClaw.Shared.Study.Knowledge.StudyGroundedAnswerSynthesizer GroundedAI => _groundedAI;

    public StudyCourse? ActiveCourse => _activeCourse;
    public StudySession? ActiveSession => _activeSession;

    public event EventHandler<StudySessionMessage>? MessageReceived;
    public event EventHandler<StudyProgress>? ProgressUpdated;
    public event EventHandler<StudyCourse?>? ActiveCourseChanged;
    public event EventHandler<StudySession?>? ActiveSessionChanged;

    public StudyDivisionService(
        IStudyDataStore? dataStore = null,
        ISparkModelRouter? modelRouter = null,
        ISparkMemoryStore? memoryStore = null,
        ISparkApprovalManager? approvalManager = null,
        IStudyEventBus? eventBus = null,
        IStudyTutorService? tutorService = null,
        IStudySkillRegistry? skillRegistry = null,
        IStudyToolRegistry? toolRegistry = null,
        IStudyCommandRegistry? commandRegistry = null,
        IStudyCommandParser? commandParser = null,
        IStudyExecutionPipeline? executionPipeline = null)
    {
        _dataStore = dataStore ?? new StudyDataStore();
        _eventBus = eventBus ?? new StudyEventBus();
        _permissionManager = new StudyPermissionManager(approvalManager);

        var intentRouter = new StudyIntentRouter();
        var contextProvider = new StudyContextProvider(_dataStore, memoryStore);

        _orchestrator = new StudyOrchestrator(
            intentRouter,
            contextProvider,
            modelRouter,
            _permissionManager,
            _eventBus);

        _tutorService = tutorService ?? new StudyTutorService(modelRouter, intentRouter);

        _skillRegistry = skillRegistry ?? new StudySkillRegistry(_dataStore);
        _toolRegistry = toolRegistry ?? new StudyToolRegistry(_permissionManager, _dataStore);
        _commandParser = commandParser ?? new StudyCommandParser();
        _commandRegistry = commandRegistry ?? new StudyCommandRegistry(_commandParser, _dataStore);
        _executionPipeline = executionPipeline ?? new StudyExecutionPipeline(
            _commandParser,
            _commandRegistry,
            _skillRegistry,
            _toolRegistry,
            intentRouter,
            _permissionManager,
            _tutorService,
            modelRouter);

        _knowledge = new OpenClaw.Shared.Study.Knowledge.StudyKnowledgeService(_dataStore);
        _ingestion = new OpenClaw.Shared.Study.Knowledge.StudyDocumentIngestionService(_dataStore);
        _search = new OpenClaw.Shared.Study.Knowledge.StudySearchService(_dataStore);
        _research = new OpenClaw.Shared.Study.Research.StudyResearchService(_dataStore);
        _groundedAI = new OpenClaw.Shared.Study.Knowledge.StudyGroundedAnswerSynthesizer(_search);

        _api = new StudyApiService(
            _dataStore,
            modelRouter,
            _eventBus,
            _tutorService,
            _skillRegistry,
            _toolRegistry,
            _commandRegistry,
            _commandParser,
            _executionPipeline,
            _permissionManager,
            _knowledge,
            _ingestion,
            _search,
            _research,
            _groundedAI);

        // Subscribe to internal domain events
        _messageSubscription = _eventBus.Subscribe<StudyMessageReceivedEvent>((evt, ct) =>
        {
            var msg = new StudySessionMessage(
                Guid.NewGuid().ToString("N"),
                string.Equals(evt.Sender, "User", StringComparison.OrdinalIgnoreCase) ? StudyMessageSender.User : StudyMessageSender.Tutor,
                evt.Content,
                evt.TimestampUtc);

            _activeSession?.Messages.Add(msg);
            MessageReceived?.Invoke(this, msg);
            return Task.CompletedTask;
        });

        _progressSubscription = _eventBus.Subscribe<StudyProgressUpdatedEvent>(async (evt, ct) =>
        {
            var prog = await _dataStore.GetProgressAsync(evt.CourseId, evt.UserId, ct);
            ProgressUpdated?.Invoke(this, prog);
        });
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _dataStore.InitializeAsync(ct);
        var courses = await _dataStore.ListCoursesAsync("default", ct);
        if (courses.Count > 0 && _activeCourse == null)
        {
            _activeCourse = courses[0];
            ActiveCourseChanged?.Invoke(this, _activeCourse);
        }
    }

    public async Task SetActiveCourseAsync(string courseId, string userId = "default", CancellationToken ct = default)
    {
        _activeCourse = await _dataStore.GetCourseAsync(courseId, userId, ct);
        ActiveCourseChanged?.Invoke(this, _activeCourse);
    }

    public async Task<StudySession> StartSessionAsync(
        string courseId,
        string? lessonId = null,
        TeachingMode mode = TeachingMode.Standard,
        LearningLanguage language = LearningLanguage.English,
        EducationLevel level = EducationLevel.University,
        string? topic = null,
        string userId = "default",
        CancellationToken ct = default)
    {
        var response = await _api.CreateSessionAsync(
            new CreateSessionRequest(
                CourseId: courseId,
                LessonId: lessonId,
                Title: "Learning Session: " + (_activeCourse?.Title ?? courseId),
                TeachingMode: mode,
                Language: language,
                Level: level,
                Topic: topic),
            userId,
            null,
            ct);

        _activeSession = response.Data ?? new StudySession
        {
            CourseId = courseId,
            LessonId = lessonId,
            TeachingMode = mode,
            Language = language,
            Level = level,
            CurrentTopic = topic
        };

        ActiveSessionChanged?.Invoke(this, _activeSession);
        return _activeSession;
    }

    public async Task<StudySession> ResumeSessionAsync(string sessionId, string userId = "default", CancellationToken ct = default)
    {
        var response = await _api.ResumeSessionAsync(sessionId, new ResumeSessionRequest(), userId, null, ct);
        if (response.Success && response.Data != null)
        {
            _activeSession = response.Data;
            ActiveSessionChanged?.Invoke(this, _activeSession);
            return _activeSession;
        }

        throw new InvalidOperationException($"Failed to resume session '{sessionId}': {response.Error?.Message ?? "Unknown error"}");
    }

    public async Task<StudySession> UpdateActiveSessionPreferencesAsync(
        TeachingMode? mode,
        LearningLanguage? language,
        EducationLevel? level,
        string? topic,
        string userId = "default",
        CancellationToken ct = default)
    {
        if (_activeSession == null)
        {
            throw new InvalidOperationException("No active session exists to update preferences.");
        }

        var response = await _api.UpdateSessionPreferencesAsync(
            _activeSession.Id,
            new UpdateSessionPreferencesRequest(mode, language, level, topic),
            userId,
            null,
            ct);

        if (response.Success && response.Data != null)
        {
            _activeSession = response.Data;
            ActiveSessionChanged?.Invoke(this, _activeSession);
            return _activeSession;
        }

        throw new InvalidOperationException($"Failed to update session preferences: {response.Error?.Message ?? "Unknown error"}");
    }

    public async Task<StudySessionMessage> SendLearnerMessageAsync(
        string prompt,
        TeachingMode? overrideMode = null,
        LearningLanguage? overrideLanguage = null,
        string userId = "default",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (_activeSession == null)
        {
            var courseId = _activeCourse?.Id ?? "course-foundation-cs";
            await StartSessionAsync(courseId, null, TeachingMode.Standard, LearningLanguage.English, EducationLevel.University, null, userId, ct);
        }

        var response = await _api.AddSessionMessageAsync(
            _activeSession!.Id,
            new PostSessionMessageRequest(prompt, overrideMode, overrideLanguage),
            userId,
            null,
            ct);

        if (response.Success && response.Data != null)
        {
            return response.Data;
        }

        throw new InvalidOperationException($"Failed to generate tutor response: {response.Error?.Message ?? "Unknown error"}");
    }

    public async Task<StudyInteractionResult> SendMessageAsync(string prompt, string userId = "default", CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (_activeSession == null)
        {
            var courseId = _activeCourse?.Id ?? "general";
            await StartSessionAsync(courseId, null, TeachingMode.Standard, LearningLanguage.English, EducationLevel.University, null, userId, ct);
        }

        var userMessage = new StudySessionMessage(
            Guid.NewGuid().ToString("N"),
            StudyMessageSender.User,
            prompt,
            DateTime.UtcNow);

        _activeSession!.Messages.Add(userMessage);
        MessageReceived?.Invoke(this, userMessage);

        var request = new StudyInteractionRequest(
            Query: prompt,
            UserId: userId,
            CourseId: _activeSession.CourseId,
            LessonId: _activeSession.LessonId,
            SessionId: _activeSession.Id);

        var result = await _orchestrator.ProcessTurnAsync(request, ct);

        // Save session update
        await _dataStore.SaveSessionAsync(_activeSession, userId, ct);

        return result;
    }

    public async Task<IReadOnlyList<StudyCourse>> LoadCoursesAsync(string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.ListCoursesAsync(userId, ct);
    }

    public async Task<IReadOnlyList<LearningPath>> GetLearningPathsAsync(EducationLevel? level = null, string userId = "default", CancellationToken ct = default)
    {
        var response = await _api.GetLearningPathsAsync(level, userId, null, ct);
        return response.Data ?? [];
    }

    public async Task<IReadOnlyList<LearningObjective>> GetActiveLessonObjectivesAsync(string userId = "default", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_activeSession?.LessonId))
        {
            return [];
        }

        var response = await _api.GetLessonObjectivesAsync(_activeSession.LessonId, userId, null, ct);
        return response.Data ?? [];
    }

    public async Task<PipelineExecutionResult> ExecutePipelineAsync(
        string input,
        TeachingMode? mode = null,
        LearningLanguage? language = null,
        string userId = "default",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (_activeSession == null)
        {
            var courseId = _activeCourse?.Id ?? "course-foundation-cs";
            await StartSessionAsync(courseId, "lesson-asymptotic", mode ?? TeachingMode.Standard, language ?? LearningLanguage.English, EducationLevel.University, null, userId, ct);
        }

        var effectiveMode = mode ?? _activeSession!.TeachingMode;
        var effectiveLang = language ?? _activeSession!.Language;

        // User message
        var userMsg = new StudySessionMessage(
            Id: Guid.NewGuid().ToString("N"),
            Sender: StudyMessageSender.User,
            Content: input,
            TimestampUtc: DateTime.UtcNow,
            Intent: null,
            Language: effectiveLang,
            TeachingMode: effectiveMode);

        _activeSession!.Messages.Add(userMsg);
        MessageReceived?.Invoke(this, userMsg);

        var pipelineReq = new PipelineExecutionRequest(
            input,
            userId,
            _activeSession.Id,
            _activeSession.CourseId,
            _activeSession.LessonId,
            null,
            effectiveMode,
            effectiveLang,
            _activeSession.Level);

        var result = await _executionPipeline.ProcessInputAsync(pipelineReq, ct);

        // Tutor response message
        var tutorMsg = new StudySessionMessage(
            Id: Guid.NewGuid().ToString("N"),
            Sender: StudyMessageSender.Tutor,
            Content: result.FinalResponse,
            TimestampUtc: DateTime.UtcNow,
            Intent: result.SelectedSkill?.Category.ToString(),
            Language: effectiveLang,
            TeachingMode: effectiveMode,
            ExplanationStyle: null,
            ModelUsed: result.SelectedSkill != null ? $"skill:{result.SelectedSkill.Id}" : "spark-pipeline");

        _activeSession.Messages.Add(tutorMsg);
        MessageReceived?.Invoke(this, tutorMsg);

        // Update session
        await _dataStore.SaveSessionAsync(_activeSession, userId, ct);

        return result;
    }

    public void Dispose()
    {
        _messageSubscription?.Dispose();
        _progressSubscription?.Dispose();
    }
}
