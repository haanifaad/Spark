using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ExecApprovals;

namespace OpenClaw.Shared.Security.Audit;

/// <summary>
/// Tamper-evident, append-only JSONL audit logger with automated credential redaction.
/// Protects audit trail integrity via cryptographic hashes.
/// </summary>
public class SparkAuditLogger : ISparkAuditLogger, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _logFilePath;

    public SparkAuditLogger(string? logDirectory = null)
    {
        var dir = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "Audit");

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _logFilePath = Path.Combine(dir, "spark_audit.jsonl");
    }

    public async Task LogEventAsync(SparkAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        // 1. Redact any potential credentials, API keys, passwords, or tokens
        record.Operation = RedactText(record.Operation);
        record.ResultSummary = RedactText(record.ResultSummary);
        record.ErrorMessage = RedactText(record.ErrorMessage);

        // 2. Compute cryptographic integrity checksum
        record.IntegrityChecksum = ComputeChecksum(record);

        // 3. Serialize to canonical single-line JSONL
        var jsonLine = JsonSerializer.Serialize(record) + Environment.NewLine;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(_logFilePath, jsonLine, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SparkAuditRecord>> QueryAuditTrailAsync(
        SparkAuditQueryFilter? filter = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_logFilePath)) return Array.Empty<SparkAuditRecord>();

        var results = new List<SparkAuditRecord>();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var lines = await File.ReadAllLinesAsync(_logFilePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

            // Read in reverse (newest first)
            for (int i = lines.Length - 1; i >= 0 && results.Count < limit; i--)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var record = JsonSerializer.Deserialize<SparkAuditRecord>(line);
                    if (record == null) continue;

                    if (filter != null)
                    {
                        if (!string.IsNullOrWhiteSpace(filter.UserId) && !string.Equals(record.UserId, filter.UserId, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrWhiteSpace(filter.SessionId) && !string.Equals(record.SessionId, filter.SessionId, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrWhiteSpace(filter.ToolId) && !string.Equals(record.ToolId, filter.ToolId, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (filter.MinimumRiskLevel.HasValue && record.RiskLevel < filter.MinimumRiskLevel.Value)
                            continue;
                        if (filter.SuccessOnly.HasValue && record.Success != filter.SuccessOnly.Value)
                            continue;
                        if (filter.FromTimestamp.HasValue && record.Timestamp < filter.FromTimestamp.Value)
                            continue;
                        if (filter.ToTimestamp.HasValue && record.Timestamp > filter.ToTimestamp.Value)
                            continue;
                    }

                    results.Add(record);
                }
                catch
                {
                    // Skip corrupted lines
                }
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> VerifyLogIntegrityAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_logFilePath)) return true;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var lines = await File.ReadAllLinesAsync(_logFilePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var record = JsonSerializer.Deserialize<SparkAuditRecord>(line);
                    if (record == null) return false;

                    var storedChecksum = record.IntegrityChecksum;
                    var computed = ComputeChecksum(record);
                    if (!string.Equals(storedChecksum, computed, StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // Tampering detected!
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string RedactText(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return ExecApprovalSecretRedactor.Redact(input);
    }

    private static string ComputeChecksum(SparkAuditRecord r)
    {
        var canonical = $"{r.AuditId}|{r.Timestamp.ToUnixTimeMilliseconds()}|{r.ToolId}|{r.Operation}|{r.RiskLevel}|{r.Success}|{r.ResultSummary}|{r.ErrorMessage}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
