using System;
using System.Collections.Generic;
using OpenClaw.Shared.Coding.BuildAnything;
using OpenClaw.Shared.Coding.CliIntelligence;
using OpenClaw.Shared.Coding.CyberSecurity;
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.Coding.ReverseEngineering;

namespace OpenClaw.Shared.Coding;

/// <summary>
/// Unified shared workspace context for the SPARK Code Division.
/// Seamlessly bridges AI Development, Build Anything, Reverse Engineering,
/// CLI Intelligence, and Cybersecurity Auditing without losing state.
/// </summary>
public sealed class CodingDivisionWorkspaceContext
{
    private static readonly Lazy<CodingDivisionWorkspaceContext> s_instance = new(() => new CodingDivisionWorkspaceContext());
    public static CodingDivisionWorkspaceContext Instance => s_instance.Value;

    public string? WorkspaceRoot { get; set; }
    public RepositoryAnalysis? Analysis { get; set; }
    public CodingPlan? ActivePlan { get; set; }
    public List<FileChangeRecord> ModifiedFiles { get; } = new();
    public List<CodingMessage> ConversationHistory { get; } = new();

    // Integrated Capabilities State
    public SecurityAuditSession? ActiveAuditSession { get; set; }
    public CraftCreationSession? ActiveBuildSession { get; set; }
    public ReaInspectionResult? ActiveReverseEngineeringResult { get; set; }
    public List<CliExecutionRecord> CliHistory { get; } = new();

    public event EventHandler? ContextChanged;

    public void SetWorkspace(string root, RepositoryAnalysis? analysis = null)
    {
        WorkspaceRoot = root;
        Analysis = analysis;
        NotifyChanged();
    }

    public void NotifyChanged()
    {
        ContextChanged?.Invoke(this, EventArgs.Empty);
    }
}
