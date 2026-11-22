using System;
using System.Collections.Generic;
using OpenClaw.Shared.Study.Contracts;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// Domain categories for Slash Commands.
/// </summary>
public enum CommandCategory
{
    Learning,
    Academic,
    Research,
    Productivity,
    System
}

/// <summary>
/// Argument specification for a slash command.
/// </summary>
public sealed class StudyCommandArgumentDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "string"; // "string", "int", "bool", "float", "choice"
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public List<string>? AllowedValues { get; set; }

    public StudyCommandArgumentDefinition() { }

    public StudyCommandArgumentDefinition(
        string name,
        string description,
        string type = "string",
        bool isRequired = false,
        string? defaultValue = null,
        IEnumerable<string>? allowedValues = null)
    {
        Name = name;
        Description = description;
        Type = type;
        IsRequired = isRequired;
        DefaultValue = defaultValue;
        AllowedValues = allowedValues != null ? [.. allowedValues] : null;
    }
}

/// <summary>
/// Registered specification of a slash command in the Study Division.
/// </summary>
public sealed class StudyCommandDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public CommandCategory Category { get; set; } = CommandCategory.Learning;
    public string? RequiredSkillId { get; set; }
    public List<string> RequiredTools { get; set; } = [];
    public List<StudyCommandArgumentDefinition> Arguments { get; set; } = [];
    public StudyPermissionKind RequiredPermission { get; set; } = StudyPermissionKind.ReadOnly;
    public string UsageExample { get; set; } = string.Empty;

    public StudyCommandDefinition() { }

    public StudyCommandDefinition(
        string name,
        string description,
        CommandCategory category,
        string? requiredSkillId = null,
        IEnumerable<string>? aliases = null,
        IEnumerable<string>? requiredTools = null,
        IEnumerable<StudyCommandArgumentDefinition>? arguments = null,
        StudyPermissionKind requiredPermission = StudyPermissionKind.ReadOnly,
        string? usageExample = null)
    {
        Name = name.TrimStart('/');
        Description = description;
        Category = category;
        RequiredSkillId = requiredSkillId;
        Aliases = aliases != null ? [.. aliases] : [];
        RequiredTools = requiredTools != null ? [.. requiredTools] : [];
        Arguments = arguments != null ? [.. arguments] : [];
        RequiredPermission = requiredPermission;
        UsageExample = usageExample ?? $"/{name}";
    }
}

/// <summary>
/// Parsed slash command representation.
/// </summary>
public sealed class ParsedStudyCommand
{
    public string CommandName { get; set; } = string.Empty;
    public string RawInput { get; set; } = string.Empty;
    public Dictionary<string, string> Arguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> PositionalArguments { get; set; } = [];
    public bool IsValid { get; set; } = true;
    public List<string> ValidationErrors { get; set; } = [];

    public ParsedStudyCommand() { }

    public ParsedStudyCommand(
        string commandName,
        string rawInput,
        Dictionary<string, string>? arguments = null,
        IEnumerable<string>? positionalArguments = null,
        bool isValid = true,
        IEnumerable<string>? validationErrors = null)
    {
        CommandName = commandName.TrimStart('/');
        RawInput = rawInput;
        Arguments = arguments != null ? new Dictionary<string, string>(arguments, StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase);
        PositionalArguments = positionalArguments != null ? [.. positionalArguments] : [];
        IsValid = isValid;
        ValidationErrors = validationErrors != null ? [.. validationErrors] : [];
    }

    public string? GetArgument(string name, string? defaultValue = null)
    {
        if (Arguments.TryGetValue(name, out var val))
        {
            return val;
        }

        return defaultValue;
    }
}

/// <summary>
/// Autocomplete item returned when typing '/' in chat.
/// </summary>
public sealed record CommandAutocompleteItem(
    string CommandName,
    string DisplayText,
    string Description,
    string Category,
    string InsertTemplate,
    IReadOnlyList<string> ArgumentNames);

/// <summary>
/// Audit record of an executed slash command.
/// </summary>
public sealed class CommandExecutionRecord
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");
    public string CommandName { get; set; } = string.Empty;
    public string CommandId => CommandName;
    public string UserId { get; set; } = "default";
    public string RawCommand { get; set; } = string.Empty;
    public string RawInput { get => RawCommand; set => RawCommand = value; }
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Completed;
    public bool Success => Status == ExecutionStatus.Completed;
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
    public long DurationMs { get; set; }
    public string? ResolvedSkillId { get; set; }
    public string? OutputSummary { get; set; }
    public string? ErrorMessage { get; set; }

    public CommandExecutionRecord() { }

    public CommandExecutionRecord(
        string executionId,
        string commandName,
        string userId,
        string rawCommand,
        ExecutionStatus status,
        DateTime executedAtUtc,
        long durationMs,
        string? resolvedSkillId = null,
        string? outputSummary = null,
        string? errorMessage = null)
    {
        ExecutionId = executionId;
        CommandName = commandName;
        UserId = userId;
        RawCommand = rawCommand;
        Status = status;
        ExecutedAtUtc = executedAtUtc;
        DurationMs = durationMs;
        ResolvedSkillId = resolvedSkillId;
        OutputSummary = outputSummary;
        ErrorMessage = errorMessage;
    }
}
