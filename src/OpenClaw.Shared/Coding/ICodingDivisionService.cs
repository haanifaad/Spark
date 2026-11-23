using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Agents;
using OpenClaw.Shared.Coding.BuildAnything;
using OpenClaw.Shared.Coding.CliIntelligence;
using OpenClaw.Shared.Coding.CyberSecurity;
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.Coding.Orchestrator;
using OpenClaw.Shared.Coding.ReverseEngineering;

namespace OpenClaw.Shared.Coding;

/// <summary>
/// Service contract exposing the complete SPARK Coding Division capabilities
/// to UI pages, background automations, and external hosts.
/// </summary>
public interface ICodingDivisionService
{
    string? CurrentWorkspaceRoot { get; }
    RepositoryAnalysis? CurrentAnalysis { get; }
    CodingSession? CurrentSession { get; }
    CodingAgentOrchestrator? CurrentOrchestrator { get; }

    ShannonAuditEngine AuditEngine { get; }
    CraftEngine CraftEngine { get; }
    CliAnythingEngine CliEngine { get; }
    ReaEngine ReverseEngine { get; }
    CodingDivisionWorkspaceContext Context { get; }

    event EventHandler<CodingAgentActivity>? ActivityChanged;
    event EventHandler<CodingPlan>? PlanUpdated;
    event EventHandler<FileChangeRecord>? FileChanged;
    event EventHandler<CodingMessage>? MessageAppended;

    Task<RepositoryAnalysis> OpenWorkspaceAsync(string workspaceRoot, CancellationToken ct = default);
    Task<CodingMessage> SendPromptAsync(string prompt, Func<string, Task<string>>? modelGenerator = null, CancellationToken ct = default);
    Task<FileChangeRecord> EditFileAsync(string filePath, string oldString, string newString, bool replaceAll = false, CancellationToken ct = default);
    Task<CommandExecutionResult> ExecuteCommandAsync(string commandLine, string? workDir = null, CancellationToken ct = default);
    Task<TestRunSummary> RunTestsAsync(string? testCommand = null, CancellationToken ct = default);
    Task<(IReadOnlyList<string> Modified, IReadOnlyList<string> Untracked, IReadOnlyList<string> Staged, string RawStatus)> GetGitStatusAsync(CancellationToken ct = default);
    Task<string> GetGitDiffAsync(bool stagedOnly = false, string? filePath = null, CancellationToken ct = default);
    Task<bool> RollbackChangeAsync(FileChangeRecord change, CancellationToken ct = default);
    Task<IReadOnlyList<CodingSession>> GetSavedSessionsAsync(CancellationToken ct = default);
}
