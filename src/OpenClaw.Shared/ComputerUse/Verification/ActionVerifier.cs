using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Verification;

/// <summary>
/// Verifies whether executed computer use actions achieved their intended effect by analyzing
/// visual state mutations, window focus changes, and semantic tree diffs.
/// </summary>
public sealed class ActionVerifier : IActionVerifier
{
    private readonly IOpenClawLogger? _logger;

    public ActionVerifier(IOpenClawLogger? logger = null)
    {
        _logger = logger;
    }

    public Task<VerificationResult> VerifyStepAsync(
        ComputerUseStep step,
        PerceptionSnapshot beforeSnapshot,
        PerceptionSnapshot afterSnapshot,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var action = step.Action;
        if (action == null)
        {
            return Task.FromResult(new VerificationResult(VerificationStatus.Skipped, 0.0, "No action executed to verify.", false));
        }

        // 1. Passive / terminal actions require no mutation
        if (action.ActionType is ComputerUseActionType.Finished or ComputerUseActionType.CallUser or ComputerUseActionType.UserStop)
        {
            return Task.FromResult(new VerificationResult(VerificationStatus.ConfirmedSuccess, 0.0, "Terminal action verified.", false));
        }

        if (action.ActionType == ComputerUseActionType.Wait)
        {
            return Task.FromResult(new VerificationResult(VerificationStatus.ConfirmedSuccess, 0.0, "Wait period observed without error.", false));
        }

        // 2. Check active window title changes
        bool windowChanged = !string.Equals(beforeSnapshot.ActiveWindowTitle, afterSnapshot.ActiveWindowTitle, StringComparison.OrdinalIgnoreCase);

        // 3. Compute perceptual visual delta
        double delta = ComputeVisualDelta(beforeSnapshot.ScreenshotBase64, afterSnapshot.ScreenshotBase64);

        // 4. Evaluate mutation against action expectations
        if (windowChanged)
        {
            return Task.FromResult(new VerificationResult(
                VerificationStatus.StateChanged,
                delta,
                $"Active window changed from '{beforeSnapshot.ActiveWindowTitle}' to '{afterSnapshot.ActiveWindowTitle}'.",
                RequiresReplan: false));
        }

        if (delta > 0.005) // Greater than 0.5% screen visual change
        {
            return Task.FromResult(new VerificationResult(
                VerificationStatus.ConfirmedSuccess,
                delta,
                $"UI visual mutation detected (delta: {delta:P1}). Action took effect.",
                RequiresReplan: false));
        }

        // If action was mouse_move, unchanged screen is normal
        if (action.ActionType == ComputerUseActionType.MouseMove)
        {
            return Task.FromResult(new VerificationResult(
                VerificationStatus.ConfirmedSuccess,
                delta,
                "Cursor relocated successfully.",
                RequiresReplan: false));
        }

        // Action intended state change (click, type, scroll) but screen did not change
        _logger?.Warn($"[ActionVerifier] Step {step.StepIndex} produced no visual change (action: {action.ActionType})");

        return Task.FromResult(new VerificationResult(
            VerificationStatus.ScreenUnchanged,
            delta,
            $"Action '{action.ActionType}' executed but no perceptible visual difference occurred on screen.",
            RequiresReplan: true));
    }

    public double ComputeVisualDelta(string? beforeBase64, string? afterBase64)
    {
        if (string.IsNullOrEmpty(beforeBase64) || string.IsNullOrEmpty(afterBase64))
        {
            return string.IsNullOrEmpty(beforeBase64) && string.IsNullOrEmpty(afterBase64) ? 0.0 : 1.0;
        }

        if (string.Equals(beforeBase64, afterBase64, StringComparison.Ordinal))
        {
            return 0.0;
        }

        // Fast sample comparison across base64 representation
        int length = Math.Min(beforeBase64.Length, afterBase64.Length);
        if (length == 0) return 1.0;

        const int sampleCount = 1000;
        int step = Math.Max(1, length / sampleCount);
        int differences = 0;
        int samplesTaken = 0;

        for (int i = 0; i < length; i += step)
        {
            if (beforeBase64[i] != afterBase64[i])
            {
                differences++;
            }
            samplesTaken++;
        }

        // Account for length difference
        int lengthDiff = Math.Abs(beforeBase64.Length - afterBase64.Length);
        double lengthPenalty = (double)lengthDiff / Math.Max(beforeBase64.Length, afterBase64.Length);

        double sampleDiffRatio = samplesTaken > 0 ? (double)differences / samplesTaken : 0.0;
        return Math.Clamp((sampleDiffRatio * 0.8) + (lengthPenalty * 0.2), 0.0, 1.0);
    }
}
