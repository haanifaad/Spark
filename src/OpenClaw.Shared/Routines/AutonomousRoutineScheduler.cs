using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Routines;

/// <summary>
/// Status of an autonomous scheduled routine.
/// </summary>
public enum RoutineStatus
{
    Active,
    PausedErrors,
    CompletedCeiling,
    Disabled
}

/// <summary>
/// Definition of a recurring autonomous routine with unattended execution guardrails.
/// </summary>
public sealed record AutonomousRoutine
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Prompt { get; init; }
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public bool IsEnabled { get; set; } = true;
    public RoutineStatus Status { get; set; } = RoutineStatus.Active;

    public int MaxConsecutiveErrors { get; init; } = 3;
    public int MaxIterations { get; init; } = 15;
    public int CurrentConsecutiveErrors { get; set; }
    public int TotalIterations { get; set; }
    public string? PauseReason { get; set; }
}

public sealed record RoutineRunResult
{
    public required string RoutineId { get; init; }
    public required bool Success { get; init; }
    public string? Summary { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Autonomous Routine Scheduler for SPARK proactive background intelligence.
/// Manages scheduled execution, enforces unattended loop ceilings, and prevents notification storms.
/// </summary>
public sealed class AutonomousRoutineScheduler
{
    private readonly ConcurrentDictionary<string, AutonomousRoutine> _routines = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterRoutine(AutonomousRoutine routine)
    {
        ArgumentNullException.ThrowIfNull(routine);
        routine.NextRunAt ??= DateTimeOffset.UtcNow.Add(routine.Interval);
        _routines[routine.Id] = routine;
    }

    public bool RemoveRoutine(string routineId) => _routines.TryRemove(routineId, out _);

    public AutonomousRoutine? GetRoutine(string routineId) =>
        _routines.TryGetValue(routineId, out var r) ? r : null;

    public IReadOnlyList<AutonomousRoutine> GetAllRoutines() => _routines.Values.ToList();

    public IReadOnlyList<AutonomousRoutine> GetDueRoutines(DateTimeOffset now)
    {
        return _routines.Values
            .Where(r => r.IsEnabled && r.Status == RoutineStatus.Active && r.NextRunAt <= now)
            .ToList();
    }

    /// <summary>
    /// Records the execution outcome of a routine and applies unattended safety ceilings.
    /// </summary>
    public void RecordExecutionOutcome(string routineId, bool success, string? message = null)
    {
        if (!_routines.TryGetValue(routineId, out var routine))
            return;

        var now = DateTimeOffset.UtcNow;
        routine.LastRunAt = now;
        routine.TotalIterations++;

        if (success)
        {
            routine.CurrentConsecutiveErrors = 0;
        }
        else
        {
            routine.CurrentConsecutiveErrors++;
        }

        // 1. Check consecutive error ceiling
        if (routine.CurrentConsecutiveErrors >= routine.MaxConsecutiveErrors)
        {
            routine.IsEnabled = false;
            routine.Status = RoutineStatus.PausedErrors;
            routine.PauseReason = $"Exceeded consecutive error limit of {routine.MaxConsecutiveErrors}. Stopped for safety.";
            return;
        }

        // 2. Check unattended total iterations ceiling
        if (routine.TotalIterations >= routine.MaxIterations)
        {
            routine.IsEnabled = false;
            routine.Status = RoutineStatus.CompletedCeiling;
            routine.PauseReason = $"Reached unattended execution ceiling of {routine.MaxIterations} iterations.";
            return;
        }

        // Schedule next execution
        routine.NextRunAt = now.Add(routine.Interval);
    }
}
