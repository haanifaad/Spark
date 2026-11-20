using System.Net.Http.Json;
using System.Text.Json;

namespace OpenClaw.Shared.Channels;

/// <summary>
/// Production Telegram Bot API channel adapter.
/// Supports bidirectional messaging, photo/document/voice payloads, long-polling, and health checks.
/// </summary>
public sealed class TelegramChannelAdapter : ISparkChannelAdapter
{
    private readonly ISparkChannelCredentialStore _credentialStore;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private SparkChannelConfig? _config;
    private string? _botToken;
    private SparkChannelConnectionStatus _status = SparkChannelConnectionStatus.Disconnected;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private long _lastUpdateId;

    public SparkChannelType ChannelType => SparkChannelType.Telegram;
    public string ChannelId => _config?.ChannelId ?? "telegram_default";
    public SparkChannelConnectionStatus Status => _status;

    public event EventHandler<SparkChannelConnectionStatus>? StatusChanged;
    public event EventHandler<SparkChannelMessage>? MessageReceived;

    public TelegramChannelAdapter(
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
        _botToken = await _credentialStore.GetCredentialAsync(_config.ChannelId, "BotToken", cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(_botToken) && _config.Settings.TryGetValue("BotToken", out var inlineToken))
        {
            _botToken = inlineToken;
            await _credentialStore.SetCredentialAsync(_config.ChannelId, "BotToken", inlineToken, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_config == null)
            throw new InvalidOperationException("Adapter must be initialized before connecting.");

        SetStatus(SparkChannelConnectionStatus.Connecting);

        if (string.IsNullOrWhiteSpace(_botToken))
        {
            _botToken = await _credentialStore.GetCredentialAsync(_config.ChannelId, "BotToken", cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(_botToken))
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
        StartPolling();
        return true;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        StopPolling();
        SetStatus(SparkChannelConnectionStatus.Disconnected);
        return Task.CompletedTask;
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_botToken))
            return false;

        try
        {
            var url = BuildEndpoint("getMe");
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    public async Task<SparkChannelMessage> SendMessageAsync(SparkChannelMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(_botToken))
        {
            return message with
            {
                DeliveryStatus = SparkMessageDeliveryStatus.Failed,
                ErrorMessage = "Telegram Bot Token is not configured."
            };
        }

        try
        {
            var targetChatId = message.RecipientId;
            var attachment = message.Attachments.FirstOrDefault();

            HttpResponseMessage response;
            if (attachment != null && attachment.MediaType == SparkMediaType.Image && !string.IsNullOrWhiteSpace(attachment.RemoteUrl))
            {
                var payload = new
                {
                    chat_id = targetChatId,
                    photo = attachment.RemoteUrl,
                    caption = message.TextContent
                };
                response = await _httpClient.PostAsJsonAsync(BuildEndpoint("sendPhoto"), payload, cancellationToken).ConfigureAwait(false);
            }
            else if (attachment != null && attachment.MediaType == SparkMediaType.Document && !string.IsNullOrWhiteSpace(attachment.RemoteUrl))
            {
                var payload = new
                {
                    chat_id = targetChatId,
                    document = attachment.RemoteUrl,
                    caption = message.TextContent
                };
                response = await _httpClient.PostAsJsonAsync(BuildEndpoint("sendDocument"), payload, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var payload = new
                {
                    chat_id = targetChatId,
                    text = message.TextContent
                };
                response = await _httpClient.PostAsJsonAsync(BuildEndpoint("sendMessage"), payload, cancellationToken).ConfigureAwait(false);
            }

            if (response.IsSuccessStatusCode)
            {
                return message with
                {
                    DeliveryStatus = SparkMessageDeliveryStatus.Delivered,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }

            var errBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return message with
            {
                DeliveryStatus = SparkMessageDeliveryStatus.Failed,
                ErrorMessage = $"Telegram API error ({(int)response.StatusCode}): {errBody}"
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

    public void ProcessRawTelegramUpdate(JsonElement update)
    {
        if (update.TryGetProperty("update_id", out var updateIdProp))
        {
            var updateId = updateIdProp.GetInt64();
            if (updateId > _lastUpdateId)
                _lastUpdateId = updateId;
        }

        if (!update.TryGetProperty("message", out var msgElement))
            return;

        var messageId = msgElement.TryGetProperty("message_id", out var mId) ? mId.ToString() : Guid.NewGuid().ToString("N");
        var senderId = "unknown";
        string? senderName = null;

        if (msgElement.TryGetProperty("from", out var fromProp))
        {
            if (fromProp.TryGetProperty("id", out var fId)) senderId = fId.ToString();
            if (fromProp.TryGetProperty("first_name", out var fName)) senderName = fName.GetString();
        }

        var chatId = senderId;
        if (msgElement.TryGetProperty("chat", out var chatProp) && chatProp.TryGetProperty("id", out var cId))
        {
            chatId = cId.ToString();
        }

        var text = msgElement.TryGetProperty("text", out var tProp) ? (tProp.GetString() ?? string.Empty) : string.Empty;
        var attachments = new List<SparkMediaAttachment>();

        if (msgElement.TryGetProperty("photo", out var photoArray) && photoArray.GetArrayLength() > 0)
        {
            var bestPhoto = photoArray[photoArray.GetArrayLength() - 1];
            var fileId = bestPhoto.TryGetProperty("file_id", out var fId) ? fId.GetString() ?? "" : "";
            attachments.Add(new SparkMediaAttachment
            {
                AttachmentId = fileId,
                MediaType = SparkMediaType.Image,
                MimeType = "image/jpeg"
            });
        }
        else if (msgElement.TryGetProperty("voice", out var voiceProp))
        {
            var fileId = voiceProp.TryGetProperty("file_id", out var fId) ? fId.GetString() ?? "" : "";
            var mime = voiceProp.TryGetProperty("mime_type", out var mProp) ? mProp.GetString() ?? "audio/ogg" : "audio/ogg";
            attachments.Add(new SparkMediaAttachment
            {
                AttachmentId = fileId,
                MediaType = SparkMediaType.AudioVoice,
                MimeType = mime
            });
        }

        var channelMsg = new SparkChannelMessage
        {
            MessageId = messageId,
            ChannelType = SparkChannelType.Telegram,
            ChannelId = ChannelId,
            SenderId = senderId,
            SenderDisplayName = senderName,
            RecipientId = _config?.AccountOrBotId ?? "spark_bot",
            SparkSessionId = $"telegram_{chatId}",
            TextContent = text,
            Attachments = attachments,
            Direction = SparkMessageDirection.Inbound,
            Timestamp = DateTimeOffset.UtcNow,
            DeliveryStatus = SparkMessageDeliveryStatus.Delivered
        };

        MessageReceived?.Invoke(this, channelMsg);
    }

    private void StartPolling()
    {
        if (_pollingTask != null && !_pollingTask.IsCompleted)
            return;

        _pollingCts = new CancellationTokenSource();
        _pollingTask = Task.Run(() => PollingLoopAsync(_pollingCts.Token));
    }

    private void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
    }

    private async Task PollingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var offset = _lastUpdateId + 1;
                var url = BuildEndpoint($"getUpdates?offset={offset}&timeout=10");
                using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("result", out var results) && results.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var update in results.EnumerateArray())
                        {
                            ProcessRawTelegramUpdate(update);
                        }
                    }
                }
                else
                {
                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private string BuildEndpoint(string method)
    {
        var baseUri = _config?.ApiBaseUrl?.TrimEnd('/') ?? "https://api.telegram.org";
        return $"{baseUri}/bot{_botToken}/{method}";
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
        StopPolling();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
