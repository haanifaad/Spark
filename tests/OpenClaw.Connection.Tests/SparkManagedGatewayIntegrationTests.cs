using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenClaw.Connection.NativeGateway;
using OpenClaw.Shared;
using Xunit;

namespace OpenClaw.Connection.Tests;

public sealed class SparkManagedGatewayIntegrationTests : IAsyncDisposable
{
    private const int TestPort = 18798;
    private readonly GatewayRecord _record = new GatewayRecordBuilder()
        .WithId("gw-spark-integration")
        .WithUrl($"ws://127.0.0.1:{TestPort}")
        .Local()
        .Build() with { NativeRuntimeContract = SparkManagedGatewayRuntime.SparkContract };

    [Fact]
    public async Task SparkGateway_Lifecycle_StartHealthAndStop()
    {
        var nodeExe = SparkManagedGatewayRuntime.ResolveNodeExecutablePath();
        var gatewayScript = SparkManagedGatewayRuntime.ResolveGatewayScriptPath();

        // Skip if Node.js or Gateway script is unavailable in current environment
        if (!File.Exists(nodeExe) && nodeExe == "node.exe")
        {
            // Verify node can be found in PATH
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("node", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                using var p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit(3000);
            }
            catch
            {
                return; // Node not installed on PATH
            }
        }

        if (!File.Exists(gatewayScript))
        {
            return; // Gateway script not found
        }

        await using var runtime = new SparkManagedGatewayRuntime(
            new WindowsNativeGatewayProcessHost(),
            TimeSpan.FromSeconds(30));

        try
        {
            // 1. Ensure running
            await runtime.EnsureRunningAsync(_record, CancellationToken.None);

            // 2. Verify inspection proves live managed gateway
            var provenance = await runtime.InspectAsync(_record, CancellationToken.None);
            Assert.Equal(GatewayEndpointProvenanceKind.ExpectedManagedGateway, provenance.Kind);
            Assert.Equal(TestPort, provenance.Port);

            // 3. Verify HTTP /health endpoint responds with { "ok": true }
            using var http = new HttpClient();
            using var response = await http.GetAsync($"http://127.0.0.1:{TestPort}/health");
            Assert.True(response.IsSuccessStatusCode);
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());

            // 4. Connect WebSocket directly to test loopback transport
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{TestPort}"), cts.Token);
            Assert.Equal(WebSocketState.Open, ws.State);

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "test completed", CancellationToken.None);
        }
        finally
        {
            // 5. Graceful shutdown
            await runtime.StopAsync(CancellationToken.None);

            // 6. Verify inspection confirms port is free
            var postStopProvenance = await runtime.InspectAsync(_record, CancellationToken.None);
            Assert.Equal(GatewayEndpointProvenanceKind.NoListener, postStopProvenance.Kind);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
