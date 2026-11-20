using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Audio.Live;
using OpenClaw.Shared.Session;
using OpenClaw.Shared.Video;
using OpenClaw.Shared.Vision;
using Xunit;

#pragma warning disable CS0067

namespace OpenClaw.Shared.Tests;

public sealed class SparkLiveSessionManagerTests
{
    private sealed class MockLiveTransport : ISparkLiveTransport
    {
        public bool IsConnected { get; private set; }
        public event Action<ReadOnlyMemory<byte>>? AudioChunkReceived;
        public event Action<string, string>? ControlMessageReceived;
        public event Action<Exception>? TransportError;
        public event Action? Disconnected;

        public Task ConnectAsync(string endpoint, CancellationToken cancellationToken)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task SendAudioChunkAsync(ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendControlMessageAsync(string type, string payload, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            IsConnected = false;
            Disconnected?.Invoke();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MockCameraCaptureService : ICameraCaptureService
    {
        public bool IsCapturing { get; private set; }
        public CameraDeviceInfo? ActiveCamera { get; private set; }
        public event Action<LiveVideoFrame>? FrameCaptured;
        public event Action<Exception>? CaptureError;
        public event Action? CameraDisconnected;

        public Task<IReadOnlyList<CameraDeviceInfo>> GetAvailableCamerasAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CameraDeviceInfo> cams = [new CameraDeviceInfo("c1", "Webcam")];
            return Task.FromResult(cams);
        }

        public Task StartCaptureAsync(CameraCaptureOptions options, CancellationToken cancellationToken = default)
        {
            IsCapturing = true;
            return Task.CompletedTask;
        }

        public Task StopCaptureAsync(CancellationToken cancellationToken = default)
        {
            IsCapturing = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsCapturing = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MockSyncTarget : ISparkConversationTurnSyncTarget
    {
        public List<(string ConvoId, string Role, string Text)> SyncedTurns { get; } = [];

        public Task SyncTurnAsync(
            string conversationId,
            string role,
            string text,
            DateTime timestamp,
            bool isInterrupted,
            CancellationToken cancellationToken = default)
        {
            SyncedTurns.Add((conversationId, role, text));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RegisterLiveSession_EnforcesSingleActiveSessionInvariant()
    {
        var manager = new SparkLiveSessionManager();

        var transport1 = new MockLiveTransport();
        var session1 = new SparkLiveSession(new SparkLiveSessionOptions(), transport1);
        await session1.ConnectAsync("wss://live1");

        var transport2 = new MockLiveTransport();
        var session2 = new SparkLiveSession(new SparkLiveSessionOptions(), transport2);
        await session2.ConnectAsync("wss://live2");

        // First session registers successfully
        await manager.RegisterLiveSessionAsync(session1, "convo-1");
        Assert.True(manager.HasActiveSession);
        Assert.Equal("convo-1", manager.ActiveConversationId);
        Assert.Same(session1, manager.ActiveSession);

        // Attempting to register second session without forceSwap throws
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.RegisterLiveSessionAsync(session2, "convo-2", forceSwap: false));

        // Registering second session WITH forceSwap terminates previous session
        await manager.RegisterLiveSessionAsync(session2, "convo-2", forceSwap: true);
        Assert.True(manager.HasActiveSession);
        Assert.Equal("convo-2", manager.ActiveConversationId);
        Assert.Same(session2, manager.ActiveSession);
        Assert.False(transport1.IsConnected); // First session was disconnected

        await manager.EndActiveSessionAsync();
        Assert.False(manager.HasActiveSession);
    }

    [Fact]
    public async Task LiveSessionTurns_SynchronizeFaithfullyToChatHistoryTarget()
    {
        var syncTarget = new MockSyncTarget();
        var manager = new SparkLiveSessionManager(syncTarget);

        var transport = new MockLiveTransport();
        var session = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        await session.ConnectAsync("wss://live");

        await manager.RegisterLiveSessionAsync(session, "convo-test");

        // User speaks
        session.NotifyUserSpeechStarted();
        session.HandleTranscriptUpdate("user", "What is the capital of France?", false);
        session.NotifyUserSpeechEnded();

        // Assistant replies
        session.HandleTranscriptUpdate("assistant", "The capital of France is Paris.", true);

        // Both turns should have been synchronized to persistent target
        Assert.Equal(2, syncTarget.SyncedTurns.Count);
        Assert.Equal(("convo-test", "user", "What is the capital of France?"), syncTarget.SyncedTurns[0]);
        Assert.Equal(("convo-test", "assistant", "The capital of France is Paris."), syncTarget.SyncedTurns[1]);

        await manager.EndActiveSessionAsync();
    }

    [Fact]
    public async Task RegisterVideoCall_SharesSingleActiveSessionSlot_AndCleansUpOnEnd()
    {
        var syncTarget = new MockSyncTarget();
        var manager = new SparkLiveSessionManager(syncTarget);

        var transport = new MockLiveTransport();
        var session = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        var camera = new MockCameraCaptureService();
        var vision = new SparkLiveVisionController(camera);

        var call = new SparkVideoCallController(session, vision);
        await call.StartCallAsync("wss://live-call");

        await manager.RegisterVideoCallAsync(call, "convo-call");
        Assert.True(manager.HasActiveSession);
        Assert.Same(call, manager.ActiveVideoCall);
        Assert.Same(session, manager.ActiveSession);

        // End call via call controller -> propagates to manager cleanup
        await call.EndCallAsync();

        Assert.False(manager.HasActiveSession);
        Assert.Null(manager.ActiveVideoCall);
        Assert.Null(manager.ActiveSession);
    }
}
