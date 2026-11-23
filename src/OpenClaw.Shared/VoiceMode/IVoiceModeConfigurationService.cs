using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.VoiceMode;

/// <summary>
/// Service coordinating the 5 independent Voice Mode model capabilities,
/// their encrypted credentials, connection tests, and runtime synchronization.
/// </summary>
public interface IVoiceModeConfigurationService
{
    IVoiceModeCredentialStore Credentials { get; }

    Task<VoiceModeModelsConfig> LoadConfigurationAsync(CancellationToken ct = default);

    Task SaveConfigurationAsync(
        VoiceModeModelsConfig config,
        Dictionary<VoiceCapabilityKind, string?>? updatedApiKeys = null,
        CancellationToken ct = default);

    Task<VoiceCapabilityTestResult> TestConnectionAsync(
        VoiceCapabilityKind capability,
        VoiceModelCapabilityConfig config,
        string? candidateApiKey = null,
        CancellationToken ct = default);

    Task<bool> SyncToRuntimeAsync(CancellationToken ct = default);
}
