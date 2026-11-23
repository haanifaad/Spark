using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenClaw.Shared.Tools;

namespace OpenClaw.Shared.Security.Approvals;

/// <summary>
/// Categories of high-impact operations requiring explicit human approval.
/// </summary>
public enum SparkHighImpactOperationType
{
    SendExternalMessage = 0,
    DeleteFile = 1,
    ModifyImportantFile = 2,
    ExecutePrivilegedCommand = 3,
    PublishContent = 4,
    FinancialTransaction = 5,
    ChangeSecuritySettings = 6,
    CustomHighRisk = 7
}

/// <summary>
/// Status lifecycle of human approval requests.
/// </summary>
public enum SparkApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
    Expired = 4
}

/// <summary>
/// Persistent record representing an explicit human-in-the-loop authorization request.
/// Strictly bound to the exact payload via SHA-256 operation signature.
/// </summary>
public class SparkApprovalRequest
{
    [JsonPropertyName("approvalId")]
    public string ApprovalId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("operationType")]
    public SparkHighImpactOperationType OperationType { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("operationDetails")]
    public Dictionary<string, string> OperationDetails { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("riskLevel")]
    public SparkToolRiskLevel RiskLevel { get; set; } = SparkToolRiskLevel.High;

    [JsonPropertyName("requestedBy")]
    public string RequestedBy { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddMinutes(15);

    [JsonPropertyName("status")]
    public SparkApprovalStatus Status { get; set; } = SparkApprovalStatus.Pending;

    [JsonPropertyName("decidedAt")]
    public DateTimeOffset? DecidedAt { get; set; }

    [JsonPropertyName("decidedBy")]
    public string? DecidedBy { get; set; }

    [JsonPropertyName("decisionReason")]
    public string? DecisionReason { get; set; }

    /// <summary>
    /// Cryptographic SHA-256 hash of the exact operation parameters.
    /// If the operation parameters change, this signature changes and the approval is invalidated.
    /// </summary>
    [JsonPropertyName("operationBindingSignature")]
    public string OperationBindingSignature { get; set; } = string.Empty;

    public static string ComputeBindingSignature(SparkHighImpactOperationType opType, IDictionary<string, string> details)
    {
        var sorted = new SortedDictionary<string, string>(details, StringComparer.Ordinal);
        var canonical = $"{opType}:" + string.Join(";", sorted.Select(kv => $"{kv.Key}={kv.Value}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
