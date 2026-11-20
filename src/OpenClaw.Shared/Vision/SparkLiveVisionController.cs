using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Vision;

/// <summary>
/// Controls camera capture, enforces AI model frame rate throttling,
/// maintains audio/video clock synchronization, and generates multimodal payloads.
/// </summary>
public sealed class SparkLiveVisionController : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly ICameraCaptureService _captureService;
    private readonly CameraCaptureOptions _options;
    private readonly Stopwatch _clock = new();

    private long _lastDispatchedTimestampMs = -1;
    private long _minFrameIntervalMs;
    private LiveVideoFrame? _latestFrame;
    private bool _isThrottlingEnabled = true;
    private bool _disposed;

    public bool IsCapturing => _captureService.IsCapturing;
    public CameraCaptureOptions Options => _options;
    public LiveVideoFrame? LatestFrame
    {
        get { lock (_gate) return _latestFrame; }
    }

    /// <summary>Fired when a throttled frame is ready to be dispatched to the AI provider.</summary>
    public event Action<LiveVideoFrame>? ModelFrameReady;

    /// <summary>Fired for every raw frame (e.g. for smooth UI preview render).</summary>
    public event Action<LiveVideoFrame>? PreviewFrameReady;

    public event Action<Exception>? ErrorOccurred;
    public event Action? CameraDisconnected;

    public SparkLiveVisionController(ICameraCaptureService captureService, CameraCaptureOptions? options = null)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _options = options ?? new CameraCaptureOptions();

        var fps = Math.Max(0.1f, _options.ModelInputFps);
        _minFrameIntervalMs = (long)(1000.0f / fps);

        _captureService.FrameCaptured += OnRawFrameCaptured;
        _captureService.CaptureError += err => ErrorOccurred?.Invoke(err);
        _captureService.CameraDisconnected += () => CameraDisconnected?.Invoke();
    }

    /// <summary>
    /// Starts camera capture with the configured options.
    /// Resets the monotonic synchronization clock.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SparkLiveVisionController));
            _clock.Restart();
            _lastDispatchedTimestampMs = -1;
        }

        await _captureService.StartCaptureAsync(_options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops camera capture cleanly.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _clock.Stop();
        }

        await _captureService.StopCaptureAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes a single high-priority snapshot frame for the next conversational turn,
    /// bypassing throttling interval.
    /// </summary>
    public LiveVideoFrame? CaptureSnapshot()
    {
        lock (_gate)
        {
            if (_latestFrame == null) return null;
            return _latestFrame;
        }
    }

    /// <summary>
    /// Formats a video frame as a provider-compatible Gemini multimodal inline data part.
    /// </summary>
    public static object FormatGeminiInlineData(LiveVideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return new
        {
            inline_data = new
            {
                mime_type = "image/jpeg",
                data = frame.ToBase64()
            }
        };
    }

    /// <summary>
    /// Formats a video frame as an OpenAI-compatible image_url part.
    /// </summary>
    public static object FormatOpenAiImageUrl(LiveVideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return new
        {
            type = "image_url",
            image_url = new
            {
                url = frame.ToBase64DataUrl(),
                detail = "auto"
            }
        };
    }

    private void OnRawFrameCaptured(LiveVideoFrame rawFrame)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _latestFrame = rawFrame;
        }

        // Preview receives every frame
        PreviewFrameReady?.Invoke(rawFrame);

        // Check throttling for AI model frame dispatch
        var nowMs = _clock.ElapsedMilliseconds;
        lock (_gate)
        {
            if (_isThrottlingEnabled && _lastDispatchedTimestampMs >= 0)
            {
                if (nowMs - _lastDispatchedTimestampMs < _minFrameIntervalMs)
                {
                    return; // Throttled
                }
            }

            _lastDispatchedTimestampMs = nowMs;
        }

        ModelFrameReady?.Invoke(rawFrame);
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _clock.Stop();
        }

        _captureService.FrameCaptured -= OnRawFrameCaptured;
        await _captureService.DisposeAsync().ConfigureAwait(false);
    }
}
