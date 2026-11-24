using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Audio.WakeWord;

/// <summary>
/// Production local wake-word detector.
/// Operates entirely in memory on 16 kHz mono PCM float audio streams.
/// Never uploads standby microphone audio to cloud services.
/// </summary>
public sealed class SparkWakeWordDetector : IWakeWordDetector
{
    private readonly object _gate = new();
    private readonly AudioRingBuffer _ringBuffer;
    private WakeWordConfiguration _config;
    private DateTime _lastTriggerTimeUtc = DateTime.MinValue;
    private DateTime _lastActivityTimeUtc = DateTime.UtcNow;
    private DateTime _repeatWindowExpirationUtc = DateTime.MinValue;
    private string? _firstPhraseDetected;
    private bool _asleep = true; // Sleeps until called
    private bool _disposed;

    public WakeWordConfiguration Configuration
    {
        get
        {
            lock (_gate) return _config;
        }
    }

    /// <summary>True when SPARK is in sleep mode awaiting a wake word.</summary>
    public bool IsAsleep
    {
        get
        {
            lock (_gate) return _asleep;
        }
    }

    public event Action<WakeWordDetectionResult>? WakeWordDetected;
    public event Action? SleepStateChanged;

    public SparkWakeWordDetector(WakeWordConfiguration? configuration = null)
    {
        _config = configuration ?? new WakeWordConfiguration();
        // 2 seconds buffer @ 16 kHz = 32,000 samples
        _ringBuffer = new AudioRingBuffer(32000);
        _lastActivityTimeUtc = DateTime.UtcNow;
    }

    /// <summary>Puts SPARK to sleep locally. Audio is never streamed over the network while asleep.</summary>
    public void Sleep(string reason = "manual")
    {
        lock (_gate)
        {
            _asleep = true;
            ResetInternal();
        }
        SleepStateChanged?.Invoke();
    }

    /// <summary>Wakes SPARK up and starts the 2-minute auto-sleep countdown.</summary>
    public void Wake(string detectedPhrase = "Hey Spark")
    {
        lock (_gate)
        {
            _asleep = false;
            _lastActivityTimeUtc = DateTime.UtcNow;
            ResetInternal();
        }
        SleepStateChanged?.Invoke();
    }

    /// <summary>Notifies the detector that user speech was detected, resetting the 2-minute auto-sleep timer.</summary>
    public void NotifyUserSpeech()
    {
        lock (_gate)
        {
            _lastActivityTimeUtc = DateTime.UtcNow;
        }
    }

    public void UpdateConfiguration(WakeWordConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        lock (_gate)
        {
            _config = config;
            ResetInternal();
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            ResetInternal();
        }
    }

    private void ResetInternal()
    {
        _ringBuffer.Clear();
        _repeatWindowExpirationUtc = DateTime.MinValue;
        _firstPhraseDetected = null;
    }

    /// <summary>
    /// Feed incoming 16 kHz audio samples into the detector.
    /// Returns detection result if wake word triggered, or null otherwise.
    /// </summary>
    public WakeWordDetectionResult? ProcessSamples(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
            return null;

        lock (_gate)
        {
            if (_disposed || !_config.IsEnabled)
                return null;

            _ringBuffer.Write(samples);

            var now = DateTime.UtcNow;

            // Check auto-sleep timeout: if awake and silence exceeds AutoSleepSeconds (default 120s / 2 min), go to sleep
            if (!_asleep)
            {
                if ((now - _lastActivityTimeUtc).TotalSeconds >= _config.AutoSleepSeconds)
                {
                    _asleep = true;
                    ResetInternal();
                    SleepStateChanged?.Invoke();
                }
            }

            // Fast-path: calculate RMS of the new samples to filter out background silence
            float rms = AudioLevelMeter.CalculateRms(samples);
            // Dynamic threshold based on sensitivity
            float minEnergy = Math.Max(0.005f, 0.05f * (1.0f - _config.Sensitivity));
            if (rms < minEnergy)
                return null;

            // Check cooldown
            if ((now - _lastTriggerTimeUtc).TotalSeconds < _config.CooldownSeconds)
                return null;

            // Analyze sliding window (last ~1.2s = 19,200 samples)
            Span<float> window = stackalloc float[Math.Min(19200, _ringBuffer.AvailableSamples)];
            int peeked = _ringBuffer.PeekLatest(window);
            if (peeked < 4000) // Need at least ~250ms of audio
                return null;

            // Evaluate acoustic syllabic patterns against configured phrases
            var detected = MatchAcousticPattern(window.Slice(0, peeked), _config);
            if (detected is null)
                return null;

            // Check repeated activation phrase requirements
            if (_config.RequireRepeatedPhrase)
            {
                if (_firstPhraseDetected is null || now > _repeatWindowExpirationUtc)
                {
                    // First phrase registered, start repeat window
                    _firstPhraseDetected = detected.Value.Phrase;
                    _repeatWindowExpirationUtc = now.AddSeconds(_config.RepeatWindowSeconds);
                    return null;
                }
                else
                {
                    // Second phrase detected within repeat window!
                    _firstPhraseDetected = null;
                    _repeatWindowExpirationUtc = DateTime.MinValue;
                    _lastTriggerTimeUtc = now;
                    bool wasAsleepRepeat = _asleep;
                    _asleep = false;
                    _lastActivityTimeUtc = now;
                    if (wasAsleepRepeat) SleepStateChanged?.Invoke();

                    var repeatResult = new WakeWordDetectionResult(
                        detected.Value.Phrase,
                        detected.Value.Confidence,
                        now,
                        IsRepeatTrigger: true);

                    WakeWordDetected?.Invoke(repeatResult);
                    return repeatResult;
                }
            }

            // Normal single activation
            _lastTriggerTimeUtc = now;
            bool wasAsleep = _asleep;
            _asleep = false;
            _lastActivityTimeUtc = now;
            if (wasAsleep) SleepStateChanged?.Invoke();

            var result = new WakeWordDetectionResult(
                detected.Value.Phrase,
                detected.Value.Confidence,
                now,
                IsRepeatTrigger: false);

            WakeWordDetected?.Invoke(result);
            return result;
        }
    }

    /// <summary>
    /// Evaluates syllable envelope count and energy crest factor to identify activation phrases.
    /// </summary>
    private static (string Phrase, float Confidence)? MatchAcousticPattern(
        ReadOnlySpan<float> window,
        WakeWordConfiguration config)
    {
        // 1. Detect syllabic crests in 32ms frames (512 samples)
        int frameSize = 512;
        int frameCount = window.Length / frameSize;
        if (frameCount < 4)
            return null;

        var energyFrames = new float[frameCount];
        var zcrFrames = new float[frameCount];
        float maxEnergy = 0f;
        float totalEnergy = 0f;

        for (int i = 0; i < frameCount; i++)
        {
            var frame = window.Slice(i * frameSize, frameSize);
            float frameRms = AudioLevelMeter.CalculateRms(frame);
            energyFrames[i] = frameRms;
            totalEnergy += frameRms;
            if (frameRms > maxEnergy) maxEnergy = frameRms;

            // Zero Crossing Rate
            int crossings = 0;
            for (int j = 1; j < frame.Length; j++)
            {
                if ((frame[j - 1] >= 0f && frame[j] < 0f) || (frame[j - 1] < 0f && frame[j] >= 0f))
                {
                    crossings++;
                }
            }
            zcrFrames[i] = (float)crossings / frame.Length;
        }

        if (maxEnergy < 0.02f)
            return null;

        float avgEnergy = totalEnergy / frameCount;
        float crestFactor = maxEnergy / Math.Max(0.001f, avgEnergy);

        // Reject continuous uniform noise (fans, hums have crest factor < 1.7)
        if (crestFactor < 1.7f)
            return null;

        // Count syllables and measure syllabic peak durations
        int validSyllables = 0;
        float peakThreshold = maxEnergy * 0.35f;
        int currentPeakWidth = 0;
        bool inPeak = false;

        for (int i = 0; i < frameCount; i++)
        {
            if (energyFrames[i] > peakThreshold)
            {
                currentPeakWidth++;
                inPeak = true;
            }
            else if (inPeak)
            {
                // Speech syllables typically span 2 to 16 frames (~64ms to ~500ms)
                // Impulsive clicks (1 frame) or steady drone (>20 frames) are rejected
                if (currentPeakWidth >= 2 && currentPeakWidth <= 20)
                {
                    validSyllables++;
                }
                currentPeakWidth = 0;
                inPeak = false;
            }
        }
        if (inPeak && currentPeakWidth >= 2 && currentPeakWidth <= 20)
        {
            validSyllables++;
        }

        if (validSyllables == 0)
            return null;

        // Check ZCR variance: human speech has a mix of low-ZCR vowels and high-ZCR consonants
        float zcrMin = float.MaxValue;
        float zcrMax = float.MinValue;
        for (int i = 0; i < frameCount; i++)
        {
            if (energyFrames[i] > peakThreshold)
            {
                if (zcrFrames[i] < zcrMin) zcrMin = zcrFrames[i];
                if (zcrFrames[i] > zcrMax) zcrMax = zcrFrames[i];
            }
        }
        float zcrSpread = zcrMax - zcrMin;

        // Match against normalized phrases
        var phrases = config.GetAllNormalizedPhrases();
        foreach (var phrase in phrases)
        {
            int expectedSyllables = EstimateSyllableCount(phrase);
            if (Math.Abs(validSyllables - expectedSyllables) <= 1)
            {
                // Dynamic acoustic confidence incorporating crest factor and spectral modulation
                float cadenceScore = 1.0f - (Math.Abs(validSyllables - expectedSyllables) * 0.15f);
                float spectralBonus = Math.Clamp(zcrSpread * 0.5f, 0.0f, 0.15f);
                float baseConfidence = 0.65f + (cadenceScore * 0.20f) + spectralBonus + (config.Sensitivity * 0.10f);

                if (baseConfidence >= (1.0f - config.Sensitivity))
                {
                    return (phrase, Math.Min(0.99f, baseConfidence));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Heuristic syllable estimation for trigger phrase length matching.
    /// </summary>
    internal static int EstimateSyllableCount(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return 0;
        var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int total = 0;
        foreach (var word in words)
        {
            string w = word.ToLowerInvariant();
            if (w == "spark") total += 1;
            else if (w == "hey") total += 1;
            else if (w == "good") total += 1;
            else if (w == "morning") total += 2;
            else if (w is "whats" or "what's") total += 1;
            else if (w == "up") total += 1;
            else if (w == "computer") total += 3;
            else if (w == "openclaw") total += 3;
            else
            {
                // Vowel count heuristic
                int count = 0;
                bool prevIsVowel = false;
                foreach (char c in w)
                {
                    bool isVowel = "aeiouy".Contains(c);
                    if (isVowel && !prevIsVowel) count++;
                    prevIsVowel = isVowel;
                }
                total += Math.Max(1, count);
            }
        }
        return total;
    }

    /// <summary>
    /// Test helper to deterministically inject a simulated trigger event.
    /// </summary>
    internal WakeWordDetectionResult? TestInjectPhrase(string phrase, float confidence = 0.95f)
    {
        lock (_gate)
        {
            if (_disposed || !_config.IsEnabled)
                return null;

            var now = DateTime.UtcNow;

            // Check auto-sleep timeout: if awake and silence exceeds AutoSleepSeconds (default 120s / 2 min), go to sleep
            if (!_asleep)
            {
                if ((now - _lastActivityTimeUtc).TotalSeconds >= _config.AutoSleepSeconds)
                {
                    _asleep = true;
                    ResetInternal();
                    SleepStateChanged?.Invoke();
                }
            }

            // Check cooldown
            if ((now - _lastTriggerTimeUtc).TotalSeconds < _config.CooldownSeconds)
                return null;

            if (_config.RequireRepeatedPhrase)
            {
                if (_firstPhraseDetected is null || now > _repeatWindowExpirationUtc)
                {
                    _firstPhraseDetected = phrase;
                    _repeatWindowExpirationUtc = now.AddSeconds(_config.RepeatWindowSeconds);
                    return null;
                }
                else
                {
                    _firstPhraseDetected = null;
                    _repeatWindowExpirationUtc = DateTime.MinValue;
                    _lastTriggerTimeUtc = now;
                    _asleep = false;
                    _lastActivityTimeUtc = now;
                    var res = new WakeWordDetectionResult(phrase, confidence, now, IsRepeatTrigger: true);
                    WakeWordDetected?.Invoke(res);
                    return res;
                }
            }

            _lastTriggerTimeUtc = now;
            _asleep = false;
            _lastActivityTimeUtc = now;
            var result = new WakeWordDetectionResult(phrase, confidence, now, IsRepeatTrigger: false);
            WakeWordDetected?.Invoke(result);
            return result;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ResetInternal();
        }
    }
}
