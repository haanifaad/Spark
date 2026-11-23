using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Tools;

/// <summary>
/// Event arguments for tool execution telemetry and auditing.
/// </summary>
public class SparkToolExecutionEventArgs : EventArgs
{
    public SparkToolExecutionRequest Request { get; }
    public SparkToolExecutionResponse Response { get; }

    public SparkToolExecutionEventArgs(SparkToolExecutionRequest request, SparkToolExecutionResponse response)
    {
        Request = request;
        Response = response;
    }
}

/// <summary>
/// Central registry managing all executable tools, permissions, risk levels, and execution dispatching.
/// </summary>
public interface ISparkToolRegistry
{
    event EventHandler<SparkToolExecutionEventArgs>? ToolExecutionCompleted;

    void RegisterTool(ISparkTool tool);

    void RegisterStandardTools(Filesystem.SparkFilesystemBoundary? filesystemBoundary = null);

    bool UnregisterTool(string toolId);

    ISparkTool? GetTool(string toolId);

    IReadOnlyList<SparkToolMetadata> ListTools(
        SparkToolCategory? category = null,
        SparkToolRiskLevel? maxRiskLevel = null,
        bool includeDisabled = false);

    Task<SparkToolExecutionResponse> ExecuteToolAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}
