using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Audio.Live;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkLiveSessionTests
{
    private sealed class MockLiveTransport : ISparkLiveTransport
    {
        public bool IsConnected { get; private set; }
        public List<byte[]> SentAudioChunks { get; } = [];
        public List<(string Type, string Payload)> SentControlMessages { get; } = [];

        public event Action<ReadOnlyMemory<byte>>? AudioChunkReceived;
        public event Action<string, string>? ControlMessageReceived;
        public event Action<Exception>? TransportError;
        public event Action? Disconnected;

        public Task ConnectAsync(string endpoint, CancellationToken cancellationToken)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task SendAudioChunkAsync(ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken)
        {
            SentAudioChunks.Add(chunk.ToArray());
            return Task.CompletedTask;
        }

        public Task SendControlMessageAsync(string type, string payload, CancellationToken cancellationToken)
        {
            SentControlMessages.Add((type, payload));
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            IsConnected = false;
            Disconnected?.Invoke();
            return Task.CompletedTask;
        }

        public void SimulateAudioReceived(byte[] audio) =>
            AudioChunkReceived?.Invoke(audio);

        public void SimulateControlReceived(string type, string payload) =>
            ControlMessageReceived?.Invoke(type, payload);

        public void SimulateError(Exception ex) =>
            TransportError?.Invoke(ex);

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task ConnectAsync_TransitionsToConnectingThenConnectedIdle()
    {
        var transport = new MockLiveTransport();
        var session = new SparkLiveSession(new SparkLiveSessionOptions(), transport);

        Assert.Equal(SparkLiveState.Disconnected, session.State);

        await session.ConnectAsync("wss://live.spark.local/stream");
        Assert.Equal(SparkLiveState.ConnectedIdle, session.State);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task NotifyUserSpeechStartedAndEnded_CreatesAndCompletesUserTurn()
    {
        var transport = new MockLiveTransport();
        var session = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        await session.ConnectAsync("wss://live.spark.local/stream");

        SparkLiveTurn? completedTurn = null;
        session.TurnCompleted += turn => completedTurn = turn;

        session.NotifyUserSpeechStarted();
        Assert.Equal(SparkLiveState.UserSpeaking, session.State);

        session.HandleTranscriptUpdate("user", "Hello SPARK", false);

        session.NotifyUserSpeechEnded();
        Assert.Equal(SparkLiveState.ProcessingTurn, session.State);
        Assert.NotNull(completedTurn);
        Assert.Equal("user", completedTurn.Role);
        Assert.Equal("Hello SPARK", completedTurn.Text);
        Assert.True(completedTurn.IsComplete);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task AssistantAudioReceived_TransitionsToAssistantSpeaking()
    {
        var transport = new MockLiveTransport();
        var session = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        await session.ConnectAsync("wss://live.spark.local/stream");

        ReadOnlyMemory<byte>? receivedAudio = null;
        session.AssistantAudioReceived += chunk => receivedAudio = chunk;

        transport.SimulateAudioReceived([1, 2, 3, 4]);

        Assert.Equal(SparkLiveState.AssistantSpeaking, session.State);
        Assert.NotNull(receivedAudio);
        Assert.Equal(4, receivedAudio.Value.Length);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task BargeIn_AbortsAssistantTurn_SendsControlMessageAndTransitionsToInterrupted()
    {
        var transport = new MockLiveTransport();
        var session = new SparkLiveSession(new SparkLiveSessionOptions { EnableBargeIn = true }, transport);
        await session.ConnectAsync("wss://live.spark.local/stream");

        // Assistant is speaking
        transport.SimulateAudioReceived([1, 2, 3]);
        session.HandleTranscriptUpdate("assistant", "I am answering...", false);
        Assert.Equal(SparkLiveState.AssistantSpeaking, session.State);

        var bargeInFired = false;
        session.BargeInOccurred += () => bargeInFired = true;

        // User speaks to interrupt
        session.NotifyUserSpeechStarted();

        Assert.True(bargeInFired);
        Assert.Contains(transport.SentControlMessages, m => m.Type == "interrupt");
        Assert.Equal(SparkLiveState.UserSpeaking, session.State);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task Disconnection_TriggersAutomaticReconnect()
    {
        var transport = new MockLiveTransport();
        var session = new SparkLiveSession(
            new SparkLiveSessionOptions
            {
                MaxReconnectAttempts = 2,
                ReconnectBackoff = TimeSpan.FromMilliseconds(50)
            },
            transport);

        await session.ConnectAsync("wss://live.spark.local/stream");
        Assert.Equal(SparkLiveState.ConnectedIdle, session.State);

        // Simulate transport error
        transport.SimulateError(new Exception("Network drop"));

        Assert.Equal(SparkLiveState.Reconnecting, session.State);

        // Allow reconnect backoff to execute
        await Task.Delay(120);

        Assert.Equal(SparkLiveState.ConnectedIdle, session.State);

        await session.DisposeAsync();
    }
}
