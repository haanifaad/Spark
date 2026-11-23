using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Inference.Routing;

/// <summary>
/// Capability-based, privacy-enforced multi-model intelligence router.
/// </summary>
public interface ISparkModelRouter
{
    void RegisterModel(SparkModelDescriptor model);

    bool UnregisterModel(string modelId);

    IReadOnlyList<SparkModelDescriptor> ListModels(bool availableOnly = true);

    SparkModelDescriptor? GetModel(string modelId);

    /// <summary>
    /// Calculates the optimal model and fallback chain for a given task specification.
    /// </summary>
    SparkRoutingDecision Route(SparkRoutingRequest request);

    /// <summary>
    /// Executes an inference call with automatic retry on transient failure and progressive fallback along the chain.
    /// </summary>
    Task<SparkInferenceResult> ExecuteWithFallbackAsync(
        SparkRoutingRequest request,
        Func<SparkModelDescriptor, CancellationToken, Task<string>> modelInvoker,
        CancellationToken cancellationToken = default);
}
