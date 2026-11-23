using System.Collections.Generic;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Sessions;

/// <summary>
/// Manages Computer Use sessions, step history, and telemetry persistence.
/// </summary>
public interface IComputerUseSessionManager
{
    ComputerUseSession? ActiveSession { get; }

    ComputerUseSession CreateSession(string goal, ComputerUseSessionOptions? options = null);

    ComputerUseSession? GetSession(string sessionId);

    IReadOnlyList<ComputerUseSession> ListSessions();

    void AddStep(string sessionId, ComputerUseStep step);

    void UpdateSessionStatus(string sessionId, ComputerUseSessionStatus status, string? summary = null, string? error = null);

    void SetActiveModel(string sessionId, string modelId);

    void RecordTokensUsed(string sessionId, int tokens);
}
