using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Application;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyLearningPathTests : IDisposable
{
    private readonly string _testRoot;
    private readonly IStudyDataStore _store;
    private readonly IStudyApiService _api;

    public StudyLearningPathTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "spark_study_path_tests_" + Guid.NewGuid().ToString("N"));
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
    public async Task Migration1_1_0_AppliesBothMigrationsAndSeedsObjectivesAndPaths()
    {
        await _store.InitializeAsync();

        var migrationRunner = new StudyDatabaseMigrations(_testRoot);
        var applied = await migrationRunner.GetAppliedMigrationsAsync();

        Assert.Equal(2, applied.Count);
        Assert.Contains(applied, m => m.Version == "1.0.0");
        Assert.Contains(applied, m => m.Version == "1.1.0");

        // Verify seeded objectives
        var objectives = await _store.ListObjectivesAsync(userId: "default");
        Assert.True(objectives.Count >= 4);
        Assert.Contains(objectives, o => o.LessonId == "lesson-asymptotic");
        Assert.Contains(objectives, o => o.LessonId == "lesson-recursion");

        // Verify seeded paths
        var paths = await _store.ListLearningPathsAsync(userId: "default");
        Assert.True(paths.Count >= 3);
        Assert.Contains(paths, p => p.Level == EducationLevel.University);
        Assert.Contains(paths, p => p.Level == EducationLevel.School);
        Assert.Contains(paths, p => p.Level == EducationLevel.Professional);
    }

    [Fact]
    public async Task GetLessonObjectives_ReturnsOnlyObjectivesForSpecifiedLesson()
    {
        await _store.InitializeAsync();

        var response = await _api.GetLessonObjectivesAsync("lesson-asymptotic", userId: "default");

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.NotEmpty(response.Data);
        Assert.All(response.Data, obj => Assert.Equal("lesson-asymptotic", obj.LessonId));
    }

    [Fact]
    public async Task MarkObjectiveMastered_UpdatesStateSuccessfully()
    {
        await _store.InitializeAsync();

        var objectives = await _store.ListObjectivesAsync("lesson-asymptotic", userId: "default");
        var target = objectives.First();
        Assert.False(target.IsMastered);

        var markResp = await _api.MarkObjectiveMasteredAsync(target.Id, isMastered: true, userId: "default");
        Assert.True(markResp.Success);
        Assert.True(markResp.Data);

        var refreshed = await _store.GetObjectiveAsync(target.Id, userId: "default");
        Assert.NotNull(refreshed);
        Assert.True(refreshed.IsMastered);
    }

    [Fact]
    public async Task GetLearningPaths_FiltersByEducationLevel()
    {
        await _store.InitializeAsync();

        var schoolPaths = await _api.GetLearningPathsAsync(EducationLevel.School, userId: "default");
        Assert.True(schoolPaths.Success);
        Assert.NotNull(schoolPaths.Data);
        Assert.All(schoolPaths.Data, p => Assert.Equal(EducationLevel.School, p.Level));

        var uniPaths = await _api.GetLearningPathsAsync(EducationLevel.University, userId: "default");
        Assert.True(uniPaths.Success);
        Assert.NotNull(uniPaths.Data);
        Assert.All(uniPaths.Data, p => Assert.Equal(EducationLevel.University, p.Level));
    }

    [Fact]
    public async Task SaveLearningPath_CreatesAndRetrievesCustomPath()
    {
        await _store.InitializeAsync();

        var customPath = new LearningPath
        {
            Id = "path-custom-ai-research",
            Title = "Doctoral Foundations in Autonomous Agentics",
            Description = "Advanced research curriculum for AI agents.",
            Level = EducationLevel.Research,
            TargetAudience = "Doctoral Candidates & Research Scientists",
            OrderedCourseIds = ["course-foundation-cs"],
            OrderedLessonIds = ["lesson-asymptotic", "lesson-recursion"],
            EstimatedTotalHours = 120
        };

        var saved = await _store.SaveLearningPathAsync(customPath, userId: "default");
        Assert.NotNull(saved);

        var retrieved = await _api.GetLearningPathByIdAsync("path-custom-ai-research", userId: "default");
        Assert.True(retrieved.Success);
        Assert.NotNull(retrieved.Data);
        Assert.Equal("Doctoral Foundations in Autonomous Agentics", retrieved.Data.Title);
        Assert.Equal(EducationLevel.Research, retrieved.Data.Level);
        Assert.Equal(120, retrieved.Data.EstimatedTotalHours);
    }
}
