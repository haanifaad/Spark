using System;
using System.IO;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyDataStoreTests : IDisposable
{
    private readonly string _testRoot;
    private readonly StudyDataStore _store;

    public StudyDataStoreTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SparkStudyTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _store = new StudyDataStore(_testRoot);
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
    public async Task InitializeAsync_AppliesVersion1MigrationAndSeedsSampleCourse()
    {
        await _store.InitializeAsync();

        var courses = await _store.ListCoursesAsync("default");
        Assert.NotEmpty(courses);

        var first = courses[0];
        Assert.Equal("course-foundation-cs", first.Id);
        Assert.Equal("Computer Science Foundations & AI", first.Title);
        Assert.NotEmpty(first.Subjects);
    }

    [Fact]
    public async Task CourseOperations_SaveAndRetrieve_AreConsistent()
    {
        await _store.InitializeAsync();

        var newCourse = new StudyCourse
        {
            Id = "quantum-101",
            UserId = "user-alice",
            Title = "Quantum Computing Fundamentals",
            Description = "Qubits, superposition, and quantum gates.",
            Level = EducationLevel.University
        };

        var saved = await _store.SaveCourseAsync(newCourse, "user-alice");
        Assert.NotNull(saved);

        var fetched = await _store.GetCourseAsync("quantum-101", "user-alice");
        Assert.NotNull(fetched);
        Assert.Equal("Quantum Computing Fundamentals", fetched.Title);

        var allAlice = await _store.ListCoursesAsync("user-alice");
        Assert.Single(allAlice);

        // Verify multi-user isolation: default user cannot see alice's custom course
        var defaultCourses = await _store.ListCoursesAsync("default");
        Assert.DoesNotContain(defaultCourses, c => c.Id == "quantum-101");

        // Delete course
        var deleted = await _store.DeleteCourseAsync("quantum-101", "user-alice");
        Assert.True(deleted);

        var afterDelete = await _store.GetCourseAsync("quantum-101", "user-alice");
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task SessionOperations_TrackMessagesAndLifecycle()
    {
        await _store.InitializeAsync();

        var session = new StudySession
        {
            Id = "session-test-1",
            UserId = "user-bob",
            CourseId = "course-foundation-cs",
            Title = "Algorithms Deep Dive",
            Status = StudySessionStatus.Active
        };

        session.Messages.Add(new StudySessionMessage(
            Guid.NewGuid().ToString("N"),
            StudyMessageSender.User,
            "Can you explain quicksort?",
            DateTime.UtcNow));

        await _store.SaveSessionAsync(session, "user-bob");

        var loaded = await _store.GetSessionAsync("session-test-1", "user-bob");
        Assert.NotNull(loaded);
        Assert.Equal("Algorithms Deep Dive", loaded.Title);
        Assert.Single(loaded.Messages);
        Assert.Equal("Can you explain quicksort?", loaded.Messages[0].Content);
    }

    [Fact]
    public async Task ProgressOperations_RecordCompletionsAndMastery()
    {
        await _store.InitializeAsync();

        var progress = await _store.GetProgressAsync("course-foundation-cs", "user-carol");
        Assert.NotNull(progress);
        Assert.Empty(progress.CompletedLessonIds);

        progress.CompletedLessonIds.Add("lesson-asymptotic");
        progress.CompletedLessonIds.Add("lesson-recursion");
        progress.MasteryTier = MasteryTier.Proficient;
        progress.OverallScore = 94.5;

        await _store.SaveProgressAsync(progress, "user-carol");

        var updated = await _store.GetProgressAsync("course-foundation-cs", "user-carol");
        Assert.Equal(2, updated.CompletedLessonIds.Count);
        Assert.Equal(MasteryTier.Proficient, updated.MasteryTier);
        Assert.Equal(94.5, updated.OverallScore);
    }
}
