using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using OpenClaw.Connection.NativeGateway;
using OpenClaw.Shared;
using Xunit;

namespace OpenClaw.Connection.Tests;

public sealed class SparkManagedGatewayRuntimeTests : IAsyncDisposable
{
    private readonly GatewayRecord _record = new GatewayRecordBuilder()
        .WithId("gw-spark")
        .WithUrl("ws://127.0.0.1:18789")
        .Local()
        .Build() with { NativeRuntimeContract = SparkManagedGatewayRuntime.SparkContract };

    [Fact]
    public void ResolveNodeExecutablePath_ReturnsNonEmptyPath()
    {
        var path = SparkManagedGatewayRuntime.ResolveNodeExecutablePath();
        Assert.False(string.IsNullOrWhiteSpace(path));
    }

    [Fact]
    public void ResolveGatewayScriptPath_ReturnsNonEmptyPath()
    {
        var path = SparkManagedGatewayRuntime.ResolveGatewayScriptPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
    }

    [Fact]
    public void ExtractPort_ExtractsValidPortFromRecordUrl()
    {
        var record1 = _record with { Url = "ws://127.0.0.1:18789" };
        var record2 = _record with { Url = "ws://127.0.0.1:29999" };
        var record3 = _record with { Url = "invalid-url" };

        Assert.Equal(18789, SparkManagedGatewayRuntime.ExtractPort(record1));
        Assert.Equal(29999, SparkManagedGatewayRuntime.ExtractPort(record2));
        Assert.Equal(SparkManagedGatewayRuntime.DefaultPort, SparkManagedGatewayRuntime.ExtractPort(record3));
    }

    [Fact]
    public async Task Inspect_WhenNoProcessRunning_ReturnsNoListener()
    {
        await using var runtime = new SparkManagedGatewayRuntime();
        var provenance = runtime.Inspect(_record);

        Assert.Equal(GatewayEndpointProvenanceKind.NoListener, provenance.Kind);
        Assert.Equal(18789, provenance.Port);
    }

    [Fact]
    public async Task EnsureRunningAsync_LaunchesViaHost_AndStopsCleanly()
    {
        var host = new TestProcessHost();
        var runtime = new SparkManagedGatewayRuntime(host, TimeSpan.FromMilliseconds(500));

        // When script doesn't exist, EnsureRunningAsync throws FileNotFoundException
        // We set SPARK_GATEWAY_SCRIPT to a mock temporary file to test process invocation
        var tempScript = Path.GetTempFileName();
        try
        {
            Environment.SetEnvironmentVariable("SPARK_GATEWAY_SCRIPT", tempScript);

            // Host will start process, but health check will timeout in 500ms since mock process isn't listening on HTTP
            await Assert.ThrowsAsync<TimeoutException>(() =>
                runtime.EnsureRunningAsync(_record, CancellationToken.None));

            // Verify host was called with correct start spec
            Assert.Single(host.Specs);
            var spec = host.Specs[0];
            Assert.Equal(18789, spec.Port);
            Assert.Contains(tempScript, spec.Arguments);
            Assert.Contains("--port 18789", spec.Arguments);
            Assert.Contains("--bind loopback", spec.Arguments);
            Assert.Contains("--dev", spec.Arguments);
            Assert.Equal("spark-managed", spec.Environment["OPENCLAW_SERVICE_MARKER"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SPARK_GATEWAY_SCRIPT", null);
            if (File.Exists(tempScript))
                File.Delete(tempScript);
            await runtime.DisposeAsync();
        }
    }

    [Fact]
    public async Task StopAsync_DisposesRunningProcess()
    {
        var host = new TestProcessHost();
        var runtime = new SparkManagedGatewayRuntime(host, TimeSpan.FromSeconds(1));

        await runtime.StopAsync(CancellationToken.None);
        var provenance = runtime.Inspect(_record);
        Assert.Equal(GatewayEndpointProvenanceKind.NoListener, provenance.Kind);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class TestProcessHost : INativeGatewayProcessHost
    {
        public List<NativeGatewayStartSpec> Specs { get; } = [];
        public List<TestProcess> Processes { get; } = [];

        public Task<INativeGatewayProcess> StartAsync(NativeGatewayStartSpec spec, CancellationToken cancellationToken)
        {
            Specs.Add(spec);
            var proc = new TestProcess(spec.Port);
            Processes.Add(proc);
            return Task.FromResult<INativeGatewayProcess>(proc);
        }
    }

    private sealed class TestProcess(int port) : INativeGatewayProcess
    {
        public bool HasExited { get; set; }
        public bool Disposed { get; private set; }

        public bool Owns(WindowsTcpListenerInfo listener) => !HasExited && listener.Port == port;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            HasExited = true;
            return ValueTask.CompletedTask;
        }
    }
}
