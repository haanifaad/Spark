using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Permissions;
using OpenClaw.Shared.Coding.Terminal;

namespace OpenClaw.Shared.Coding.CliIntelligence;

/// <summary>
/// Autonomous CLI intelligence and command execution engine based on CLI Anything.
/// Manages software harness registries, translates natural-language to CLI recipes,
/// enforces safety guardrails, and maintains an auditable execution history.
/// </summary>
public sealed class CliAnythingEngine
{
    private readonly CodingTerminalEngine _terminalEngine;
    private readonly CodingPermissionManager _permissionManager;
    private readonly List<CliHarnessEntry> _harnessRegistry = new();
    private readonly List<CliExecutionRecord> _executionHistory = new();

    public IReadOnlyList<CliHarnessEntry> HarnessRegistry => _harnessRegistry;
    public IReadOnlyList<CliExecutionRecord> ExecutionHistory => _executionHistory;

    public event EventHandler<CliExecutionRecord>? CommandExecuting;
    public event EventHandler<CliExecutionRecord>? CommandCompleted;

    public CliAnythingEngine(string workspaceRoot, CodingPermissionManager? permissionManager = null)
    {
        _terminalEngine = new CodingTerminalEngine(workspaceRoot);
        _permissionManager = permissionManager ?? new CodingPermissionManager();
        PopulateHarnessRegistry();
    }

    /// <summary>
    /// Evaluates the safety tier of a command line before execution.
    /// </summary>
    public static CliCommandSafetyTier ClassifySafetyTier(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return CliCommandSafetyTier.Tier1_ReadOnly;

        var cmd = commandLine.ToLowerInvariant().Trim();

        // Destructive patterns requiring explicit approval
        var destructivePatterns = new[]
        {
            @"\brm\s+-[rf]+",
            @"\bdel\s+/[sfaq]+",
            @"\brmdir\s+/[sq]+",
            @"\bgit\s+reset\s+--hard\b",
            @"\bgit\s+clean\s+-[xfd]+",
            @"\bformat\s+[a-z]:",
            @"\bdd\s+if=",
            @"\bdrop\s+database\b",
            @"\bdrop\s+table\b",
            @"\bkill\s+-9\b",
            @"\bshutdown\b",
            @"\bformat-volume\b"
        };

        foreach (var pattern in destructivePatterns)
        {
            if (Regex.IsMatch(cmd, pattern))
            {
                return CliCommandSafetyTier.Tier3_DestructiveOperation;
            }
        }

        // Modifying patterns
        var modifyingPatterns = new[]
        {
            @"\bmkdir\b", @"\btouch\b", @"\bcp\b", @"\bmv\b", @"\bcopy\b", @"\bmove\b",
            @"\bdotnet\s+build\b", @"\bdotnet\s+run\b", @"\bnpm\s+install\b", @"\bnpm\s+run\b",
            @"\bpip\s+install\b", @"\bcargo\s+build\b", @"\bgit\s+commit\b", @"\bgit\s+add\b"
        };

        foreach (var pattern in modifyingPatterns)
        {
            if (Regex.IsMatch(cmd, pattern))
            {
                return CliCommandSafetyTier.Tier2_SafeModification;
            }
        }

        return CliCommandSafetyTier.Tier1_ReadOnly;
    }

    /// <summary>
    /// Executes a command with safety guardrails, timeouts, and auditable logging.
    /// </summary>
    public async Task<CliExecutionRecord> ExecuteAsync(
        string commandLine,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);

        var safetyTier = ClassifySafetyTier(commandLine);
        var recordId = $"cli_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
        var workDir = workingDirectory ?? _terminalEngine.WorkspaceRoot;

        var record = new CliExecutionRecord
        {
            Id = recordId,
            Command = commandLine,
            WorkingDirectory = workDir,
            SafetyTier = safetyTier,
            ApprovedByUser = true
        };

        // If destructive, verify permission via permission manager
        if (safetyTier == CliCommandSafetyTier.Tier3_DestructiveOperation)
        {
            var isAllowed = await _permissionManager.RequestPermissionAsync(
                Models.CodingPermissionLevel.DestructiveOperation,
                "Execute Destructive Command",
                commandLine,
                ct: ct);

            if (!isAllowed)
            {
                record.ExitCode = -1;
                record.StandardError = "Command execution was rejected by safety policy or declined by user.";
                record.ApprovedByUser = false;
                _executionHistory.Add(record);
                return record;
            }
        }

        CommandExecuting?.Invoke(this, record);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _terminalEngine.ExecuteAsync(commandLine, workDir, ct: ct);
            stopwatch.Stop();

            record.ExitCode = result.ExitCode;
            record.StandardOutput = result.StandardOutput;
            record.StandardError = result.StandardError;
            record.WallTimeSeconds = stopwatch.Elapsed.TotalSeconds;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            record.ExitCode = -1;
            record.StandardError = $"Execution error: {ex.Message}";
            record.WallTimeSeconds = stopwatch.Elapsed.TotalSeconds;
        }

        _executionHistory.Add(record);
        CommandCompleted?.Invoke(this, record);
        return record;
    }

    /// <summary>
    /// Translates natural-language user requests into actionable CLI commands.
    /// </summary>
    public string TranslateNaturalLanguageToCli(string query)
    {
        var q = query.ToLowerInvariant().Trim();

        if (q.Contains("build") && q.Contains("project"))
            return "dotnet build";
        if (q.Contains("run") && q.Contains("test"))
            return "dotnet test";
        if (q.Contains("git status") || q.Contains("changed files") || q.Contains("git state"))
            return "git status --short";
        if (q.Contains("list files") || q.Contains("show directory"))
            return "Get-ChildItem";
        if (q.Contains("docker") && q.Contains("containers"))
            return "docker ps -a";
        if (q.Contains("npm") && q.Contains("start"))
            return "npm start";

        // Harness matching across all built-in CLI Hub harnesses
        var matchedHarness = _harnessRegistry.FirstOrDefault(h =>
            q.Contains(h.Name.ToLowerInvariant()) || 
            q.Contains(h.DisplayName.ToLowerInvariant()) ||
            (!string.IsNullOrEmpty(h.EntryPoint) && q.Contains(h.EntryPoint.ToLowerInvariant())));

        if (matchedHarness != null)
        {
            return $"{matchedHarness.EntryPoint} --help";
        }

        return query; // Default fallback to verbatim
    }

    /// <summary>
    /// Capability workflow matrices available in the SPARK CLI Hub.
    /// </summary>
    public IReadOnlyList<CliMatrixEntry> Matrices => SparkCliHubCatalog.Matrices;

    /// <summary>
    /// Searches the built-in CLI Hub catalog.
    /// </summary>
    public IReadOnlyList<CliHarnessEntry> SearchHarnesses(string? query) => SparkCliHubCatalog.Search(query);

    /// <summary>
    /// Filters harnesses by category.
    /// </summary>
    public IReadOnlyList<CliHarnessEntry> FilterByCategory(string category) => SparkCliHubCatalog.FindByCategory(category);

    /// <summary>
    /// Filters harnesses by capability matrix.
    /// </summary>
    public IReadOnlyList<CliHarnessEntry> FilterByMatrix(string matrixId) => SparkCliHubCatalog.FindByMatrix(matrixId);

    private void PopulateHarnessRegistry()
    {
        _harnessRegistry.AddRange(SparkCliHubCatalog.Harnesses);
    }
}
