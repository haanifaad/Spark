using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Audio.Live;
using OpenClaw.Shared.Vision;

namespace OpenClaw.Shared.Video;

/// <summary>
/// Controller for the dedicated AI Video Call experience.
/// Coordinates camera video capture, microphone audio streaming, live transcript rail,
/// screen sharing, and realtime AI bidirectional interaction.
/// </summary>
public sealed class SparkVideoCallController : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SparkLiveSession _liveSession;
    private readonly SparkLiveVisionController _visionController;
    private readonly SparkVideoCallSessionOptions _options;
    private readonly List<VideoCallTranscriptEntry> _transcripts = [];

    private VideoCallState _state = VideoCallState.Idle;
    private bool _isMicMuted;
    private bool _isCameraOn;
    private bool _isScreenSharing;
    private float _audioLevel;
    private bool _disposed;

    public VideoCallState State
    {
        get { lock (_gate) return _state; }
    }

    public bool IsMicMuted
    {
        get { lock (_gate) return _isMicMuted; }
    }

    public bool IsCameraOn
    {
        get { lock (_gate) return _isCameraOn; }
    }

    public bool IsScreenSharing
    {
        get { lock (_gate) return _isScreenSharing; }
    }

    public float AudioLevel
    {
        get { lock (_gate) return _audioLevel; }
    }

    public IReadOnlyList<VideoCallTranscriptEntry> Transcripts
    {
        get { lock (_gate) return _transcripts.ToArray(); }
    }

    public SparkLiveSession LiveSession => _liveSession;
    public SparkLiveVisionController VisionController => _visionController;

    public event Action<VideoCallState>? StateChanged;
    public event Action<bool>? MicMutedChanged;
    public event Action<bool>? CameraStateChanged;
    public event Action<bool>? ScreenSharingChanged;
    public event Action<float>? AudioLevelChanged;
    public event Action<VideoCallTranscriptEntry>? TranscriptUpdated;
    public event Action? CallEnded;

    public SparkVideoCallController(
        SparkLiveSession liveSession,
        SparkLiveVisionController visionController,
        SparkVideoCallSessionOptions? options = null)
    {
        _liveSession = liveSession ?? throw new ArgumentNullException(nameof(liveSession));
        _visionController = visionController ?? throw new ArgumentNullException(nameof(visionController));
        _options = options ?? new SparkVideoCallSessionOptions();

        _isMicMuted = _options.StartWithMicMuted;
        _isCameraOn = _options.StartWithCameraOn;
        _isScreenSharing = _options.EnableScreenShare;

        _liveSession.StateChanged += OnLiveStateChanged;
        _liveSession.TurnUpdated += OnLiveTurnUpdated;
        _liveSession.TurnCompleted += OnLiveTurnCompleted;
        _liveSession.BargeInOccurred += OnLiveBargeIn;

        _visionController.ModelFrameReady += OnModelFrameReady;
    }

    /// <summary>
    /// Starts the AI Video Call. Connects the live session and starts camera capture.
    /// </summary>
    public async Task StartCallAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SparkVideoCallController));
            if (_state != VideoCallState.Idle) return;
            TransitionTo(VideoCallState.Connecting);
        }

        try
        {
            await _liveSession.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);

            if (_isCameraOn)
            {
                await _visionController.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (_gate)
            {
                TransitionTo(VideoCallState.Connected);
            }
        }
        catch
        {
            lock (_gate)
            {
                TransitionTo(VideoCallState.Faulted);
            }
            throw;
        }
    }

    /// <summary>
    /// Ends the video call cleanly, disconnects live session, stops camera, and fires CallEnded.
    /// </summary>
    public async Task EndCallAsync()
    {
        lock (_gate)
        {
            if (_disposed || _state is VideoCallState.Ended or VideoCallState.Idle)
                return;
            TransitionTo(VideoCallState.Ended);
        }

        try
        {
            if (_visionController.IsCapturing)
            {
                await _visionController.StopAsync().ConfigureAwait(false);
            }
            await _liveSession.DisconnectAsync().ConfigureAwait(false);
        }
        finally
        {
            CallEnded?.Invoke();
        }
    }

    /// <summary>
    /// Toggles the microphone mute state.
    /// </summary>
    public void ToggleMicMute()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _isMicMuted = !_isMicMuted;
            MicMutedChanged?.Invoke(_isMicMuted);
        }
    }

    /// <summary>
    /// Toggles the local video camera on/off.
    /// </summary>
    public async Task ToggleCameraAsync()
    {
        bool newCameraState;
        lock (_gate)
        {
            if (_disposed) return;
            _isCameraOn = !_isCameraOn;
            newCameraState = _isCameraOn;
        }

        if (newCameraState)
        {
            await _visionController.StartAsync().ConfigureAwait(false);
        }
        else
        {
            await _visionController.StopAsync().ConfigureAwait(false);
        }

        CameraStateChanged?.Invoke(newCameraState);
    }

    /// <summary>
    /// Toggles screen sharing on/off.
    /// </summary>
    public Task ToggleScreenShareAsync()
    {
        bool newShareState;
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            _isScreenSharing = !_isScreenSharing;
            newShareState = _isScreenSharing;
        }

        ScreenSharingChanged?.Invoke(newShareState);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates live audio level for waveform visualization.
    /// </summary>
    public void UpdateAudioLevel(float level)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _audioLevel = Math.Clamp(level, 0.0f, 1.0f);
        }
        AudioLevelChanged?.Invoke(_audioLevel);
    }

    private void OnLiveStateChanged(SparkLiveState liveState)
    {
        lock (_gate)
        {
            if (_disposed || _state is VideoCallState.Ended or VideoCallState.Idle) return;

            if (liveState == SparkLiveState.Reconnecting)
            {
                TransitionTo(VideoCallState.Reconnecting);
            }
            else if (liveState == SparkLiveState.ConnectedIdle && _state == VideoCallState.Reconnecting)
            {
                TransitionTo(VideoCallState.Connected);
            }
            else if (liveState == SparkLiveState.Closed || liveState == SparkLiveState.Disconnected)
            {
                TransitionTo(VideoCallState.Ended);
                CallEnded?.Invoke();
            }
        }
    }

    private void OnLiveTurnUpdated(SparkLiveTurn turn)
    {
        lock (_gate)
        {
            if (_disposed) return;
            var entry = _transcripts.FirstOrDefault(t => t.Id == turn.TurnId);
            if (entry == null)
            {
                entry = new VideoCallTranscriptEntry
                {
                    Id = turn.TurnId,
                    Speaker = turn.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "User" : "SPARK",
                    Text = turn.Text,
                    Timestamp = turn.StartTime
                };
                _transcripts.Add(entry);
            }
            else
            {
                entry.Text = turn.Text;
            }

            TranscriptUpdated?.Invoke(entry);
        }
    }

    private void OnLiveTurnCompleted(SparkLiveTurn turn)
    {
        OnLiveTurnUpdated(turn);
    }

    private void OnLiveBargeIn()
    {
        lock (_gate)
        {
            if (_disposed) return;
            var lastAssistant = _transcripts.LastOrDefault(t => t.Speaker == "SPARK");
            if (lastAssistant != null)
            {
                lastAssistant.IsInterrupted = true;
                TranscriptUpdated?.Invoke(lastAssistant);
            }
        }
    }

    private void OnModelFrameReady(LiveVideoFrame frame)
    {
        // When a throttled frame is captured, format and send over live session control/data stream
        if (_liveSession.State != SparkLiveState.Disconnected && _liveSession.State != SparkLiveState.Closed)
        {
            var geminiPart = SparkLiveVisionController.FormatGeminiInlineData(frame);
            var json = System.Text.Json.JsonSerializer.Serialize(geminiPart);
            _ = _liveSession.PushInputAudioAsync(System.Text.Encoding.UTF8.GetBytes(json));
        }
    }

    private void TransitionTo(VideoCallState newState)
    {
        if (_state == newState) return;
        _state = newState;
        StateChanged?.Invoke(newState);
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _state = VideoCallState.Ended;
        }

        _liveSession.StateChanged -= OnLiveStateChanged;
        _liveSession.TurnUpdated -= OnLiveTurnUpdated;
        _liveSession.TurnCompleted -= OnLiveTurnCompleted;
        _liveSession.BargeInOccurred -= OnLiveBargeIn;
        _visionController.ModelFrameReady -= OnModelFrameReady;

        await _visionController.DisposeAsync().ConfigureAwait(false);
        await _liveSession.DisposeAsync().ConfigureAwait(false);
    }
}
