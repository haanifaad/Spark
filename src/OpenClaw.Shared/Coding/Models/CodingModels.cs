using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenClaw.Shared.Coding.Models;

/// <summary>
/// Explicit security permission levels for coding operations.
/// </summary>
public enum CodingPermissionLevel
{
    /// <summary>Safe read-only access (file reading, search, git status/diff).</summary>
    ReadOnly = 1,

    /// <summary>Modifying or creating files within the designated workspace boundary.</summary>
    WorkspaceFileModification = 2,

    /// <summary>Executing safe terminal and build/test commands within the workspace.</summary>
    TerminalExecution = 3,

    /// <summary>Installing new packages, libraries, or dependencies.</summary>
    DependencyInstallation = 4,

    /// <summary>Creating git branches, commits, or staging files.</summary>
    GitModification = 5,

    /// <summary>Potentially destructive operations (file deletion, git reset/clean).</summary>
    DestructiveOperation = 6
}

/// <summary>
/// Status of an individual task step in an autonomous coding plan.
/// </summary>
public enum PlanStepStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}

/// <summary>
/// An individual step in an autonomous coding implementation plan.
/// </summary>
public sealed record CodingPlanStep
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = string.Empty;
    public PlanStepStatus Status { get; init; } = PlanStepStatus.Pending;
    public IReadOnlyList<string> TargetFiles { get; init; } = Array.Empty<string>();
    public string? ResultSummary { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

/// <summary>
/// An overall plan for an autonomous coding workflow.
/// </summary>
public sealed record CodingPlan
{
    public required string Goal { get; init; }
    public IReadOnlyList<CodingPlanStep> Steps { get; init; } = Array.Empty<CodingPlanStep>();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool IsCompleted => Steps.Count > 0 &&
        Steps.All(s => s.Status is PlanStepStatus.Completed or PlanStepStatus.Skipped);

    public int CompletedStepCount => Steps.Count(s => s.Status == PlanStepStatus.Completed);
    public int TotalStepCount => Steps.Count;
    public double ProgressPercent => TotalStepCount == 0 ? 0.0 : (double)CompletedStepCount / TotalStepCount * 100.0;
}

/// <summary>
/// Kind of file modification.
/// </summary>
public enum FileChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}

/// <summary>
/// Approval and application status of a proposed file modification.
/// </summary>
public enum FileChangeStatus
{
    Proposed,
    Approved,
    Applied,
    Rejected,
    Reverted
}

/// <summary>
/// A single hunk in a unified diff.
/// </summary>
public sealed record UnifiedDiffHunk(
    int OldStart,
    int OldLines,
    int NewStart,
    int NewLines,
    IReadOnlyList<string> Lines,
    string Header);

/// <summary>
/// Structured unified diff representing changes between two file states.
/// </summary>
public sealed record UnifiedDiff
{
    public required string FilePath { get; init; }
    public int Additions { get; init; }
    public int Deletions { get; init; }
    public IReadOnlyList<UnifiedDiffHunk> Hunks { get; init; } = Array.Empty<UnifiedDiffHunk>();
    public string RawDiffText { get; init; } = string.Empty;
}

/// <summary>
/// Record of a file change proposed or applied by the coding agent.
/// </summary>
public sealed record FileChangeRecord
{
    public required string FilePath { get; init; }
    public FileChangeType ChangeType { get; init; } = FileChangeType.Modified;
    public FileChangeStatus Status { get; init; } = FileChangeStatus.Proposed;
    public string? OldContent { get; init; }
    public string? NewContent { get; init; }
    public UnifiedDiff? Diff { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? BackupPath { get; init; }
}

/// <summary>
/// Structural and architectural analysis of a project repository.
/// </summary>
public sealed record RepositoryAnalysis
{
    public required string ProjectRoot { get; init; }
    public required string ProjectName { get; init; }
    public IReadOnlyList<string> DetectedLanguages { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PrimaryFrameworks { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EntryPoints { get; init; } = Array.Empty<string>();
    public string? BuildSystem { get; init; }
    public IReadOnlyList<string> PackageConfigFiles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ImportantDirectories { get; init; } = Array.Empty<string>();
    public int TotalSourceFiles { get; init; }
    public string? GitBranch { get; init; }
    public string? GitCommit { get; init; }
    public bool GitIsDirty { get; init; }
    public IReadOnlyList<string> InstructionFiles { get; init; } = Array.Empty<string>();
    public string? ArchitectureOverview { get; init; }
    public DateTimeOffset AnalyzedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Result of running a terminal command.
/// </summary>
public sealed record CommandExecutionResult
{
    public required string Command { get; init; }
    public required string WorkingDirectory { get; init; }
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public string CombinedOutput { get; init; } = string.Empty;
    public double WallTimeSeconds { get; init; }
    public bool TimedOut { get; init; }
    public bool IsBackground { get; init; }
    public int? ProcessId { get; init; }
    public bool Truncated { get; init; }
    public string? TruncatedOutputPath { get; init; }
}

/// <summary>
/// Diagnostic severity from compiler, linter, or test runner.
/// </summary>
public enum CodingDiagnosticSeverity
{
    Error,
    Warning,
    Information,
    Hint
}

/// <summary>
/// Diagnostic found in code during analysis, build, or test.
/// </summary>
public sealed record CodingDiagnostic
{
    public CodingDiagnosticSeverity Severity { get; init; } = CodingDiagnosticSeverity.Error;
    public required string Message { get; init; }
    public string? FilePath { get; init; }
    public int? LineNumber { get; init; }
    public int? ColumnNumber { get; init; }
    public string? RuleId { get; init; }
    public string Source { get; init; } = "Compiler";
    public string? SuggestedFix { get; init; }
}

/// <summary>
/// Finding produced during code review.
/// </summary>
public sealed record CodeReviewFinding
{
    public CodingDiagnosticSeverity Severity { get; init; } = CodingDiagnosticSeverity.Warning;
    public string Category { get; init; } = "Quality";
    public required string FilePath { get; init; }
    public int? LineNumber { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? Recommendation { get; init; }
}

/// <summary>
/// Complete code review report.
/// </summary>
public sealed record CodeReviewReport
{
    public required string Summary { get; init; }
    public IReadOnlyList<CodeReviewFinding> Findings { get; init; } = Array.Empty<CodeReviewFinding>();
    public int TotalFindings => Findings.Count;
    public int ErrorCount => Findings.Count(f => f.Severity == CodingDiagnosticSeverity.Error);
    public int WarningCount => Findings.Count(f => f.Severity == CodingDiagnosticSeverity.Warning);
    public double QualityScore { get; init; } = 100.0;
    public bool IsApproved => ErrorCount == 0;
    public DateTimeOffset ReviewedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Point-in-time snapshot of modified files for undo/checkpointing.
/// </summary>
public sealed record CodingSessionCheckpoint
{
    public required string CheckpointId { get; init; }
    public required string Description { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, string> FileSnapshots { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// A turn or message in a coding conversation.
/// </summary>
public sealed record CodingMessage
{
    public required string Id { get; init; }
    public required string Role { get; init; } // "user", "agent", "system", "tool"
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? ToolName { get; init; }
    public string? ToolCallId { get; init; }
    public IReadOnlyList<FileChangeRecord>? FileChanges { get; init; }
    public CommandExecutionResult? CommandResult { get; init; }
}

/// <summary>
/// Persistent state for an active or saved coding session.
/// </summary>
public sealed record CodingSession
{
    public required string SessionId { get; init; }
    public required string ProjectRoot { get; init; }
    public string Title { get; init; } = "New Coding Session";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActiveAt { get; init; } = DateTimeOffset.UtcNow;
    public List<CodingMessage> History { get; init; } = new();
    public CodingPlan? ActivePlan { get; init; }
    public List<FileChangeRecord> ModifiedFiles { get; init; } = new();
    public List<CodingSessionCheckpoint> Checkpoints { get; init; } = new();
    public string Status { get; init; } = "Idle"; // "Idle", "Planning", "Executing", "WaitingApproval", "Debugging", "Completed"
    public string? ActiveModelId { get; init; }
}

/// <summary>
/// Category of agent background activity.
/// </summary>
public enum CodingAgentActivityType
{
    Thinking,
    Planning,
    AnalyzingRepository,
    ReadingFile,
    SearchingCodebase,
    GeneratingCode,
    WritingFile,
    EditingFile,
    ExecutingTerminal,
    RunningTests,
    Debugging,
    ReviewingCode,
    RequestingApproval,
    Completed,
    Error
}

/// <summary>
/// Real-time event describing the agent's current step.
/// </summary>
public sealed record CodingAgentActivity
{
    public CodingAgentActivityType Type { get; init; }
    public required string Description { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public double? ProgressPercent { get; init; }
}
