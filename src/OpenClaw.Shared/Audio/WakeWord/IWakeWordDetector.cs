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

    /// <summary>Whether the assistant is currently in sleep mode awaiting a wake word.</summary>
    bool IsAsleep => true;

    /// <summary>Puts the detector into sleep mode to conserve resources and halt audio dispatch.</summary>
    void Sleep(string reason = "manual") { }

    /// <summary>Wakes the detector and opens the conversation pipeline.</summary>
    void Wake(string detectedPhrase = "Hey Spark") { }

    /// <summary>Notifies the detector that user speech was received, resetting auto-sleep.</summary>
    void NotifyUserSpeech() { }

    /// <summary>Fired when sleep state transitions between sleeping and awake.</summary>
    event Action? SleepStateChanged { add { } remove { } }
}
