using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Tools.Mcp;

public enum SparkMcpTransportType
{
    Http = 0,
    Stdio = 1
}

public class SparkMcpServerConfig
{
    [JsonPropertyName("serverId")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("serverName")]
    public string ServerName { get; set; } = string.Empty;

    [JsonPropertyName("endpointUrl")]
    public string EndpointUrl { get; set; } = string.Empty;

    [JsonPropertyName("authToken")]
    public string? AuthToken { get; set; }

    [JsonPropertyName("transportType")]
    public SparkMcpTransportType TransportType { get; set; } = SparkMcpTransportType.Http;

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("allowedRiskLevel")]
    public SparkToolRiskLevel MaxAllowedRiskLevel { get; set; } = SparkToolRiskLevel.High;
}

public class SparkMcpServerStatus
{
    [JsonPropertyName("serverId")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("isOnline")]
    public bool IsOnline { get; set; }

    [JsonPropertyName("discoveredToolsCount")]
    public int DiscoveredToolsCount { get; set; }

    [JsonPropertyName("lastHealthCheck")]
    public DateTimeOffset? LastHealthCheck { get; set; }

    [JsonPropertyName("lastError")]
    public string? LastError { get; set; }
}

public interface ISparkMcpManager
{
    Task<bool> AddServerAsync(SparkMcpServerConfig config, CancellationToken cancellationToken = default);
    Task<bool> RemoveServerAsync(string serverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SparkMcpServerConfig>> GetServersAsync(CancellationToken cancellationToken = default);
    Task<SparkMcpServerStatus?> GetServerStatusAsync(string serverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SparkToolMetadata>> RefreshServerToolsAsync(string serverId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Proxy tool exposing an external MCP tool as an ISparkTool inside the registry.
/// </summary>
public class SparkMcpToolProxy : ISparkTool
{
    private readonly SparkMcpServerConfig _serverConfig;
    private readonly HttpClient _httpClient;

    public SparkMcpToolProxy(SparkMcpServerConfig serverConfig, SparkToolMetadata metadata, HttpClient? httpClient = null)
    {
        _serverConfig = serverConfig;
        Metadata = metadata;
        _httpClient = httpClient ?? new HttpClient();
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Metadata.RiskLevel > _serverConfig.MaxAllowedRiskLevel)
        {
            return SparkToolExecutionResponse.Fail(
                Metadata.Id,
                $"Execution denied: Tool risk level ({Metadata.RiskLevel}) exceeds server configured limit ({_serverConfig.MaxAllowedRiskLevel}).",
                Metadata.RiskLevel,
                TimeSpan.Zero);
        }

        // Build standard JSON-RPC 2.0 tools/call request
        var rpcRequest = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = "tools/call",
            @params = new
            {
                name = Metadata.Name,
                arguments = request.Arguments
            }
        };

        var json = JsonSerializer.Serialize(rpcRequest);
        using var reqMsg = new HttpRequestMessage(HttpMethod.Post, _serverConfig.EndpointUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_serverConfig.AuthToken))
        {
            reqMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serverConfig.AuthToken);
        }

        try
        {
            using var resp = await _httpClient.SendAsync(reqMsg, cancellationToken).ConfigureAwait(false);
            var respBody = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                return SparkToolExecutionResponse.Fail(
                    Metadata.Id,
                    $"MCP server returned HTTP status {(int)resp.StatusCode}: {respBody}",
                    Metadata.RiskLevel,
                    TimeSpan.Zero);
            }

            using var doc = JsonDocument.Parse(respBody);
            if (doc.RootElement.TryGetProperty("error", out var errorProp))
            {
                var errMsg = errorProp.GetRawText();
                return SparkToolExecutionResponse.Fail(Metadata.Id, $"MCP Error: {errMsg}", Metadata.RiskLevel, TimeSpan.Zero);
            }

            if (doc.RootElement.TryGetProperty("result", out var resultProp))
            {
                var parsedResult = JsonSerializer.Deserialize<object>(resultProp.GetRawText());
                return SparkToolExecutionResponse.Ok(Metadata.Id, parsedResult, Metadata.RiskLevel, TimeSpan.Zero);
            }

            return SparkToolExecutionResponse.Ok(Metadata.Id, respBody, Metadata.RiskLevel, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, $"MCP invocation failed: {ex.Message}", Metadata.RiskLevel, TimeSpan.Zero);
        }
    }
}

/// <summary>
/// Thread-safe manager for MCP server discovery, connections, health checks, and tool registry bridging.
/// </summary>
public class SparkMcpManager : ISparkMcpManager
{
    private readonly ConcurrentDictionary<string, SparkMcpServerConfig> _servers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SparkMcpServerStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ISparkToolRegistry _toolRegistry;
    private readonly HttpClient _httpClient;

    public SparkMcpManager(ISparkToolRegistry toolRegistry, HttpClient? httpClient = null)
    {
        _toolRegistry = toolRegistry;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<bool> AddServerAsync(SparkMcpServerConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(config.ServerId)) throw new ArgumentException("ServerId is required.", nameof(config));

        _servers[config.ServerId] = config;
        _statuses[config.ServerId] = new SparkMcpServerStatus
        {
            ServerId = config.ServerId,
            IsOnline = false,
            LastHealthCheck = DateTimeOffset.UtcNow
        };

        if (config.IsEnabled)
        {
            await RefreshServerToolsAsync(config.ServerId, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public Task<bool> RemoveServerAsync(string serverId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverId)) return Task.FromResult(false);

        _servers.TryRemove(serverId, out _);
        _statuses.TryRemove(serverId, out _);

        // Unregister all tools belonging to this MCP server
        var toolsToRemove = _toolRegistry.ListTools(SparkToolCategory.Mcp, includeDisabled: true)
            .Where(t => t.McpServerId == serverId)
            .ToList();

        foreach (var t in toolsToRemove)
        {
            _toolRegistry.UnregisterTool(t.Id);
        }

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<SparkMcpServerConfig>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SparkMcpServerConfig>>(_servers.Values.ToList());
    }

    public Task<SparkMcpServerStatus?> GetServerStatusAsync(string serverId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverId)) return Task.FromResult<SparkMcpServerStatus?>(null);
        return Task.FromResult(_statuses.TryGetValue(serverId, out var status) ? status : null);
    }

    public async Task<IReadOnlyList<SparkToolMetadata>> RefreshServerToolsAsync(string serverId, CancellationToken cancellationToken = default)
    {
        if (!_servers.TryGetValue(serverId, out var config) || !config.IsEnabled)
        {
            return Array.Empty<SparkToolMetadata>();
        }

        var status = _statuses.GetOrAdd(serverId, id => new SparkMcpServerStatus { ServerId = id });
        status.LastHealthCheck = DateTimeOffset.UtcNow;

        var rpcRequest = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = "tools/list",
            @params = new { }
        };

        var json = JsonSerializer.Serialize(rpcRequest);
        using var reqMsg = new HttpRequestMessage(HttpMethod.Post, config.EndpointUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(config.AuthToken))
        {
            reqMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AuthToken);
        }

        try
        {
            using var resp = await _httpClient.SendAsync(reqMsg, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                status.IsOnline = false;
                status.LastError = $"Server returned HTTP status {(int)resp.StatusCode}";
                return Array.Empty<SparkToolMetadata>();
            }

            var respText = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(respText);

            var discovered = new List<SparkToolMetadata>();

            if (doc.RootElement.TryGetProperty("result", out var resultProp) &&
                resultProp.TryGetProperty("tools", out var toolsArray) &&
                toolsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var toolElem in toolsArray.EnumerateArray())
                {
                    var name = toolElem.GetProperty("name").GetString() ?? string.Empty;
                    var desc = toolElem.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                    var schema = toolElem.TryGetProperty("inputSchema", out var s) ? s.GetRawText() : "{}";

                    var toolMeta = new SparkToolMetadata
                    {
                        Id = $"mcp_{serverId}_{name}",
                        Name = name,
                        Description = desc,
                        Category = SparkToolCategory.Mcp,
                        RiskLevel = SparkToolRiskLevel.Medium,
                        InputSchemaJson = schema,
                        IsMcpTool = true,
                        McpServerId = serverId,
                        IsEnabled = true
                    };

                    var toolProxy = new SparkMcpToolProxy(config, toolMeta, _httpClient);
                    _toolRegistry.RegisterTool(toolProxy);
                    discovered.Add(toolMeta);
                }
            }

            status.IsOnline = true;
            status.LastError = null;
            status.DiscoveredToolsCount = discovered.Count;
            return discovered;
        }
        catch (Exception ex)
        {
            status.IsOnline = false;
            status.LastError = ex.Message;
            return Array.Empty<SparkToolMetadata>();
        }
    }
}
