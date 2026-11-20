using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Models;

namespace OpenClaw.Shared.Coding.Permissions;

/// <summary>
/// A permission request requiring evaluation and optional user authorization.
/// </summary>
public sealed record CodingPermissionRequest
{
    public required string Id { get; init; }
    public CodingPermissionLevel Level { get; init; }
    public required string Operation { get; init; }
    public required string Resource { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Audit entry for an evaluated permission request.
/// </summary>
public sealed record PermissionAuditEntry(
    CodingPermissionRequest Request,
    bool Approved,
    string DecidedBy,
    DateTimeOffset DecidedAt);

/// <summary>
/// Permission manager enforcing multi-level authorization for coding operations,
/// with session-scoped auto-approval rules and complete audit logging.
/// </summary>
public sealed class CodingPermissionManager
{
    private readonly ConcurrentDictionary<CodingPermissionLevel, bool> _sessionAutoApproved = new();
    private readonly List<PermissionAuditEntry> _auditLog = new();
    private readonly object _lock = new();

    /// <summary>
    /// Interactive prompt delegate provided by the UI or host to ask user for approval.
    /// Returns (Approved, RememberForSession).
    /// </summary>
    public Func<CodingPermissionRequest, Task<(bool Approved, bool RememberForSession)>>? UserPromptHandler { get; set; }

    /// <summary>
    /// Audit log of all permission decisions made during this session.
    /// </summary>
    public IReadOnlyList<PermissionAuditEntry> AuditLog
    {
        get
        {
            lock (_lock) return _auditLog.ToArray();
        }
    }

    /// <summary>
    /// Evaluates whether an operation is permitted, prompting the user if necessary.
    /// </summary>
    public async Task<bool> RequestPermissionAsync(
        CodingPermissionLevel level,
        string operation,
        string resource,
        string? details = null,
        CancellationToken ct = default)
    {
        // Level 1: Read-only is always safe and permitted
        if (level == CodingPermissionLevel.ReadOnly)
        {
            RecordAudit(new CodingPermissionRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                Level = level,
                Operation = operation,
                Resource = resource,
                Details = details
            }, approved: true, decidedBy: "Policy (ReadOnly Default)");
            return true;
        }

        // Check if session has auto-approval for this level
        if (_sessionAutoApproved.TryGetValue(level, out bool autoApproved) && autoApproved)
        {
            RecordAudit(new CodingPermissionRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                Level = level,
                Operation = operation,
                Resource = resource,
                Details = details
            }, approved: true, decidedBy: "Session Auto-Approval");
            return true;
        }

        var req = new CodingPermissionRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            Level = level,
            Operation = operation,
            Resource = resource,
            Details = details
        };

        // If no user prompt handler is wired, default to deny for safety
        if (UserPromptHandler == null)
        {
            RecordAudit(req, approved: false, decidedBy: "Policy (No Prompt Handler Available)");
            return false;
        }

        var (approved, remember) = await UserPromptHandler(req);

        if (approved && remember)
        {
            _sessionAutoApproved[level] = true;
        }

        RecordAudit(req, approved, decidedBy: "User Interactive Prompt");
        return approved;
    }

    /// <summary>
    /// Explicitly grants auto-approval for a permission level for the remainder of the session.
    /// </summary>
    public void GrantSessionApproval(CodingPermissionLevel level)
    {
        _sessionAutoApproved[level] = true;
    }

    /// <summary>
    /// Revokes auto-approval for a permission level or all levels.
    /// </summary>
    public void RevokeSessionApprovals()
    {
        _sessionAutoApproved.Clear();
    }

    private void RecordAudit(CodingPermissionRequest req, bool approved, string decidedBy)
    {
        lock (_lock)
        {
            _auditLog.Add(new PermissionAuditEntry(req, approved, decidedBy, DateTimeOffset.UtcNow));
        }
    }
}
