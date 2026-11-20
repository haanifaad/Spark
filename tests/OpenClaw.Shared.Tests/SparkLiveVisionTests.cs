using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Vision;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkLiveVisionTests
{
    private sealed class MockCameraCaptureService : ICameraCaptureService
    {
        public bool IsCapturing { get; private set; }
        public CameraDeviceInfo? ActiveCamera { get; private set; }

        public event Action<LiveVideoFrame>? FrameCaptured;
        public event Action<Exception>? CaptureError;
        public event Action? CameraDisconnected;

        public Task<IReadOnlyList<CameraDeviceInfo>> GetAvailableCamerasAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CameraDeviceInfo> cameras =
            [
                new CameraDeviceInfo("cam-0", "Integrated Webcam", true, 1280, 720, 30),
                new CameraDeviceInfo("cam-1", "External USB Camera", false, 1920, 1080, 60)
            ];
            return Task.FromResult(cameras);
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

        public void EmitFrame(int width, int height, long timestampMs)
        {
            var fakeJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
            FrameCaptured?.Invoke(new LiveVideoFrame
            {
                JpegBytes = fakeJpeg,
                Width = width,
                Height = height,
                TimestampMs = timestampMs
            });
        }

        public void SimulateDisconnect() => CameraDisconnected?.Invoke();
        public void SimulateError(Exception ex) => CaptureError?.Invoke(ex);

        public ValueTask DisposeAsync()
        {
            IsCapturing = false;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task StartAndStop_ManagesCaptureServiceLifecycle()
    {
        var mock = new MockCameraCaptureService();
        var controller = new SparkLiveVisionController(mock);

        Assert.False(controller.IsCapturing);

        await controller.StartAsync();
        Assert.True(controller.IsCapturing);

        await controller.StopAsync();
        Assert.False(controller.IsCapturing);

        await controller.DisposeAsync();
    }

    [Fact]
    public async Task FrameThrottling_LimitsModelFrames_WhileDeliveringAllPreviewFrames()
    {
        var mock = new MockCameraCaptureService();
        // 1 frame per second (1000ms interval)
        var options = new CameraCaptureOptions { ModelInputFps = 1.0f };
        var controller = new SparkLiveVisionController(mock, options);

        var previewCount = 0;
        var modelCount = 0;

        controller.PreviewFrameReady += _ => previewCount++;
        controller.ModelFrameReady += _ => modelCount++;

        await controller.StartAsync();

        // Emit 5 frames in quick succession
        mock.EmitFrame(1280, 720, 0);
        mock.EmitFrame(1280, 720, 100);
        mock.EmitFrame(1280, 720, 200);
        mock.EmitFrame(1280, 720, 300);
        mock.EmitFrame(1280, 720, 400);

        // Preview should receive all 5 frames
        Assert.Equal(5, previewCount);

        // Model should receive only 1 frame due to 1.0s throttling
        Assert.Equal(1, modelCount);

        await controller.DisposeAsync();
    }

    [Fact]
    public async Task CaptureSnapshot_ReturnsLatestFrameOnDemand()
    {
        var mock = new MockCameraCaptureService();
        var controller = new SparkLiveVisionController(mock);
        await controller.StartAsync();

        Assert.Null(controller.CaptureSnapshot());

        mock.EmitFrame(640, 480, 1000);

        var snapshot = controller.CaptureSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal(640, snapshot.Width);
        Assert.Equal(480, snapshot.Height);

        await controller.DisposeAsync();
    }

    [Fact]
    public void FormatGeminiInlineData_ProducesExpectedStructure()
    {
        var frame = new LiveVideoFrame
        {
            JpegBytes = [1, 2, 3, 4],
            Width = 640,
            Height = 480,
            TimestampMs = 100
        };

        var part = SparkLiveVisionController.FormatGeminiInlineData(frame);
        var json = JsonSerializer.Serialize(part);

        Assert.Contains("inline_data", json);
        Assert.Contains("image/jpeg", json);
        Assert.Contains("AQIDBA==", json); // Base64 of [1, 2, 3, 4]
    }

    [Fact]
    public void FormatOpenAiImageUrl_ProducesExpectedStructure()
    {
        var frame = new LiveVideoFrame
        {
            JpegBytes = [1, 2, 3, 4],
            Width = 640,
            Height = 480,
            TimestampMs = 100
        };

        var part = SparkLiveVisionController.FormatOpenAiImageUrl(frame);
        var json = JsonSerializer.Serialize(part);

        Assert.Contains("image_url", json);
        Assert.Contains("data:image/jpeg;base64,AQIDBA==", json);
    }

    [Fact]
    public async Task CameraDisconnected_PropagatesEvent()
    {
        var mock = new MockCameraCaptureService();
        var controller = new SparkLiveVisionController(mock);
        var disconnectedFired = false;
        controller.CameraDisconnected += () => disconnectedFired = true;

        await controller.StartAsync();
        mock.SimulateDisconnect();

        Assert.True(disconnectedFired);

        await controller.DisposeAsync();
    }
}
