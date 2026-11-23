using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Sessions;

/// <summary>
/// Thread-safe in-memory session manager for tracking computer use sessions, execution history, and steps.
/// </summary>
public sealed class ComputerUseSessionManager : IComputerUseSessionManager
{
    private readonly ConcurrentDictionary<string, ComputerUseSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _sessionOrder = new();
    private readonly object _lock = new();
    private string? _activeSessionId;

    public ComputerUseSession? ActiveSession
    {
        get
        {
            lock (_lock)
            {
                return _activeSessionId != null && _sessions.TryGetValue(_activeSessionId, out var s) ? s : null;
            }
        }
    }

    public ComputerUseSession CreateSession(string goal, ComputerUseSessionOptions? options = null)
    {
        var session = new ComputerUseSession
        {
            Goal = goal,
            Options = options ?? new ComputerUseSessionOptions(),
            Status = ComputerUseSessionStatus.Initializing,
            StartTime = DateTimeOffset.UtcNow
        };

        _sessions[session.SessionId] = session;

        lock (_lock)
        {
            _sessionOrder.Insert(0, session.SessionId);
            _activeSessionId = session.SessionId;
        }

        return session;
    }

    public ComputerUseSession? GetSession(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return null;
        _sessions.TryGetValue(sessionId, out var s);
        return s;
    }

    public IReadOnlyList<ComputerUseSession> ListSessions()
    {
        lock (_lock)
        {
            return _sessionOrder
                .Select(id => _sessions.TryGetValue(id, out var s) ? s : null)
                .Where(s => s != null)
                .Select(s => s!)
                .ToList();
        }
    }

    public void AddStep(string sessionId, ComputerUseStep step)
    {
        if (_sessions.TryGetValue(sessionId, out var s))
        {
            lock (s)
            {
                s.Steps.Add(step);
            }
        }
    }

    public void UpdateSessionStatus(string sessionId, ComputerUseSessionStatus status, string? summary = null, string? error = null)
    {
        if (_sessions.TryGetValue(sessionId, out var s))
        {
            lock (s)
            {
                s.Status = status;
                if (summary != null) s.ResultSummary = summary;
                if (error != null) s.ErrorMessage = error;

                if (status is ComputerUseSessionStatus.Completed or ComputerUseSessionStatus.Failed or ComputerUseSessionStatus.StoppedByUser)
                {
                    s.EndTime = DateTimeOffset.UtcNow;
                }
            }
        }
    }

    public void SetActiveModel(string sessionId, string modelId)
    {
        if (_sessions.TryGetValue(sessionId, out var s))
        {
            lock (s)
            {
                s.ActiveModel = modelId;
            }
        }
    }

    public void RecordTokensUsed(string sessionId, int tokens)
    {
        if (tokens <= 0) return;
        if (_sessions.TryGetValue(sessionId, out var s))
        {
            lock (s)
            {
                s.TotalTokensUsed += tokens;
            }
        }
    }
}
