using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Agents;
using OpenClaw.Shared.Coding.BuildAnything;
using OpenClaw.Shared.Coding.CliIntelligence;
using OpenClaw.Shared.Coding.CyberSecurity;
using OpenClaw.Shared.Coding.Memory;
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.Coding.Orchestrator;
using OpenClaw.Shared.Coding.Permissions;
using OpenClaw.Shared.Coding.ReverseEngineering;
using OpenClaw.Shared.Coding.Sessions;
using OpenClaw.Shared.Memory;

namespace OpenClaw.Shared.Coding;

/// <summary>
/// Singleton/scoped service managing the active Coding Division orchestrator,
/// session persistence, and workspace lifecycle.
/// </summary>
public sealed class CodingDivisionService : ICodingDivisionService
{
    private readonly ISparkMemoryStore? _memoryStore;
    private readonly CodingSessionManager _sessionManager;
    private readonly CodingPermissionManager _permissionManager;
    private CodingAgentOrchestrator? _orchestrator;
    private RepositoryAnalysis? _analysis;
    private CliAnythingEngine _cliEngine;

    public string? CurrentWorkspaceRoot => _orchestrator?.WorkspaceRoot;
    public RepositoryAnalysis? CurrentAnalysis => _analysis;
    public CodingSession? CurrentSession => _orchestrator?.ActiveSession;
    public CodingAgentOrchestrator? CurrentOrchestrator => _orchestrator;
    public CodingPermissionManager PermissionManager => _permissionManager;

    public ShannonAuditEngine AuditEngine { get; } = new();
    public CraftEngine CraftEngine { get; } = new();
    public CliAnythingEngine CliEngine => _cliEngine;
    public ReaEngine ReverseEngine { get; } = new();
    public CodingDivisionWorkspaceContext Context => CodingDivisionWorkspaceContext.Instance;

    public event EventHandler<CodingAgentActivity>? ActivityChanged;
    public event EventHandler<CodingPlan>? PlanUpdated;
    public event EventHandler<FileChangeRecord>? FileChanged;
    public event EventHandler<CodingMessage>? MessageAppended;

    public CodingDivisionService(
        ISparkMemoryStore? memoryStore = null,
        CodingPermissionManager? permissionManager = null,
        CodingSessionManager? sessionManager = null)
    {
        _memoryStore = memoryStore;
        _permissionManager = permissionManager ?? new CodingPermissionManager();
        _sessionManager = sessionManager ?? new CodingSessionManager();
        _cliEngine = new CliAnythingEngine(Environment.CurrentDirectory, _permissionManager);
    }

    public async Task<RepositoryAnalysis> OpenWorkspaceAsync(string workspaceRoot, CancellationToken ct = default)
    {
        // Unbind previous orchestrator events
        if (_orchestrator != null)
        {
            _orchestrator.ActivityChanged -= OnActivityChanged;
            _orchestrator.PlanUpdated -= OnPlanUpdated;
            _orchestrator.FileChanged -= OnFileChanged;
            _orchestrator.MessageAppended -= OnMessageAppended;
        }

        var memory = _memoryStore != null ? new CodingMemoryManager(_memoryStore, workspaceRoot) : null;
        _orchestrator = new CodingAgentOrchestrator(workspaceRoot, _permissionManager, memory, _sessionManager);

        _orchestrator.ActivityChanged += OnActivityChanged;
        _orchestrator.PlanUpdated += OnPlanUpdated;
        _orchestrator.FileChanged += OnFileChanged;
        _orchestrator.MessageAppended += OnMessageAppended;

        _cliEngine = new CliAnythingEngine(workspaceRoot, _permissionManager);
        _analysis = await _orchestrator.AnalyzeWorkspaceAsync(ct);
        Context.SetWorkspace(workspaceRoot, _analysis);

        return _analysis;
    }

    public async Task<CodingMessage> SendPromptAsync(string prompt, Func<string, Task<string>>? modelGenerator = null, CancellationToken ct = default)
    {
        EnsureWorkspaceOpened();
        return await _orchestrator!.ExecuteTaskAsync(prompt, modelGenerator, ct);
    }

    public async Task<FileChangeRecord> EditFileAsync(string filePath, string oldString, string newString, bool replaceAll = false, CancellationToken ct = default)
    {
        EnsureWorkspaceOpened();
        return await _orchestrator!.EditFileAsync(filePath, oldString, newString, replaceAll, ct);
    }

    public async Task<CommandExecutionResult> ExecuteCommandAsync(string commandLine, string? workDir = null, CancellationToken ct = default)
    {
        EnsureWorkspaceOpened();
        return await _orchestrator!.ExecuteCommandAsync(commandLine, workDir, ct: ct);
    }

    public async Task<TestRunSummary> RunTestsAsync(string? testCommand = null, CancellationToken ct = default)
    {
        EnsureWorkspaceOpened();
        return await _orchestrator!.Tester.RunTestsAsync(testCommand, _analysis, ct: ct);
    }

    public async Task<(IReadOnlyList<string> Modified, IReadOnlyList<string> Untracked, IReadOnlyList<string> Staged, string RawStatus)> GetGitStatusAsync(CancellationToken ct = default)
    {
        EnsureWorkspaceOpened();
        return await _orchestrator!.Git.GetStatusAsync(ct);
    }

    public async Task<string> GetGitDiffAsync(bool stagedOnly = false, string? filePath = null, CancellationToken ct = default)
    {
        EnsureWorkspaceOpened();
        return await _orchestrator!.Git.GetDiffAsync(stagedOnly, filePath, ct);
    }

    public async Task<bool> RollbackChangeAsync(FileChangeRecord change, CancellationToken ct = default)
    {
        EnsureWorkspaceOpened();
        return await _orchestrator!.FileOps.RevertChangeAsync(change, ct);
    }

    public Task<IReadOnlyList<CodingSession>> GetSavedSessionsAsync(CancellationToken ct = default)
    {
        return _sessionManager.ListSavedSessionsAsync(ct);
    }

    private void EnsureWorkspaceOpened()
    {
        if (_orchestrator == null)
        {
            throw new InvalidOperationException("No workspace is currently open. Call OpenWorkspaceAsync first.");
        }
    }

    private void OnActivityChanged(object? sender, CodingAgentActivity e) => ActivityChanged?.Invoke(this, e);
    private void OnPlanUpdated(object? sender, CodingPlan e) => PlanUpdated?.Invoke(this, e);
    private void OnFileChanged(object? sender, FileChangeRecord e) => FileChanged?.Invoke(this, e);
    private void OnMessageAppended(object? sender, CodingMessage e) => MessageAppended?.Invoke(this, e);
}
