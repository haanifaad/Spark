using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Divisions;

/// <summary>
/// Event arguments for an upcoming division transition that can be cancelled.
/// </summary>
public sealed class DivisionChangingEventArgs : EventArgs
{
    public SparkDivision CurrentDivision { get; }
    public SparkDivision TargetDivision { get; }
    public object? Parameter { get; }
    public bool Cancel { get; set; }

    public DivisionChangingEventArgs(SparkDivision currentDivision, SparkDivision targetDivision, object? parameter = null)
    {
        CurrentDivision = currentDivision;
        TargetDivision = targetDivision;
        Parameter = parameter;
    }
}

/// <summary>
/// Event arguments for a completed division transition.
/// </summary>
public sealed class DivisionChangedEventArgs : EventArgs
{
    public SparkDivision PreviousDivision { get; }
    public SparkDivision ActiveDivision { get; }
    public DivisionMetadata ActiveMetadata { get; }

    public DivisionChangedEventArgs(SparkDivision previousDivision, SparkDivision activeDivision, DivisionMetadata activeMetadata)
    {
        PreviousDivision = previousDivision;
        ActiveDivision = activeDivision;
        ActiveMetadata = activeMetadata;
    }
}

/// <summary>
/// Central manager contract governing division transitions, lifecycle management,
/// and division-specific state preservation across SPARK.
/// </summary>
public interface IDivisionManager
{
    /// <summary>Gets the current active division.</summary>
    SparkDivision ActiveDivision { get; }

    /// <summary>Gets the metadata for the active division.</summary>
    DivisionMetadata ActiveDivisionMetadata { get; }

    /// <summary>Gets all available divisions in SPARK.</summary>
    IReadOnlyList<DivisionMetadata> AvailableDivisions { get; }

    /// <summary>Checks whether a given division is available for interaction.</summary>
    bool IsDivisionAvailable(SparkDivision division);

    /// <summary>Transitions to the specified division.</summary>
    Task<bool> SwitchDivisionAsync(SparkDivision division, object? parameter = null);

    /// <summary>
    /// Notifies the manager that the active division changed through an external route (e.g. navigation history or link).
    /// </summary>
    void NotifyDivisionActive(SparkDivision division);

    /// <summary>Registers an implementation-specific navigation handler (e.g. from the application shell).</summary>
    void RegisterNavigationHandler(Func<SparkDivision, object?, Task<bool>> handler);

    /// <summary>Saves state for a specific division.</summary>
    void SetDivisionState(SparkDivision division, object state);

    /// <summary>Retrieves preserved state for a division, or default if none was saved.</summary>
    T? GetDivisionState<T>(SparkDivision division) where T : class;

    /// <summary>Fired before a division transition begins.</summary>
    event EventHandler<DivisionChangingEventArgs>? DivisionChanging;

    /// <summary>Fired after a division transition completes.</summary>
    event EventHandler<DivisionChangedEventArgs>? DivisionChanged;
}
