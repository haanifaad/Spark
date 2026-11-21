using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Security.Approvals;
using OpenClaw.Shared.Study.Contracts;

namespace OpenClaw.Shared.Study.Infrastructure;

/// <summary>
/// Enforces Study Division execution policies: manual confirmation by default,
/// fail-closed gating, and integration with SPARK Human-in-the-Loop approvals.
/// </summary>
public sealed class StudyPermissionManager : IStudyPermissionManager
{
    private readonly ISparkApprovalManager? _approvalManager;

    /// <summary>
    /// Optional user prompt delegate for desktop UI interaction.
    /// </summary>
    public Func<string, string, Task<bool>>? UserPromptHandler { get; set; }

    public StudyPermissionManager(ISparkApprovalManager? approvalManager = null)
    {
        _approvalManager = approvalManager;
    }

    public async Task<StudyPermissionDecision> AuthorizeAsync(
        StudyPermissionKind permission,
        string targetResource,
        string description,
        string userId = "default",
        CancellationToken ct = default)
    {
        // 1. Read-only operations are permitted by default
        if (permission == StudyPermissionKind.ReadOnly)
        {
            return new StudyPermissionDecision(IsAllowed: true, RequiredUserApproval: false);
        }

        // 2. Sensitive operations (CodeExecution, DestructiveOperation, FileModification, ExternalApiAccess)
        // require explicit manual approval
        if (_approvalManager != null)
        {
            try
            {
                var opType = permission switch
                {
                    StudyPermissionKind.CodeExecution => SparkHighImpactOperationType.ExecutePrivilegedCommand,
                    StudyPermissionKind.DestructiveOperation => SparkHighImpactOperationType.DeleteFile,
                    StudyPermissionKind.FileModification => SparkHighImpactOperationType.ModifyImportantFile,
                    StudyPermissionKind.ExternalApiAccess => SparkHighImpactOperationType.SendExternalMessage,
                    _ => SparkHighImpactOperationType.CustomHighRisk
                };

                var details = new Dictionary<string, string>
                {
                    ["TargetResource"] = targetResource,
                    ["Description"] = description,
                    ["UserId"] = userId
                };

                var req = await _approvalManager.RequestApprovalAsync(
                    opType,
                    $"Study Division: {permission}",
                    details,
                    requestedBy: "StudyDivision",
                    lifetime: TimeSpan.FromMinutes(5),
                    cancellationToken: ct);

                return new StudyPermissionDecision(
                    IsAllowed: false,
                    RequiredUserApproval: true,
                    Reason: $"Operation queued for user approval (ID: {req.ApprovalId}).");
            }
            catch (Exception ex)
            {
                return new StudyPermissionDecision(
                    IsAllowed: false,
                    RequiredUserApproval: true,
                    Reason: $"Approval gateway error: {ex.Message}");
            }
        }

        // 3. Desktop UI direct prompt fallback
        if (UserPromptHandler != null)
        {
            var approved = await UserPromptHandler(permission.ToString(), $"{description} on {targetResource}");
            return new StudyPermissionDecision(
                IsAllowed: approved,
                RequiredUserApproval: true,
                Reason: approved ? "Approved by user." : "Denied by user.");
        }

        // 4. Default policy: Fail-closed for sensitive operations without explicit approval mechanism
        return new StudyPermissionDecision(
            IsAllowed: false,
            RequiredUserApproval: true,
            Reason: $"Manual execution required: {permission} on {targetResource} is gated.");
    }
}
