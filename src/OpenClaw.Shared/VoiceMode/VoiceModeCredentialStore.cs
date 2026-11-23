using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.VoiceMode;

/// <summary>
/// Production-grade AES-256-GCM encrypted credential vault for Voice Mode capabilities.
/// Enforces absolute isolation between Hearing, Thinking, Speaking, Background, and Coding keys.
/// </summary>
public sealed class VoiceModeCredentialStore : IVoiceModeCredentialStore
{
    private readonly string _storageDirectory;
    private readonly string _keyFilePath;
    private readonly string _vaultFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly byte[] _masterKey;

    public VoiceModeCredentialStore(string? baseDirectory = null)
    {
        var root = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "security",
            "voicemode");

        _storageDirectory = root;
        Directory.CreateDirectory(_storageDirectory);
        _keyFilePath = Path.Combine(_storageDirectory, ".master.key");
        _vaultFilePath = Path.Combine(_storageDirectory, "credentials.vault");

        _masterKey = InitializeMasterKey();
    }

    private byte[] InitializeMasterKey()
    {
        if (File.Exists(_keyFilePath))
        {
            try
            {
                var existing = File.ReadAllBytes(_keyFilePath);
                if (existing.Length == 32)
                {
                    return existing;
                }
            }
            catch { }
        }

        var newKey = new byte[32];
        RandomNumberGenerator.Fill(newKey);
        File.WriteAllBytes(_keyFilePath, newKey);
        try
        {
            File.SetAttributes(_keyFilePath, FileAttributes.Hidden | FileAttributes.System);
        }
        catch { }
        return newKey;
    }

    private static string GetCapabilityKey(VoiceCapabilityKind capability) => capability switch
    {
        VoiceCapabilityKind.Hearing => "hearing_api_key",
        VoiceCapabilityKind.Thinking => "thinking_api_key",
        VoiceCapabilityKind.Speaking => "speaking_api_key",
        VoiceCapabilityKind.Background => "background_api_key",
        VoiceCapabilityKind.Coding => "coding_api_key",
        _ => capability.ToString().ToLowerInvariant() + "_api_key"
    };

    public async Task SetApiKeyAsync(VoiceCapabilityKind capability, string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await ClearApiKeyAsync(capability, ct).ConfigureAwait(false);
            return;
        }

        var keyName = GetCapabilityKey(capability);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var vault = await ReadVaultUnsafeAsync(ct).ConfigureAwait(false);
            vault[keyName] = apiKey.Trim();
            await WriteVaultUnsafeAsync(vault, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetApiKeyAsync(VoiceCapabilityKind capability, CancellationToken ct = default)
    {
        var keyName = GetCapabilityKey(capability);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var vault = await ReadVaultUnsafeAsync(ct).ConfigureAwait(false);
            return vault.TryGetValue(keyName, out var key) && !string.IsNullOrWhiteSpace(key) ? key : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearApiKeyAsync(VoiceCapabilityKind capability, CancellationToken ct = default)
    {
        var keyName = GetCapabilityKey(capability);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var vault = await ReadVaultUnsafeAsync(ct).ConfigureAwait(false);
            if (vault.Remove(keyName))
            {
                await WriteVaultUnsafeAsync(vault, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> HasApiKeyAsync(VoiceCapabilityKind capability, CancellationToken ct = default)
    {
        var key = await GetApiKeyAsync(capability, ct).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(key);
    }

    public string MaskKey(string? rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return string.Empty;
        var trimmed = rawKey.Trim();
        if (trimmed.Length <= 8) return "••••••••";
        var prefix = trimmed[..4];
        var suffix = trimmed[^4..];
        return $"{prefix}...••••{suffix}";
    }

    private async Task<Dictionary<string, string>> ReadVaultUnsafeAsync(CancellationToken ct)
    {
        if (!File.Exists(_vaultFilePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var encryptedBytes = await File.ReadAllBytesAsync(_vaultFilePath, ct).ConfigureAwait(false);
            if (encryptedBytes.Length < 12 + 16)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var nonce = encryptedBytes.AsSpan(0, 12);
            var tag = encryptedBytes.AsSpan(12, 16);
            var cipherText = encryptedBytes.AsSpan(28);
            var plainBytes = new byte[cipherText.Length];

            using var aesGcm = new AesGcm(_masterKey, 16);
            aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);

            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task WriteVaultUnsafeAsync(Dictionary<string, string> vault, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(vault);
        var plainBytes = Encoding.UTF8.GetBytes(json);

        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[16];
        var cipherText = new byte[plainBytes.Length];

        using var aesGcm = new AesGcm(_masterKey, 16);
        aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);

        var encryptedPayload = new byte[12 + 16 + cipherText.Length];
        Buffer.BlockCopy(nonce, 0, encryptedPayload, 0, 12);
        Buffer.BlockCopy(tag, 0, encryptedPayload, 12, 16);
        Buffer.BlockCopy(cipherText, 0, encryptedPayload, 28, cipherText.Length);

        var tmpPath = _vaultFilePath + ".tmp";
        await File.WriteAllBytesAsync(tmpPath, encryptedPayload, ct).ConfigureAwait(false);
        File.Move(tmpPath, _vaultFilePath, overwrite: true);
    }
}
