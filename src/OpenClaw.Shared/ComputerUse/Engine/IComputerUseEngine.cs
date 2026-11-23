using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Engine;

/// <summary>
/// Core execution engine coordinating perception, model reasoning, safety checks, Win32 actuation, and verification.
/// </summary>
public interface IComputerUseEngine
{
    /// <summary>
    /// Starts a new computer-use session with the given goal and configuration options.
    /// </summary>
    Task<ComputerUseSession> StartSessionAsync(string goal, ComputerUseSessionOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a single discrete cycle: OBSERVE -> PLAN -> ACT -> OBSERVE -> VERIFY.
    /// </summary>
    Task<ComputerUseStep?> ExecuteStepAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Runs the autonomous execution loop until goal is achieved, max steps reached, or stopped.
    /// </summary>
    Task RunSessionLoopAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Pauses execution of the specified session.
    /// </summary>
    void PauseSession(string sessionId);

    /// <summary>
    /// Resumes execution of a paused session.
    /// </summary>
    void ResumeSession(string sessionId);

    /// <summary>
    /// Gracefully stops the active session.
    /// </summary>
    void StopSession(string sessionId, string reason = "User stopped");

    /// <summary>
    /// Immediately triggers an unrecoverable emergency stop across all sessions.
    /// </summary>
    void TriggerEmergencyStop();

    /// <summary>
    /// Responds to a sensitive action confirmation prompt.
    /// </summary>
    void ConfirmSensitiveAction(string sessionId, bool approved);
}
