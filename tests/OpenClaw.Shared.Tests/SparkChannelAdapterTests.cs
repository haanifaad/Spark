using System.Net;
using System.Text;
using System.Text.Json;
using OpenClaw.Shared.Channels;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkChannelAdapterTests : IDisposable
{
    private readonly string _testDirectory;

    public SparkChannelAdapterTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "spark_channel_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public async Task CredentialStore_EncryptsAtRest_AndRetrievesCorrectly()
    {
        var store = new SparkChannelCredentialStore(_testDirectory);
        var rawSecret = "super-secret-telegram-token-123456:ABC-DEF";

        await store.SetCredentialAsync("tg_main", "BotToken", rawSecret);

        var retrieved = await store.GetCredentialAsync("tg_main", "BotToken");
        Assert.Equal(rawSecret, retrieved);

        // Verify that the vault file on disk does NOT contain the plaintext secret!
        var vaultFile = Path.Combine(_testDirectory, "credentials.vault");
        Assert.True(File.Exists(vaultFile));
        var vaultBytes = await File.ReadAllBytesAsync(vaultFile);
        var vaultText = Encoding.UTF8.GetString(vaultBytes);
        Assert.DoesNotContain("super-secret-telegram-token", vaultText);

        // Verify deletion
        var deleted = await store.DeleteCredentialAsync("tg_main", "BotToken");
        Assert.True(deleted);

        var afterDelete = await store.GetCredentialAsync("tg_main", "BotToken");
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task TelegramAdapter_SendMessage_And_InboundUpdate_Succeeds()
    {
        var credStore = new SparkChannelCredentialStore(Path.Combine(_testDirectory, "tg_creds"));
        await credStore.SetCredentialAsync("tg_test", "BotToken", "123456:TEST_TOKEN");

        var fakeHandler = new FakeChannelHttpMessageHandler(req =>
        {
            if (req.RequestUri != null && req.RequestUri.ToString().Contains("getMe"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"ok\":true,\"result\":{\"id\":123456,\"is_bot\":true,\"first_name\":\"SparkBot\"}}")
                };
            }

            if (req.RequestUri != null && req.RequestUri.ToString().Contains("sendMessage"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"ok\":true,\"result\":{\"message_id\":42}}")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var adapter = new TelegramChannelAdapter(credStore, fakeHandler);
        var config = new SparkChannelConfig
        {
            ChannelType = SparkChannelType.Telegram,
            ChannelId = "tg_test",
            DisplayName = "Test Telegram Bot"
        };

        await adapter.InitializeAsync(config);
        var connected = await adapter.ConnectAsync();
        Assert.True(connected);
        Assert.Equal(SparkChannelConnectionStatus.Connected, adapter.Status);

        // Test outbound message
        var msg = new SparkChannelMessage
        {
            ChannelType = SparkChannelType.Telegram,
            ChannelId = "tg_test",
            SenderId = "spark_agent",
            RecipientId = "987654321",
            SparkSessionId = "telegram_987654321",
            TextContent = "Hello from SPARK Autonomous Agent!"
        };

        var sentResult = await adapter.SendMessageAsync(msg);
        Assert.Equal(SparkMessageDeliveryStatus.Delivered, sentResult.DeliveryStatus);
        Assert.Null(sentResult.ErrorMessage);

        // Test inbound raw update processing
        SparkChannelMessage? receivedMsg = null;
        adapter.MessageReceived += (_, incoming) => receivedMsg = incoming;

        var rawUpdateJson = """
        {
            "update_id": 1001,
            "message": {
                "message_id": 777,
                "from": {
                    "id": 987654321,
                    "first_name": "Alice"
                },
                "chat": {
                    "id": 987654321,
                    "type": "private"
                },
                "text": "What is the status of my tasks?"
            }
        }
        """;

        using var doc = JsonDocument.Parse(rawUpdateJson);
        adapter.ProcessRawTelegramUpdate(doc.RootElement);

        Assert.NotNull(receivedMsg);
        Assert.Equal("987654321", receivedMsg.SenderId);
        Assert.Equal("Alice", receivedMsg.SenderDisplayName);
        Assert.Equal("telegram_987654321", receivedMsg.SparkSessionId);
        Assert.Equal("What is the status of my tasks?", receivedMsg.TextContent);
        Assert.Equal(SparkMessageDirection.Inbound, receivedMsg.Direction);
    }

    [Fact]
    public async Task WhatsAppAdapter_SendMessage_And_InboundWebhook_Succeeds()
    {
        var credStore = new SparkChannelCredentialStore(Path.Combine(_testDirectory, "wa_creds"));
        await credStore.SetCredentialAsync("wa_test", "AccessToken", "EAAG_TEST_ACCESS_TOKEN");

        var fakeHandler = new FakeChannelHttpMessageHandler(req =>
        {
            // Health check
            if (req.Method == HttpMethod.Get && req.RequestUri != null && req.RequestUri.ToString().Contains("100200300400"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"verified_name\":\"SPARK Assistant\",\"id\":\"100200300400\"}")
                };
            }

            // Send message
            if (req.Method == HttpMethod.Post && req.RequestUri != null && req.RequestUri.ToString().Contains("messages"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"messaging_product\":\"whatsapp\",\"messages\":[{\"id\":\"wamid.HBgL...\"}]}")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var adapter = new WhatsAppChannelAdapter(credStore, fakeHandler);
        var config = new SparkChannelConfig
        {
            ChannelType = SparkChannelType.WhatsAppCloudApi,
            ChannelId = "wa_test",
            DisplayName = "Official WhatsApp Business",
            AccountOrBotId = "100200300400"
        };

        await adapter.InitializeAsync(config);
        var connected = await adapter.ConnectAsync();
        Assert.True(connected);
        Assert.Equal(SparkChannelConnectionStatus.Connected, adapter.Status);

        // Outbound send message
        var msg = new SparkChannelMessage
        {
            ChannelType = SparkChannelType.WhatsAppCloudApi,
            ChannelId = "wa_test",
            SenderId = "spark_agent",
            RecipientId = "+15551234567",
            SparkSessionId = "whatsapp_+15551234567",
            TextContent = "Your daily summary is ready."
        };

        var sent = await adapter.SendMessageAsync(msg);
        Assert.Equal(SparkMessageDeliveryStatus.Sent, sent.DeliveryStatus);
        Assert.Null(sent.ErrorMessage);

        // Inbound webhook processing
        SparkChannelMessage? receivedMsg = null;
        adapter.MessageReceived += (_, incoming) => receivedMsg = incoming;

        var webhookJson = """
        {
            "object": "whatsapp_business_account",
            "entry": [{
                "id": "100200300400",
                "changes": [{
                    "value": {
                        "messaging_product": "whatsapp",
                        "metadata": { "display_phone_number": "15550001", "phone_number_id": "100200300400" },
                        "contacts": [{ "profile": { "name": "Bob" }, "wa_id": "15551234567" }],
                        "messages": [{
                            "from": "15551234567",
                            "id": "wamid.inbound.999",
                            "timestamp": "1732000000",
                            "text": { "body": "Summarize inbox please" },
                            "type": "text"
                        }]
                    },
                    "field": "messages"
                }]
            }]
        }
        """;

        adapter.ProcessWebhookPayload(webhookJson);

        Assert.NotNull(receivedMsg);
        Assert.Equal("15551234567", receivedMsg.SenderId);
        Assert.Equal("whatsapp_15551234567", receivedMsg.SparkSessionId);
        Assert.Equal("Summarize inbox please", receivedMsg.TextContent);
        Assert.Equal(SparkMessageDirection.Inbound, receivedMsg.Direction);
    }

    [Fact]
    public async Task SparkChannelManager_CoordinatesHistory_And_Dispatch()
    {
        var credStore = new SparkChannelCredentialStore(Path.Combine(_testDirectory, "mgr_creds"));
        await credStore.SetCredentialAsync("tg_mgr", "BotToken", "TEST_BOT_TOKEN");

        var fakeHandler = new FakeChannelHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true,\"result\":{\"id\":123}}")
            });

        using var tgAdapter = new TelegramChannelAdapter(credStore, fakeHandler);
        using var manager = new SparkChannelManager(_testDirectory);

        var config = new SparkChannelConfig
        {
            ChannelType = SparkChannelType.Telegram,
            ChannelId = "tg_mgr",
            DisplayName = "Manager Telegram Channel"
        };

        await manager.SaveConfigAsync(config);
        await tgAdapter.InitializeAsync(config);
        await manager.RegisterAdapterAsync(tgAdapter);

        Assert.Single(manager.GetActiveAdapters());
        Assert.NotNull(manager.GetAdapter("tg_mgr"));

        // Dispatch outbound message
        var outbound = new SparkChannelMessage
        {
            ChannelType = SparkChannelType.Telegram,
            ChannelId = "tg_mgr",
            SenderId = "agent",
            RecipientId = "chat_456",
            SparkSessionId = "telegram_chat_456",
            TextContent = "Scheduled reminder: Project sync in 10 minutes."
        };

        var dispatched = await manager.DispatchOutboundMessageAsync(outbound);
        Assert.Equal(SparkMessageDeliveryStatus.Delivered, dispatched.DeliveryStatus);

        // Verify history tracking
        var sessionHistory = await manager.GetSessionHistoryAsync("telegram_chat_456");
        Assert.Single(sessionHistory);
        Assert.Equal("Scheduled reminder: Project sync in 10 minutes.", sessionHistory[0].TextContent);

        // Verify channel history tracking
        var channelHistory = await manager.GetChannelHistoryAsync("tg_mgr");
        Assert.Single(channelHistory);
    }

    private sealed class FakeChannelHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeChannelHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
