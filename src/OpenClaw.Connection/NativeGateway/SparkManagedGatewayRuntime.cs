using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using OpenClaw.Shared;

namespace OpenClaw.Connection.NativeGateway;

/// <summary>
/// Manages the embedded OpenClaw Gateway runtime lifecycle as a supervised Node.js process.
/// Process lifetime is bound to a Win32 Job Object with KILL_ON_JOB_CLOSE, ensuring no orphan
/// processes survive if Spark crashes or terminates.
/// </summary>
public sealed class SparkManagedGatewayRuntime : INativeGatewayRuntime
{
    public const string SparkContract = "spark-managed";
    public const int DefaultPort = 18789;

    private readonly IOpenClawLogger _logger;
    private readonly INativeGatewayProcessHost _host;
    private readonly HttpClient _http;
    private readonly TimeSpan _startupTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private INativeGatewayProcess? _process;
    private GatewayRecord? _record;
    private int _disposed;

    public SparkManagedGatewayRuntime(IOpenClawLogger? logger = null)
        : this(new WindowsNativeGatewayProcessHost(), TimeSpan.FromSeconds(30), logger)
    {
    }

    internal SparkManagedGatewayRuntime(
        INativeGatewayProcessHost host,
        TimeSpan startupTimeout,
        IOpenClawLogger? logger = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _startupTimeout = startupTimeout;
        _logger = logger ?? NullLogger.Instance;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    }

    /// <summary>
    /// Resolves the Node.js executable path using bundled runtimes, environment variables,
    /// standard installation paths, or system PATH.
    /// </summary>
    public static string ResolveNodeExecutablePath()
    {
        var custom = Environment.GetEnvironmentVariable("OPENCLAW_NODE_PATH")
            ?? Environment.GetEnvironmentVariable("SPARK_NODE_PATH");
        if (!string.IsNullOrWhiteSpace(custom) && File.Exists(custom))
            return custom;

        // Check bundled runtime inside application folder
        var bundled = Path.Combine(AppContext.BaseDirectory, "runtimes", "node", "node.exe");
        if (File.Exists(bundled))
            return bundled;

        // Check standard 64-bit Node.js installation
        var standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
        if (File.Exists(standard))
            return standard;

        // Check local app data node
        var localAppDataNode = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "node", "node.exe");
        if (File.Exists(localAppDataNode))
            return localAppDataNode;

        return "node.exe";
    }

    /// <summary>
    /// Resolves the OpenClaw Gateway launcher script (openclaw.mjs) path.
    /// Priority: explicit environment override -> bundled application runtime -> sibling checkout -> user profile.
    /// </summary>
    public static string ResolveGatewayScriptPath()
    {
        var custom = Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_SCRIPT")
            ?? Environment.GetEnvironmentVariable("SPARK_GATEWAY_SCRIPT");
        if (!string.IsNullOrWhiteSpace(custom) && File.Exists(custom))
            return custom;

        // Check bundled runtime inside application folder
        var bundled = Path.Combine(AppContext.BaseDirectory, "runtimes", "gateway", "openclaw.mjs");
        if (File.Exists(bundled))
            return bundled;

        // Walk ancestor directories from the application base directory for sibling development checkouts
        try
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            for (var depth = 0; depth < 8 && current is not null; depth++, current = current.Parent)
            {
                foreach (var folderName in new[] { "openclaw-main", "openclaw" })
                {
                    var candidate = Path.Combine(current.FullName, folderName, "openclaw.mjs");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
        }
        catch
        {
            // Ignore path resolution errors
        }

        // Check user profile fallback
        try
        {
            var profileCandidate = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "openclaw",
                "openclaw.mjs");
            if (File.Exists(profileCandidate))
                return profileCandidate;
        }
        catch
        {
            // Ignore path resolution errors
        }

        return bundled;
    }

    public static int ExtractPort(GatewayRecord record)
    {
        if (Uri.TryCreate(record.Url, UriKind.Absolute, out var uri) && uri.Port > 0)
            return uri.Port;
        return DefaultPort;
    }

    public async Task EnsureRunningAsync(GatewayRecord record, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var port = ExtractPort(record);

            // If an owned process is already running and the health endpoint returns OK, we are done
            if (_process is { HasExited: false } && await CheckHealthAsync(port, cancellationToken).ConfigureAwait(false))
            {
                _record = record;
                return;
            }

            // If a previous owned process crashed or was terminated, dispose it
            if (_process != null)
            {
                await StopInternalAsync().ConfigureAwait(false);
            }

            // Check if gateway is already listening (e.g. external or pre-existing dev instance)
            if (await CheckHealthAsync(port, cancellationToken).ConfigureAwait(false))
            {
                _logger.Info($"[SparkGateway] Detected existing live Gateway on port {port}.");
                _record = record;
                return;
            }

            // Check if port is occupied by another process that is not a healthy Gateway
            var listeners = WindowsTcpListenerSnapshot.Capture();
            if (listeners.Listeners.Any(l => l.Port == port))
            {
                throw new InvalidOperationException($"[SparkGateway] Port {port} is already in use by another application. Free port {port} or configure a different port before launching.");
            }

            var nodeExe = ResolveNodeExecutablePath();
            var gatewayScript = ResolveGatewayScriptPath();
            var workingDir = Path.GetDirectoryName(gatewayScript) ?? AppContext.BaseDirectory;

            if (!File.Exists(nodeExe))
            {
                try
                {
                    using var testProcess = Process.Start(new ProcessStartInfo(nodeExe, "--version")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    });
                    testProcess?.WaitForExit(2000);
                }
                catch (Exception ex)
                {
                    throw new FileNotFoundException($"Spark cannot locate Node.js runtime executable ('{nodeExe}'). Please install Node.js 22+ or set SPARK_NODE_PATH.", nodeExe, ex);
                }
            }

            if (!File.Exists(gatewayScript))
            {
                throw new FileNotFoundException($"Spark cannot locate OpenClaw Gateway script at '{gatewayScript}'.", gatewayScript);
            }

            _logger.Info($"[SparkGateway] Launching Managed OpenClaw Gateway on port {port} using '{nodeExe}' '{gatewayScript}'...");

            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["OPENCLAW_GATEWAY_PORT"] = port.ToString(),
                ["OPENCLAW_SERVICE_MARKER"] = "spark-managed"
            };

            var spec = new NativeGatewayStartSpec(
                ExecutablePath: nodeExe,
                WorkingDirectory: workingDir,
                Port: port,
                Environment: env,
                Arguments: $"\"{gatewayScript}\" gateway run --port {port} --bind loopback --dev"
            );

            _process = await _host.StartAsync(spec, cancellationToken).ConfigureAwait(false);
            _record = record;

            // Wait until the Gateway's HTTP /health endpoint becomes responsive
            var sw = Stopwatch.StartNew();
            var healthy = false;
            while (sw.Elapsed < _startupTimeout && !cancellationToken.IsCancellationRequested)
            {
                if (_process.HasExited)
                {
                    throw new InvalidOperationException("[SparkGateway] The managed Gateway process terminated prematurely during startup.");
                }

                if (await CheckHealthAsync(port, cancellationToken).ConfigureAwait(false))
                {
                    healthy = true;
                    break;
                }

                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }

            if (!healthy)
            {
                await StopInternalAsync().ConfigureAwait(false);
                throw new TimeoutException($"[SparkGateway] Timed out waiting for Gateway to become healthy on port {port} after {_startupTimeout.TotalSeconds}s.");
            }

            _logger.Info($"[SparkGateway] Managed OpenClaw Gateway is healthy on port {port}.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopInternalAsync()
    {
        if (_process != null)
        {
            _logger.Info("[SparkGateway] Stopping managed Gateway lifecycle job...");
            try
            {
                await _process.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"[SparkGateway] Error while terminating process: {ex.Message}", ex);
            }
            finally
            {
                _process = null;
                _record = null;
            }
        }
    }

    public async Task<GatewayEndpointProvenance> InspectAsync(GatewayRecord record, CancellationToken cancellationToken)
    {
        var port = ExtractPort(record);
        var isLive = await CheckHealthAsync(port, cancellationToken).ConfigureAwait(false);
        if (isLive)
        {
            return new GatewayEndpointProvenance(
                Kind: GatewayEndpointProvenanceKind.ExpectedManagedGateway,
                Port: port,
                ProcessName: "node",
                Detail: "Spark Managed Gateway"
            );
        }

        var listeners = WindowsTcpListenerSnapshot.Capture();
        if (listeners.Listeners.Any(l => l.Port == port))
        {
            return new GatewayEndpointProvenance(
                Kind: GatewayEndpointProvenanceKind.UnknownListener,
                Port: port,
                Detail: $"Port {port} is occupied by an unverified process."
            );
        }

        return new GatewayEndpointProvenance(
            Kind: GatewayEndpointProvenanceKind.NoListener,
            Port: port
        );
    }

    public GatewayEndpointProvenance Inspect(GatewayRecord record)
    {
        var port = ExtractPort(record);
        if (_process is { HasExited: false })
        {
            return new GatewayEndpointProvenance(
                Kind: GatewayEndpointProvenanceKind.ExpectedManagedGateway,
                Port: port,
                ProcessName: "node",
                Detail: "Spark Managed Gateway"
            );
        }

        var listeners = WindowsTcpListenerSnapshot.Capture();
        if (listeners.Listeners.Any(l => l.Port == port))
        {
            return new GatewayEndpointProvenance(
                Kind: GatewayEndpointProvenanceKind.UnknownListener,
                Port: port,
                Detail: $"Port {port} is occupied by an unverified process."
            );
        }

        return new GatewayEndpointProvenance(
            Kind: GatewayEndpointProvenanceKind.NoListener,
            Port: port
        );
    }

    private async Task<bool> CheckHealthAsync(int port, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync($"http://127.0.0.1:{port}/health", ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
                {
                    return true;
                }
            }
        }
        catch
        {
            // Expected while server is starting up or stopped
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            await StopInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Dispose();
            _http.Dispose();
        }
    }
}
