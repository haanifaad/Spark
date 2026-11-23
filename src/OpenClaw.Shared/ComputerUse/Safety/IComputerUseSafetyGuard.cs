using System;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Safety;

/// <summary>
/// Safety evaluation result for a planned computer use action.
/// </summary>
public sealed record SafetyEvaluationResult(
    bool IsAllowed,
    bool RequiresUserConfirmation,
    string? Reason,
    string? SensitiveCategory);

/// <summary>
/// Safety and permissions contract for protecting the user's machine during computer use.
/// </summary>
public interface IComputerUseSafetyGuard
{
    /// <summary>
    /// Gets whether the user has explicitly granted desktop control permission.
    /// </summary>
    bool IsDesktopControlPermitted { get; }

    /// <summary>
    /// Gets whether an emergency stop has been activated.
    /// </summary>
    bool IsEmergencyStopped { get; }

    /// <summary>
    /// Enables or disables desktop control permissions.
    /// </summary>
    void SetDesktopControlPermitted(bool permitted);

    /// <summary>
    /// Immediately halts all actuation and locks down the computer use engine.
    /// </summary>
    void TriggerEmergencyStop();

    /// <summary>
    /// Resets the emergency stop latch after explicit user review.
    /// </summary>
    void ResetEmergencyStop();

    /// <summary>
    /// Evaluates the planned action against safety policies, injection risks, and sensitive criteria.
    /// </summary>
    Task<SafetyEvaluationResult> EvaluateActionAsync(
        ComputerUseAction action,
        PerceptionSnapshot currentPerception,
        CancellationToken ct = default);

    /// <summary>
    /// Event triggered when an emergency stop occurs.
    /// </summary>
    event EventHandler? EmergencyStopTriggered;
}
