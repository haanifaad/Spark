using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Security.Approvals;

/// <summary>
/// Production Human-in-the-Loop approval manager.
/// Enforces strict human-only decisions, cryptographic operation binding, and automatic expiration.
/// </summary>
public class SparkApprovalManager : ISparkApprovalManager, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, SparkApprovalRequest> _approvals = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _storageFilePath;

    public event EventHandler<SparkApprovalRequest>? ApprovalRequested;
    public event EventHandler<SparkApprovalRequest>? ApprovalStatusChanged;

    public SparkApprovalManager(string? storagePath = null)
    {
        var rootDir = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "Security");

        if (!Directory.Exists(rootDir))
        {
            Directory.CreateDirectory(rootDir);
        }

        _storageFilePath = Path.Combine(rootDir, "approvals.json");
        LoadFromDisk();
    }

    public async Task<SparkApprovalRequest> RequestApprovalAsync(
        SparkHighImpactOperationType operationType,
        string title,
        IDictionary<string, string> operationDetails,
        string requestedBy,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationDetails);
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));

        SparkApprovalRequest request;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ExpireStaleRequestsInternal();

            var bindingSig = SparkApprovalRequest.ComputeBindingSignature(operationType, operationDetails);
            var req = new SparkApprovalRequest
            {
                OperationType = operationType,
                Title = title,
                OperationDetails = new Dictionary<string, string>(operationDetails, StringComparer.OrdinalIgnoreCase),
                RequestedBy = requestedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(15)),
                Status = SparkApprovalStatus.Pending,
                OperationBindingSignature = bindingSig
            };

            _approvals[req.ApprovalId] = req;
            try
            {
                SaveToDisk();
            }
            catch
            {
                _approvals.Remove(req.ApprovalId);
                throw;
            }

            request = req;
        }
        finally
        {
            _gate.Release();
        }

        ApprovalRequested?.Invoke(this, request);
        return request;
    }

    public async Task<IReadOnlyList<SparkApprovalRequest>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ExpireStaleRequestsInternal();
            return _approvals.Values
                .Where(a => a.Status == SparkApprovalStatus.Pending)
                .OrderBy(a => a.CreatedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkApprovalRequest?> GetApprovalByIdAsync(string approvalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return null;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ExpireStaleRequestsInternal();
            return _approvals.TryGetValue(approvalId, out var req) ? req : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ApproveAsync(
        string approvalId,
        string approverIdentity,
        string expectedBindingSignature,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approvalId) || string.IsNullOrWhiteSpace(approverIdentity)) return false;

        // Strict rule: AI models and agents cannot approve operations
        if (IsAutomatedAgentIdentity(approverIdentity))
        {
            throw new InvalidOperationException(
                $"Authorization failed: Automated entity '{approverIdentity}' cannot approve high-impact operations. Explicit human approval is required.");
        }

        SparkApprovalRequest? approvedRequest = null;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ExpireStaleRequestsInternal();

            if (!_approvals.TryGetValue(approvalId, out var req)) return false;
            if (req.Status != SparkApprovalStatus.Pending) return false;

            // Strict operation binding check
            if (!string.Equals(req.OperationBindingSignature, expectedBindingSignature, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Authorization rejected: The operation parameters have changed since approval was requested. A new approval request must be submitted.");
            }

            var originalStatus = req.Status;
            var originalDecidedAt = req.DecidedAt;
            var originalDecidedBy = req.DecidedBy;
            var originalDecisionReason = req.DecisionReason;
            req.Status = SparkApprovalStatus.Approved;
            req.DecidedAt = DateTimeOffset.UtcNow;
            req.DecidedBy = approverIdentity;
            req.DecisionReason = "Approved by authorized human operator.";

            try
            {
                SaveToDisk();
            }
            catch
            {
                req.Status = originalStatus;
                req.DecidedAt = originalDecidedAt;
                req.DecidedBy = originalDecidedBy;
                req.DecisionReason = originalDecisionReason;
                throw;
            }

            approvedRequest = req;
        }
        finally
        {
            _gate.Release();
        }

        if (approvedRequest is null) return false;
        ApprovalStatusChanged?.Invoke(this, approvedRequest);
        return true;
    }

    public async Task<bool> RejectAsync(
        string approvalId,
        string approverIdentity,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return false;

        SparkApprovalRequest? rejectedRequest = null;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ExpireStaleRequestsInternal();

            if (!_approvals.TryGetValue(approvalId, out var req)) return false;
            if (req.Status != SparkApprovalStatus.Pending) return false;

            var originalStatus = req.Status;
            var originalDecidedAt = req.DecidedAt;
            var originalDecidedBy = req.DecidedBy;
            var originalDecisionReason = req.DecisionReason;
            req.Status = SparkApprovalStatus.Rejected;
            req.DecidedAt = DateTimeOffset.UtcNow;
            req.DecidedBy = approverIdentity;
            req.DecisionReason = reason;

            try
            {
                SaveToDisk();
            }
            catch
            {
                req.Status = originalStatus;
                req.DecidedAt = originalDecidedAt;
                req.DecidedBy = originalDecidedBy;
                req.DecisionReason = originalDecisionReason;
                throw;
            }

            rejectedRequest = req;
        }
        finally
        {
            _gate.Release();
        }

        if (rejectedRequest is null) return false;
        ApprovalStatusChanged?.Invoke(this, rejectedRequest);
        return true;
    }

    public async Task<bool> CancelAsync(
        string approvalId,
        string callerIdentity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return false;

        SparkApprovalRequest? cancelledRequest = null;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ExpireStaleRequestsInternal();

            if (!_approvals.TryGetValue(approvalId, out var req)) return false;
            if (req.Status != SparkApprovalStatus.Pending) return false;

            var originalStatus = req.Status;
            var originalDecidedAt = req.DecidedAt;
            var originalDecidedBy = req.DecidedBy;
            var originalDecisionReason = req.DecisionReason;
            req.Status = SparkApprovalStatus.Cancelled;
            req.DecidedAt = DateTimeOffset.UtcNow;
            req.DecidedBy = callerIdentity;
            req.DecisionReason = "Cancelled by caller.";

            try
            {
                SaveToDisk();
            }
            catch
            {
                req.Status = originalStatus;
                req.DecidedAt = originalDecidedAt;
                req.DecidedBy = originalDecidedBy;
                req.DecisionReason = originalDecisionReason;
                throw;
            }

            cancelledRequest = req;
        }
        finally
        {
            _gate.Release();
        }

        if (cancelledRequest is null) return false;
        ApprovalStatusChanged?.Invoke(this, cancelledRequest);
        return true;
    }

    public async Task<bool> VerifyAuthorizedAsync(
        string approvalId,
        SparkHighImpactOperationType operationType,
        IDictionary<string, string> currentOperationDetails,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return false;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ExpireStaleRequestsInternal();

            if (!_approvals.TryGetValue(approvalId, out var req)) return false;
            if (req.Status != SparkApprovalStatus.Approved) return false;

            if (req.ExpiresAt < DateTimeOffset.UtcNow)
            {
                req.Status = SparkApprovalStatus.Expired;
                SaveToDisk();
                return false;
            }

            var currentSig = SparkApprovalRequest.ComputeBindingSignature(operationType, currentOperationDetails);
            return string.Equals(req.OperationBindingSignature, currentSig, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SparkApprovalRequest>> GetApprovalHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ExpireStaleRequestsInternal();
            return _approvals.Values
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ExpireStaleRequestsInternal()
    {
        var now = DateTimeOffset.UtcNow;
        bool changed = false;

        foreach (var req in _approvals.Values)
        {
            if (req.Status == SparkApprovalStatus.Pending && req.ExpiresAt <= now)
            {
                req.Status = SparkApprovalStatus.Expired;
                req.DecidedAt = now;
                req.DecisionReason = "Approval window expired without human response.";
                changed = true;
            }
        }

        if (changed) SaveToDisk();
    }

    private static bool IsAutomatedAgentIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return true;
        var lower = identity.ToLowerInvariant();
        return lower.Contains("agent") ||
               lower.Contains("llm") ||
               lower.Contains("ai") ||
               lower.Contains("model") ||
               lower.Contains("spark-bot") ||
               lower.Contains("autonomous");
    }

    private void SaveToDisk()
    {
        var tmp = _storageFilePath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(_approvals.Values, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tmp, json);
            File.Move(tmp, _storageFilePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tmp))
            {
                File.Delete(tmp);
            }

            throw;
        }
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_storageFilePath)) return;
        try
        {
            var json = File.ReadAllText(_storageFilePath);
            var list = JsonSerializer.Deserialize<List<SparkApprovalRequest>>(json);
            if (list != null)
            {
                foreach (var a in list)
                {
                    _approvals[a.ApprovalId] = a;
                }
            }
        }
        catch
        {
            // Ignore corrupted cache
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
