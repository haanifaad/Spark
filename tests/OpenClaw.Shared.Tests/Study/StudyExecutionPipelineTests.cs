using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Commands;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Orchestration;
using OpenClaw.Shared.Study.Skills;
using OpenClaw.Shared.Study.Tools;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyExecutionPipelineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudyDataStore _dataStore;
    private readonly StudyPermissionManager _permissionManager;
    private readonly StudySkillRegistry _skillRegistry;
    private readonly StudyToolRegistry _toolRegistry;
    private readonly StudyCommandRegistry _commandRegistry;
    private readonly StudyCommandParser _commandParser;
    private readonly StudyExecutionPipeline _pipeline;

    public StudyExecutionPipelineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spark_pipeline_tests_" + Guid.NewGuid().ToString("N"));
        _dataStore = new StudyDataStore(_tempDir);
        _permissionManager = new StudyPermissionManager();
        _skillRegistry = new StudySkillRegistry(_dataStore);
        _toolRegistry = new StudyToolRegistry(_dataStore, _permissionManager);
        _commandRegistry = new StudyCommandRegistry(_dataStore);
        _commandParser = new StudyCommandParser();

        _pipeline = new StudyExecutionPipeline(
            _skillRegistry,
            _toolRegistry,
            _commandRegistry,
            _commandParser,
            _permissionManager);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ProcessInputAsync_SlashCommand_ResolvesSkillAndExecutesPipeline()
    {
        var request = new PipelineExecutionRequest(
            userInput: "/explain topic=Recursion",
            userId: "learner-pipe-1"
        );

        var result = await _pipeline.ProcessInputAsync(request);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(PipelineInputType.SlashCommand, result.InputType);
        Assert.NotNull(result.ResolvedCommand);
        Assert.Equal("explain", result.ResolvedCommand.CommandName);
        Assert.NotNull(result.SelectedSkill);
        Assert.Equal("concept-explainer", result.SelectedSkill.Id);
        Assert.False(string.IsNullOrWhiteSpace(result.FinalResponse));
        Assert.True(result.DurationMs >= 0);
    }

    [Fact]
    public async Task ProcessInputAsync_NaturalLanguage_SelectsAppropriateSkillAndSynthesizesResponse()
    {
        var request = new PipelineExecutionRequest(
            userInput: "Can you create a quiz for me on Data Structures?",
            userId: "learner-pipe-2"
        );

        var result = await _pipeline.ProcessInputAsync(request);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(PipelineInputType.NaturalLanguage, result.InputType);
        Assert.Null(result.ResolvedCommand);
        Assert.NotNull(result.SelectedSkill);
        Assert.Equal("question-generator", result.SelectedSkill.Id);
        Assert.Contains("quiz-generator", result.SelectedTools);
        Assert.NotEmpty(result.ToolResults);
        Assert.False(string.IsNullOrWhiteSpace(result.FinalResponse));
    }

    [Fact]
    public async Task ProcessInputAsync_ToolInvocationChaining_ExecutesMathematicalTool()
    {
        var request = new PipelineExecutionRequest(
            userInput: "/solve problem=\"x^2 - 4 = 0\"",
            userId: "learner-pipe-3"
        );

        var result = await _pipeline.ProcessInputAsync(request);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("equation-evaluator", result.SelectedTools);
        Assert.NotEmpty(result.ToolResults);

        var toolResult = result.ToolResults.First(t => t.ToolId.Contains("equation-evaluator"));
        Assert.True(toolResult.Success);
        Assert.Equal("equation-evaluator", toolResult.ToolId);
        Assert.False(string.IsNullOrWhiteSpace(result.FinalResponse));
    }

    [Fact]
    public async Task ProcessInputAsync_PedagogicalModeAndLanguage_PropagatedCorrectly()
    {
        var request = new PipelineExecutionRequest(
            userInput: "Explain the Big O complexity of QuickSort",
            teachingMode: TeachingMode.Socratic,
            language: LearningLanguage.Malayalam,
            userId: "learner-pipe-4"
        );

        var result = await _pipeline.ProcessInputAsync(request);

        Assert.True(result.Success);
        Assert.Equal(TeachingMode.Socratic, result.TeachingMode);
        Assert.Equal(LearningLanguage.Malayalam, result.Language);
        Assert.False(string.IsNullOrWhiteSpace(result.FinalResponse));
    }

    [Fact]
    public async Task ProcessInputAsync_InvalidSlashCommand_ReturnsClearErrorMessage()
    {
        // /explain without topic
        var request = new PipelineExecutionRequest(
            userInput: "/explain",
            userId: "learner-pipe-5"
        );

        var result = await _pipeline.ProcessInputAsync(request);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("topic", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessInputAsync_ForceSkill_RespectsSkillOverride()
    {
        var request = new PipelineExecutionRequest(
            userInput: "Tell me about quantum computing",
            forceSkillId: "concept-explainer",
            userId: "learner-pipe-6"
        );

        var result = await _pipeline.ProcessInputAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.SelectedSkill);
        Assert.Equal("concept-explainer", result.SelectedSkill.Id);
    }
}
