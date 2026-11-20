using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Audio.Live;

/// <summary>
/// Manages a realtime bidirectional SPARK Live session, including state transitions,
/// audio chunk streaming, turn boundaries, barge-in interruptions, and session recovery.
/// </summary>
public sealed class SparkLiveSession : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SparkLiveSessionOptions _options;
    private readonly ISparkLiveTransport _transport;
    private readonly List<SparkLiveTurn> _turns = [];

    private SparkLiveState _state = SparkLiveState.Disconnected;
    private string? _endpoint;
    private int _reconnectAttempts;
    private CancellationTokenSource? _sessionCts;
    private SparkLiveTurn? _currentUserTurn;
    private SparkLiveTurn? _currentAssistantTurn;
    private bool _disposed;

    public SparkLiveState State
    {
        get { lock (_gate) return _state; }
    }

    public SparkLiveSessionOptions Options => _options;
    public IReadOnlyList<SparkLiveTurn> Turns
    {
        get { lock (_gate) return _turns.ToArray(); }
    }

    public event Action<SparkLiveState>? StateChanged;
    public event Action<ReadOnlyMemory<byte>>? AssistantAudioReceived;
    public event Action<SparkLiveTurn>? TurnUpdated;
    public event Action<SparkLiveTurn>? TurnCompleted;
    public event Action? BargeInOccurred;
    public event Action<Exception>? SessionFaulted;

    public SparkLiveSession(SparkLiveSessionOptions? options, ISparkLiveTransport transport)
    {
        _options = options ?? new SparkLiveSessionOptions();
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));

        _transport.AudioChunkReceived += OnTransportAudioReceived;
        _transport.ControlMessageReceived += OnTransportControlReceived;
        _transport.TransportError += OnTransportError;
        _transport.Disconnected += OnTransportDisconnected;
    }

    /// <summary>
    /// Connects to the realtime streaming endpoint and initializes the session.
    /// </summary>
    public async Task ConnectAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SparkLiveSession));
            if (_state != SparkLiveState.Disconnected && _state != SparkLiveState.Closed)
                return;

            _endpoint = endpoint;
            _reconnectAttempts = 0;
            _sessionCts = new CancellationTokenSource();
            TransitionTo(SparkLiveState.Connecting);
        }

        try
        {
            await _transport.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                TransitionTo(SparkLiveState.ConnectedIdle);
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                TransitionTo(SparkLiveState.Faulted);
            }
            SessionFaulted?.Invoke(ex);
            throw;
        }
    }

    /// <summary>
    /// Disconnects and ends the live session cleanly.
    /// </summary>
    public async Task DisconnectAsync()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            if (_disposed || _state is SparkLiveState.Disconnected or SparkLiveState.Closed)
                return;

            cts = _sessionCts;
            _sessionCts = null;
            TransitionTo(SparkLiveState.Closed);
        }

        cts?.Cancel();
        cts?.Dispose();

        try
        {
            await _transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SessionFaulted?.Invoke(ex);
        }
    }

    /// <summary>
    /// Pushes an outgoing audio chunk from the client microphone to the live stream.
    /// </summary>
    public async Task PushInputAudioAsync(ReadOnlyMemory<byte> audioChunk, CancellationToken cancellationToken = default)
    {
        if (audioChunk.IsEmpty) return;

        lock (_gate)
        {
            if (_disposed || _state is SparkLiveState.Disconnected or SparkLiveState.Closed or SparkLiveState.Faulted)
                return;
        }

        await _transport.SendAudioChunkAsync(audioChunk, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Informs the session that client-side VAD has detected user speech.
    /// Handles barge-in if the assistant is currently speaking.
    /// </summary>
    public void NotifyUserSpeechStarted()
    {
        lock (_gate)
        {
            if (_disposed) return;

            if (_state == SparkLiveState.AssistantSpeaking && _options.EnableBargeIn)
            {
                // BARGE-IN INTERRUPTION: Truncate current assistant turn and notify server
                if (_currentAssistantTurn != null)
                {
                    _currentAssistantTurn.EndTime = DateTime.UtcNow;
                    _currentAssistantTurn.IsComplete = true;
                    TurnCompleted?.Invoke(_currentAssistantTurn);
                    _currentAssistantTurn = null;
                }

                TransitionTo(SparkLiveState.Interrupted);
                BargeInOccurred?.Invoke();

                _ = _transport.SendControlMessageAsync("interrupt", "{}", CancellationToken.None);
            }

            // Start or continue user turn
            _currentUserTurn = new SparkLiveTurn
            {
                TurnId = Guid.NewGuid().ToString("N"),
                Role = "user",
                StartTime = DateTime.UtcNow
            };
            _turns.Add(_currentUserTurn);
            TransitionTo(SparkLiveState.UserSpeaking);
        }
    }

    /// <summary>
    /// Informs the session that client-side VAD detected user speech ended.
    /// </summary>
    public void NotifyUserSpeechEnded()
    {
        lock (_gate)
        {
            if (_disposed || _state != SparkLiveState.UserSpeaking) return;

            if (_currentUserTurn != null)
            {
                _currentUserTurn.EndTime = DateTime.UtcNow;
                _currentUserTurn.IsComplete = true;
                TurnCompleted?.Invoke(_currentUserTurn);
            }

            TransitionTo(SparkLiveState.ProcessingTurn);
        }
    }

    /// <summary>
    /// Handles inbound transcript delta or turn markers from the live server.
    /// </summary>
    public void HandleTranscriptUpdate(string role, string textDelta, bool isComplete)
    {
        lock (_gate)
        {
            if (_disposed) return;

            if (role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
            {
                if (_currentAssistantTurn == null)
                {
                    _currentAssistantTurn = new SparkLiveTurn
                    {
                        TurnId = Guid.NewGuid().ToString("N"),
                        Role = "assistant",
                        StartTime = DateTime.UtcNow
                    };
                    _turns.Add(_currentAssistantTurn);
                }

                _currentAssistantTurn.Text += textDelta;
                if (!isComplete && _state != SparkLiveState.AssistantSpeaking)
                {
                    TransitionTo(SparkLiveState.AssistantSpeaking);
                }
                TurnUpdated?.Invoke(_currentAssistantTurn);

                if (isComplete)
                {
                    _currentAssistantTurn.EndTime = DateTime.UtcNow;
                    _currentAssistantTurn.IsComplete = true;
                    TurnCompleted?.Invoke(_currentAssistantTurn);
                    _currentAssistantTurn = null;
                    TransitionTo(SparkLiveState.ConnectedIdle);
                }
            }
            else if (role.Equals("user", StringComparison.OrdinalIgnoreCase))
            {
                if (_currentUserTurn != null)
                {
                    _currentUserTurn.Text += textDelta;
                    TurnUpdated?.Invoke(_currentUserTurn);
                }
            }
        }
    }

    private void OnTransportAudioReceived(ReadOnlyMemory<byte> chunk)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_state != SparkLiveState.AssistantSpeaking && _state != SparkLiveState.ProcessingTurn)
            {
                TransitionTo(SparkLiveState.AssistantSpeaking);
            }
        }

        AssistantAudioReceived?.Invoke(chunk);
    }

    private void OnTransportControlReceived(string type, string payload)
    {
        if (type.Equals("turn_complete", StringComparison.OrdinalIgnoreCase))
        {
            lock (_gate)
            {
                if (_currentAssistantTurn != null)
                {
                    _currentAssistantTurn.EndTime = DateTime.UtcNow;
                    _currentAssistantTurn.IsComplete = true;
                    TurnCompleted?.Invoke(_currentAssistantTurn);
                    _currentAssistantTurn = null;
                }
                TransitionTo(SparkLiveState.ConnectedIdle);
            }
        }
    }

    private void OnTransportError(Exception ex)
    {
        SessionFaulted?.Invoke(ex);
        HandleDisconnection();
    }

    private void OnTransportDisconnected()
    {
        HandleDisconnection();
    }

    private void HandleDisconnection()
    {
        lock (_gate)
        {
            if (_disposed || _state is SparkLiveState.Closed or SparkLiveState.Disconnected)
                return;

            if (_reconnectAttempts < _options.MaxReconnectAttempts && !string.IsNullOrWhiteSpace(_endpoint))
            {
                _reconnectAttempts++;
                TransitionTo(SparkLiveState.Reconnecting);
                _ = AttemptReconnectAsync(_endpoint);
            }
            else
            {
                TransitionTo(SparkLiveState.Disconnected);
            }
        }
    }

    private async Task AttemptReconnectAsync(string endpoint)
    {
        try
        {
            await Task.Delay(_options.ReconnectBackoff).ConfigureAwait(false);
            await _transport.ConnectAsync(endpoint, CancellationToken.None).ConfigureAwait(false);

            lock (_gate)
            {
                _reconnectAttempts = 0;
                TransitionTo(SparkLiveState.ConnectedIdle);
            }
        }
        catch (Exception ex)
        {
            SessionFaulted?.Invoke(ex);
            lock (_gate)
            {
                if (_reconnectAttempts >= _options.MaxReconnectAttempts)
                {
                    TransitionTo(SparkLiveState.Disconnected);
                }
                else
                {
                    _ = AttemptReconnectAsync(endpoint);
                }
            }
        }
    }

    private void TransitionTo(SparkLiveState newState)
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
            _state = SparkLiveState.Closed;
        }

        _transport.AudioChunkReceived -= OnTransportAudioReceived;
        _transport.ControlMessageReceived -= OnTransportControlReceived;
        _transport.TransportError -= OnTransportError;
        _transport.Disconnected -= OnTransportDisconnected;

        _sessionCts?.Cancel();
        _sessionCts?.Dispose();

        await _transport.DisposeAsync().ConfigureAwait(false);
    }
}
