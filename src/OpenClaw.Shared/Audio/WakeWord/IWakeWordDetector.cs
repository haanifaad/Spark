using System;

namespace OpenClaw.Shared.Audio.WakeWord;

/// <summary>Result of a successful wake word detection.</summary>
public sealed record WakeWordDetectionResult(
    string DetectedPhrase,
    float Confidence,
    DateTime DetectedAtUtc,
    bool IsRepeatTrigger = false);

/// <summary>
/// Interface for local wake-word detection systems.
/// Guarantees that standby microphone audio is never uploaded to cloud services.
/// </summary>
public interface IWakeWordDetector : IDisposable
{
    WakeWordConfiguration Configuration { get; }
    void UpdateConfiguration(WakeWordConfiguration config);
    WakeWordDetectionResult? ProcessSamples(ReadOnlySpan<float> samples);
    void Reset();
    event Action<WakeWordDetectionResult>? WakeWordDetected;
}
