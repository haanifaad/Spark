using System;
using OpenClaw.Shared.ComputerUse.Safety;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Events;

/// <summary>
/// Event hub implementation for streaming live computer-use telemetry and UI updates.
/// </summary>
public sealed class ComputerUseEventHub : IComputerUseEventHub
{
    public event EventHandler<PerceptionSnapshot>? ScreenshotUpdated;
    public event EventHandler<ComputerUseStep>? StepStarted;
    public event EventHandler<ComputerUseAction>? ActionExecuting;
    public event EventHandler<ComputerUseStep>? StepCompleted;
    public event EventHandler<VerificationResult>? VerificationCompleted;
    public event EventHandler<ComputerUseSession>? SessionStatusChanged;
    public event EventHandler<ComputerUseLogEntry>? LogEmitted;
    public event EventHandler<SafetyEvaluationResult>? ConfirmationRequired;

    public void PublishScreenshot(PerceptionSnapshot snapshot) =>
        ScreenshotUpdated?.Invoke(this, snapshot);

    public void PublishStepStarted(ComputerUseStep step) =>
        StepStarted?.Invoke(this, step);

    public void PublishActionExecuting(ComputerUseAction action) =>
        ActionExecuting?.Invoke(this, action);

    public void PublishStepCompleted(ComputerUseStep step) =>
        StepCompleted?.Invoke(this, step);

    public void PublishVerification(VerificationResult verification) =>
        VerificationCompleted?.Invoke(this, verification);

    public void PublishSessionStatus(ComputerUseSession session) =>
        SessionStatusChanged?.Invoke(this, session);

    public void PublishLog(ComputerUseLogLevel level, string source, string message) =>
        LogEmitted?.Invoke(this, new ComputerUseLogEntry(DateTimeOffset.UtcNow, level, source, message));

    public void PublishConfirmationRequired(SafetyEvaluationResult safety) =>
        ConfirmationRequired?.Invoke(this, safety);
}
