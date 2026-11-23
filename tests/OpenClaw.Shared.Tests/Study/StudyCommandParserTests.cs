using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Commands;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyCommandParserTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudyDataStore _dataStore;
    private readonly StudyCommandRegistry _commandRegistry;
    private readonly StudyCommandParser _parser;

    public StudyCommandParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spark_commands_tests_" + Guid.NewGuid().ToString("N"));
        _dataStore = new StudyDataStore(_tempDir);
        _commandRegistry = new StudyCommandRegistry(_dataStore);
        _parser = new StudyCommandParser();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void BuiltInCommands_ContainsAllFiveCategories_AndAtLeast23Commands()
    {
        var allCommands = _commandRegistry.ListCommands();

        Assert.True(allCommands.Count >= 23, $"Expected at least 23 commands, got {allCommands.Count}");

        var categories = allCommands.Select(c => c.Category).Distinct().ToList();
        Assert.Contains(CommandCategory.Learning, categories);
        Assert.Contains(CommandCategory.Academic, categories);
        Assert.Contains(CommandCategory.Research, categories);
        Assert.Contains(CommandCategory.Productivity, categories);
        Assert.Contains(CommandCategory.System, categories);
    }

    [Fact]
    public void Parse_PositionalArguments_ExtractsArgumentsCorrectly()
    {
        var parsed = _parser.Parse("/explain QuickSort", _commandRegistry.ListCommands());

        Assert.True(parsed.IsValid);
        Assert.Equal("explain", parsed.CommandName);
        Assert.Contains("topic", parsed.Arguments.Keys);
        Assert.Equal("QuickSort", parsed.Arguments["topic"]);
    }

    [Fact]
    public void Parse_KeyValueAndQuotedStrings_ParsedAccurately()
    {
        var parsed = _parser.Parse("/quiz subject=\"Data Structures\" count=10 difficulty=medium", _commandRegistry.ListCommands());

        Assert.True(parsed.IsValid);
        Assert.Equal("quiz", parsed.CommandName);
        Assert.Equal("Data Structures", parsed.Arguments["subject"]);
        Assert.Equal("10", parsed.Arguments["count"]);
        Assert.Equal("medium", parsed.Arguments["difficulty"]);
    }

    [Fact]
    public void Parse_Aliases_ResolvesToCanonicalCommand()
    {
        var cmd = _commandRegistry.ListCommands();
        var parsedH = _parser.Parse("/h", cmd);
        Assert.True(parsedH.IsValid);
        Assert.Equal("help", parsedH.CommandName);

        var parsedCalc = _parser.Parse("/calc problem=2+2", cmd);
        Assert.True(parsedCalc.IsValid);
        Assert.Equal("solve", parsedCalc.CommandName);
        Assert.Equal("2+2", parsedCalc.Arguments["problem"]);

        var parsedQ = _parser.Parse("/q subject=Algorithms", cmd);
        Assert.True(parsedQ.IsValid);
        Assert.Equal("quiz", parsedQ.CommandName);
    }

    [Fact]
    public void Parse_MissingRequiredArgument_GeneratesValidationErrors()
    {
        // /explain requires 'topic'
        var parsed = _parser.Parse("/explain", _commandRegistry.ListCommands());

        Assert.False(parsed.IsValid);
        Assert.NotEmpty(parsed.ValidationErrors);
        Assert.Contains(parsed.ValidationErrors, e => e.Contains("topic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_UnknownCommand_FlagsAsInvalid()
    {
        var parsed = _parser.Parse("/nonexistentcommand 123", _commandRegistry.ListCommands());

        Assert.False(parsed.IsValid);
        Assert.NotEmpty(parsed.ValidationErrors);
        Assert.Contains(parsed.ValidationErrors, e => e.Contains("Unknown command", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/", 10)]
    [InlineData("/ex", 2)]
    [InlineData("/qui", 1)]
    [InlineData("/solv", 1)]
    public void Autocomplete_ReturnsRelevantSuggestions(string prefix, int minExpected)
    {
        var suggestions = _commandRegistry.GetAutocompleteSuggestions(prefix);

        Assert.True(suggestions.Count >= minExpected, $"Expected >= {minExpected} suggestions for '{prefix}', got {suggestions.Count}");
        Assert.All(suggestions, s => Assert.False(string.IsNullOrWhiteSpace(s.CommandName)));
    }

    [Fact]
    public async Task RecordCommandExecutionAsync_PersistsAuditTrail()
    {
        var record = new CommandExecutionRecord(
            executionId: "cmd-exec-" + Guid.NewGuid().ToString("N")[..8],
            commandName: "explain",
            userId: "learner-cmd-1",
            rawCommand: "/explain topic=Recursion",
            status: ExecutionStatus.Completed,
            executedAtUtc: DateTime.UtcNow,
            durationMs: 85,
            resolvedSkillId: "skill-concept-explainer",
            outputSummary: "Recursion explained with base and recursive cases.",
            errorMessage: null
        );

        await _commandRegistry.RecordCommandExecutionAsync(record, "learner-cmd-1");

        var history = await _commandRegistry.GetCommandExecutionHistoryAsync("explain", "learner-cmd-1");
        Assert.NotEmpty(history);
        var latest = history.First();
        Assert.Equal("explain", latest.CommandName);
        Assert.Equal(ExecutionStatus.Completed, latest.Status);
        Assert.Equal(85, latest.DurationMs);
    }
}
