using System;
using System.IO;
using System.Text;

namespace OpenClaw.Shared.Audio;

/// <summary>
/// Fast, allocation-conscious audio format conversions for speech and streaming audio.
/// Converts between PCM 16-bit, IEEE 32-bit float, multi-channel to mono, and sample rates.
/// </summary>
public static class AudioFormatConverter
{
    /// <summary>
    /// Converts raw 16-bit little-endian PCM byte span to normalized [-1.0f, 1.0f] float span.
    /// </summary>
    public static int Pcm16ToFloat(ReadOnlySpan<byte> pcm16Bytes, Span<float> destination)
    {
        int sampleCount = Math.Min(pcm16Bytes.Length / 2, destination.Length);
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)(pcm16Bytes[i * 2] | (pcm16Bytes[i * 2 + 1] << 8));
            destination[i] = sample / 32768.0f;
        }
        return sampleCount;
    }

    /// <summary>
    /// Converts normalized [-1.0f, 1.0f] float span to 16-bit little-endian PCM byte span.
    /// Clamps out-of-range values to prevent integer overflow.
    /// </summary>
    public static int FloatToPcm16(ReadOnlySpan<float> samples, Span<byte> destination)
    {
        int sampleCount = Math.Min(samples.Length, destination.Length / 2);
        for (int i = 0; i < sampleCount; i++)
        {
            float clamped = Math.Clamp(samples[i], -1.0f, 1.0f);
            short intSample = (short)(clamped * 32767.0f);
            destination[i * 2] = (byte)(intSample & 0xFF);
            destination[i * 2 + 1] = (byte)((intSample >> 8) & 0xFF);
        }
        return sampleCount * 2;
    }

    /// <summary>
    /// Downmixes interleaved stereo samples (L, R, L, R) into mono samples: (L + R) / 2.
    /// </summary>
    public static int StereoToMono(ReadOnlySpan<float> stereoSamples, Span<float> destinationMono)
    {
        int frames = Math.Min(stereoSamples.Length / 2, destinationMono.Length);
        for (int i = 0; i < frames; i++)
        {
            float left = stereoSamples[i * 2];
            float right = stereoSamples[i * 2 + 1];
            destinationMono[i] = (left + right) * 0.5f;
        }
        return frames;
    }

    /// <summary>
    /// Resamples an input audio buffer from inputRate to outputRate using linear interpolation.
    /// Returns the number of output samples generated.
    /// </summary>
    public static int Resample(ReadOnlySpan<float> input, int inputRate, Span<float> output, int outputRate)
    {
        if (input.IsEmpty || output.IsEmpty)
            return 0;

        if (inputRate == outputRate)
        {
            int toCopy = Math.Min(input.Length, output.Length);
            input.Slice(0, toCopy).CopyTo(output);
            return toCopy;
        }

        if (inputRate <= 0 || outputRate <= 0)
            throw new ArgumentOutOfRangeException("Sample rates must be positive.");

        double ratio = (double)inputRate / outputRate;
        int maxOutputSamples = (int)Math.Floor(input.Length / ratio);
        int outputCount = Math.Min(maxOutputSamples, output.Length);

        for (int i = 0; i < outputCount; i++)
        {
            double srcPos = i * ratio;
            int srcIndex = (int)srcPos;
            double frac = srcPos - srcIndex;

            float s0 = input[srcIndex];
            float s1 = (srcIndex + 1 < input.Length) ? input[srcIndex + 1] : s0;

            output[i] = (float)(s0 + frac * (s1 - s0));
        }

        return outputCount;
    }

    /// <summary>
    /// Resamples an input buffer and allocates the exact resulting float array.
    /// </summary>
    public static float[] ResampleToArray(ReadOnlySpan<float> input, int inputRate, int outputRate)
    {
        if (input.IsEmpty)
            return Array.Empty<float>();

        if (inputRate == outputRate)
            return input.ToArray();

        double ratio = (double)inputRate / outputRate;
        int expectedLength = (int)Math.Floor(input.Length / ratio);
        if (expectedLength <= 0)
            return Array.Empty<float>();

        var result = new float[expectedLength];
        int actual = Resample(input, inputRate, result.AsSpan(), outputRate);
        if (actual < result.Length)
            Array.Resize(ref result, actual);

        return result;
    }

    /// <summary>
    /// Creates a complete WAV file stream with RIFF header from float samples at specified sample rate.
    /// </summary>
    public static MemoryStream PcmToWavStream(ReadOnlySpan<float> samples, int sampleRate)
    {
        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            const short channels = 1;
            const short bitsPerSample = 16;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            short blockAlign = (short)(channels * bitsPerSample / 8);
            int dataSize = samples.Length * blockAlign;

            // RIFF header
            writer.Write("RIFF"u8);
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8);

            // fmt chunk
            writer.Write("fmt "u8);
            writer.Write(16); // Chunk size
            writer.Write((short)1); // PCM
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            // data chunk
            writer.Write("data"u8);
            writer.Write(dataSize);

            // Audio data
            Span<byte> byteBuffer = stackalloc byte[1024];
            int offset = 0;
            while (offset < samples.Length)
            {
                int chunkSize = Math.Min(512, samples.Length - offset);
                int bytesWritten = FloatToPcm16(samples.Slice(offset, chunkSize), byteBuffer);
                writer.Write(byteBuffer.Slice(0, bytesWritten));
                offset += chunkSize;
            }

            writer.Flush();
        }

        ms.Position = 0;
        return ms;
    }
}
