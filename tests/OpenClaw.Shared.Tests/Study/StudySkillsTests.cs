using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Skills;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudySkillsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudyDataStore _dataStore;
    private readonly StudySkillRegistry _skillRegistry;

    public StudySkillsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spark_skills_tests_" + Guid.NewGuid().ToString("N"));
        _dataStore = new StudyDataStore(_tempDir);
        _skillRegistry = new StudySkillRegistry(_dataStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task BuiltInSkills_ContainsAllFiveCategories_AndAtLeast35Skills()
    {
        var allSkills = await _skillRegistry.ListSkillsAsync(includeDisabled: true);

        Assert.True(allSkills.Count >= 35, $"Expected at least 35 skills, got {allSkills.Count}");

        var categories = allSkills.Select(s => s.Category).Distinct().ToList();
        Assert.Contains(SkillCategory.Learning, categories);
        Assert.Contains(SkillCategory.Academic, categories);
        Assert.Contains(SkillCategory.Research, categories);
        Assert.Contains(SkillCategory.Technical, categories);
        Assert.Contains(SkillCategory.Productivity, categories);
    }

    [Fact]
    public async Task GetSkillAsync_ReturnsSpecificSkill_WithStructuredMetadata()
    {
        var skill = await _skillRegistry.GetSkillAsync("concept-explainer");

        Assert.NotNull(skill);
        Assert.Equal("Concept Explainer", skill.Name);
        Assert.Equal(SkillCategory.Learning, skill.Category);
        Assert.NotEmpty(skill.Instructions);
        Assert.NotEmpty(skill.TriggerConditions);
        Assert.NotEmpty(skill.AllowedTools);
        Assert.Equal(SkillStatus.Active, skill.Status);
    }

    [Fact]
    public async Task SearchSkillsAsync_FindsRelevantSkillsByKeyword()
    {
        var mathSkills = await _skillRegistry.SearchSkillsAsync("math");
        Assert.NotEmpty(mathSkills);
        Assert.Contains(mathSkills, s => s.Id.Contains("math") || s.Description.Contains("math", StringComparison.OrdinalIgnoreCase));

        var socraticSkills = await _skillRegistry.SearchSkillsAsync("socratic");
        Assert.NotEmpty(socraticSkills);
        Assert.Contains(socraticSkills, s => s.Id.Contains("socratic"));
    }

    [Fact]
    public async Task SetSkillEnabledAsync_TogglesSkillStatusCorrectly()
    {
        var targetSkillId = "flashcard-creator";

        var disabled = await _skillRegistry.SetSkillEnabledAsync(targetSkillId, false);
        Assert.True(disabled);

        var skillAfterDisable = await _skillRegistry.GetSkillAsync(targetSkillId);
        Assert.NotNull(skillAfterDisable);
        Assert.Equal(SkillStatus.Disabled, skillAfterDisable.Status);

        // When listing active skills, disabled skill should be omitted
        var activeSkills = await _skillRegistry.ListSkillsAsync(includeDisabled: false);
        Assert.DoesNotContain(activeSkills, s => s.Id == targetSkillId);

        // Re-enable
        var reEnabled = await _skillRegistry.SetSkillEnabledAsync(targetSkillId, true);
        Assert.True(reEnabled);

        var skillAfterReEnable = await _skillRegistry.GetSkillAsync(targetSkillId);
        Assert.NotNull(skillAfterReEnable);
        Assert.Equal(SkillStatus.Active, skillAfterReEnable.Status);
    }

    [Fact]
    public async Task ValidateSkillDefinitionAsync_RejectsInvalidSkillDefinitions()
    {
        var invalidSkill = new StudySkillDefinition(
            id: "", // Invalid empty ID
            name: "", // Invalid empty Name
            description: "", // Invalid empty Description
            category: SkillCategory.Learning,
            version: "1.0.0",
            instructions: "" // Invalid empty Instructions
        );

        var errors = await _skillRegistry.ValidateSkillDefinitionAsync(invalidSkill);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("ID", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Description", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Instructions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecordSkillExecutionAsync_PersistsAndRetrievesAuditHistory()
    {
        var record = new SkillExecutionRecord(
            executionId: "exec-" + Guid.NewGuid().ToString("N")[..8],
            skillId: "skill-feynman-technique",
            userId: "test-user-1",
            sessionId: "sess-101",
            startedAtUtc: DateTime.UtcNow.AddSeconds(-2),
            completedAtUtc: DateTime.UtcNow,
            status: ExecutionStatus.Completed,
            durationMs: 145,
            inputsSummary: "Explain quantum superposition",
            outputsSummary: "Imagine a coin spinning on a table...",
            errorMessage: null
        );

        await _skillRegistry.RecordSkillExecutionAsync(record, "test-user-1");

        var history = await _skillRegistry.GetSkillExecutionHistoryAsync("skill-feynman-technique", "test-user-1");
        Assert.NotEmpty(history);
        var latest = history.First();
        Assert.Equal(record.ExecutionId, latest.ExecutionId);
        Assert.Equal("skill-feynman-technique", latest.SkillId);
        Assert.Equal(ExecutionStatus.Completed, latest.Status);
        Assert.Equal(145, latest.DurationMs);
    }

    [Fact]
    public async Task RegisterSkillAsync_AddsCustomSkillSuccessfully()
    {
        var customSkill = new StudySkillDefinition(
            id: "skill-custom-exam-cram",
            name: "High Yield Exam Cram",
            description: "Rapid review of high-probability exam concepts.",
            category: SkillCategory.Academic,
            version: "1.0.0",
            instructions: "Focus exclusively on formulas and definitions.",
            triggerConditions: ["exam cram", "quick review"],
            requiredContext: ["CurriculumObjectives"],
            allowedTools: ["tool-calculator", "tool-flashcards"],
            requiredPermissions: [StudyPermissionKind.ReadOnly]
        );

        await _skillRegistry.RegisterSkillAsync(customSkill, "test-user-custom");

        var retrieved = await _skillRegistry.GetSkillAsync("skill-custom-exam-cram", "test-user-custom");
        Assert.NotNull(retrieved);
        Assert.Equal("High Yield Exam Cram", retrieved.Name);
        Assert.Equal(SkillCategory.Academic, retrieved.Category);
        Assert.Contains("tool-flashcards", retrieved.AllowedTools);
    }
}
