using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Divisions;

/// <summary>
/// Centralized thread-safe implementation of <see cref="IDivisionManager"/> that orchestrates
/// division transitions, maintains active division state, and preserves division-specific state.
/// </summary>
public sealed class DivisionManager : IDivisionManager
{
    private readonly ConcurrentDictionary<SparkDivision, object> _divisionStates = new();
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private Func<SparkDivision, object?, Task<bool>>? _navigationHandler;
    private SparkDivision _activeDivision = SparkDivision.Chat;

    public SparkDivision ActiveDivision => _activeDivision;

    public DivisionMetadata ActiveDivisionMetadata => DivisionCatalog.GetMetadata(_activeDivision);

    public IReadOnlyList<DivisionMetadata> AvailableDivisions => DivisionCatalog.AllDivisions;

    public event EventHandler<DivisionChangingEventArgs>? DivisionChanging;
    public event EventHandler<DivisionChangedEventArgs>? DivisionChanged;

    public bool IsDivisionAvailable(SparkDivision division)
    {
        try
        {
            var meta = DivisionCatalog.GetMetadata(division);
            return meta.IsActive;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public void RegisterNavigationHandler(Func<SparkDivision, object?, Task<bool>> handler)
    {
        _navigationHandler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public async Task<bool> SwitchDivisionAsync(SparkDivision targetDivision, object? parameter = null)
    {
        if (_activeDivision == targetDivision)
            return true;

        if (!IsDivisionAvailable(targetDivision))
            return false;

        await _transitionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_activeDivision == targetDivision)
                return true;

            var changingArgs = new DivisionChangingEventArgs(_activeDivision, targetDivision, parameter);
            DivisionChanging?.Invoke(this, changingArgs);
            if (changingArgs.Cancel)
                return false;

            var previous = _activeDivision;
            bool success = true;

            if (_navigationHandler != null)
            {
                success = await _navigationHandler(targetDivision, parameter).ConfigureAwait(false);
            }

            if (success)
            {
                _activeDivision = targetDivision;
                DivisionChanged?.Invoke(this, new DivisionChangedEventArgs(previous, targetDivision, ActiveDivisionMetadata));
            }

            return success;
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    public void NotifyDivisionActive(SparkDivision division)
    {
        if (_activeDivision == division)
            return;

        var previous = _activeDivision;
        _activeDivision = division;
        DivisionChanged?.Invoke(this, new DivisionChangedEventArgs(previous, division, ActiveDivisionMetadata));
    }

    public void SetDivisionState(SparkDivision division, object state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _divisionStates[division] = state;
    }

    public T? GetDivisionState<T>(SparkDivision division) where T : class
    {
        if (_divisionStates.TryGetValue(division, out var state) && state is T typedState)
            return typedState;

        return null;
    }
}
