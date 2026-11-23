using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Commands;

/// <summary>
/// Categories for SPARK slash commands.
/// </summary>
public enum SparkCommandCategory
{
    Conversation,
    Model,
    AI,
    Research,
    Context,
    Files,
    Interface,
    Coding,
    Security
}

/// <summary>
/// Choice option for command arguments.
/// </summary>
public sealed record SparkCommandArgChoice(string Value, string Label, string? Description = null);

/// <summary>
/// Schema definition for slash command arguments.
/// </summary>
public sealed class SparkCommandArgumentSchema
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsRequired { get; init; }
    public IReadOnlyList<SparkCommandArgChoice> Choices { get; init; } = Array.Empty<SparkCommandArgChoice>();
    public Func<string?, (bool IsValid, string? ErrorMessage)>? CustomValidator { get; init; }

    public (bool IsValid, string? ErrorMessage) Validate(string? argValue)
    {
        if (IsRequired && string.IsNullOrWhiteSpace(argValue))
        {
            return (false, $"Argument '<{Name}>' is required.");
        }

        if (!string.IsNullOrWhiteSpace(argValue) && Choices.Count > 0)
        {
            var match = Choices.Any(c => string.Equals(c.Value, argValue.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!match)
            {
                var options = string.Join(", ", Choices.Select(c => c.Value));
                return (false, $"Invalid value '{argValue}' for '<{Name}>'. Allowed values: {options}");
            }
        }

        if (CustomValidator != null)
        {
            return CustomValidator(argValue);
        }

        return (true, null);
    }
}

/// <summary>
/// Runtime context provided to a slash command execution handler.
/// </summary>
public sealed class SparkCommandExecutionContext
{
    public required string SessionKey { get; init; }
    public string? SessionTitle { get; init; }
    public string? CurrentModel { get; init; }
    public string? WorkspacePath { get; init; }
    public bool IsConnected { get; init; }
    public bool HasActiveRun { get; init; }
    public IReadOnlyList<string>? AvailableModels { get; init; }
    public object? HostServices { get; init; }
}

/// <summary>
/// Result of executing a slash command.
/// </summary>
public sealed class SparkCommandResult
{
    public bool Succeeded { get; init; }
    public bool IsSuccess => Succeeded;
    public string? OutputMessage { get; init; }
    public string? Message => OutputMessage ?? ErrorMessage;
    public string? ErrorMessage { get; init; }
    public bool RequiresConfirmation { get; init; }
    public string? ConfirmationPrompt { get; init; }
    public string? ClientAction { get; init; }
    public string? ActionPayload { get; init; }
    public Func<CancellationToken, Task<SparkCommandResult>>? ConfirmedExecution { get; init; }

    public static SparkCommandResult Success(string? message = null, string? clientAction = null, string? actionPayload = null) =>
        new()
        {
            Succeeded = true,
            OutputMessage = message,
            ClientAction = clientAction,
            ActionPayload = actionPayload
        };

    public static SparkCommandResult Failure(string error) =>
        new()
        {
            Succeeded = false,
            ErrorMessage = error
        };

    public static SparkCommandResult NeedsConfirmation(
        string prompt,
        Func<CancellationToken, Task<SparkCommandResult>> confirmedExecution) =>
        new()
        {
            Succeeded = true,
            RequiresConfirmation = true,
            ConfirmationPrompt = prompt,
            ConfirmedExecution = confirmedExecution
        };
}

/// <summary>
/// Definition of a first-class SPARK slash command.
/// </summary>
public sealed class SparkSlashCommand
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public required string Description { get; init; }
    public required SparkCommandCategory Category { get; init; }
    public IReadOnlyList<SparkCommandArgumentSchema> Arguments { get; init; } = Array.Empty<SparkCommandArgumentSchema>();
    public string? KeyboardShortcut { get; init; }
    public bool RequiresConfirmation { get; init; }
    public string? ConfirmationPrompt { get; init; }
    public Func<SparkCommandExecutionContext, bool>? AvailabilityPredicate { get; init; }
    public required Func<SparkCommandExecutionContext, string?, CancellationToken, Task<SparkCommandResult>> Handler { get; init; }

    public bool Matches(string commandToken)
    {
        if (string.IsNullOrWhiteSpace(commandToken)) return false;
        var token = commandToken.Trim();
        if (!token.StartsWith('/')) token = "/" + token;

        if (string.Equals(Name, token, StringComparison.OrdinalIgnoreCase)) return true;
        return Aliases.Any(alias => string.Equals(alias.StartsWith('/') ? alias : "/" + alias, token, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsAvailable(SparkCommandExecutionContext context)
    {
        return AvailabilityPredicate == null || AvailabilityPredicate(context);
    }

    public string BuildArgTemplate()
    {
        if (Arguments.Count == 0) return string.Empty;
        return string.Join(" ", Arguments.Select(a => a.IsRequired ? $"<{a.Name}>" : $"[{a.Name}]"));
    }
}
