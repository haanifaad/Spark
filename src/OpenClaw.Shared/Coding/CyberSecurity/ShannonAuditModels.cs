using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClaw.Shared.Coding.CyberSecurity;

/// <summary>
/// Vulnerability classes evaluated by the Shannon cybersecurity testing pipeline.
/// </summary>
public enum VulnClass
{
    Injection,
    Xss,
    Auth,
    Authz,
    Ssrf,
    Miscellaneous
}

/// <summary>
/// Finding severity classifications.
/// </summary>
public enum AuditSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Finding confidence levels based on proof-of-concept verification.
/// </summary>
public enum AuditConfidence
{
    Low,
    Medium,
    High
}

/// <summary>
/// Execution phases of the 5-phase Shannon cybersecurity audit pipeline.
/// </summary>
public enum AuditPhase
{
    NotStarted,
    PreRecon,
    Recon,
    VulnIdentification,
    ExploitationVerification,
    ReportAuthoring,
    Completed,
    Failed,
    Aborted
}

/// <summary>
/// Status of an individual security agent.
/// </summary>
public enum AuditAgentStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}

/// <summary>
/// Execution state of a specialized security agent in the Shannon pipeline.
/// </summary>
public sealed record SecurityAgentState
{
    public required string AgentId { get; init; }
    public required string DisplayName { get; init; }
    public required string Phase { get; init; }
    public AuditAgentStatus Status { get; set; } = AuditAgentStatus.Pending;
    public int FindingsCount { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>
/// A verified security finding with reproducible evidence, root cause analysis, and remediation.
/// Follows Shannon's core paradigm: "No exploit, no report."
/// </summary>
public sealed record SecurityFinding
{
    public required string Id { get; init; } // e.g., INJ-VULN-01, AUTH-VULN-01
    public required VulnClass Class { get; init; }
    public required AuditSeverity Severity { get; init; }
    public required AuditConfidence Confidence { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string VulnerableEndpoint { get; init; }
    public string? SourceFile { get; init; }
    public int? SourceLine { get; init; }
    public string Prerequisites { get; init; } = "None";
    public IReadOnlyList<string> ExploitationSteps { get; init; } = Array.Empty<string>();
    public required string ProofOfImpact { get; init; }
    public string? RawPayload { get; init; }
    public string? HttpEvidence { get; init; }
    public required string Remediation { get; init; }
    public string? CweId { get; init; }
    public DateTimeOffset DiscoveredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Configuration options for a cybersecurity audit.
/// </summary>
public sealed record SecurityAuditOptions
{
    public required string TargetUrl { get; init; }
    public string? TargetRepository { get; init; }
    public IReadOnlyList<string> AvoidRules { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FocusRules { get; init; } = Array.Empty<string>();
    public AuditSeverity MinSeverity { get; init; } = AuditSeverity.Low;
    public AuditConfidence MinConfidence { get; init; } = AuditConfidence.Low;
    public bool EnableSast { get; init; } = true;
    public bool EmitSarif { get; init; } = true;
    public string Description { get; init; } = "Authorized Defensive Security Assessment";
    public bool IsAuthorizedTarget { get; init; } = true;
}

/// <summary>
/// Log entry recorded during a security audit execution.
/// </summary>
public sealed record SecurityAuditLogEntry(
    DateTimeOffset Timestamp,
    AuditPhase Phase,
    string SourceAgent,
    string Message,
    bool IsWarning = false,
    bool IsError = false);

/// <summary>
/// Complete session state of an autonomous cybersecurity audit.
/// </summary>
public sealed record SecurityAuditSession
{
    public required string AuditId { get; init; }
    public required string TargetUrl { get; init; }
    public string? TargetRepository { get; init; }
    public required SecurityAuditOptions Options { get; init; }
    public AuditPhase Phase { get; set; } = AuditPhase.NotStarted;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public List<SecurityAgentState> AgentStates { get; init; } = new();
    public List<SecurityFinding> Findings { get; init; } = new();
    public List<SecurityAuditLogEntry> LogEntries { get; init; } = new();
    public string ReportMarkdown { get; set; } = string.Empty;
    public string ReportSarif { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = "Initialized";

    public int CriticalCount => Findings.Count(f => f.Severity == AuditSeverity.Critical);
    public int HighCount => Findings.Count(f => f.Severity == AuditSeverity.High);
    public int MediumCount => Findings.Count(f => f.Severity == AuditSeverity.Medium);
    public int LowCount => Findings.Count(f => f.Severity == AuditSeverity.Low);
    public int TotalFindings => Findings.Count;
}
