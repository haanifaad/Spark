using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Types;
using OpenClaw.Shared.Inference.Routing;

namespace OpenClaw.Shared.ComputerUse.Providers;

/// <summary>
/// Request sent to a vision-language model for computer-use reasoning.
/// </summary>
public sealed class ComputerUseModelRequest
{
    public string Goal { get; init; } = string.Empty;
    public string CurrentScreenshotBase64 { get; init; } = string.Empty;
    public string? ActiveWindowTitle { get; init; }
    public IReadOnlyList<ComputerUseStep> HistorySteps { get; init; } = Array.Empty<ComputerUseStep>();
    public IReadOnlyList<PerceptionElement> VisibleElements { get; init; } = Array.Empty<PerceptionElement>();
    public string? PreferredModelId { get; init; }
    public SparkModelDescriptor? ModelDescriptor { get; init; }
    public double Temperature { get; init; } = 0.1;
    public int MaxTokens { get; init; } = 1024;
}

/// <summary>
/// Response received from the vision-language model.
/// </summary>
public sealed class ComputerUseModelResponse
{
    public bool Success { get; init; }
    public string RawPrediction { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public int TokensUsed { get; init; }
    public double LatencyMs { get; init; }
    public string? ErrorMessage { get; init; }

    public static ComputerUseModelResponse Ok(string prediction, string modelId, int tokens, double latencyMs) =>
        new() { Success = true, RawPrediction = prediction, ModelId = modelId, TokensUsed = tokens, LatencyMs = latencyMs };

    public static ComputerUseModelResponse Fail(string error, string modelId = "unknown") =>
        new() { Success = false, ErrorMessage = error, ModelId = modelId };
}

/// <summary>
/// Provider interface for model reasoning in Computer Use.
/// Integrates with SPARK's model routing without duplicating provider systems.
/// </summary>
public interface IComputerUseModelProvider
{
    /// <summary>
    /// Invokes the VLM to inspect the current desktop perception and generate the next action.
    /// </summary>
    Task<ComputerUseModelResponse> GenerateActionAsync(
        ComputerUseModelRequest request,
        CancellationToken ct = default);
}
