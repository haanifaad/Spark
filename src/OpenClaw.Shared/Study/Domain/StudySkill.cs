using System;
using System.Collections.Generic;
using OpenClaw.Shared.Study.Contracts;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// Broad domain categorization for Study Division skills.
/// </summary>
public enum SkillCategory
{
    Learning,
    Academic,
    Research,
    Technical,
    Productivity,
    System,
    Custom
}

/// <summary>
/// Lifecycle state of a registered Study Skill.
/// </summary>
public enum SkillStatus
{
    Active,
    Disabled,
    Deprecated
}

/// <summary>
/// Error recovery and fallback configuration for skill execution.
/// </summary>
public sealed record SkillErrorHandlingConfig(
    int MaxRetries = 1,
    string? FallbackSkillId = null,
    string? FallbackMessage = null);

/// <summary>
/// Structured definition of an AI learning capability in the Study Division.
/// Defines pedagogical instructions, context requirements, tool allowances, and execution rules.
/// </summary>
public sealed class StudySkillDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SkillCategory Category { get; set; } = SkillCategory.Learning;
    public string Version { get; set; } = "1.0.0";
    public string Instructions { get; set; } = string.Empty;
    public List<string> TriggerConditions { get; set; } = [];
    public List<string> RequiredContext { get; set; } = [];
    public string InputSchema { get; set; } = "{}";
    public string OutputSchema { get; set; } = "{}";
    public List<string> AllowedTools { get; set; } = [];
    public string ModelPreference { get; set; } = "balanced";
    public List<StudyPermissionKind> RequiredPermissions { get; set; } = [StudyPermissionKind.ReadOnly];
    public bool IsEnabled { get; set; } = true;
    public SkillStatus Status { get; set; } = SkillStatus.Active;
    public SkillErrorHandlingConfig ErrorHandling { get; set; } = new();
    public List<string> Tags { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public StudySkillDefinition() { }

    public StudySkillDefinition(
        string id,
        string name,
        string description,
        SkillCategory category,
        string version,
        string instructions,
        IEnumerable<string>? triggerConditions = null,
        IEnumerable<string>? requiredContext = null,
        string? inputSchema = null,
        string? outputSchema = null,
        IEnumerable<string>? allowedTools = null,
        string modelPreference = "balanced",
        IEnumerable<StudyPermissionKind>? requiredPermissions = null,
        bool isEnabled = true,
        SkillErrorHandlingConfig? errorHandling = null,
        IEnumerable<string>? tags = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        Version = version;
        Instructions = instructions;
        TriggerConditions = triggerConditions != null ? [.. triggerConditions] : [];
        RequiredContext = requiredContext != null ? [.. requiredContext] : [];
        InputSchema = inputSchema ?? "{}";
        OutputSchema = outputSchema ?? "{}";
        AllowedTools = allowedTools != null ? [.. allowedTools] : [];
        ModelPreference = modelPreference;
        RequiredPermissions = requiredPermissions != null ? [.. requiredPermissions] : [StudyPermissionKind.ReadOnly];
        IsEnabled = isEnabled;
        Status = isEnabled ? SkillStatus.Active : SkillStatus.Disabled;
        ErrorHandling = errorHandling ?? new SkillErrorHandlingConfig();
        Tags = tags != null ? [.. tags] : [];
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Status of an execution action.
/// </summary>
public enum ExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Historical record of a skill invocation.
/// </summary>
public sealed class SkillExecutionRecord
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");
    public string SkillId { get; set; } = string.Empty;
    public string UserId { get; set; } = "default";
    public string? SessionId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Completed;
    public long DurationMs { get; set; }
    public List<string> InvokedTools { get; set; } = [];
    public string? InputsSummary { get; set; }
    public string? OutputsSummary { get; set; }
    public string? ErrorMessage { get; set; }

    public SkillExecutionRecord() { }

    public SkillExecutionRecord(
        string executionId,
        string skillId,
        string userId,
        string? sessionId,
        DateTime startedAtUtc,
        DateTime? completedAtUtc,
        ExecutionStatus status,
        long durationMs,
        string? inputsSummary = null,
        string? outputsSummary = null,
        string? errorMessage = null)
    {
        ExecutionId = executionId;
        SkillId = skillId;
        UserId = userId;
        SessionId = sessionId;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Status = status;
        DurationMs = durationMs;
        InputsSummary = inputsSummary;
        OutputsSummary = outputsSummary;
        ErrorMessage = errorMessage;
    }
}
