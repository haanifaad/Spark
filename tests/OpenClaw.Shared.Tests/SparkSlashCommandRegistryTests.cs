using System;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Shared.Commands;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkSlashCommandRegistryTests
{
    [Fact]
    public void Registry_ContainsRequiredDefaultCommands()
    {
        var registry = SparkSlashCommandRegistry.Instance;
        var all = registry.GetAllCommands();

        Assert.NotEmpty(all);
        Assert.Contains(all, c => c.Name == "/new" && c.Category == SparkCommandCategory.Conversation);
        Assert.Contains(all, c => c.Name == "/clear" && c.Category == SparkCommandCategory.Conversation);
        Assert.Contains(all, c => c.Name == "/rename" && c.Category == SparkCommandCategory.Conversation);
        Assert.Contains(all, c => c.Name == "/delete" && c.Category == SparkCommandCategory.Conversation);
        Assert.Contains(all, c => c.Name == "/export" && c.Category == SparkCommandCategory.Conversation);
        Assert.Contains(all, c => c.Name == "/pin" && c.Category == SparkCommandCategory.Conversation);

        Assert.Contains(all, c => c.Name == "/model" && c.Category == SparkCommandCategory.Model);
        Assert.Contains(all, c => c.Name == "/models" && c.Category == SparkCommandCategory.Model);
        Assert.Contains(all, c => c.Name == "/favorite" && c.Category == SparkCommandCategory.Model);

        Assert.Contains(all, c => c.Name == "/summarize" && c.Category == SparkCommandCategory.AI);
        Assert.Contains(all, c => c.Name == "/explain" && c.Category == SparkCommandCategory.AI);
        Assert.Contains(all, c => c.Name == "/rewrite" && c.Category == SparkCommandCategory.AI);
        Assert.Contains(all, c => c.Name == "/continue" && c.Category == SparkCommandCategory.AI);
        Assert.Contains(all, c => c.Name == "/regenerate" && c.Category == SparkCommandCategory.AI);
        Assert.Contains(all, c => c.Name == "/retry" && c.Category == SparkCommandCategory.AI);
        Assert.Contains(all, c => c.Name == "/stop" && c.Category == SparkCommandCategory.AI);

        Assert.Contains(all, c => c.Name == "/search" && c.Category == SparkCommandCategory.Research);
        Assert.Contains(all, c => c.Name == "/sources" && c.Category == SparkCommandCategory.Research);

        Assert.Contains(all, c => c.Name == "/memory" && c.Category == SparkCommandCategory.Context);
        Assert.Contains(all, c => c.Name == "/context" && c.Category == SparkCommandCategory.Context);
        Assert.Contains(all, c => c.Name == "/compact" && c.Category == SparkCommandCategory.Context);

        Assert.Contains(all, c => c.Name == "/attach" && c.Category == SparkCommandCategory.Files);
        Assert.Contains(all, c => c.Name == "/files" && c.Category == SparkCommandCategory.Files);

        Assert.Contains(all, c => c.Name == "/settings" && c.Category == SparkCommandCategory.Interface);
        Assert.Contains(all, c => c.Name == "/theme" && c.Category == SparkCommandCategory.Interface);
        Assert.Contains(all, c => c.Name == "/help" && c.Category == SparkCommandCategory.Interface);
    }

    [Theory]
    [InlineData("/new", "/new")]
    [InlineData("new", "/new")]
    [InlineData("/clear", "/clear")]
    [InlineData("/reset", "/clear")]
    [InlineData("/name", "/rename")]
    [InlineData("/commands", "/help")]
    public void FindCommand_ResolvesNamesAndAliases(string query, string expectedName)
    {
        var registry = SparkSlashCommandRegistry.Instance;
        var cmd = registry.FindCommand(query);

        Assert.NotNull(cmd);
        Assert.Equal(expectedName, cmd.Name);
    }

    [Fact]
    public void SearchCommands_FuzzyMatchesTypos()
    {
        var registry = SparkSlashCommandRegistry.Instance;

        // Typo: "clera" -> "/clear"
        var clearMatches = registry.SearchCommands("clera");
        Assert.NotEmpty(clearMatches);
        Assert.Equal("/clear", clearMatches[0].Name);

        // Typo: "sumarize" -> "/summarize"
        var sumMatches = registry.SearchCommands("sumarize");
        Assert.NotEmpty(sumMatches);
        Assert.Equal("/summarize", sumMatches[0].Name);

        // Substring: "ren" -> "/rename"
        var renMatches = registry.SearchCommands("ren");
        Assert.NotEmpty(renMatches);
        Assert.Equal("/rename", renMatches[0].Name);
    }

    [Fact]
    public void SuggestAlternatives_ReturnsHelpfulSuggestionsForUnknownCommands()
    {
        var registry = SparkSlashCommandRegistry.Instance;
        var suggestions = registry.SuggestAlternatives("/mdl", 3);

        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Name == "/model" || s.Name == "/models");
    }

    [Fact]
    public async Task ExecuteLineAsync_ValidCommand_ReturnsSuccessResult()
    {
        var registry = SparkSlashCommandRegistry.Instance;
        var ctx = new SparkCommandExecutionContext
        {
            SessionKey = "session-1",
            SessionTitle = "Main Chat",
            CurrentModel = "qwen2.5-coder:7b",
            IsConnected = true
        };

        var result = await registry.ExecuteLineAsync("/new", ctx);
        Assert.True(result.Succeeded);
        Assert.Equal("CreateNewSession", result.ClientAction);

        var renameResult = await registry.ExecuteLineAsync("/rename Project Discussion", ctx);
        Assert.True(renameResult.Succeeded);
        Assert.Equal("RenameSession", renameResult.ClientAction);
        Assert.Equal("Project Discussion", renameResult.ActionPayload);
    }

    [Fact]
    public async Task ExecuteLineAsync_MissingRequiredArgument_ReturnsFailure()
    {
        var registry = SparkSlashCommandRegistry.Instance;
        var ctx = new SparkCommandExecutionContext
        {
            SessionKey = "session-1"
        };

        var result = await registry.ExecuteLineAsync("/rename", ctx);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteLineAsync_UnknownCommand_ReturnsHelpfulAlternative()
    {
        var registry = SparkSlashCommandRegistry.Instance;
        var ctx = new SparkCommandExecutionContext
        {
            SessionKey = "session-1"
        };

        var result = await registry.ExecuteLineAsync("/clera", ctx);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Unknown command", result.ErrorMessage);
        Assert.Contains("/clear", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteLineAsync_DestructiveCommand_RequiresConfirmation()
    {
        var registry = SparkSlashCommandRegistry.Instance;
        var ctx = new SparkCommandExecutionContext
        {
            SessionKey = "session-1"
        };

        var result = await registry.ExecuteLineAsync("/clear", ctx);
        Assert.True(result.Succeeded);
        Assert.True(result.RequiresConfirmation);
        Assert.NotNull(result.ConfirmationPrompt);
        Assert.NotNull(result.ConfirmedExecution);

        // Run confirmed execution
        var confirmedResult = await result.ConfirmedExecution(default);
        Assert.True(confirmedResult.Succeeded);
        Assert.Equal("ClearSession", confirmedResult.ClientAction);
        Assert.Equal("session-1", confirmedResult.ActionPayload);
    }
}
