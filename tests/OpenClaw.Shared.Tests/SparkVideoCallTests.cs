using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Audio.Live;
using OpenClaw.Shared.Video;
using OpenClaw.Shared.Vision;
using Xunit;

#pragma warning disable CS0067

namespace OpenClaw.Shared.Tests;

public sealed class SparkVideoCallTests
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
            IReadOnlyList<CameraDeviceInfo> list =
            [
                new CameraDeviceInfo("cam-0", "Integrated Webcam", true)
            ];
            return Task.FromResult(list);
        }

        public Task StartCaptureAsync(CameraCaptureOptions options, CancellationToken cancellationToken = default)
        {
            IsCapturing = true;
            ActiveCamera = new CameraDeviceInfo("cam-0", "Integrated Webcam", true);
            return Task.CompletedTask;
        }

        public Task StopCaptureAsync(CancellationToken cancellationToken = default)
        {
            IsCapturing = false;
            ActiveCamera = null;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsCapturing = false;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task StartCallAsync_ConnectsLiveSessionAndStartsCamera_TransitionsToConnected()
    {
        var transport = new MockLiveTransport();
        var liveSession = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        var camera = new MockCameraCaptureService();
        var vision = new SparkLiveVisionController(camera);

        var controller = new SparkVideoCallController(
            liveSession,
            vision,
            new SparkVideoCallSessionOptions { StartWithCameraOn = true });

        Assert.Equal(VideoCallState.Idle, controller.State);
        Assert.False(camera.IsCapturing);

        await controller.StartCallAsync("wss://live.spark.local/video");

        Assert.Equal(VideoCallState.Connected, controller.State);
        Assert.True(transport.IsConnected);
        Assert.True(camera.IsCapturing);

        await controller.DisposeAsync();
    }

    [Fact]
    public async Task EndCallAsync_StopsCamera_DisconnectsSession_TransitionsToEnded()
    {
        var transport = new MockLiveTransport();
        var liveSession = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        var camera = new MockCameraCaptureService();
        var vision = new SparkLiveVisionController(camera);

        var controller = new SparkVideoCallController(liveSession, vision);
        var callEndedFired = false;
        controller.CallEnded += () => callEndedFired = true;

        await controller.StartCallAsync("wss://live.spark.local/video");
        Assert.Equal(VideoCallState.Connected, controller.State);

        await controller.EndCallAsync();

        Assert.Equal(VideoCallState.Ended, controller.State);
        Assert.False(camera.IsCapturing);
        Assert.False(transport.IsConnected);
        Assert.True(callEndedFired);

        await controller.DisposeAsync();
    }

    [Fact]
    public async Task ToggleMicMute_TogglesStateAndFiresEvent()
    {
        var transport = new MockLiveTransport();
        var liveSession = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        var camera = new MockCameraCaptureService();
        var vision = new SparkLiveVisionController(camera);

        var controller = new SparkVideoCallController(
            liveSession,
            vision,
            new SparkVideoCallSessionOptions { StartWithMicMuted = false });

        bool? lastEventValue = null;
        controller.MicMutedChanged += val => lastEventValue = val;

        Assert.False(controller.IsMicMuted);

        controller.ToggleMicMute();
        Assert.True(controller.IsMicMuted);
        Assert.True(lastEventValue);

        controller.ToggleMicMute();
        Assert.False(controller.IsMicMuted);
        Assert.False(lastEventValue);

        await controller.DisposeAsync();
    }

    [Fact]
    public async Task ToggleCameraAsync_TogglesCameraStateAndStartsOrStopsCapture()
    {
        var transport = new MockLiveTransport();
        var liveSession = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        var camera = new MockCameraCaptureService();
        var vision = new SparkLiveVisionController(camera);

        var controller = new SparkVideoCallController(
            liveSession,
            vision,
            new SparkVideoCallSessionOptions { StartWithCameraOn = true });

        await controller.StartCallAsync("wss://live.spark.local/video");
        Assert.True(controller.IsCameraOn);
        Assert.True(camera.IsCapturing);

        // Turn off camera
        await controller.ToggleCameraAsync();
        Assert.False(controller.IsCameraOn);
        Assert.False(camera.IsCapturing);

        // Turn camera back on
        await controller.ToggleCameraAsync();
        Assert.True(controller.IsCameraOn);
        Assert.True(camera.IsCapturing);

        await controller.DisposeAsync();
    }

    [Fact]
    public async Task ToggleScreenShareAsync_TogglesStateAndFiresEvent()
    {
        var transport = new MockLiveTransport();
        var liveSession = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        var camera = new MockCameraCaptureService();
        var vision = new SparkLiveVisionController(camera);

        var controller = new SparkVideoCallController(
            liveSession,
            vision,
            new SparkVideoCallSessionOptions { EnableScreenShare = false });

        bool? shareEvent = null;
        controller.ScreenSharingChanged += val => shareEvent = val;

        Assert.False(controller.IsScreenSharing);

        await controller.ToggleScreenShareAsync();
        Assert.True(controller.IsScreenSharing);
        Assert.True(shareEvent);

        await controller.ToggleScreenShareAsync();
        Assert.False(controller.IsScreenSharing);
        Assert.False(shareEvent);

        await controller.DisposeAsync();
    }

    [Fact]
    public async Task TranscriptUpdatesAndBargeIn_ReflectInTranscriptRail()
    {
        var transport = new MockLiveTransport();
        var liveSession = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        var camera = new MockCameraCaptureService();
        var vision = new SparkLiveVisionController(camera);

        var controller = new SparkVideoCallController(liveSession, vision);
        await controller.StartCallAsync("wss://live.spark.local/video");

        var transcriptEvents = new List<VideoCallTranscriptEntry>();
        controller.TranscriptUpdated += entry => transcriptEvents.Add(entry);

        // 1. User turn
        liveSession.NotifyUserSpeechStarted();
        liveSession.HandleTranscriptUpdate("user", "Can you see my screen?", false);
        liveSession.NotifyUserSpeechEnded();

        Assert.Single(controller.Transcripts);
        Assert.Equal("User", controller.Transcripts[0].Speaker);
        Assert.Equal("Can you see my screen?", controller.Transcripts[0].Text);

        // 2. Assistant turn begins
        liveSession.HandleTranscriptUpdate("assistant", "Yes, I can see it clearly.", false);
        Assert.Equal(2, controller.Transcripts.Count);
        Assert.Equal("SPARK", controller.Transcripts[1].Speaker);
        Assert.False(controller.Transcripts[1].IsInterrupted);

        // 3. User interrupts assistant (Barge-in)
        liveSession.NotifyUserSpeechStarted();
        Assert.True(controller.Transcripts[1].IsInterrupted);

        await controller.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAudioLevel_ClampsValuesAndFiresEvent()
    {
        var transport = new MockLiveTransport();
        var liveSession = new SparkLiveSession(new SparkLiveSessionOptions(), transport);
        var camera = new MockCameraCaptureService();
        var vision = new SparkLiveVisionController(camera);

        var controller = new SparkVideoCallController(liveSession, vision);

        float? capturedLevel = null;
        controller.AudioLevelChanged += lvl => capturedLevel = lvl;

        controller.UpdateAudioLevel(0.75f);
        Assert.Equal(0.75f, controller.AudioLevel);
        Assert.Equal(0.75f, capturedLevel);

        // Over clamping test
        controller.UpdateAudioLevel(1.5f);
        Assert.Equal(1.0f, controller.AudioLevel);

        await controller.DisposeAsync();
    }
}
