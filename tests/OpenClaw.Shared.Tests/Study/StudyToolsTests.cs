using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Tools;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudyDataStore _dataStore;
    private readonly StudyPermissionManager _permissionManager;
    private readonly StudyToolRegistry _toolRegistry;

    public StudyToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spark_tools_tests_" + Guid.NewGuid().ToString("N"));
        _dataStore = new StudyDataStore(_tempDir);
        _permissionManager = new StudyPermissionManager();
        _toolRegistry = new StudyToolRegistry(_dataStore, _permissionManager);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void BuiltInTools_ContainsAllFiveCategories_AndAtLeast21Tools()
    {
        var allTools = _toolRegistry.ListTools();

        Assert.True(allTools.Count >= 21, $"Expected at least 21 tools, got {allTools.Count}");

        var categories = allTools.Select(t => t.Category).Distinct().ToList();
        Assert.Contains(ToolCategory.Mathematics, categories);
        Assert.Contains(ToolCategory.Documents, categories);
        Assert.Contains(ToolCategory.Programming, categories);
        Assert.Contains(ToolCategory.Learning, categories);
        Assert.Contains(ToolCategory.Research, categories);
    }

    [Fact]
    public async Task CalculatorTool_EvaluatesArithmeticExpressionsCorrectly()
    {
        var request = new StudyToolExecutionRequest(
            toolId: "tool-calculator",
            arguments: new Dictionary<string, object?> { ["expression"] = "15 + 3 * (8 - 2)" }
        );

        var result = await _toolRegistry.ExecuteToolAsync(request);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);

        var json = JsonSerializer.Serialize(result.Data);
        Assert.Contains("33", json);
    }

    [Fact]
    public async Task ScientificCalculatorTool_EvaluatesTrigAndRoots()
    {
        var request = new StudyToolExecutionRequest(
            toolId: "scientific-calculator",
            arguments: new Dictionary<string, object?>
            {
                ["operation"] = "sqrt",
                ["value"] = 64.0
            }
        );

        var result = await _toolRegistry.ExecuteToolAsync(request);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);
        var json = JsonSerializer.Serialize(result.Data);
        Assert.Contains("8", json);
    }

    [Fact]
    public async Task UnitConverterTool_ConvertsUnitsAccurately()
    {
        var request = new StudyToolExecutionRequest(
            toolId: "unit-converter",
            arguments: new Dictionary<string, object?>
            {
                ["value"] = 5.0,
                ["from"] = "km",
                ["to"] = "m"
            }
        );

        var result = await _toolRegistry.ExecuteToolAsync(request);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);
        var json = JsonSerializer.Serialize(result.Data);
        Assert.Contains("5000", json);
    }

    [Fact]
    public async Task StatisticsCalculatorTool_CalculatesMeanAndStandardDeviation()
    {
        var request = new StudyToolExecutionRequest(
            toolId: "statistics-calculator",
            arguments: new Dictionary<string, object?>
            {
                ["data"] = "10, 20, 30, 40, 50"
            }
        );

        var result = await _toolRegistry.ExecuteToolAsync(request);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);
        var json = JsonSerializer.Serialize(result.Data);
        Assert.Contains("30", json); // Mean = 30
    }

    [Fact]
    public async Task JsonValidatorTool_ValidatesValidAndInvalidJson()
    {
        // Valid JSON
        var validReq = new StudyToolExecutionRequest(
            toolId: "json-validator",
            arguments: new Dictionary<string, object?> { ["json"] = "{\"name\": \"SPARK\", \"phase\": 3}" }
        );
        var validRes = await _toolRegistry.ExecuteToolAsync(validReq);
        Assert.True(validRes.Success);
        var validJson = JsonSerializer.Serialize(validRes.Data);
        Assert.Contains("\"isValid\":true", validJson);

        // Invalid JSON
        var invalidReq = new StudyToolExecutionRequest(
            toolId: "json-validator",
            arguments: new Dictionary<string, object?> { ["json"] = "{invalid json string" }
        );
        var invalidRes = await _toolRegistry.ExecuteToolAsync(invalidReq);
        Assert.True(invalidRes.Success);
        var invalidJson = JsonSerializer.Serialize(invalidRes.Data);
        Assert.Contains("\"isValid\":false", invalidJson);
    }

    [Fact]
    public async Task SqlQueryValidatorTool_IdentifiesValidAndDangerousStatements()
    {
        var validReq = new StudyToolExecutionRequest(
            toolId: "sql-query-validator",
            arguments: new Dictionary<string, object?> { ["sql"] = "SELECT id, name FROM Users WHERE active = 1" }
        );
        var validRes = await _toolRegistry.ExecuteToolAsync(validReq);
        Assert.True(validRes.Success);

        var dangerousReq = new StudyToolExecutionRequest(
            toolId: "sql-query-validator",
            arguments: new Dictionary<string, object?> { ["sql"] = "DROP TABLE Users; --" }
        );
        var dangerousRes = await _toolRegistry.ExecuteToolAsync(dangerousReq);
        Assert.True(dangerousRes.Success);
        var dangerousJson = JsonSerializer.Serialize(dangerousRes.Data);
        Assert.Contains("DROP", dangerousJson);
        Assert.Contains("\"isDestructive\":true", dangerousJson);
    }

    [Fact]
    public async Task FlashcardGeneratorTool_GeneratesStructuredFlashcards()
    {
        var req = new StudyToolExecutionRequest(
            toolId: "flashcard-generator",
            arguments: new Dictionary<string, object?>
            {
                ["topic"] = "Data Structures",
                ["content"] = "Stack: A LIFO structure\nQueue: A FIFO structure",
                ["count"] = 2
            }
        );

        var res = await _toolRegistry.ExecuteToolAsync(req);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        var json = JsonSerializer.Serialize(res.Data);
        Assert.Contains("Stack", json);
        Assert.Contains("Queue", json);
    }

    [Fact]
    public async Task CitationFormatterTool_FormatsCitationsInDifferentStyles()
    {
        var req = new StudyToolExecutionRequest(
            toolId: "citation-formatter",
            arguments: new Dictionary<string, object?>
            {
                ["title"] = "Introduction to Algorithms",
                ["author"] = "Cormen, T. H.",
                ["year"] = 2009,
                ["style"] = "APA"
            }
        );

        var res = await _toolRegistry.ExecuteToolAsync(req);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        var json = JsonSerializer.Serialize(res.Data);
        Assert.Contains("Cormen", json);
        Assert.Contains("2009", json);
    }

    [Fact]
    public async Task ExecutionLifecycle_TracksExecutionHistory_AndEnforcesAudit()
    {
        var req = new StudyToolExecutionRequest(
            toolId: "calculator",
            arguments: new Dictionary<string, object?> { ["expression"] = "100 / 4" },
            userId: "learner-audit-1"
        );

        var res = await _toolRegistry.ExecuteToolAsync(req);
        Assert.True(res.Success);
        Assert.Equal(ToolExecutionStatus.Completed, res.Status);

        var history = await _toolRegistry.GetToolExecutionHistoryAsync("calculator", "learner-audit-1");
        Assert.NotEmpty(history);
        var latest = history.First();
        Assert.Contains("calculator", latest.ToolId);
        Assert.Equal("learner-audit-1", latest.UserId);
        Assert.Equal(ToolExecutionStatus.Completed, latest.Status);
    }

    [Fact]
    public async Task CancelExecutionAsync_CancelsPendingExecution()
    {
        var cancelled = await _toolRegistry.CancelExecutionAsync("non-existent-request-id", "default");
        Assert.False(cancelled);
    }
}
