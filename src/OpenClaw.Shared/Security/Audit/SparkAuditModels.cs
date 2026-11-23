using System;
using System.Text.Json.Serialization;
using OpenClaw.Shared.Security.Approvals;
using OpenClaw.Shared.Tools;

namespace OpenClaw.Shared.Security.Audit;

/// <summary>
/// Tamper-evident, secret-redacted audit record.
/// </summary>
public class SparkAuditRecord
{
    [JsonPropertyName("auditId")]
    public string AuditId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("agentId")]
    public string AgentId { get; set; } = "SPARK-Core";

    [JsonPropertyName("toolId")]
    public string ToolId { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("riskLevel")]
    public SparkToolRiskLevel RiskLevel { get; set; } = SparkToolRiskLevel.Low;

    [JsonPropertyName("approvalStatus")]
    public SparkApprovalStatus? ApprovalStatus { get; set; }

    [JsonPropertyName("approvalId")]
    public string? ApprovalId { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("resultSummary")]
    public string? ResultSummary { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("integrityChecksum")]
    public string IntegrityChecksum { get; set; } = string.Empty;
}

public class SparkAuditQueryFilter
{
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public string? ToolId { get; set; }
    public SparkToolRiskLevel? MinimumRiskLevel { get; set; }
    public bool? SuccessOnly { get; set; }
    public DateTimeOffset? FromTimestamp { get; set; }
    public DateTimeOffset? ToTimestamp { get; set; }
}
