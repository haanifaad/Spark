namespace OpenClaw.Shared.Security.Sentinel;

/// <summary>
/// Independent safety reviewer evaluating proposed high-risk agent operations before execution.
/// Enforces deterministic checks that cannot be bypassed by the primary agent or LLMs.
/// </summary>
public interface ISentinelReviewer
{
    Task<SentinelReviewResult> ReviewOperationAsync(SentinelEvaluationRequest request, CancellationToken ct = default);
}
