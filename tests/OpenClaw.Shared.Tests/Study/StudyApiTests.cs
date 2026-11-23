using System;
using System.IO;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Application;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyApiTests : IDisposable
{
    private readonly string _testRoot;
    private readonly StudyDataStore _store;
    private readonly StudyApiService _api;

    public StudyApiTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SparkStudyApiTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _store = new StudyDataStore(_testRoot);
        _api = new StudyApiService(_store);
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
    public async Task GetHealthAsync_ReturnsHealthyAndValidSchema()
    {
        await _store.InitializeAsync();

        var response = await _api.GetHealthAsync("test-corr-1");

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Healthy", response.Data.Status);
        Assert.Equal(_store.SchemaVersion, response.Data.SchemaVersion);
        Assert.Equal("test-corr-1", response.CorrelationId);
    }

    [Fact]
    public async Task GetCoursesAsync_ReturnsPagedResultsWithPaginationMetadata()
    {
        await _store.InitializeAsync();

        var response = await _api.GetCoursesAsync(userId: "default", pageNumber: 1, pageSize: 5);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.NotEmpty(response.Data.Items);
        Assert.Equal(1, response.Data.PageNumber);
        Assert.True(response.Data.TotalCount >= 1);
    }

    [Fact]
    public async Task CreateCourseAsync_WithValidPayload_Succeeds()
    {
        await _store.InitializeAsync();

        var req = new CreateCourseRequest(
            Title: "Discrete Mathematics",
            Description: "Logic, proofs, and set theory.",
            Level: EducationLevel.University,
            Subject: "Mathematics");

        var response = await _api.CreateCourseAsync(req, "test-user");

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Discrete Mathematics", response.Data.Title);
        Assert.Single(response.Data.Subjects);
        Assert.Equal("Mathematics", response.Data.Subjects[0].Name);
    }

    [Fact]
    public async Task CreateCourseAsync_WithEmptyTitle_FailsValidation()
    {
        await _store.InitializeAsync();

        var req = new CreateCourseRequest(
            Title: "",
            Description: "No title provided",
            Level: EducationLevel.School);

        var response = await _api.CreateCourseAsync(req, "test-user");

        Assert.False(response.Success);
        Assert.NotNull(response.Error);
        Assert.Equal("VALIDATION_ERROR", response.Error.Code);
    }

    [Fact]
    public async Task RecordProgressAsync_UpdatesProgressAndReturnsEnvelope()
    {
        await _store.InitializeAsync();

        var req = new RecordProgressRequest(
            CourseId: "course-foundation-cs",
            LessonId: "lesson-asymptotic",
            Score: 98.0,
            Mastery: MasteryTier.Master);

        var response = await _api.RecordProgressAsync(req, "student-1");

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Contains("lesson-asymptotic", response.Data.CompletedLessonIds);
        Assert.Equal(MasteryTier.Master, response.Data.MasteryTier);
        Assert.Equal(98.0, response.Data.OverallScore);
    }
}
