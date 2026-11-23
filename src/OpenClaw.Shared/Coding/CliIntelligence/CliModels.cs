using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Coding.CliIntelligence;

/// <summary>
/// Safety classification tier for CLI command execution.
/// </summary>
public enum CliCommandSafetyTier
{
    Tier1_ReadOnly = 1,
    Tier2_SafeModification = 2,
    Tier3_DestructiveOperation = 3
}

/// <summary>
/// Definition of a software CLI harness in the SPARK CLI Anything hub.
/// </summary>
public sealed record CliHarnessEntry
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; } // e.g. "devops", "database", "3d", "media", "system"
    public required string Description { get; init; }
    public string Requires { get; init; } = "None";
    public string? InstallCmd { get; init; }
    public required string EntryPoint { get; init; }
    public string? SkillPath { get; init; }
    public string? Homepage { get; init; }
    public string Version { get; init; } = "1.0.0";
    public IReadOnlyList<string> MatrixTags { get; init; } = Array.Empty<string>();
    public bool IsInstalled { get; set; }
}

/// <summary>
/// Capability-based workflow matrix grouping multiple CLI harnesses.
/// </summary>
public sealed record CliMatrixEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public IReadOnlyList<string> Clis { get; init; } = Array.Empty<string>();
}

/// <summary>
/// An auditable record of an executed or proposed CLI command.
/// </summary>
public sealed record CliExecutionRecord
{
    public required string Id { get; init; }
    public required string Command { get; init; }
    public required string WorkingDirectory { get; init; }
    public CliCommandSafetyTier SafetyTier { get; init; } = CliCommandSafetyTier.Tier1_ReadOnly;
    public bool RequiresApproval => SafetyTier == CliCommandSafetyTier.Tier3_DestructiveOperation;
    public bool ApprovedByUser { get; set; } = true;
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public double WallTimeSeconds { get; set; }
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsSuccess => ExitCode == 0;
}
