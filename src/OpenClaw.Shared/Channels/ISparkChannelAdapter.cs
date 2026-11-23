namespace OpenClaw.Shared.Channels;

/// <summary>
/// Core contract for bidirectional messaging channel adapters (Telegram, WhatsApp, etc.).
/// </summary>
public interface ISparkChannelAdapter : IDisposable
{
    SparkChannelType ChannelType { get; }
    string ChannelId { get; }
    SparkChannelConnectionStatus Status { get; }

    event EventHandler<SparkChannelConnectionStatus>? StatusChanged;
    event EventHandler<SparkChannelMessage>? MessageReceived;

    Task InitializeAsync(SparkChannelConfig config, CancellationToken cancellationToken = default);
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<SparkChannelMessage> SendMessageAsync(SparkChannelMessage message, CancellationToken cancellationToken = default);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
