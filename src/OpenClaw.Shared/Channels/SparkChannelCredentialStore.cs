using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenClaw.Shared.Channels;

/// <summary>
/// Production-grade authenticated AES-256-GCM encrypted credential store.
/// Protects messaging channel tokens, bot keys, and webhooks from plaintext exposure on disk.
/// </summary>
public sealed class SparkChannelCredentialStore : ISparkChannelCredentialStore
{
    private readonly string _storageDirectory;
    private readonly string _keyFilePath;
    private readonly string _vaultFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly byte[] _masterKey;

    public SparkChannelCredentialStore(string? baseDirectory = null)
    {
        var root = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "security",
            "channels");

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
                var existingKey = File.ReadAllBytes(_keyFilePath);
                if (existingKey.Length == 32)
                {
                    return existingKey;
                }
            }
            catch
            {
                // Fall through to re-generate if corrupted
            }
        }

        var newKey = new byte[32];
        RandomNumberGenerator.Fill(newKey);
        File.WriteAllBytes(_keyFilePath, newKey);

        try
        {
            File.SetAttributes(_keyFilePath, FileAttributes.Hidden | FileAttributes.System);
        }
        catch
        {
            // Best effort on file attributes
        }

        return newKey;
    }

    public async Task SetCredentialAsync(string channelId, string secretKey, string secretValue, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        ArgumentNullException.ThrowIfNull(secretValue);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var vault = await ReadVaultUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var compositeKey = BuildCompositeKey(channelId, secretKey);
            vault[compositeKey] = secretValue;
            await WriteVaultUnsafeAsync(vault, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetCredentialAsync(string channelId, string secretKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var vault = await ReadVaultUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var compositeKey = BuildCompositeKey(channelId, secretKey);
            return vault.TryGetValue(compositeKey, out var secret) ? secret : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> HasCredentialAsync(string channelId, string secretKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var vault = await ReadVaultUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var compositeKey = BuildCompositeKey(channelId, secretKey);
            return vault.ContainsKey(compositeKey);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteCredentialAsync(string channelId, string secretKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var vault = await ReadVaultUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var compositeKey = BuildCompositeKey(channelId, secretKey);
            if (vault.Remove(compositeKey))
            {
                await WriteVaultUnsafeAsync(vault, cancellationToken).ConfigureAwait(false);
                return true;
            }
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string BuildCompositeKey(string channelId, string secretKey) =>
        $"{channelId.Trim().ToLowerInvariant()}:{secretKey.Trim()}";

    private async Task<Dictionary<string, string>> ReadVaultUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_vaultFilePath))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var encryptedBytes = await File.ReadAllBytesAsync(_vaultFilePath, cancellationToken).ConfigureAwait(false);
        if (encryptedBytes.Length < 12 + 16) // Nonce (12) + Tag (16)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

    private async Task WriteVaultUnsafeAsync(Dictionary<string, string> vault, CancellationToken cancellationToken)
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
        await File.WriteAllBytesAsync(tmpPath, encryptedPayload, cancellationToken).ConfigureAwait(false);
        File.Move(tmpPath, _vaultFilePath, overwrite: true);
    }
}
