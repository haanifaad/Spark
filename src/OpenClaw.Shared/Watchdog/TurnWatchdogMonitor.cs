using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OpenClaw.Shared.Watchdog;

/// <summary>
/// Active execution monitor tracking running agent turns, tool invocations,
/// and child process liveness. Detects wedged operations, dead engine processes,
/// and outputs secret-scrubbed stall notices.
/// </summary>
public sealed class TurnWatchdogMonitor
{
    private static readonly Regex SecretPatterns = new(
        @"(?:sk-[a-zA-Z0-9_\-]{20,}|ghp_[a-zA-Z0-9]{30,}|Bearer\s+[a-zA-Z0-9_\-\.]{20,}|password\s*=\s*[^\s;&]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record WatchedTurn
    {
        public required string TurnId { get; init; }
        public required string SessionId { get; init; }
        public required string ToolName { get; init; }
        public string? Command { get; init; }
        public int? ProcessId { get; init; }
        public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastHeartbeatAt { get; set; } = DateTimeOffset.UtcNow;
        public bool WaitingOnHuman { get; set; }
    }

    public sealed record StallNotice
    {
        public required string TurnId { get; init; }
        public required string SessionId { get; init; }
        public required string Reason { get; init; } // "silent", "engine-exited", "stalled-command"
        public required string NoticeMessage { get; init; }
        public TimeSpan Elapsed { get; init; }
    }

    private readonly ConcurrentDictionary<string, WatchedTurn> _turns = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterTurn(WatchedTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        _turns[turn.TurnId] = turn;
    }

    public void RecordHeartbeat(string turnId)
    {
        if (_turns.TryGetValue(turnId, out var turn))
        {
            turn.LastHeartbeatAt = DateTimeOffset.UtcNow;
        }
    }

    public bool EndTurn(string turnId)
    {
        return _turns.TryRemove(turnId, out _);
    }

    public IReadOnlyList<StallNotice> CheckStalls(TimeSpan stallThreshold)
    {
        var now = DateTimeOffset.UtcNow;
        var notices = new List<StallNotice>();

        foreach (var (turnId, turn) in _turns)
        {
            if (turn.WaitingOnHuman) continue;

            // Check if process has terminated unexpectedly
            if (turn.ProcessId.HasValue && !IsProcessAlive(turn.ProcessId.Value))
            {
                notices.Add(new StallNotice
                {
                    TurnId = turnId,
                    SessionId = turn.SessionId,
                    Reason = "engine-exited",
                    NoticeMessage = $"The engine process (PID {turn.ProcessId}) running tool '{turn.ToolName}' has unexpectedly terminated.",
                    Elapsed = now - turn.StartedAt
                });
                continue;
            }

            // Check for silence/stalled tool execution
            var silence = now - turn.LastHeartbeatAt;
            if (silence > stallThreshold)
            {
                var cleanCommand = RedactSecrets(turn.Command ?? turn.ToolName);
                notices.Add(new StallNotice
                {
                    TurnId = turnId,
                    SessionId = turn.SessionId,
                    Reason = "stalled-command",
                    NoticeMessage = $"Operation has been silent for {Math.Round(silence.TotalMinutes, 1)} minutes: {cleanCommand}",
                    Elapsed = now - turn.StartedAt
                });
            }
        }

        return notices;
    }

    public static string RedactSecrets(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return SecretPatterns.Replace(input, "[REDACTED_SECRET]");
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }
}
