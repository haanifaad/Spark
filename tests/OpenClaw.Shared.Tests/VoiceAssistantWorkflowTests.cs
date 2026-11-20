using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Audio.Vocabulary;
using OpenClaw.Shared.Audio.Voice;
using OpenClaw.Shared.Audio.WakeWord;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class VoiceAssistantWorkflowTests
{
    private sealed class MockWakeWordDetector : IWakeWordDetector
    {
        public WakeWordConfiguration Configuration { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
        public float Sensitivity { get; set; } = 0.65f;
        public event Action<WakeWordDetectionResult>? WakeWordDetected;

        public void UpdateConfiguration(WakeWordConfiguration configuration)
        {
            Configuration = configuration ?? new();
        }

        public void TriggerWakeWord(string phrase)
        {
            WakeWordDetected?.Invoke(new WakeWordDetectionResult(phrase, 0.95f, DateTime.UtcNow));
        }

        public WakeWordDetectionResult? ProcessSamples(ReadOnlySpan<float> samples) => null;
        public void Reset() { }
        public void Dispose() { }
    }

    [Fact]
    public void Start_WithHandsFree_EntersWaitingForWakeWord()
    {
        var mockWakeWord = new MockWakeWordDetector();
        using var loop = new SparkVoiceAssistantLoop(
            new VoiceAssistantConfiguration { HandsFreeEnabled = true },
            mockWakeWord,
            new SparkPersonalVocabulary(),
            _ => Task.FromResult("response"),
            (_, _) => Task.CompletedTask,
            () => { });

        Assert.Equal(VoiceAssistantState.Idle, loop.State);

        loop.Start();
        Assert.Equal(VoiceAssistantState.WaitingForWakeWord, loop.State);
    }

    [Fact]
    public void WakeWordDetected_TransitionsToListening()
    {
        var mockWakeWord = new MockWakeWordDetector();
        string? detectedPhrase = null;

        using var loop = new SparkVoiceAssistantLoop(
            new VoiceAssistantConfiguration { HandsFreeEnabled = true },
            mockWakeWord,
            new SparkPersonalVocabulary(),
            _ => Task.FromResult("response"),
            (_, _) => Task.CompletedTask,
            () => { });

        loop.WakeWordTriggered += phrase => detectedPhrase = phrase;
        loop.Start();

        mockWakeWord.TriggerWakeWord("Hey Spark");

        Assert.Equal("Hey Spark", detectedPhrase);
        Assert.Equal(VoiceAssistantState.Listening, loop.State);
    }

    [Fact]
    public async Task ProcessCompletedUtterance_NormalizesVocabulary_AndSendsToChat()
    {
        var mockWakeWord = new MockWakeWordDetector();
        string? sentToChat = null;
        string? transcribedText = null;

        using var loop = new SparkVoiceAssistantLoop(
            new VoiceAssistantConfiguration { HandsFreeEnabled = true },
            mockWakeWord,
            new SparkPersonalVocabulary(),
            msg =>
            {
                sentToChat = msg;
                return Task.FromResult("Deploying to Kubernetes now.");
            },
            (_, _) => Task.CompletedTask,
            () => { });

        loop.UtteranceTranscribed += t => transcribedText = t;
        loop.Start();
        mockWakeWord.TriggerWakeWord("Hey Spark");

        await loop.ProcessCompletedUtteranceAsync("deploy service to k8s using grpc");

        Assert.Equal("deploy service to Kubernetes using gRPC", transcribedText);
        Assert.Equal("deploy service to Kubernetes using gRPC", sentToChat);
        Assert.Equal(VoiceAssistantState.WaitingForWakeWord, loop.State);
    }

    [Fact]
    public async Task BargeIn_AbortsPlayback_AndTransitionsToListening()
    {
        var mockWakeWord = new MockWakeWordDetector();
        var playbackStopped = false;
        var bargeInFired = false;
        var playbackTcs = new TaskCompletionSource<bool>();

        using var loop = new SparkVoiceAssistantLoop(
            new VoiceAssistantConfiguration { HandsFreeEnabled = true, EnableBargeIn = true },
            mockWakeWord,
            new SparkPersonalVocabulary(),
            _ => Task.FromResult("This is a long response that will be interrupted"),
            async (_, ct) =>
            {
                using (ct.Register(() => playbackTcs.TrySetCanceled()))
                {
                    await playbackTcs.Task;
                }
            },
            () => playbackStopped = true);

        loop.BargeInDetected += () => bargeInFired = true;
        loop.Start();
        mockWakeWord.TriggerWakeWord("Hey Spark");

        var turnTask = loop.ProcessCompletedUtteranceAsync("tell me about kubernetes");

        // Give the loop time to transition to AssistantSpeaking
        await Task.Delay(50);
        Assert.Equal(VoiceAssistantState.AssistantSpeaking, loop.State);

        // User barges in!
        loop.NotifyUserSpeechStarted();

        Assert.True(playbackStopped);
        Assert.True(bargeInFired);
        Assert.Equal(VoiceAssistantState.Listening, loop.State);

        await turnTask;
    }

    [Fact]
    public void Stop_CancelsPendingTurns_AndTransitionsToIdle()
    {
        var playbackStopped = false;
        using var loop = new SparkVoiceAssistantLoop(
            new VoiceAssistantConfiguration { HandsFreeEnabled = true },
            new MockWakeWordDetector(),
            new SparkPersonalVocabulary(),
            _ => Task.FromResult("ok"),
            (_, _) => Task.CompletedTask,
            () => playbackStopped = true);

        loop.Start();
        Assert.Equal(VoiceAssistantState.WaitingForWakeWord, loop.State);

        loop.Stop();
        Assert.Equal(VoiceAssistantState.Idle, loop.State);
        Assert.True(playbackStopped);
    }

    [Fact]
    public void EmptyUtterance_ResetsToWaitingForWakeWord()
    {
        var mockWakeWord = new MockWakeWordDetector();
        using var loop = new SparkVoiceAssistantLoop(
            new VoiceAssistantConfiguration { HandsFreeEnabled = true },
            mockWakeWord,
            new SparkPersonalVocabulary(),
            _ => Task.FromResult("response"),
            (_, _) => Task.CompletedTask,
            () => { });

        loop.Start();
        mockWakeWord.TriggerWakeWord("Hey Spark");
        Assert.Equal(VoiceAssistantState.Listening, loop.State);

        var _ = loop.ProcessCompletedUtteranceAsync("   ");
        Assert.Equal(VoiceAssistantState.WaitingForWakeWord, loop.State);
    }
}
