using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Verification;

/// <summary>
/// Contract for verifying action execution effects on desktop state.
/// </summary>
public interface IActionVerifier
{
    /// <summary>
    /// Verifies the execution of a step by comparing before-and-after observations.
    /// </summary>
    Task<VerificationResult> VerifyStepAsync(
        ComputerUseStep step,
        PerceptionSnapshot beforeSnapshot,
        PerceptionSnapshot afterSnapshot,
        CancellationToken ct = default);

    /// <summary>
    /// Computes perceptual visual difference ratio between two Base64 image buffers (0.0 = identical, 1.0 = completely changed).
    /// </summary>
    double ComputeVisualDelta(string? beforeBase64, string? afterBase64);
}
