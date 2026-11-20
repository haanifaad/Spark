using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Vision;

/// <summary>
/// Service abstraction for local camera capture.
/// </summary>
public interface ICameraCaptureService : IAsyncDisposable
{
    bool IsCapturing { get; }
    CameraDeviceInfo? ActiveCamera { get; }

    Task<IReadOnlyList<CameraDeviceInfo>> GetAvailableCamerasAsync(CancellationToken cancellationToken = default);
    Task StartCaptureAsync(CameraCaptureOptions options, CancellationToken cancellationToken = default);
    Task StopCaptureAsync(CancellationToken cancellationToken = default);

    event Action<LiveVideoFrame>? FrameCaptured;
    event Action<Exception>? CaptureError;
    event Action? CameraDisconnected;
}
