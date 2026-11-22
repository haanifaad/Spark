using System;
using System.Collections.Generic;
using OpenClaw.Shared.Study.Contracts;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// Domain categories for Study tools.
/// </summary>
public enum ToolCategory
{
    Mathematics,
    Documents,
    Programming,
    Learning,
    Research,
    System
}

/// <summary>
/// Detailed lifecycle state of a tool execution.
/// Follows: Requested -> Validating -> PermissionCheck -> AwaitingApproval -> Executing -> Completed / Failed / Cancelled.
/// </summary>
public enum ToolExecutionStatus
{
    Requested,
    Validating,
    PermissionCheck,
    AwaitingApproval,
    Executing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Record of human-in-the-loop approval or rejection for tool execution.
/// </summary>
public sealed class StudyApprovalInfo
{
    public string ApprovalId { get; set; } = Guid.NewGuid().ToString("N");
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime DecidedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsApproved { get; set; }
    public string? DecisionNote { get; set; }

    public StudyApprovalInfo() { }

    public StudyApprovalInfo(string approvalId, string approvedBy, DateTime decidedAtUtc, bool isApproved, string? decisionNote = null)
    {
        ApprovalId = approvalId;
        ApprovedBy = approvedBy;
        DecidedAtUtc = decidedAtUtc;
        IsApproved = isApproved;
        DecisionNote = decisionNote;
    }
}

/// <summary>
/// Typed definition of a tool available in the SPARK Study Division.
/// </summary>
public sealed class StudyToolDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ToolCategory Category { get; set; } = ToolCategory.System;
    public string Version { get; set; } = "1.0.0";
    public string InputSchema { get; set; } = "{}";
    public string OutputSchema { get; set; } = "{}";
    public StudyPermissionKind RequiredPermission { get; set; } = StudyPermissionKind.ReadOnly;
    public int TimeoutMs { get; set; } = 10000;
    public bool IsAvailable { get; set; } = true;
    public string? AvailabilityReason { get; set; }
    public List<string> Tags { get; set; } = [];

    public StudyToolDefinition() { }

    public StudyToolDefinition(
        string id,
        string name,
        string description,
        ToolCategory category,
        string version,
        string inputSchema,
        string outputSchema,
        StudyPermissionKind requiredPermission = StudyPermissionKind.ReadOnly,
        int timeoutMs = 10000,
        bool isAvailable = true,
        string? availabilityReason = null,
        IEnumerable<string>? tags = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        Version = version;
        InputSchema = inputSchema;
        OutputSchema = outputSchema;
        RequiredPermission = requiredPermission;
        TimeoutMs = timeoutMs;
        IsAvailable = isAvailable;
        AvailabilityReason = availabilityReason;
        Tags = tags != null ? [.. tags] : [];
    }
}

/// <summary>
/// Invocation request for executing a tool.
/// </summary>
public sealed class StudyToolExecutionRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string ToolId { get; set; } = string.Empty;
    public string UserId { get; set; } = "default";
    public string? SessionId { get; set; }
    public Dictionary<string, object?> Arguments { get; set; } = [];
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public StudyToolExecutionRequest() { }

    public StudyToolExecutionRequest(
        string toolId,
        Dictionary<string, object?>? arguments = null,
        string userId = "default",
        string? sessionId = null,
        string? requestId = null)
    {
        RequestId = requestId ?? Guid.NewGuid().ToString("N");
        ToolId = toolId;
        Arguments = arguments != null ? new Dictionary<string, object?>(arguments, StringComparer.OrdinalIgnoreCase) : [];
        UserId = userId;
        SessionId = sessionId;
        RequestedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Result and audit metadata of a tool execution.
/// </summary>
public sealed class StudyToolExecutionResult
{
    public string RequestId { get; set; } = string.Empty;
    public string ToolId { get; set; } = string.Empty;
    public string UserId { get; set; } = "default";
    public ToolExecutionStatus Status { get; set; } = ToolExecutionStatus.Requested;
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
    public StudyApprovalInfo? ApprovalRecord { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public StudyToolExecutionResult() { }

    public StudyToolExecutionResult(
        string requestId,
        string toolId,
        string userId,
        ToolExecutionStatus status,
        bool success,
        object? data = null,
        string? errorMessage = null,
        long durationMs = 0,
        StudyApprovalInfo? approvalRecord = null)
    {
        RequestId = requestId;
        ToolId = toolId;
        UserId = userId;
        Status = status;
        Success = success;
        Data = data;
        ErrorMessage = errorMessage;
        DurationMs = durationMs;
        ApprovalRecord = approvalRecord;
        TimestampUtc = DateTime.UtcNow;
    }
}
