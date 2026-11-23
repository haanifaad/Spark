using System.Text.Json.Serialization;

namespace OpenClaw.Shared.Channels;

/// <summary>
/// Supported messaging channel integration types.
/// </summary>
public enum SparkChannelType
{
    Telegram,
    WhatsAppCloudApi,
    Custom
}

/// <summary>
/// Connection state of a channel adapter.
/// </summary>
public enum SparkChannelConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Degraded,
    Error
}

/// <summary>
/// Delivery lifecycle state of a message.
/// </summary>
public enum SparkMessageDeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Read,
    Failed
}

/// <summary>
/// Classification of media payload attached to a channel message.
/// </summary>
public enum SparkMediaType
{
    Text,
    Image,
    Document,
    AudioVoice,
    Video
}

/// <summary>
/// Direction of a channel message.
/// </summary>
public enum SparkMessageDirection
{
    Inbound,
    Outbound
}

/// <summary>
/// Represents a media attachment in a channel message.
/// </summary>
public sealed record SparkMediaAttachment
{
    public string AttachmentId { get; init; } = Guid.NewGuid().ToString("N");
    public required SparkMediaType MediaType { get; init; }
    public required string MimeType { get; init; }
    public string? FileName { get; init; }
    public long FileSizeBytes { get; init; }
    public string? LocalUri { get; init; }
    public string? RemoteUrl { get; init; }
    public string? Sha256Checksum { get; init; }
}

/// <summary>
/// Represents a normalized cross-channel message.
/// </summary>
public sealed record SparkChannelMessage
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");
    public required SparkChannelType ChannelType { get; init; }
    public required string ChannelId { get; init; }
    public required string SenderId { get; init; }
    public string? SenderDisplayName { get; init; }
    public required string RecipientId { get; init; }
    public required string SparkSessionId { get; init; }
    public required string TextContent { get; init; }
    public IReadOnlyList<SparkMediaAttachment> Attachments { get; init; } = Array.Empty<SparkMediaAttachment>();
    public SparkMessageDirection Direction { get; init; } = SparkMessageDirection.Outbound;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public SparkMessageDeliveryStatus DeliveryStatus { get; init; } = SparkMessageDeliveryStatus.Pending;
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Configuration for a specific messaging channel instance.
/// </summary>
public sealed class SparkChannelConfig
{
    public required SparkChannelType ChannelType { get; set; }
    public required string ChannelId { get; set; }
    public required string DisplayName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool AutoReconnect { get; set; } = true;
    public string? AccountOrBotId { get; set; }
    public string? WebhookUrl { get; set; }
    public string? ApiBaseUrl { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int ReconnectBackoffSeconds { get; set; } = 5;
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
