using System;

namespace OpenClaw.Shared.Audio;

/// <summary>
/// Fast audio level and energy metering for waveforms, visual meters, and speech activity.
/// </summary>
public static class AudioLevelMeter
{
    /// <summary>
    /// Computes Root Mean Square (RMS) energy across a buffer of float samples.
    /// Returns a value between 0.0f and 1.0f.
    /// </summary>
    public static float CalculateRms(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
            return 0.0f;

        double sumSquares = 0.0;
        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i];
            sumSquares += s * s;
        }

        double mean = sumSquares / samples.Length;
        return (float)Math.Sqrt(mean);
    }

    /// <summary>
    /// Computes peak absolute amplitude across a buffer of float samples.
    /// </summary>
    public static float CalculatePeak(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
            return 0.0f;

        float peak = 0.0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float abs = Math.Abs(samples[i]);
            if (abs > peak)
                peak = abs;
        }

        return Math.Min(1.0f, peak);
    }

    /// <summary>
    /// Converts a linear amplitude value [0.0, 1.0] to decibels (dBFS).
    /// Returns values between minDb (e.g. -60 dB) and 0.0 dB.
    /// </summary>
    public static float ToDecibels(float linear, float minDb = -60.0f)
    {
        if (linear <= 0.00001f)
            return minDb;

        float db = 20.0f * (float)Math.Log10(linear);
        return Math.Max(minDb, Math.Min(0.0f, db));
    }

    /// <summary>
    /// Normalizes raw RMS energy to a UI-friendly 0.0f..1.0f range with configurable gain sensitivity.
    /// </summary>
    public static float NormalizeLevel(float rms, float gain = 3.5f)
    {
        if (rms <= 0.001f)
            return 0.0f;

        // Apply logarithmic-like compression for responsive visual feedback
        float scaled = (float)Math.Sqrt(rms) * gain;
        return Math.Clamp(scaled, 0.0f, 1.0f);
    }
}
