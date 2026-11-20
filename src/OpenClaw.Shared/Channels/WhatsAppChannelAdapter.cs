using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpenClaw.Shared.Channels;

/// <summary>
/// Production WhatsApp Cloud API channel adapter using official Meta Graph API.
/// Strictly uses authorized Meta Cloud API endpoints without scraping or unauthorized web sessions.
/// </summary>
public sealed class WhatsAppChannelAdapter : ISparkChannelAdapter
{
    private const string DefaultGraphApiBase = "https://graph.facebook.com/v21.0";
    private readonly ISparkChannelCredentialStore _credentialStore;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private SparkChannelConfig? _config;
    private string? _accessToken;
    private string? _phoneNumberId;
    private SparkChannelConnectionStatus _status = SparkChannelConnectionStatus.Disconnected;

    public SparkChannelType ChannelType => SparkChannelType.WhatsAppCloudApi;
    public string ChannelId => _config?.ChannelId ?? "whatsapp_default";
    public SparkChannelConnectionStatus Status => _status;

    public event EventHandler<SparkChannelConnectionStatus>? StatusChanged;
    public event EventHandler<SparkChannelMessage>? MessageReceived;

    public WhatsAppChannelAdapter(
        ISparkChannelCredentialStore credentialStore,
        HttpMessageHandler? httpHandler = null)
    {
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        if (httpHandler != null)
        {
            _httpClient = new HttpClient(httpHandler);
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = new HttpClient();
            _ownsHttpClient = true;
        }
    }

    public async Task InitializeAsync(SparkChannelConfig config, CancellationToken cancellationToken = default)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _phoneNumberId = config.AccountOrBotId ?? (config.Settings.TryGetValue("PhoneNumberId", out var pId) ? pId : null);

        _accessToken = await _credentialStore.GetCredentialAsync(_config.ChannelId, "AccessToken", cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(_accessToken) && _config.Settings.TryGetValue("AccessToken", out var inlineToken))
        {
            _accessToken = inlineToken;
            await _credentialStore.SetCredentialAsync(_config.ChannelId, "AccessToken", inlineToken, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_config == null)
            throw new InvalidOperationException("Adapter must be initialized before connecting.");

        SetStatus(SparkChannelConnectionStatus.Connecting);

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            _accessToken = await _credentialStore.GetCredentialAsync(_config.ChannelId, "AccessToken", cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(_accessToken) || string.IsNullOrWhiteSpace(_phoneNumberId))
        {
            SetStatus(SparkChannelConnectionStatus.Error);
            return false;
        }

        var isHealthy = await HealthCheckAsync(cancellationToken).ConfigureAwait(false);
        if (!isHealthy)
        {
            SetStatus(SparkChannelConnectionStatus.Error);
            return false;
        }

        SetStatus(SparkChannelConnectionStatus.Connected);
        return true;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        SetStatus(SparkChannelConnectionStatus.Disconnected);
        return Task.CompletedTask;
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_accessToken) || string.IsNullOrWhiteSpace(_phoneNumberId))
            return false;

        try
        {
            var baseUri = _config?.ApiBaseUrl?.TrimEnd('/') ?? DefaultGraphApiBase;
            var url = $"{baseUri}/{_phoneNumberId}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<SparkChannelMessage> SendMessageAsync(SparkChannelMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(_accessToken) || string.IsNullOrWhiteSpace(_phoneNumberId))
        {
            return message with
            {
                DeliveryStatus = SparkMessageDeliveryStatus.Failed,
                ErrorMessage = "WhatsApp Cloud API credentials (AccessToken or PhoneNumberId) are not configured."
            };
        }

        try
        {
            var baseUri = _config?.ApiBaseUrl?.TrimEnd('/') ?? DefaultGraphApiBase;
            var url = $"{baseUri}/{_phoneNumberId}/messages";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var attachment = message.Attachments.FirstOrDefault();
            object payload;

            if (attachment != null && attachment.MediaType == SparkMediaType.Image && !string.IsNullOrWhiteSpace(attachment.RemoteUrl))
            {
                payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = message.RecipientId,
                    type = "image",
                    image = new { link = attachment.RemoteUrl, caption = message.TextContent }
                };
            }
            else if (attachment != null && attachment.MediaType == SparkMediaType.Document && !string.IsNullOrWhiteSpace(attachment.RemoteUrl))
            {
                payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = message.RecipientId,
                    type = "document",
                    document = new { link = attachment.RemoteUrl, filename = attachment.FileName ?? "document" }
                };
            }
            else if (attachment != null && attachment.MediaType == SparkMediaType.AudioVoice && !string.IsNullOrWhiteSpace(attachment.RemoteUrl))
            {
                payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = message.RecipientId,
                    type = "audio",
                    audio = new { link = attachment.RemoteUrl }
                };
            }
            else
            {
                payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = message.RecipientId,
                    type = "text",
                    text = new { preview_url = false, body = message.TextContent }
                };
            }

            request.Content = JsonContent.Create(payload);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return message with
                {
                    DeliveryStatus = SparkMessageDeliveryStatus.Sent,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }

            var errBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return message with
            {
                DeliveryStatus = SparkMessageDeliveryStatus.Failed,
                ErrorMessage = $"WhatsApp API error ({(int)response.StatusCode}): {errBody}"
            };
        }
        catch (Exception ex)
        {
            return message with
            {
                DeliveryStatus = SparkMessageDeliveryStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Ingests and processes a validated inbound Meta Webhook JSON payload.
    /// </summary>
    public void ProcessWebhookPayload(string jsonPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPayload);

        using var doc = JsonDocument.Parse(jsonPayload);
        if (!doc.RootElement.TryGetProperty("entry", out var entryArray) || entryArray.GetArrayLength() == 0)
            return;

        foreach (var entry in entryArray.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changesArray)) continue;

            foreach (var change in changesArray.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var valueObj)) continue;
                if (!valueObj.TryGetProperty("messages", out var messagesArray)) continue;

                foreach (var msgObj in messagesArray.EnumerateArray())
                {
                    var msgId = msgObj.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
                    var fromPhone = msgObj.TryGetProperty("from", out var fromProp) ? fromProp.GetString() ?? "unknown" : "unknown";
                    var type = msgObj.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "text";

                    var text = string.Empty;
                    var attachments = new List<SparkMediaAttachment>();

                    if (type == "text" && msgObj.TryGetProperty("text", out var textObj) && textObj.TryGetProperty("body", out var bodyProp))
                    {
                        text = bodyProp.GetString() ?? string.Empty;
                    }
                    else if (type == "image" && msgObj.TryGetProperty("image", out var imgObj))
                    {
                        var mediaId = imgObj.TryGetProperty("id", out var mId) ? mId.GetString() ?? "" : "";
                        var mime = imgObj.TryGetProperty("mime_type", out var mProp) ? mProp.GetString() ?? "image/jpeg" : "image/jpeg";
                        attachments.Add(new SparkMediaAttachment
                        {
                            AttachmentId = mediaId,
                            MediaType = SparkMediaType.Image,
                            MimeType = mime
                        });
                    }
                    else if (type == "audio" && msgObj.TryGetProperty("audio", out var audioObj))
                    {
                        var mediaId = audioObj.TryGetProperty("id", out var mId) ? mId.GetString() ?? "" : "";
                        var mime = audioObj.TryGetProperty("mime_type", out var mProp) ? mProp.GetString() ?? "audio/ogg" : "audio/ogg";
                        attachments.Add(new SparkMediaAttachment
                        {
                            AttachmentId = mediaId,
                            MediaType = SparkMediaType.AudioVoice,
                            MimeType = mime
                        });
                    }

                    var channelMsg = new SparkChannelMessage
                    {
                        MessageId = msgId,
                        ChannelType = SparkChannelType.WhatsAppCloudApi,
                        ChannelId = ChannelId,
                        SenderId = fromPhone,
                        SenderDisplayName = fromPhone,
                        RecipientId = _phoneNumberId ?? "spark_whatsapp",
                        SparkSessionId = $"whatsapp_{fromPhone}",
                        TextContent = text,
                        Attachments = attachments,
                        Direction = SparkMessageDirection.Inbound,
                        Timestamp = DateTimeOffset.UtcNow,
                        DeliveryStatus = SparkMessageDeliveryStatus.Delivered
                    };

                    MessageReceived?.Invoke(this, channelMsg);
                }
            }
        }
    }

    private void SetStatus(SparkChannelConnectionStatus status)
    {
        if (_status != status)
        {
            _status = status;
            StatusChanged?.Invoke(this, status);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
