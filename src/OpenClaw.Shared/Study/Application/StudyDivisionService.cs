using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Memory;
using OpenClaw.Shared.Security.Approvals;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Orchestration;

namespace OpenClaw.Shared.Study.Application;

/// <summary>
/// Primary coordinator service facade for the SPARK Study Division.
/// Seamlessly wires data access, migrations, event dispatch, AI orchestration,
/// and desktop user interaction.
/// </summary>
public sealed class StudyDivisionService : IStudyDivisionService, IDisposable
{
    private readonly IStudyDataStore _dataStore;
    private readonly IStudyOrchestrator _orchestrator;
    private readonly IStudyApiService _api;
    private readonly IStudyPermissionManager _permissionManager;
    private readonly IStudyEventBus _eventBus;
    private readonly IDisposable? _messageSubscription;
    private readonly IDisposable? _progressSubscription;

    private StudyCourse? _activeCourse;
    private StudySession? _activeSession;

    public IStudyDataStore DataStore => _dataStore;
    public IStudyOrchestrator Orchestrator => _orchestrator;
    public IStudyApiService Api => _api;
    public IStudyPermissionManager PermissionManager => _permissionManager;
    public IStudyEventBus EventBus => _eventBus;

    public StudyCourse? ActiveCourse => _activeCourse;
    public StudySession? ActiveSession => _activeSession;

    public event EventHandler<StudySessionMessage>? MessageReceived;
    public event EventHandler<StudyProgress>? ProgressUpdated;
    public event EventHandler<StudyCourse?>? ActiveCourseChanged;

    public StudyDivisionService(
        IStudyDataStore? dataStore = null,
        ISparkModelRouter? modelRouter = null,
        ISparkMemoryStore? memoryStore = null,
        ISparkApprovalManager? approvalManager = null,
        IStudyEventBus? eventBus = null)
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

        _api = new StudyApiService(_dataStore, modelRouter, _eventBus);

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

    public async Task<StudySession> StartSessionAsync(string courseId, string? lessonId = null, string userId = "default", CancellationToken ct = default)
    {
        var response = await _api.CreateSessionAsync(
            new CreateSessionRequest(courseId, lessonId, "Learning Session: " + (_activeCourse?.Title ?? courseId)),
            userId,
            null,
            ct);

        _activeSession = response.Data ?? new StudySession { CourseId = courseId, LessonId = lessonId };
        return _activeSession;
    }

    public async Task<StudyInteractionResult> SendMessageAsync(string prompt, string userId = "default", CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (_activeSession == null)
        {
            var courseId = _activeCourse?.Id ?? "general";
            await StartSessionAsync(courseId, null, userId, ct);
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

    public void Dispose()
    {
        _messageSubscription?.Dispose();
        _progressSubscription?.Dispose();
    }
}
