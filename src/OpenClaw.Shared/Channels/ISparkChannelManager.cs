namespace OpenClaw.Shared.Channels;

/// <summary>
/// Central management coordinator for all active messaging channels, configurations, and history.
/// </summary>
public interface ISparkChannelManager : IDisposable
{
    event EventHandler<SparkChannelMessage>? InboundMessageReceived;
    event EventHandler<SparkChannelConnectionStatus>? ChannelStatusChanged;

    IReadOnlyList<ISparkChannelAdapter> GetActiveAdapters();
    ISparkChannelAdapter? GetAdapter(string channelId);

    Task RegisterAdapterAsync(ISparkChannelAdapter adapter, CancellationToken cancellationToken = default);
    Task UnregisterAdapterAsync(string channelId, CancellationToken cancellationToken = default);

    Task SaveConfigAsync(SparkChannelConfig config, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SparkChannelConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default);
    Task<SparkChannelConfig?> GetConfigAsync(string channelId, CancellationToken cancellationToken = default);
    Task<bool> DeleteConfigAsync(string channelId, CancellationToken cancellationToken = default);

    Task<SparkChannelMessage> DispatchOutboundMessageAsync(SparkChannelMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SparkChannelMessage>> GetSessionHistoryAsync(string sparkSessionId, int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SparkChannelMessage>> GetChannelHistoryAsync(string channelId, int limit = 50, CancellationToken cancellationToken = default);

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
