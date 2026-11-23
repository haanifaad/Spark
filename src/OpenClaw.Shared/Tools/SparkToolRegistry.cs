using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Security.Sentinel;
using OpenClaw.Shared.Tools.Browser;
using OpenClaw.Shared.Tools.Connectors;
using OpenClaw.Shared.Tools.Development;
using OpenClaw.Shared.Tools.Filesystem;

namespace OpenClaw.Shared.Tools;

/// <summary>
/// Thread-safe implementation of the unified tool registry.
/// Enforces permission requirements, execution timeouts, risk classifications, and audit events.
/// </summary>
public class SparkToolRegistry : ISparkToolRegistry
{
    private readonly ConcurrentDictionary<string, ISparkTool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ISentinelReviewer? _sentinel;

    public SparkToolRegistry(ISentinelReviewer? sentinel = null)
    {
        _sentinel = sentinel;
    }

    public event EventHandler<SparkToolExecutionEventArgs>? ToolExecutionCompleted;

    public void RegisterTool(ISparkTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (string.IsNullOrWhiteSpace(tool.Metadata.Id))
        {
            throw new ArgumentException("Tool must provide a non-empty Id.", nameof(tool));
        }

        _tools[tool.Metadata.Id] = tool;
    }

    public void RegisterStandardTools(SparkFilesystemBoundary? filesystemBoundary = null)
    {
        var boundary = filesystemBoundary ?? new SparkFilesystemBoundary();
        RegisterTool(new SparkReadFileTool(boundary));
        RegisterTool(new SparkWriteFileTool(boundary));
        RegisterTool(new SparkEditFileTool(boundary));
        RegisterTool(new SparkListDirectoryTool(boundary));
        RegisterTool(new SparkDeleteFileTool(boundary));
        RegisterTool(new SparkGitStatusTool());
        RegisterTool(new SparkGitDiffTool());
        RegisterTool(new SparkRunCommandTool());
        RegisterTool(new SparkBuildTestTool());
        RegisterTool(new SparkBrowserNavigateTool());
        RegisterTool(new SparkBrowserSearchTool());
        RegisterTool(new SparkBrowserDownloadTool(boundary));

        var notesConnector = new LocalProductivityNotesConnector();
        RegisterTool(new SparkNotesTool(notesConnector, "create"));
        RegisterTool(new SparkNotesTool(notesConnector, "list"));
        RegisterTool(new SparkNotesTool(notesConnector, "search"));
    }

    public bool UnregisterTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId)) return false;
        return _tools.TryRemove(toolId, out _);
    }

    public ISparkTool? GetTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId)) return null;
        return _tools.TryGetValue(toolId, out var tool) ? tool : null;
    }

    public IReadOnlyList<SparkToolMetadata> ListTools(
        SparkToolCategory? category = null,
        SparkToolRiskLevel? maxRiskLevel = null,
        bool includeDisabled = false)
    {
        var query = _tools.Values.Select(t => t.Metadata);

        if (!includeDisabled)
        {
            query = query.Where(m => m.IsEnabled);
        }

        if (category.HasValue)
        {
            query = query.Where(m => m.Category == category.Value);
        }

        if (maxRiskLevel.HasValue)
        {
            query = query.Where(m => m.RiskLevel <= maxRiskLevel.Value);
        }

        return query.OrderBy(m => m.Category).ThenBy(m => m.Name).ToList();
    }

    public async Task<SparkToolExecutionResponse> ExecuteToolAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.ToolId))
        {
            sw.Stop();
            var resp = SparkToolExecutionResponse.Fail(
                string.Empty,
                "ToolId cannot be null or empty.",
                SparkToolRiskLevel.Low,
                sw.Elapsed);
            OnExecutionCompleted(request, resp);
            return resp;
        }

        if (!_tools.TryGetValue(request.ToolId, out var tool))
        {
            sw.Stop();
            var resp = SparkToolExecutionResponse.Fail(
                request.ToolId,
                $"Tool '{request.ToolId}' is not registered in the system tool catalog.",
                SparkToolRiskLevel.Low,
                sw.Elapsed);
            OnExecutionCompleted(request, resp);
            return resp;
        }

        var meta = tool.Metadata;
        if (!meta.IsEnabled)
        {
            sw.Stop();
            var resp = SparkToolExecutionResponse.Fail(
                request.ToolId,
                $"Tool '{request.ToolId}' is currently disabled by administrative policy.",
                meta.RiskLevel,
                sw.Elapsed);
            OnExecutionCompleted(request, resp);
            return resp;
        }

        // 1. Permission verification
        if (meta.RequiredPermissions.Count > 0)
        {
            var missing = meta.RequiredPermissions
                .Where(p => !request.GrantedPermissions.Contains(p))
                .ToList();

            if (missing.Count > 0)
            {
                sw.Stop();
                var resp = SparkToolExecutionResponse.Fail(
                    request.ToolId,
                    $"Execution denied: Caller lacks required permissions [{string.Join(", ", missing)}].",
                    meta.RiskLevel,
                    sw.Elapsed);
                OnExecutionCompleted(request, resp);
                return resp;
            }
        }

        // 2. Sentinel safety check at execution boundary
        if (_sentinel != null)
        {
            var payload = request.Arguments.Count > 0
                ? string.Join(" ", request.Arguments.Select(kv => $"{kv.Key}={kv.Value}"))
                : string.Empty;

            var sentinelReq = new SentinelEvaluationRequest
            {
                ProposedToolName = meta.Id,
                OperationArguments = request.Arguments,
                TaskOriginalInstruction = request.CallerIdentity,
                DataPayloadText = payload,
                SessionId = request.SessionId
            };

            var verdict = await _sentinel.ReviewOperationAsync(sentinelReq, cancellationToken).ConfigureAwait(false);
            if (verdict.Decision == SentinelDecisionKind.Rejected)
            {
                sw.Stop();
                var resp = SparkToolExecutionResponse.Fail(
                    request.ToolId,
                    $"Execution rejected by Sentinel security review: {string.Join("; ", verdict.Reasons)}",
                    meta.RiskLevel,
                    sw.Elapsed);
                OnExecutionCompleted(request, resp);
                return resp;
            }

            if (verdict.Decision == SentinelDecisionKind.RequiresUserApproval && !request.IsApproved && string.IsNullOrWhiteSpace(request.ApprovalId))
            {
                sw.Stop();
                var resp = SparkToolExecutionResponse.Fail(
                    request.ToolId,
                    $"Execution denied: Sentinel policy requires explicit user approval before executing '{request.ToolId}'. {string.Join("; ", verdict.Reasons)}",
                    meta.RiskLevel,
                    sw.Elapsed);
                OnExecutionCompleted(request, resp);
                return resp;
            }
        }

        // 3. Dry run preview support
        if (request.DryRun)
        {
            sw.Stop();
            var dryRunResult = new
            {
                dryRun = true,
                message = $"Simulated dry-run for tool '{meta.Name}' ({meta.Id}). No state was altered.",
                riskLevel = meta.RiskLevel.ToString(),
                requiredPermissions = meta.RequiredPermissions
            };
            var resp = SparkToolExecutionResponse.Ok(meta.Id, dryRunResult, meta.RiskLevel, sw.Elapsed);
            OnExecutionCompleted(request, resp);
            return resp;
        }

        // 3. Bounded execution timeout wrapping
        var effectiveTimeout = meta.DefaultTimeout > TimeSpan.Zero
            ? meta.DefaultTimeout
            : TimeSpan.FromSeconds(30);

        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var response = await tool.ExecuteAsync(request, linkedCts.Token).ConfigureAwait(false);
            sw.Stop();
            response.Duration = sw.Elapsed;
            response.RiskLevel = meta.RiskLevel;
            OnExecutionCompleted(request, response);
            return response;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            var resp = SparkToolExecutionResponse.Fail(
                meta.Id,
                $"Execution timed out after {effectiveTimeout.TotalSeconds:0.##}s bounded limit.",
                meta.RiskLevel,
                sw.Elapsed);
            OnExecutionCompleted(request, resp);
            return resp;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            var resp = SparkToolExecutionResponse.Fail(
                meta.Id,
                "Tool execution was canceled by caller.",
                meta.RiskLevel,
                sw.Elapsed);
            OnExecutionCompleted(request, resp);
            return resp;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var resp = SparkToolExecutionResponse.Fail(
                meta.Id,
                $"Tool execution error: {ex.Message}",
                meta.RiskLevel,
                sw.Elapsed);
            OnExecutionCompleted(request, resp);
            return resp;
        }
    }

    private void OnExecutionCompleted(SparkToolExecutionRequest req, SparkToolExecutionResponse res)
    {
        try
        {
            ToolExecutionCompleted?.Invoke(this, new SparkToolExecutionEventArgs(req, res));
        }
        catch
        {
            // Logging / event delivery should never crash registry execution
        }
    }
}
