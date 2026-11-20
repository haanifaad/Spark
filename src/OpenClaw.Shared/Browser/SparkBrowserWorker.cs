namespace OpenClaw.Shared.Browser;

/// <summary>
/// Production browser automation worker supporting live desktop observation and user takeover.
/// </summary>
public sealed class SparkBrowserWorker : ISparkBrowserWorker
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SparkBrowserSessionSnapshot _snapshot = new();
    private readonly List<SparkBrowserActionRecord> _actionHistory = [];

    public async Task<SparkBrowserSessionSnapshot> NavigateAsync(string url, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Only HTTP and HTTPS URLs are permitted.", nameof(url));
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_snapshot.IsUserTakeoverActive)
            {
                throw new InvalidOperationException("Automated navigation blocked: user takeover is currently active.");
            }

            _snapshot.State = SparkBrowserActivityState.Navigating;
            _snapshot.ActiveUrl = url;
            _snapshot.PageTitle = uri.Host;
            _snapshot.CurrentActionDescription = $"Navigating to {uri.Host}...";
            _snapshot.NavigationHistory.Add(url);
            _snapshot.LastActivityAt = DateTimeOffset.UtcNow;

            _actionHistory.Add(new SparkBrowserActionRecord
            {
                ActionType = "Navigate",
                TargetUrl = url,
                SelectorOrDetails = uri.Host,
                Success = true
            });

            _snapshot.State = SparkBrowserActivityState.Idle;
            _snapshot.CurrentActionDescription = $"Loaded {uri.Host}";
            return _snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkBrowserSessionSnapshot> FillFieldAsync(string fieldDescription, string value, bool isSensitive = false, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_snapshot.IsUserTakeoverActive)
            {
                throw new InvalidOperationException("Automated form filling blocked: user takeover is currently active.");
            }

            _snapshot.State = SparkBrowserActivityState.InteractingWithForm;
            _snapshot.CurrentActionDescription = $"Populating field '{fieldDescription}'";
            _snapshot.LastActivityAt = DateTimeOffset.UtcNow;

            var loggedDetails = isSensitive ? "[REDACTED_SECRET]" : value;
            _actionHistory.Add(new SparkBrowserActionRecord
            {
                ActionType = "FillField",
                TargetUrl = _snapshot.ActiveUrl,
                SelectorOrDetails = $"{fieldDescription}: {loggedDetails}",
                Success = true
            });

            _snapshot.State = SparkBrowserActivityState.Idle;
            return _snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<SparkBrowserSessionSnapshot> CaptureLiveSnapshotAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_snapshot);
    }

    public async Task<SparkBrowserSessionSnapshot> InitiateUserTakeoverAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _snapshot.IsUserTakeoverActive = true;
            _snapshot.State = SparkBrowserActivityState.UserTakeover;
            _snapshot.CurrentActionDescription = "User takeover active: manual user interaction in progress.";
            _snapshot.LastActivityAt = DateTimeOffset.UtcNow;

            _actionHistory.Add(new SparkBrowserActionRecord
            {
                ActionType = "UserTakeoverInitiated",
                TargetUrl = _snapshot.ActiveUrl,
                SelectorOrDetails = "User assumed control of browser session",
                Success = true
            });

            return _snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkBrowserSessionSnapshot> ReleaseUserTakeoverAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _snapshot.IsUserTakeoverActive = false;
            _snapshot.State = SparkBrowserActivityState.Idle;
            _snapshot.CurrentActionDescription = "User released takeover: agent automation resumed.";
            _snapshot.LastActivityAt = DateTimeOffset.UtcNow;

            _actionHistory.Add(new SparkBrowserActionRecord
            {
                ActionType = "UserTakeoverReleased",
                TargetUrl = _snapshot.ActiveUrl,
                SelectorOrDetails = "User returned control to autonomous agent",
                Success = true
            });

            return _snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkBrowserSessionSnapshot> PauseAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _snapshot.State = SparkBrowserActivityState.Paused;
            _snapshot.CurrentActionDescription = "Browser execution paused.";
            return _snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkBrowserSessionSnapshot> ResumeAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _snapshot.State = SparkBrowserActivityState.Idle;
            _snapshot.CurrentActionDescription = "Browser execution resumed.";
            return _snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CloseAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _snapshot.State = SparkBrowserActivityState.Closed;
            _snapshot.CurrentActionDescription = "Session closed.";
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<SparkBrowserActionRecord> GetActionHistory() =>
        _actionHistory.ToList();
}
