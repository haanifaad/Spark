using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Tools;

/// <summary>
/// Operational risk classification for tools.
/// </summary>
public enum SparkToolRiskLevel
{
    /// <summary>Read-only queries, non-mutating inspections.</summary>
    Low = 0,

    /// <summary>Non-destructive network requests, navigation, non-sensitive searches.</summary>
    Medium = 1,

    /// <summary>State-altering operations: writing files, committing git changes, dispatching messages.</summary>
    High = 2,

    /// <summary>Privileged operations: deleting files, running shell scripts, modifying security settings.</summary>
    Critical = 3
}

/// <summary>
/// Functional category for unified tool registry organization.
/// </summary>
public enum SparkToolCategory
{
    Filesystem = 0,
    Browser = 1,
    Development = 2,
    Mcp = 3,
    ExternalConnector = 4,
    System = 5
}

/// <summary>
/// Metadata describing a tool exposed by the registry or MCP bridge.
/// </summary>
public class SparkToolMetadata
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public SparkToolCategory Category { get; set; } = SparkToolCategory.System;

    [JsonPropertyName("riskLevel")]
    public SparkToolRiskLevel RiskLevel { get; set; } = SparkToolRiskLevel.Low;

    [JsonPropertyName("requiredPermissions")]
    public List<string> RequiredPermissions { get; set; } = new();

    [JsonPropertyName("inputSchemaJson")]
    public string InputSchemaJson { get; set; } = "{}";

    [JsonPropertyName("outputSchemaJson")]
    public string OutputSchemaJson { get; set; } = "{}";

    [JsonPropertyName("defaultTimeout")]
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    [JsonPropertyName("auditMetadata")]
    public Dictionary<string, string> AuditMetadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("isMcpTool")]
    public bool IsMcpTool { get; set; }

    [JsonPropertyName("mcpServerId")]
    public string? McpServerId { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Request to execute an invocation of a tool.
/// </summary>
public class SparkToolExecutionRequest
{
    [JsonPropertyName("toolId")]
    public string ToolId { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?> Arguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("callerIdentity")]
    public string CallerIdentity { get; set; } = "agent";

    [JsonPropertyName("dryRun")]
    public bool DryRun { get; set; }

    [JsonPropertyName("grantedPermissions")]
    public HashSet<string> GrantedPermissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("isApproved")]
    public bool IsApproved { get; set; }

    [JsonPropertyName("approvalId")]
    public string? ApprovalId { get; set; }

    /// <summary>
    /// Helper to retrieve typed argument or default value.
    /// </summary>
    public T? GetArgument<T>(string key, T? defaultValue = default)
    {
        if (!Arguments.TryGetValue(key, out var val) || val == null)
        {
            return defaultValue;
        }

        if (val is T typed)
        {
            return typed;
        }

        if (val is JsonElement jsonElement)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            catch
            {
                return defaultValue;
            }
        }

        try
        {
            return (T)Convert.ChangeType(val, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}

/// <summary>
/// Result from a tool invocation with timing, risk, and audit tracking.
/// </summary>
public class SparkToolExecutionResponse
{
    [JsonPropertyName("auditId")]
    public string AuditId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("toolId")]
    public string ToolId { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("riskLevel")]
    public SparkToolRiskLevel RiskLevel { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

    [JsonPropertyName("executedAt")]
    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;

    public static SparkToolExecutionResponse Ok(string toolId, object? result, SparkToolRiskLevel risk, TimeSpan duration)
    {
        return new SparkToolExecutionResponse
        {
            ToolId = toolId,
            Success = true,
            Result = result,
            RiskLevel = risk,
            Duration = duration
        };
    }

    public static SparkToolExecutionResponse Fail(string toolId, string error, SparkToolRiskLevel risk, TimeSpan duration)
    {
        return new SparkToolExecutionResponse
        {
            ToolId = toolId,
            Success = false,
            Error = error,
            RiskLevel = risk,
            Duration = duration
        };
    }
}

/// <summary>
/// Unified tool contract implemented by all built-in and external tools.
/// </summary>
public interface ISparkTool
{
    SparkToolMetadata Metadata { get; }

    Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lifecycle status of external API connectors.
/// Clearly distinguishes implemented connectors from planned/mocked connectors.
/// </summary>
public enum SparkConnectorStatus
{
    Planned = 0,
    Configured = 1,
    Connected = 2,
    Degraded = 3,
    Error = 4,
    Disabled = 5
}

/// <summary>
/// Standardized contract for external service connectors (e.g. Google Workspace, Notes, Email).
/// </summary>
public interface ISparkExternalConnector
{
    string ConnectorId { get; }
    string DisplayName { get; }
    string Description { get; }
    bool IsImplemented { get; }
    SparkConnectorStatus Status { get; }
    Task<IReadOnlyList<SparkToolMetadata>> GetProvidedToolsAsync(CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
