using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Security.Approvals;
using OpenClaw.Shared.Security.Audit;
using OpenClaw.Shared.Tools;
using Xunit;

namespace OpenClaw.Shared.Tests;

public class SparkSecurityAndAuditTests : IDisposable
{
    private readonly string _testStorageDir;

    public SparkSecurityAndAuditTests()
    {
        _testStorageDir = Path.Combine(Path.GetTempPath(), "SparkSecurityTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testStorageDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testStorageDir))
            {
                Directory.Delete(_testStorageDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task ApprovalManager_RequestAndApprove_BindsExactOperation()
    {
        using var manager = new SparkApprovalManager(_testStorageDir);

        var details = new Dictionary<string, string>
        {
            ["recipient"] = "+15551234567",
            ["message"] = "Meeting confirmed for 3 PM"
        };

        var req = await manager.RequestApprovalAsync(
            SparkHighImpactOperationType.SendExternalMessage,
            "Send confirmation SMS",
            details,
            requestedBy: "agent-scheduler");

        Assert.NotNull(req);
        Assert.Equal(SparkApprovalStatus.Pending, req.Status);
        Assert.NotEmpty(req.OperationBindingSignature);

        // Human operator approves
        var approved = await manager.ApproveAsync(
            req.ApprovalId,
            approverIdentity: "human-operator-admin",
            expectedBindingSignature: req.OperationBindingSignature);

        Assert.True(approved);

        // Verification with EXACT details succeeds
        var verified = await manager.VerifyAuthorizedAsync(
            req.ApprovalId,
            SparkHighImpactOperationType.SendExternalMessage,
            details);

        Assert.True(verified);

        // Verification with ALTERED details FAILS (operation tampering detection)
        var alteredDetails = new Dictionary<string, string>
        {
            ["recipient"] = "+15559999999", // Changed recipient!
            ["message"] = "Meeting confirmed for 3 PM"
        };

        var alteredVerified = await manager.VerifyAuthorizedAsync(
            req.ApprovalId,
            SparkHighImpactOperationType.SendExternalMessage,
            alteredDetails);

        Assert.False(alteredVerified);
    }

    [Fact]
    public async Task ApprovalManager_PreventsLlmSelfApproval()
    {
        using var manager = new SparkApprovalManager(_testStorageDir);

        var details = new Dictionary<string, string> { ["path"] = "/important/config.json" };
        var req = await manager.RequestApprovalAsync(
            SparkHighImpactOperationType.DeleteFile,
            "Delete Important Config",
            details,
            requestedBy: "agent-worker");

        // Attempt by an AI agent/LLM to approve its own or any operation MUST be rejected
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ApproveAsync(
                req.ApprovalId,
                approverIdentity: "agent-worker",
                expectedBindingSignature: req.OperationBindingSignature));

        Assert.Contains("Automated entity", ex.Message);
    }

    [Fact]
    public async Task ApprovalManager_RejectsApprovalIfParametersChanged()
    {
        using var manager = new SparkApprovalManager(_testStorageDir);

        var details = new Dictionary<string, string> { ["command"] = "rm -rf /cache" };
        var req = await manager.RequestApprovalAsync(
            SparkHighImpactOperationType.ExecutePrivilegedCommand,
            "Clear Cache",
            details,
            requestedBy: "agent-dev");

        var forgedSignature = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ApproveAsync(
                req.ApprovalId,
                approverIdentity: "human-admin",
                expectedBindingSignature: forgedSignature));

        Assert.Contains("parameters have changed", ex.Message);
    }

    [Fact]
    public async Task ApprovalManager_RejectionAndCancellation_UpdatesStatus()
    {
        using var manager = new SparkApprovalManager(_testStorageDir);

        var details = new Dictionary<string, string> { ["amount"] = "$100" };
        var req1 = await manager.RequestApprovalAsync(
            SparkHighImpactOperationType.FinancialTransaction,
            "Send Payment",
            details,
            requestedBy: "agent");

        var rejected = await manager.RejectAsync(req1.ApprovalId, "human-auditor", "Amount exceeds budget limit.");
        Assert.True(rejected);

        var loaded1 = await manager.GetApprovalByIdAsync(req1.ApprovalId);
        Assert.NotNull(loaded1);
        Assert.Equal(SparkApprovalStatus.Rejected, loaded1.Status);
        Assert.Equal("Amount exceeds budget limit.", loaded1.DecisionReason);

        var req2 = await manager.RequestApprovalAsync(
            SparkHighImpactOperationType.PublishContent,
            "Publish Article",
            new Dictionary<string, string> { ["title"] = "Draft" },
            requestedBy: "agent");

        var cancelled = await manager.CancelAsync(req2.ApprovalId, "agent");
        Assert.True(cancelled);

        var loaded2 = await manager.GetApprovalByIdAsync(req2.ApprovalId);
        Assert.NotNull(loaded2);
        Assert.Equal(SparkApprovalStatus.Cancelled, loaded2.Status);
    }

    [Fact]
    public async Task ApprovalManager_RaisesRequestAndStatusEventsAfterPersistingChanges()
    {
        using var manager = new SparkApprovalManager(_testStorageDir);
        var requested = new List<SparkApprovalRequest>();
        var statusChanges = new List<SparkApprovalRequest>();
        manager.ApprovalRequested += (_, request) => requested.Add(request);
        manager.ApprovalStatusChanged += (_, request) => statusChanges.Add(request);

        var request = await manager.RequestApprovalAsync(
            SparkHighImpactOperationType.PublishContent,
            "Publish release notes",
            new Dictionary<string, string> { ["title"] = "Release notes" },
            requestedBy: "agent-writer");

        var approved = await manager.ApproveAsync(
            request.ApprovalId,
            approverIdentity: "human-release-manager",
            expectedBindingSignature: request.OperationBindingSignature);

        Assert.True(approved);
        Assert.Single(requested);
        Assert.Equal(request.ApprovalId, requested[0].ApprovalId);
        Assert.Single(statusChanges);
        Assert.Equal(SparkApprovalStatus.Approved, statusChanges[0].Status);
    }

    [Fact]
    public async Task AuditLogger_RedactsSecretsAndMaintainsTamperEvidentLog()
    {
        using var logger = new SparkAuditLogger(_testStorageDir);

        var record = new SparkAuditRecord
        {
            UserId = "user-123",
            SessionId = "sess-abc",
            AgentId = "SPARK-Core",
            ToolId = "dev_run_command",
            Operation = "curl -H 'Authorization: Bearer secret_token_123456789012345' https://api.example.com",
            RiskLevel = SparkToolRiskLevel.Critical,
            Success = true,
            ResultSummary = "HTTP 200 OK api_key=super_secret_api_key_value_98765",
            ErrorMessage = null
        };

        await logger.LogEventAsync(record);

        // Verify secrets were redacted in the record
        Assert.DoesNotContain("secret_token_123456789012345", record.Operation);
        Assert.DoesNotContain("super_secret_api_key_value_98765", record.ResultSummary);

        // Verify query retrieves the log
        var records = await logger.QueryAuditTrailAsync(new SparkAuditQueryFilter { UserId = "user-123" });
        Assert.Single(records);
        Assert.Equal("dev_run_command", records[0].ToolId);

        // Verify log integrity
        var isIntact = await logger.VerifyLogIntegrityAsync();
        Assert.True(isIntact);

        // Simulate tampering with the audit file
        var logPath = Path.Combine(_testStorageDir, "spark_audit.jsonl");
        var text = await File.ReadAllTextAsync(logPath);
        var tampered = text.Replace("dev_run_command", "harmless_tool");
        await File.WriteAllTextAsync(logPath, tampered);

        // Verify tampering is immediately detected
        var isTampered = await logger.VerifyLogIntegrityAsync();
        Assert.False(isTampered);
    }
}
