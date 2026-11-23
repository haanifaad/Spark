using System.Collections.Concurrent;
using System.Text.Json;
using OpenClaw.Shared.Security.Approvals;

namespace OpenClaw.Shared.Channels;

/// <summary>
/// Production coordinator for messaging channels, routing, session identity, and history.
/// </summary>
public sealed class SparkChannelManager : ISparkChannelManager
{
    private readonly string _storageDirectory;
    private readonly string _configFilePath;
    private readonly string _historyFilePath;
    private readonly ISparkApprovalManager? _approvalManager;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, ISparkChannelAdapter> _adapters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SparkChannelConfig> _configs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SparkChannelMessage> _historyBuffer = new();
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

    public event EventHandler<SparkChannelMessage>? InboundMessageReceived;
    public event EventHandler<SparkChannelConnectionStatus>? ChannelStatusChanged;

    public SparkChannelManager(
        string? baseDirectory = null,
        ISparkApprovalManager? approvalManager = null)
    {
        _storageDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "channels");

        Directory.CreateDirectory(_storageDirectory);
        _configFilePath = Path.Combine(_storageDirectory, "channels_config.json");
        _historyFilePath = Path.Combine(_storageDirectory, "messages_history.jsonl");
        _approvalManager = approvalManager;

        LoadPersistedConfigs();
        LoadPersistedHistory();
    }

    public IReadOnlyList<ISparkChannelAdapter> GetActiveAdapters() => _adapters.Values.ToList();

    public ISparkChannelAdapter? GetAdapter(string channelId)
    {
        _adapters.TryGetValue(channelId, out var adapter);
        return adapter;
    }

    public async Task RegisterAdapterAsync(ISparkChannelAdapter adapter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_adapters.TryGetValue(adapter.ChannelId, out var oldAdapter))
            {
                oldAdapter.MessageReceived -= OnAdapterMessageReceived;
                oldAdapter.StatusChanged -= OnAdapterStatusChanged;
            }

            adapter.MessageReceived += OnAdapterMessageReceived;
            adapter.StatusChanged += OnAdapterStatusChanged;
            _adapters[adapter.ChannelId] = adapter;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UnregisterAdapterAsync(string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_adapters.TryRemove(channelId, out var adapter))
            {
                adapter.MessageReceived -= OnAdapterMessageReceived;
                adapter.StatusChanged -= OnAdapterStatusChanged;
                await adapter.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveConfigAsync(SparkChannelConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _configs[config.ChannelId] = config;
            await PersistConfigsUnsafeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<SparkChannelConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _configs.Values.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SparkChannelConfig?> GetConfigAsync(string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _configs.TryGetValue(channelId, out var config);
            return config;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteConfigAsync(string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_configs.TryRemove(channelId, out _))
            {
                await PersistConfigsUnsafeAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SparkChannelMessage> DispatchOutboundMessageAsync(SparkChannelMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_adapters.TryGetValue(message.ChannelId, out var adapter))
        {
            var failed = message with
            {
                DeliveryStatus = SparkMessageDeliveryStatus.Failed,
                ErrorMessage = $"No active adapter registered for channel ID '{message.ChannelId}'."
            };
            await RecordMessageAsync(failed, cancellationToken).ConfigureAwait(false);
            return failed;
        }

        var result = await adapter.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        await RecordMessageAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<IReadOnlyList<SparkChannelMessage>> GetSessionHistoryAsync(string sparkSessionId, int limit = 50, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sparkSessionId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _historyBuffer
                .Where(m => string.Equals(m.SparkSessionId, sparkSessionId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Timestamp)
                .TakeLast(limit)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<SparkChannelMessage>> GetChannelHistoryAsync(string channelId, int limit = 50, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _historyBuffer
                .Where(m => string.Equals(m.ChannelId, channelId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Timestamp)
                .TakeLast(limit)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_monitorTask != null && !_monitorTask.IsCompleted)
            return Task.CompletedTask;

        _monitorCts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => ReconnectionMonitorLoopAsync(_monitorCts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;

        foreach (var adapter in _adapters.Values)
        {
            try
            {
                await adapter.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Suppress on shutdown
            }
        }
    }

    private void OnAdapterMessageReceived(object? sender, SparkChannelMessage message)
    {
        _ = RecordMessageAsync(message, CancellationToken.None);
        InboundMessageReceived?.Invoke(this, message);
    }

    private void OnAdapterStatusChanged(object? sender, SparkChannelConnectionStatus status)
    {
        ChannelStatusChanged?.Invoke(sender, status);
    }

    private async Task RecordMessageAsync(SparkChannelMessage message, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _historyBuffer.Add(message);
            if (_historyBuffer.Count > 1000)
            {
                _historyBuffer.RemoveAt(0);
            }

            var line = JsonSerializer.Serialize(message) + Environment.NewLine;
            await File.AppendAllTextAsync(_historyFilePath, line, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best effort on file append
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ReconnectionMonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);

                foreach (var adapter in _adapters.Values)
                {
                    if (_configs.TryGetValue(adapter.ChannelId, out var config) && config.IsEnabled && config.AutoReconnect)
                    {
                        if (adapter.Status == SparkChannelConnectionStatus.Disconnected ||
                            adapter.Status == SparkChannelConnectionStatus.Error)
                        {
                            try
                            {
                                await adapter.ConnectAsync(cancellationToken).ConfigureAwait(false);
                            }
                            catch
                            {
                                // Retry on next iteration
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Continue loop
            }
        }
    }

    private void LoadPersistedConfigs()
    {
        if (!File.Exists(_configFilePath)) return;
        try
        {
            var json = File.ReadAllText(_configFilePath);
            var list = JsonSerializer.Deserialize<List<SparkChannelConfig>>(json);
            if (list != null)
            {
                foreach (var item in list)
                {
                    _configs[item.ChannelId] = item;
                }
            }
        }
        catch
        {
            // Best-effort config load
        }
    }

    private void LoadPersistedHistory()
    {
        if (!File.Exists(_historyFilePath)) return;
        try
        {
            var lines = File.ReadAllLines(_historyFilePath);
            foreach (var line in lines.TakeLast(500))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var msg = JsonSerializer.Deserialize<SparkChannelMessage>(line);
                    if (msg != null)
                        _historyBuffer.Add(msg);
                }
                catch
                {
                    // Ignore corrupted lines
                }
            }
        }
        catch
        {
            // Best-effort history load
        }
    }

    private async Task PersistConfigsUnsafeAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(_configs.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
        var tmpPath = _configFilePath + ".tmp";
        await File.WriteAllTextAsync(tmpPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tmpPath, _configFilePath, overwrite: true);
    }

    public void Dispose()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _lock.Dispose();
        foreach (var adapter in _adapters.Values)
        {
            adapter.Dispose();
        }
    }
}
