using System;
using System.IO;
using OpenClaw.Shared.Audio;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SharedAudioTests
{
    [Fact]
    public void AudioRingBuffer_BasicWriteAndRead_OperatesCorrectly()
    {
        var buffer = new AudioRingBuffer(1024);
        Assert.Equal(1024, buffer.Capacity);
        Assert.Equal(0, buffer.AvailableSamples);

        float[] input = [0.1f, 0.2f, 0.3f, 0.4f, 0.5f];
        buffer.Write(input);

        Assert.Equal(5, buffer.AvailableSamples);
        Assert.Equal(5, buffer.TotalWritten);

        float[] output = new float[5];
        int read = buffer.Read(output);

        Assert.Equal(5, read);
        Assert.Equal(0, buffer.AvailableSamples);
        Assert.Equal(input, output);
    }

    [Fact]
    public void AudioRingBuffer_Overflow_PreservesLatestData()
    {
        var buffer = new AudioRingBuffer(4);

        buffer.Write([1.0f, 2.0f, 3.0f]);
        Assert.Equal(3, buffer.AvailableSamples);

        buffer.Write([4.0f, 5.0f, 6.0f]);
        Assert.Equal(4, buffer.AvailableSamples);
        Assert.Equal(6, buffer.TotalWritten);

        float[] output = new float[4];
        int read = buffer.Read(output);

        Assert.Equal(4, read);
        Assert.Equal([3.0f, 4.0f, 5.0f, 6.0f], output);
    }

    [Fact]
    public void AudioRingBuffer_PeekLatest_DoesNotConsumeSamples()
    {
        var buffer = new AudioRingBuffer(8);
        buffer.Write([1.0f, 2.0f, 3.0f, 4.0f, 5.0f]);

        float[] peek = new float[3];
        int peeked = buffer.PeekLatest(peek);

        Assert.Equal(3, peeked);
        Assert.Equal([3.0f, 4.0f, 5.0f], peek);
        Assert.Equal(5, buffer.AvailableSamples);
    }

    [Fact]
    public void AudioRingBuffer_Clear_ResetsBuffer()
    {
        var buffer = new AudioRingBuffer(16);
        buffer.Write([0.5f, 0.6f, 0.7f]);
        Assert.Equal(3, buffer.AvailableSamples);

        buffer.Clear();
        Assert.Equal(0, buffer.AvailableSamples);
    }

    [Fact]
    public void AudioFormatConverter_Pcm16AndFloat_RoundTripWithinTolerance()
    {
        float[] original = [-1.0f, -0.5f, 0.0f, 0.5f, 1.0f];
        byte[] pcm = new byte[original.Length * 2];

        int bytesWritten = AudioFormatConverter.FloatToPcm16(original, pcm);
        Assert.Equal(pcm.Length, bytesWritten);

        float[] decoded = new float[original.Length];
        int samplesDecoded = AudioFormatConverter.Pcm16ToFloat(pcm, decoded);

        Assert.Equal(original.Length, samplesDecoded);
        for (int i = 0; i < original.Length; i++)
        {
            Assert.True(Math.Abs(original[i] - decoded[i]) < 0.001f,
                $"Sample {i} mismatch: expected {original[i]}, got {decoded[i]}");
        }
    }

    [Fact]
    public void AudioFormatConverter_StereoToMono_DownmixesAccurately()
    {
        float[] stereo = [1.0f, 0.5f, -0.8f, -0.2f];
        float[] mono = new float[2];

        int frames = AudioFormatConverter.StereoToMono(stereo, mono);
        Assert.Equal(2, frames);
        Assert.Equal(0.75f, mono[0], 0.001f);
        Assert.Equal(-0.5f, mono[1], 0.001f);
    }

    [Fact]
    public void AudioFormatConverter_Resample_ProducesExpectedOutputLength()
    {
        float[] input48k = new float[480];
        for (int i = 0; i < input48k.Length; i++)
        {
            input48k[i] = (float)Math.Sin(2 * Math.PI * 440 * i / 48000.0);
        }

        var output16k = AudioFormatConverter.ResampleToArray(input48k, 48000, 16000);
        Assert.Equal(160, output16k.Length);

        var output24k = AudioFormatConverter.ResampleToArray(output16k, 16000, 24000);
        Assert.Equal(240, output24k.Length);
    }

    [Fact]
    public void AudioFormatConverter_PcmToWavStream_GeneratesValidRiffHeader()
    {
        float[] samples = [0.1f, 0.2f, -0.1f, -0.2f];
        using var wavStream = AudioFormatConverter.PcmToWavStream(samples, 16000);

        byte[] header = new byte[44];
        int read = wavStream.Read(header, 0, 44);
        Assert.Equal(44, read);

        Assert.Equal((byte)'R', header[0]);
        Assert.Equal((byte)'I', header[1]);
        Assert.Equal((byte)'F', header[2]);
        Assert.Equal((byte)'F', header[3]);

        Assert.Equal((byte)'W', header[8]);
        Assert.Equal((byte)'A', header[9]);
        Assert.Equal((byte)'V', header[10]);
        Assert.Equal((byte)'E', header[11]);
    }

    [Fact]
    public void AudioLevelMeter_CalculatesRmsAndPeak()
    {
        float[] silence = [0.0f, 0.0f, 0.0f, 0.0f];
        Assert.Equal(0.0f, AudioLevelMeter.CalculateRms(silence));
        Assert.Equal(0.0f, AudioLevelMeter.CalculatePeak(silence));

        float[] signal = [0.5f, -0.5f, 0.5f, -0.5f];
        Assert.Equal(0.5f, AudioLevelMeter.CalculateRms(signal), 0.001f);
        Assert.Equal(0.5f, AudioLevelMeter.CalculatePeak(signal), 0.001f);

        Assert.Equal(0.0f, AudioLevelMeter.ToDecibels(1.0f), 0.01f);
        Assert.True(AudioLevelMeter.ToDecibels(0.1f) < -19.0f);
    }
}
