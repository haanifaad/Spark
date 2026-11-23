using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OpenClaw.Shared.Tools;

namespace OpenClaw.Shared.Security.Sentinel;

/// <summary>
/// Production implementation of the Sentinel independent second-order security reviewer.
/// </summary>
public sealed class SentinelReviewer : ISentinelReviewer
{
    private static readonly Regex InjectionPattern = new(
        @"(?i)\b(ignore\s+(all\s+)?previous\s+instructions|disregard\s+(all\s+)?(rules|safety|policies)|bypass\s+(permissions|security|sentinel)|grant\s+(root|admin|full\s+access)|override\s+system\s+policy|delete\s+all\s+(logs|audits))\b",
        RegexOptions.Compiled);

    private static readonly Regex SecretTokenPattern = new(
        @"(?i)(bearer\s+[a-zA-Z0-9_\-\.]{20,}|ghp_[a-zA-Z0-9]{36}|sk-[a-zA-Z0-9]{32,}|BEGIN\s+(RSA|OPENSSH|EC|PRIVATE)\s+KEY)",
        RegexOptions.Compiled);

    public Task<SentinelReviewResult> ReviewOperationAsync(SentinelEvaluationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reasons = new List<string>();
        var evidence = new List<string>();
        var policies = new List<string>();
        var risk = SparkToolRiskLevel.Low;

        // 1. Strict Check: Prompt Injection / System Policy Override
        var fullTextToCheck = $"{request.TaskOriginalInstruction} {request.DataPayloadText}";
        var injectionMatch = InjectionPattern.Match(fullTextToCheck);
        if (injectionMatch.Success)
        {
            reasons.Add("Prompt injection or adversarial override directive detected in instruction or payload.");
            evidence.Add($"Matched signature: '{injectionMatch.Value}'");
            policies.Add("SEC-POL-001: External content and instructions cannot override system safety boundaries.");

            return Task.FromResult(CreateResult(
                SentinelDecisionKind.Rejected,
                SparkToolRiskLevel.Critical,
                reasons,
                evidence,
                policies,
                "Adversarial request blocked; task halted immediately."));
        }

        // 2. Strict Check: Prohibited Autonomous Financial Actions
        var toolLower = request.ProposedToolName.ToLowerInvariant();
        if (toolLower.Contains("bank_transfer") || toolLower.Contains("upi_pin") || toolLower.Contains("card_auth"))
        {
            reasons.Add("Autonomous execution of direct bank transfers, UPI PIN entry, or card authentication is strictly prohibited.");
            evidence.Add($"Proposed prohibited tool: '{request.ProposedToolName}'");
            policies.Add("FIN-POL-001: Autonomous bank transfers, PINs, and card credential entry are completely forbidden.");

            return Task.FromResult(CreateResult(
                SentinelDecisionKind.Rejected,
                SparkToolRiskLevel.Critical,
                reasons,
                evidence,
                policies,
                "Direct financial transfers must be executed manually by the user outside autonomous tooling."));
        }

        // 3. Financial Transaction User Approval Mandate
        if (request.FinancialAmount.HasValue && request.FinancialAmount.Value > 0 || toolLower.Contains("checkout") || toolLower.Contains("travel_book"))
        {
            risk = SparkToolRiskLevel.Critical;
            reasons.Add($"Financial transaction detected ({request.FinancialAmount ?? 0} {request.Currency}); mandatory human approval required.");
            evidence.Add($"Amount: {request.FinancialAmount} {request.Currency}, Tool: {request.ProposedToolName}");
            policies.Add("FIN-POL-002: Every purchase, booking, or financial transaction requires explicit user authorization.");

            return Task.FromResult(CreateResult(
                SentinelDecisionKind.RequiresUserApproval,
                risk,
                reasons,
                evidence,
                policies,
                $"Review and authorize transaction for {request.FinancialAmount} {request.Currency}."));
        }

        // 4. Secret Exfiltration & Data Leakage Check
        if (!string.IsNullOrWhiteSpace(request.DataPayloadText))
        {
            var secretMatch = SecretTokenPattern.Match(request.DataPayloadText);
            if (secretMatch.Success)
            {
                // If secrets are present and target is external, reject exfiltration
                if (!string.IsNullOrWhiteSpace(request.TargetDestination) && !request.TargetDestination.StartsWith("localhost") && !request.TargetDestination.StartsWith("127.0.0.1"))
                {
                    reasons.Add("Transmission of sensitive credentials (tokens, private keys) to external destinations was intercepted.");
                    evidence.Add($"Secret pattern matched in payload destined for '{request.TargetDestination}'");
                    policies.Add("SEC-POL-002: Secret redaction and exfiltration blockade.");

                    return Task.FromResult(CreateResult(
                        SentinelDecisionKind.Rejected,
                        SparkToolRiskLevel.Critical,
                        reasons,
                        evidence,
                        policies,
                        "Credentials cannot be transmitted externally. Remove sensitive data before proceeding."));
                }
            }
        }

        // 5. Destructive Operations
        if (toolLower.Contains("delete") || toolLower.Contains("fs_delete_file") || toolLower.Contains("format") || toolLower.Contains("wipe"))
        {
            risk = SparkToolRiskLevel.Critical;
            reasons.Add("Destructive file operation detected.");
            evidence.Add($"Tool: {request.ProposedToolName}");
            policies.Add("SEC-POL-003: Irreversible and destructive operations require human confirmation.");

            // In autonomous background mode, destructive operations are strictly rejected
            if (request.IsAutonomousBackgroundExecution)
            {
                reasons.Add("Destructive operations are forbidden in autonomous background execution mode.");
                return Task.FromResult(CreateResult(
                    SentinelDecisionKind.Rejected,
                    risk,
                    reasons,
                    evidence,
                    policies,
                    "Task cannot delete files during unattended background runs."));
            }

            return Task.FromResult(CreateResult(
                SentinelDecisionKind.RequiresUserApproval,
                risk,
                reasons,
                evidence,
                policies,
                "Confirm explicit deletion of the target files."));
        }

        // 6. External Communications & Message Sending
        if (toolLower.Contains("send") && (toolLower.Contains("email") || toolLower.Contains("channel") || toolLower.Contains("message")))
        {
            risk = SparkToolRiskLevel.High;
            reasons.Add("Outbound communication to external recipient requires human verification.");
            evidence.Add($"Target: {request.TargetDestination ?? "External recipient"}, Tool: {request.ProposedToolName}");
            policies.Add("COM-POL-001: Outbound communication dispatching requires human-in-the-loop authorization.");

            return Task.FromResult(CreateResult(
                SentinelDecisionKind.RequiresUserApproval,
                risk,
                reasons,
                evidence,
                policies,
                "Review draft message, recipients, and attachments before dispatching."));
        }

        // 7. Controlled Command Execution
        if (toolLower.Contains("run_command") || toolLower.Contains("shell") || toolLower.Contains("exec"))
        {
            risk = SparkToolRiskLevel.High;
            reasons.Add("Arbitrary shell execution detected.");
            evidence.Add($"Tool: {request.ProposedToolName}");
            policies.Add("SEC-POL-004: Arbitrary command execution must be previewed and approved.");

            return Task.FromResult(CreateResult(
                SentinelDecisionKind.RequiresUserApproval,
                risk,
                reasons,
                evidence,
                policies,
                "Review proposed shell command and arguments."));
        }

        // Safe / Low-Risk Operation: Approved
        reasons.Add("Operation verified as safe within configured policy boundaries.");
        policies.Add("SEC-POL-000: Standard safe operation authorized.");

        return Task.FromResult(CreateResult(
            SentinelDecisionKind.Approved,
            SparkToolRiskLevel.Low,
            reasons,
            evidence,
            policies,
            null));
    }

    private static SentinelReviewResult CreateResult(
        SentinelDecisionKind decision,
        SparkToolRiskLevel risk,
        List<string> reasons,
        List<string> evidence,
        List<string> policies,
        string? userAction)
    {
        var canonical = $"{decision}:{risk}:" + string.Join(";", reasons);
        var sigBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        var signature = Convert.ToHexString(sigBytes).ToLowerInvariant();

        return new SentinelReviewResult
        {
            Decision = decision,
            RiskLevel = risk,
            Reasons = reasons,
            Evidence = evidence,
            PolicyReferences = policies,
            RequiredUserAction = userAction,
            EvaluatorSignature = signature,
            EvaluatedAt = DateTimeOffset.UtcNow
        };
    }
}
