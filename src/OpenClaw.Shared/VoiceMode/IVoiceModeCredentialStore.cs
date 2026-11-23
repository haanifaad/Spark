using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.VoiceMode;

/// <summary>
/// Secure credential storage interface isolating API keys for each Voice Mode capability.
/// </summary>
public interface IVoiceModeCredentialStore
{
    Task SetApiKeyAsync(VoiceCapabilityKind capability, string apiKey, CancellationToken ct = default);
    Task<string?> GetApiKeyAsync(VoiceCapabilityKind capability, CancellationToken ct = default);
    Task ClearApiKeyAsync(VoiceCapabilityKind capability, CancellationToken ct = default);
    Task<bool> HasApiKeyAsync(VoiceCapabilityKind capability, CancellationToken ct = default);
    string MaskKey(string? rawKey);
}
