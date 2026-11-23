using System;
using System.IO;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Application;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Orchestration;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudySessionResumeTests : IDisposable
{
    private readonly string _testRoot;
    private readonly IStudyDataStore _store;
    private readonly IStudyApiService _api;

    public StudySessionResumeTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "spark_study_session_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _store = new StudyDataStore(_testRoot);
        _api = new StudyApiService(_store, modelRouter: null, eventBus: null, tutorService: new StudyTutorService());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task CreateSession_WithTeachingModeAndLanguage_PersistsCorrectly()
    {
        await _store.InitializeAsync();

        var request = new CreateSessionRequest(
            CourseId: "course-foundation-cs",
            LessonId: "lesson-asymptotic",
            Title: "Advanced Complexity Session",
            TeachingMode: TeachingMode.Socratic,
            Language: LearningLanguage.Malayalam,
            Level: EducationLevel.University,
            Topic: "Big-O Analysis");

        var response = await _api.CreateSessionAsync(request, userId: "user-alpha");

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(TeachingMode.Socratic, response.Data.TeachingMode);
        Assert.Equal(LearningLanguage.Malayalam, response.Data.Language);
        Assert.Equal(EducationLevel.University, response.Data.Level);
        Assert.Equal("Big-O Analysis", response.Data.CurrentTopic);
        Assert.Equal(StudySessionStatus.Active, response.Data.Status);
    }

    [Fact]
    public async Task AddSessionMessage_InvokesTutorAndPersistsBothMessages()
    {
        await _store.InitializeAsync();

        var sessionResp = await _api.CreateSessionAsync(
            new CreateSessionRequest("course-foundation-cs", "lesson-asymptotic", "Test Session"),
            userId: "user-alpha");

        var sessionId = sessionResp.Data!.Id;

        var messageResp = await _api.AddSessionMessageAsync(
            sessionId,
            new PostSessionMessageRequest("Explain asymptotic bounds to me"),
            userId: "user-alpha");

        Assert.True(messageResp.Success);
        Assert.NotNull(messageResp.Data);
        Assert.Equal(StudyMessageSender.Tutor, messageResp.Data.Sender);
        Assert.False(string.IsNullOrWhiteSpace(messageResp.Data.Content));

        // Verify session now has 2 messages (User + Tutor)
        var updatedSession = await _store.GetSessionAsync(sessionId, userId: "user-alpha");
        Assert.NotNull(updatedSession);
        Assert.Equal(2, updatedSession.Messages.Count);
        Assert.Equal(StudyMessageSender.User, updatedSession.Messages[0].Sender);
        Assert.Equal("Explain asymptotic bounds to me", updatedSession.Messages[0].Content);
        Assert.Equal(StudyMessageSender.Tutor, updatedSession.Messages[1].Sender);
    }

    [Fact]
    public async Task ResumeSession_SetsResumedAtAndActivatesStatus()
    {
        await _store.InitializeAsync();

        var sessionResp = await _api.CreateSessionAsync(
            new CreateSessionRequest("course-foundation-cs", null, "Session To Pause"),
            userId: "user-alpha");

        var session = sessionResp.Data!;
        session.Status = StudySessionStatus.Paused;
        await _store.SaveSessionAsync(session, userId: "user-alpha");

        var resumeResp = await _api.ResumeSessionAsync(
            session.Id,
            new ResumeSessionRequest(Notes: "Resuming after revision break"),
            userId: "user-alpha");

        Assert.True(resumeResp.Success);
        Assert.NotNull(resumeResp.Data);
        Assert.Equal(StudySessionStatus.Active, resumeResp.Data.Status);
        Assert.NotNull(resumeResp.Data.ResumedAtUtc);
        Assert.Contains("Resuming after revision break", resumeResp.Data.Notes);
    }

    [Fact]
    public async Task UpdateSessionPreferences_UpdatesModeLanguageAndLevel()
    {
        await _store.InitializeAsync();

        var sessionResp = await _api.CreateSessionAsync(
            new CreateSessionRequest("course-foundation-cs", null, "Preference Session"),
            userId: "user-alpha");

        var sessionId = sessionResp.Data!.Id;

        var updateResp = await _api.UpdateSessionPreferencesAsync(
            sessionId,
            new UpdateSessionPreferencesRequest(
                TeachingMode: TeachingMode.StepByStep,
                Language: LearningLanguage.Malayalam,
                Level: EducationLevel.Technical,
                CurrentTopic: "Memory Management"),
            userId: "user-alpha");

        Assert.True(updateResp.Success);
        Assert.NotNull(updateResp.Data);
        Assert.Equal(TeachingMode.StepByStep, updateResp.Data.TeachingMode);
        Assert.Equal(LearningLanguage.Malayalam, updateResp.Data.Language);
        Assert.Equal(EducationLevel.Technical, updateResp.Data.Level);
        Assert.Equal("Memory Management", updateResp.Data.CurrentTopic);
    }

    [Fact]
    public async Task UserIsolation_OtherUserCannotResumeOrMessageSession()
    {
        await _store.InitializeAsync();

        var sessionResp = await _api.CreateSessionAsync(
            new CreateSessionRequest("course-foundation-cs", null, "Private Session"),
            userId: "user-owner");

        var sessionId = sessionResp.Data!.Id;

        // Another user tries to resume
        var resumeAttempt = await _api.ResumeSessionAsync(
            sessionId,
            new ResumeSessionRequest(),
            userId: "user-attacker");

        Assert.False(resumeAttempt.Success);
        Assert.Equal("NOT_FOUND", resumeAttempt.Error?.Code);

        // Another user tries to send message
        var messageAttempt = await _api.AddSessionMessageAsync(
            sessionId,
            new PostSessionMessageRequest("Unauthorized prompt"),
            userId: "user-attacker");

        Assert.False(messageAttempt.Success);
        Assert.Equal("NOT_FOUND", messageAttempt.Error?.Code);
    }
}
