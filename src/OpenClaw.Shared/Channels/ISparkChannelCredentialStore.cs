namespace OpenClaw.Shared.Channels;

/// <summary>
/// Secure credential storage contract for channel tokens, API keys, and secrets.
/// </summary>
public interface ISparkChannelCredentialStore
{
    Task SetCredentialAsync(string channelId, string secretKey, string secretValue, CancellationToken cancellationToken = default);
    Task<string?> GetCredentialAsync(string channelId, string secretKey, CancellationToken cancellationToken = default);
    Task<bool> HasCredentialAsync(string channelId, string secretKey, CancellationToken cancellationToken = default);
    Task<bool> DeleteCredentialAsync(string channelId, string secretKey, CancellationToken cancellationToken = default);
}
