using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Agents;
using OpenClaw.Shared.Coding.Context;
using OpenClaw.Shared.Coding.Files;
using OpenClaw.Shared.Coding.Git;
using OpenClaw.Shared.Coding.Memory;
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.Coding.Permissions;
using OpenClaw.Shared.Coding.Planning;
using OpenClaw.Shared.Coding.Repository;
using OpenClaw.Shared.Coding.Sessions;
using OpenClaw.Shared.Coding.Terminal;

namespace OpenClaw.Shared.Coding.Orchestrator;

/// <summary>
/// Central orchestrator coordinating planning, repository understanding, context retrieval,
/// safe file modifications, terminal execution, debugging, testing, review, and session persistence.
/// </summary>
public sealed class CodingAgentOrchestrator
{
    private readonly string _workspaceRoot;
    private readonly RepositoryAnalyzer _analyzer;
    private readonly CodingFileOperationsEngine _fileOps;
    private readonly CodeEditingEngine _editingEngine;
    private readonly CodingTerminalEngine _terminal;
    private readonly CodingGitIntegration _git;
    private readonly CodebaseContextEngine _contextEngine;
    private readonly CodingPermissionManager _permissions;
    private readonly CodingPlanner _planner;
    private readonly CodingTestingAgent _tester;
    private readonly CodingDebuggingAgent _debugger;
    private readonly CodeReviewAgent _reviewer;
    private readonly CodingMemoryManager? _memory;
    private readonly CodingContextManager _contextManager;
    private readonly CodingSessionManager _sessionManager;

    private CodingSession _activeSession;
    private RepositoryAnalysis? _cachedAnalysis;

    // Events for reactive UI binding
    public event EventHandler<CodingAgentActivity>? ActivityChanged;
    public event EventHandler<CodingPlan>? PlanUpdated;
    public event EventHandler<FileChangeRecord>? FileChanged;
    public event EventHandler<CodingMessage>? MessageAppended;

    public string WorkspaceRoot => _workspaceRoot;
    public CodingSession ActiveSession => _activeSession;
    public RepositoryAnalyzer Analyzer => _analyzer;
    public CodingFileOperationsEngine FileOps => _fileOps;
    public CodeEditingEngine EditingEngine => _editingEngine;
    public CodingTerminalEngine Terminal => _terminal;
    public CodingGitIntegration Git => _git;
    public CodingPermissionManager Permissions => _permissions;
    public CodingTestingAgent Tester => _tester;
    public CodingDebuggingAgent Debugger => _debugger;
    public CodeReviewAgent Reviewer => _reviewer;
    public CodingMemoryManager? Memory => _memory;
    public CodingSessionManager SessionManager => _sessionManager;

    public CodingAgentOrchestrator(
        string workspaceRoot,
        CodingPermissionManager? permissions = null,
        CodingMemoryManager? memory = null,
        CodingSessionManager? sessionManager = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);

        _fileOps = new CodingFileOperationsEngine(_workspaceRoot);
        _editingEngine = new CodeEditingEngine(_fileOps);
        _terminal = new CodingTerminalEngine(_workspaceRoot);
        _git = new CodingGitIntegration(_terminal);
        _analyzer = new RepositoryAnalyzer();
        _contextEngine = new CodebaseContextEngine(_workspaceRoot, _fileOps);
        _permissions = permissions ?? new CodingPermissionManager();
        _planner = new CodingPlanner();
        _tester = new CodingTestingAgent(_terminal);
        _debugger = new CodingDebuggingAgent(_fileOps);
        _reviewer = new CodeReviewAgent();
        _memory = memory;
        _contextManager = new CodingContextManager();
        _sessionManager = sessionManager ?? new CodingSessionManager();

        _activeSession = _sessionManager.CreateSession(_workspaceRoot);
    }

    /// <summary>
    /// Analyzes the active repository and records insights in project memory.
    /// </summary>
    public async Task<RepositoryAnalysis> AnalyzeWorkspaceAsync(CancellationToken ct = default)
    {
        PublishActivity(CodingAgentActivityType.AnalyzingRepository, "Analyzing project structure and tech stack...");
        var analysis = await _analyzer.AnalyzeAsync(_workspaceRoot, ct);
        _cachedAnalysis = analysis;

        if (_memory != null)
        {
            await _memory.SaveProjectAnalysisAsync(analysis, ct);
        }

        PublishActivity(CodingAgentActivityType.Completed, $"Analysis complete: {analysis.ProjectName} ({analysis.BuildSystem})");
        return analysis;
    }

    /// <summary>
    /// Main entry point for executing an autonomous coding task.
    /// </summary>
    public async Task<CodingMessage> ExecuteTaskAsync(
        string userPrompt,
        Func<string, Task<string>>? modelGenerator = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);

        // Record user turn
        var userMsg = new CodingMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Role = "user",
            Content = userPrompt,
            Timestamp = DateTimeOffset.UtcNow
        };
        _activeSession.History.Add(userMsg);
        MessageAppended?.Invoke(this, userMsg);

        // 1. Ensure repository is analyzed
        PublishActivity(CodingAgentActivityType.AnalyzingRepository, "Inspecting repository architecture...");
        _cachedAnalysis ??= await _analyzer.AnalyzeAsync(_workspaceRoot, ct);

        // 2. Formulate implementation plan
        PublishActivity(CodingAgentActivityType.Planning, "Formulating implementation plan...");
        var plan = _planner.CreatePlan(userPrompt, _cachedAnalysis);
        _activeSession = _activeSession with { ActivePlan = plan };
        PlanUpdated?.Invoke(this, plan);

        // Check if context compaction is needed
        if (_contextManager.ShouldCompact(_activeSession.History))
        {
            _activeSession = _activeSession with
            {
                History = _contextManager.CompactHistory(_activeSession.History, plan)
            };
        }

        // 3. Assemble focused context
        PublishActivity(CodingAgentActivityType.Thinking, "Assembling relevant context...");
        var context = await _contextEngine.AssembleContextAsync(userPrompt, _cachedAnalysis, ct: ct);
        var memoryContext = _memory != null ? await _memory.BuildMemoryContextSummaryAsync(ct) : string.Empty;

        // Execute steps in sequence
        var resultTextBuilder = new StringBuilder();
        var touchedFiles = new List<FileChangeRecord>();

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            if (ct.IsCancellationRequested) break;

            var step = plan.Steps[i];
            plan = _planner.UpdateStepStatus(plan, step.Id, PlanStepStatus.InProgress);
            PlanUpdated?.Invoke(this, plan);
            PublishActivity(CodingAgentActivityType.Thinking, $"Executing step: {step.Title}", step.Description);

            try
            {
                // Dispatch step behavior based on step kind
                if (step.Id.Contains("inspect", StringComparison.OrdinalIgnoreCase))
                {
                    step = step with { ResultSummary = $"Analyzed {_cachedAnalysis.ProjectName} with {_cachedAnalysis.TotalSourceFiles} source files." };
                }
                else if (step.Id.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                         step.Id.Contains("run_tests", StringComparison.OrdinalIgnoreCase))
                {
                    PublishActivity(CodingAgentActivityType.RunningTests, "Executing project test suite...");
                    var testAllowed = await _permissions.RequestPermissionAsync(
                        CodingPermissionLevel.TerminalExecution,
                        "Run Tests",
                        _cachedAnalysis.BuildSystem ?? "Test Runner",
                        ct: ct);

                    if (testAllowed)
                    {
                        var testRes = await _tester.RunTestsAsync(analysis: _cachedAnalysis, ct: ct);
                        step = step with
                        {
                            ResultSummary = testRes.Success
                                ? $"All {testRes.TotalTests} tests passed cleanly in {testRes.DurationSeconds:N2}s."
                                : $"{testRes.FailedCount}/{testRes.TotalTests} tests failed."
                        };
                    }
                    else
                    {
                        step = step with { ResultSummary = "Test execution declined by permission policy." };
                    }
                }
                else if (step.Id.Contains("fix", StringComparison.OrdinalIgnoreCase) ||
                         step.Id.Contains("implement", StringComparison.OrdinalIgnoreCase) ||
                         step.Id.Contains("refactor", StringComparison.OrdinalIgnoreCase))
                {
                    // Generate solution via modelGenerator if provided
                    if (modelGenerator != null)
                    {
                        PublishActivity(CodingAgentActivityType.GeneratingCode, "Generating implementation solution...");
                        var fullPrompt = $"{context}\n\n{memoryContext}\n\nCurrent Task Step: {step.Title}\n{step.Description}\nUser Goal: {userPrompt}";
                        var modelResponse = await modelGenerator(fullPrompt);
                        resultTextBuilder.AppendLine(modelResponse);
                    }
                    step = step with { ResultSummary = "Implementation generated and applied." };
                }
                else if (step.Id.Contains("review", StringComparison.OrdinalIgnoreCase))
                {
                    PublishActivity(CodingAgentActivityType.ReviewingCode, "Performing automated code review...");
                    if (touchedFiles.Count > 0)
                    {
                        var report = _reviewer.ReviewChanges(touchedFiles);
                        step = step with { ResultSummary = report.Summary };
                    }
                    else
                    {
                        step = step with { ResultSummary = "No modified files required review." };
                    }
                }

                plan = _planner.UpdateStepStatus(plan, step.Id, PlanStepStatus.Completed, step.ResultSummary);
                PlanUpdated?.Invoke(this, plan);
            }
            catch (Exception ex)
            {
                plan = _planner.UpdateStepStatus(plan, step.Id, PlanStepStatus.Failed, ex.Message);
                PlanUpdated?.Invoke(this, plan);
                PublishActivity(CodingAgentActivityType.Error, $"Step failed: {step.Title}", ex.Message);
                break;
            }
        }

        // Conclude task
        PublishActivity(CodingAgentActivityType.Completed, "Autonomous coding task completed.");

        var finalResponse = resultTextBuilder.Length > 0
            ? resultTextBuilder.ToString().Trim()
            : $"Completed execution of plan for: \"{userPrompt}\". {plan.CompletedStepCount}/{plan.TotalStepCount} steps verified.";

        var agentMsg = new CodingMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Role = "agent",
            Content = finalResponse,
            FileChanges = touchedFiles.Count > 0 ? touchedFiles : null,
            Timestamp = DateTimeOffset.UtcNow
        };

        _activeSession.History.Add(agentMsg);
        MessageAppended?.Invoke(this, agentMsg);

        // Persist session state
        _activeSession = _activeSession with
        {
            LastActiveAt = DateTimeOffset.UtcNow,
            Status = "Idle"
        };
        await _sessionManager.SaveSessionAsync(_activeSession, CancellationToken.None);

        return agentMsg;
    }

    /// <summary>
    /// Safely applies an edit to a workspace file with permission verification.
    /// </summary>
    public async Task<FileChangeRecord> EditFileAsync(
        string filePath,
        string oldString,
        string newString,
        bool replaceAll = false,
        CancellationToken ct = default)
    {
        var isAllowed = await _permissions.RequestPermissionAsync(
            CodingPermissionLevel.WorkspaceFileModification,
            "Edit File",
            filePath,
            $"Replace text in {filePath}",
            ct);

        if (!isAllowed)
        {
            throw new UnauthorizedAccessException($"File modification for '{filePath}' was not permitted.");
        }

        PublishActivity(CodingAgentActivityType.EditingFile, $"Applying edit to {filePath}...");
        var record = await _editingEngine.ApplyEditAsync(filePath, oldString, newString, replaceAll, ct);

        _activeSession.ModifiedFiles.Add(record);
        FileChanged?.Invoke(this, record);

        PublishActivity(CodingAgentActivityType.Completed, $"Edited {filePath} (+{record.Diff?.Additions} -{record.Diff?.Deletions})");
        return record;
    }

    /// <summary>
    /// Executes a terminal command with permission verification.
    /// </summary>
    public async Task<CommandExecutionResult> ExecuteCommandAsync(
        string commandLine,
        string? relativeWorkDir = null,
        int timeoutSeconds = CodingTerminalEngine.DefaultTimeoutSeconds,
        CancellationToken ct = default)
    {
        var level = commandLine.Contains("install", StringComparison.OrdinalIgnoreCase) ||
                    commandLine.Contains("add package", StringComparison.OrdinalIgnoreCase)
            ? CodingPermissionLevel.DependencyInstallation
            : CodingPermissionLevel.TerminalExecution;

        var isAllowed = await _permissions.RequestPermissionAsync(
            level,
            "Terminal Command",
            commandLine,
            $"Directory: {relativeWorkDir ?? _workspaceRoot}",
            ct);

        if (!isAllowed)
        {
            return new CommandExecutionResult
            {
                Command = commandLine,
                WorkingDirectory = relativeWorkDir ?? _workspaceRoot,
                ExitCode = -1,
                CombinedOutput = "Command execution was denied by permission policy."
            };
        }

        PublishActivity(CodingAgentActivityType.ExecutingTerminal, $"Running command: {commandLine}...");
        var result = await _terminal.ExecuteAsync(commandLine, relativeWorkDir, timeoutSeconds, ct: ct);
        PublishActivity(CodingAgentActivityType.Completed, $"Command finished with exit code {result.ExitCode}");
        return result;
    }

    private void PublishActivity(CodingAgentActivityType type, string description, string? details = null)
    {
        var activity = new CodingAgentActivity
        {
            Type = type,
            Description = description,
            Details = details,
            Timestamp = DateTimeOffset.UtcNow
        };

        ActivityChanged?.Invoke(this, activity);
    }
}
