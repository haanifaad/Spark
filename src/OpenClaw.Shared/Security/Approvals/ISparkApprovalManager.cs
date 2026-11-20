using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Security.Approvals;

/// <summary>
/// Central manager for Human-in-the-Loop operation approvals.
/// Enforces operation binding, expiration, history, and human-only authorization.
/// </summary>
public interface ISparkApprovalManager
{
    Task<SparkApprovalRequest> RequestApprovalAsync(
        SparkHighImpactOperationType operationType,
        string title,
        IDictionary<string, string> operationDetails,
        string requestedBy,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SparkApprovalRequest>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default);

    Task<SparkApprovalRequest?> GetApprovalByIdAsync(string approvalId, CancellationToken cancellationToken = default);

    Task<bool> ApproveAsync(
        string approvalId,
        string approverIdentity,
        string expectedBindingSignature,
        CancellationToken cancellationToken = default);

    Task<bool> RejectAsync(
        string approvalId,
        string approverIdentity,
        string reason,
        CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(
        string approvalId,
        string callerIdentity,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyAuthorizedAsync(
        string approvalId,
        SparkHighImpactOperationType operationType,
        IDictionary<string, string> currentOperationDetails,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SparkApprovalRequest>> GetApprovalHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
}
