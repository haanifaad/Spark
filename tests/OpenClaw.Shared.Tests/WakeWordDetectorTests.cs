using System;
using System.Threading;
using OpenClaw.Shared.Audio.WakeWord;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class WakeWordDetectorTests
{
    [Fact]
    public void Configuration_DefaultValues_AreCorrect()
    {
        var config = new WakeWordConfiguration();
        Assert.True(config.IsEnabled);
        Assert.Contains("Hey Spark", config.ActivationPhrases);
        Assert.Contains("Spark", config.ActivationPhrases);
        Assert.Contains("Computer", config.Aliases);
        Assert.Equal(0.65f, config.Sensitivity);
        Assert.False(config.RequireRepeatedPhrase);
    }

    [Fact]
    public void Configuration_NormalizedPhrases_DeduplicatesAndLowercases()
    {
        var config = new WakeWordConfiguration
        {
            ActivationPhrases = ["Hey Spark", "SPARK", "hey spark"],
            Aliases = ["computer", "Computer", "Jarvis"]
        };

        var normalized = config.GetAllNormalizedPhrases();
        Assert.Equal(4, normalized.Count);
        Assert.Contains("hey spark", normalized);
        Assert.Contains("spark", normalized);
        Assert.Contains("computer", normalized);
        Assert.Contains("jarvis", normalized);
    }

    [Fact]
    public void EstimateSyllableCount_CalculatesAccurately()
    {
        Assert.Equal(1, SparkWakeWordDetector.EstimateSyllableCount("Spark"));
        Assert.Equal(2, SparkWakeWordDetector.EstimateSyllableCount("Hey Spark"));
        Assert.Equal(3, SparkWakeWordDetector.EstimateSyllableCount("Computer"));
        Assert.Equal(3, SparkWakeWordDetector.EstimateSyllableCount("OpenClaw"));
    }

    [Fact]
    public void SingleTrigger_FiresWakeWordEvent()
    {
        using var detector = new SparkWakeWordDetector();
        WakeWordDetectionResult? eventResult = null;
        detector.WakeWordDetected += res => eventResult = res;

        var result = detector.TestInjectPhrase("hey spark", 0.92f);

        Assert.NotNull(result);
        Assert.Equal("hey spark", result.DetectedPhrase);
        Assert.Equal(0.92f, result.Confidence);
        Assert.False(result.IsRepeatTrigger);
        Assert.Same(result, eventResult);
    }

    [Fact]
    public void RequireRepeatedPhrase_RequiresTwoPhrasesWithinWindow()
    {
        var config = new WakeWordConfiguration
        {
            RequireRepeatedPhrase = true,
            RepeatWindowSeconds = 2.0f,
            CooldownSeconds = 0.05f
        };

        using var detector = new SparkWakeWordDetector(config);
        WakeWordDetectionResult? eventResult = null;
        detector.WakeWordDetected += res => eventResult = res;

        // First phrase: should NOT trigger yet
        var first = detector.TestInjectPhrase("spark");
        Assert.Null(first);
        Assert.Null(eventResult);

        // Small pause
        Thread.Sleep(50);

        // Second phrase within window: should trigger!
        var second = detector.TestInjectPhrase("spark");
        Assert.NotNull(second);
        Assert.True(second.IsRepeatTrigger);
        Assert.NotNull(eventResult);
        Assert.True(eventResult.IsRepeatTrigger);
    }

    [Fact]
    public void RequireRepeatedPhrase_FailsIfSecondPhraseExceedsWindow()
    {
        var config = new WakeWordConfiguration
        {
            RequireRepeatedPhrase = true,
            RepeatWindowSeconds = 0.1f // 100ms window
        };

        using var detector = new SparkWakeWordDetector(config);
        WakeWordDetectionResult? eventResult = null;
        detector.WakeWordDetected += res => eventResult = res;

        // First trigger
        var first = detector.TestInjectPhrase("spark");
        Assert.Null(first);

        // Wait out the repeat window (150ms > 100ms)
        Thread.Sleep(150);

        // This trigger now acts as a NEW first trigger, not second trigger
        var second = detector.TestInjectPhrase("spark");
        Assert.Null(second);
        Assert.Null(eventResult);
    }

    [Fact]
    public void Cooldown_SuppressesImmediateRetrigger()
    {
        var config = new WakeWordConfiguration
        {
            CooldownSeconds = 1.0f
        };

        using var detector = new SparkWakeWordDetector(config);

        var first = detector.TestInjectPhrase("hey spark");
        Assert.NotNull(first);

        // Immediate second inject within cooldown
        var second = detector.TestInjectPhrase("hey spark");
        Assert.Null(second);
    }

    [Fact]
    public void DisabledState_SuppressesTriggers()
    {
        var config = new WakeWordConfiguration
        {
            IsEnabled = false
        };

        using var detector = new SparkWakeWordDetector(config);
        float[] samples = new float[1600];
        Array.Fill(samples, 0.5f);

        var result = detector.ProcessSamples(samples);
        Assert.Null(result);
    }
}
