using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenClaw.Shared.VoiceMode;

/// <summary>
/// The five specialized capability domains of SPARK Voice Mode.
/// </summary>
public enum VoiceCapabilityKind
{
    Hearing,
    Thinking,
    Speaking,
    Background,
    Coding
}

/// <summary>
/// Independent configuration for one Voice Mode model capability.
/// </summary>
public sealed class VoiceModelCapabilityConfig
{
    public VoiceCapabilityKind Capability { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string? Language { get; set; }
    public string? Voice { get; set; }
    public double Speed { get; set; } = 1.0;
    public double Temperature { get; set; } = 0.7;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxOutputTokens { get; set; } = 4096;
    public int? ContextTokens { get; set; }

    [JsonIgnore]
    public string? MaskedApiKey { get; set; }

    [JsonIgnore]
    public bool HasStoredApiKey { get; set; }

    public VoiceModelCapabilityConfig() { }

    public VoiceModelCapabilityConfig(VoiceCapabilityKind capability, string provider, string model)
    {
        Capability = capability;
        Provider = provider;
        Model = model;
    }

    public VoiceModelCapabilityConfig Clone()
    {
        return new VoiceModelCapabilityConfig
        {
            Capability = Capability,
            IsEnabled = IsEnabled,
            Provider = Provider,
            Model = Model,
            Endpoint = Endpoint,
            Language = Language,
            Voice = Voice,
            Speed = Speed,
            Temperature = Temperature,
            TimeoutSeconds = TimeoutSeconds,
            MaxOutputTokens = MaxOutputTokens,
            ContextTokens = ContextTokens,
            MaskedApiKey = MaskedApiKey,
            HasStoredApiKey = HasStoredApiKey
        };
    }
}

/// <summary>
/// Complete model configuration for SPARK Voice Mode across all 5 capabilities.
/// </summary>
public sealed class VoiceModeModelsConfig
{
    public VoiceModelCapabilityConfig Hearing { get; set; } = new(VoiceCapabilityKind.Hearing, "Google", "gemini-2.5-flash-native-audio-preview-12-2025") { Language = "auto" };
    public VoiceModelCapabilityConfig Thinking { get; set; } = new(VoiceCapabilityKind.Thinking, "Google", "gemini-2.5-flash-native-audio-preview-12-2025") { Temperature = 0.7 };
    public VoiceModelCapabilityConfig Speaking { get; set; } = new(VoiceCapabilityKind.Speaking, "EdgeTTS", "edge-tts-neural") { Voice = "en-US-GuyNeural", Speed = 1.0, Language = "en" };
    public VoiceModelCapabilityConfig Background { get; set; } = new(VoiceCapabilityKind.Background, "Google", "gemini-2.5-flash") { TimeoutSeconds = 60, MaxOutputTokens = 4096 };
    public VoiceModelCapabilityConfig Coding { get; set; } = new(VoiceCapabilityKind.Coding, "Anthropic", "claude-3-5-sonnet-20241022") { Temperature = 0.2 };

    public VoiceModelCapabilityConfig GetCapability(VoiceCapabilityKind kind) => kind switch
    {
        VoiceCapabilityKind.Hearing => Hearing,
        VoiceCapabilityKind.Thinking => Thinking,
        VoiceCapabilityKind.Speaking => Speaking,
        VoiceCapabilityKind.Background => Background,
        VoiceCapabilityKind.Coding => Coding,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public void SetCapability(VoiceCapabilityKind kind, VoiceModelCapabilityConfig config)
    {
        switch (kind)
        {
            case VoiceCapabilityKind.Hearing: Hearing = config; break;
            case VoiceCapabilityKind.Thinking: Thinking = config; break;
            case VoiceCapabilityKind.Speaking: Speaking = config; break;
            case VoiceCapabilityKind.Background: Background = config; break;
            case VoiceCapabilityKind.Coding: Coding = config; break;
        }
    }
}

/// <summary>
/// Result of an individual provider connection test.
/// </summary>
public sealed record VoiceCapabilityTestResult(
    bool Success,
    string Message,
    long LatencyMs,
    DateTime TestedAtUtc,
    string? ErrorDetail = null);
