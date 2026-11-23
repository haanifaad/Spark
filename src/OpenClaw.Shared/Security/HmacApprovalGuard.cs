using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace OpenClaw.Shared.Security;

/// <summary>
/// Cryptographic HMAC approval ticket guard for high-risk tool execution.
/// Enforces single-use cryptographic nonces, short TTL windows (60s), constant-time
/// equality verification, and in-memory replay prevention.
/// </summary>
public sealed class HmacApprovalGuard : IDisposable
{
    private const int TicketLifetimeSeconds = 60;
    private readonly byte[] _hmacKey;
    private readonly ConcurrentDictionary<string, long> _consumedNonces = new(StringComparer.Ordinal);
    private long _lastPrunedMs;

    public HmacApprovalGuard(byte[]? key = null)
    {
        if (key != null && key.Length >= 32)
        {
            _hmacKey = (byte[])key.Clone();
        }
        else
        {
            _hmacKey = new byte[32];
            RandomNumberGenerator.Fill(_hmacKey);
        }

        _lastPrunedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Generates a single-use HMAC approval ticket bound to the tool name and arguments.
    /// </summary>
    public string CreateTicket(string toolName, string canonicalArgsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        canonicalArgsJson ??= "{}";

        var nonce = Guid.NewGuid().ToString("N");
        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var payload = BuildPayload(nonce, timestampMs, toolName, canonicalArgsJson);

        using var hmac = new HMACSHA256(_hmacKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var hashHex = Convert.ToHexString(hash).ToLowerInvariant();

        return $"{nonce}:{timestampMs}:{hashHex}";
    }

    /// <summary>
    /// Validates an HMAC ticket against the tool name and arguments.
    /// Rejects replay attacks, expired tickets, and tampered arguments.
    /// </summary>
    public bool ValidateTicket(string ticket, string toolName, string canonicalArgsJson)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return false;
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        canonicalArgsJson ??= "{}";

        var parts = ticket.Split(':');
        if (parts.Length != 3) return false;

        var nonce = parts[0];
        if (!long.TryParse(parts[1], out var timestampMs)) return false;
        var submittedHashHex = parts[2];

        // 1. Check ticket age (TTL expiration)
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var ageSeconds = (nowMs - timestampMs) / 1000.0;
        if (ageSeconds < -5.0 || ageSeconds > TicketLifetimeSeconds)
        {
            return false; // Expired or future-dated beyond clock skew allowance
        }

        // 2. Replay prevention: check and claim nonce atomically
        if (!_consumedNonces.TryAdd(nonce, timestampMs))
        {
            return false; // Nonce has already been consumed!
        }

        // 3. Compute expected HMAC
        var payload = BuildPayload(nonce, timestampMs, toolName, canonicalArgsJson);
        using var hmac = new HMACSHA256(_hmacKey);
        var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expectedHashHex = Convert.ToHexString(expectedHash).ToLowerInvariant();

        // 4. Constant-time verification to prevent timing attacks
        var submittedBytes = Encoding.UTF8.GetBytes(submittedHashHex);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHashHex);

        var isValid = CryptographicOperations.FixedTimeEquals(submittedBytes, expectedBytes);
        if (!isValid)
        {
            _consumedNonces.TryRemove(nonce, out _); // Release invalid nonce
            return false;
        }

        // Prune stale nonces periodically
        PruneStaleNoncesIfDue(nowMs);
        return true;
    }

    private static string BuildPayload(string nonce, long timestampMs, string toolName, string canonicalArgsJson)
    {
        return $"{nonce}|{timestampMs}|{toolName.Trim()}|{canonicalArgsJson.Trim()}";
    }

    private void PruneStaleNoncesIfDue(long nowMs)
    {
        if (nowMs - _lastPrunedMs < 60_000) return;
        _lastPrunedMs = nowMs;

        var cutoffMs = nowMs - (TicketLifetimeSeconds * 2000);
        foreach (var (k, v) in _consumedNonces)
        {
            if (v < cutoffMs)
            {
                _consumedNonces.TryRemove(k, out _);
            }
        }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_hmacKey);
    }
}
