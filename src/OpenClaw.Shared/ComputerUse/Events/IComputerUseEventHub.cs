using System;
using OpenClaw.Shared.ComputerUse.Safety;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Events;

/// <summary>
/// Central event hub streaming live screenshots, thoughts, actions, execution logs, and status updates to the UI.
/// </summary>
public interface IComputerUseEventHub
{
    event EventHandler<PerceptionSnapshot>? ScreenshotUpdated;
    event EventHandler<ComputerUseStep>? StepStarted;
    event EventHandler<ComputerUseAction>? ActionExecuting;
    event EventHandler<ComputerUseStep>? StepCompleted;
    event EventHandler<VerificationResult>? VerificationCompleted;
    event EventHandler<ComputerUseSession>? SessionStatusChanged;
    event EventHandler<ComputerUseLogEntry>? LogEmitted;
    event EventHandler<SafetyEvaluationResult>? ConfirmationRequired;

    void PublishScreenshot(PerceptionSnapshot snapshot);
    void PublishStepStarted(ComputerUseStep step);
    void PublishActionExecuting(ComputerUseAction action);
    void PublishStepCompleted(ComputerUseStep step);
    void PublishVerification(VerificationResult verification);
    void PublishSessionStatus(ComputerUseSession session);
    void PublishLog(ComputerUseLogLevel level, string source, string message);
    void PublishConfirmationRequired(SafetyEvaluationResult safety);
}
