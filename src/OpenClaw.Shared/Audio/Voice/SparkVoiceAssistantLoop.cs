using System;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Audio.Vocabulary;
using OpenClaw.Shared.Audio.WakeWord;

namespace OpenClaw.Shared.Audio.Voice;

/// <summary>
/// Lifecycle states for the continuous conversational voice assistant.
/// </summary>
public enum VoiceAssistantState
{
    Idle,
    WaitingForWakeWord,
    Listening,
    ProcessingTurn,
    AssistantSpeaking,
    Interrupted,
    Faulted
}

/// <summary>
/// Configuration for the conversational voice assistant loop.
/// </summary>
public sealed class VoiceAssistantConfiguration
{
    /// <summary>Whether hands-free wake word listening is enabled.</summary>
    public bool HandsFreeEnabled { get; set; } = true;

    /// <summary>Whether user barge-in (interruption) during TTS playback is allowed.</summary>
    public bool EnableBargeIn { get; set; } = true;

    /// <summary>Whether to automatically resume listening after the assistant finishes speaking.</summary>
    public bool AutoResumeListening { get; set; } = true;

    /// <summary>Silence timeout in seconds before an utterance is finalized.</summary>
    public float SilenceTimeoutSeconds { get; set; } = 2.0f;
}

/// <summary>
/// Orchestrates the hands-free conversational loop:
/// Wake Word -> Listen -> STT -> Vocabulary Normalization -> Chat Engine -> TTS Playback -> Resume.
/// Implements barge-in detection to abort TTS playback whenever the user starts speaking.
/// </summary>
public sealed class SparkVoiceAssistantLoop : IDisposable
{
    private readonly object _gate = new();
    private readonly VoiceAssistantConfiguration _config;
    private readonly IWakeWordDetector _wakeWordDetector;
    private readonly SparkPersonalVocabulary _vocabulary;

    private readonly Func<string, Task<string>> _sendMessageAsync;
    private readonly Func<string, CancellationToken, Task> _playSpeechAsync;
    private readonly Action _stopPlayback;

    private VoiceAssistantState _state = VoiceAssistantState.Idle;
    private CancellationTokenSource? _activeLoopCts;
    private CancellationTokenSource? _speakingCts;
    private bool _disposed;

    public VoiceAssistantState State
    {
        get { lock (_gate) return _state; }
    }

    public VoiceAssistantConfiguration Configuration => _config;
    public IWakeWordDetector WakeWordDetector => _wakeWordDetector;
    public SparkPersonalVocabulary Vocabulary => _vocabulary;

    public event Action<VoiceAssistantState>? StateChanged;
    public event Action<string>? WakeWordTriggered;
    public event Action<string>? UtteranceTranscribed;
    public event Action<string>? AssistantResponseReceived;
    public event Action? BargeInDetected;
    public event Action<Exception>? LoopFaulted;

    public SparkVoiceAssistantLoop(
        VoiceAssistantConfiguration? config,
        IWakeWordDetector? wakeWordDetector,
        SparkPersonalVocabulary? vocabulary,
        Func<string, Task<string>> sendMessageAsync,
        Func<string, CancellationToken, Task> playSpeechAsync,
        Action stopPlayback)
    {
        _config = config ?? new VoiceAssistantConfiguration();
        _wakeWordDetector = wakeWordDetector ?? new SparkWakeWordDetector();
        _vocabulary = vocabulary ?? new SparkPersonalVocabulary();
        _sendMessageAsync = sendMessageAsync ?? throw new ArgumentNullException(nameof(sendMessageAsync));
        _playSpeechAsync = playSpeechAsync ?? throw new ArgumentNullException(nameof(playSpeechAsync));
        _stopPlayback = stopPlayback ?? throw new ArgumentNullException(nameof(stopPlayback));

        _wakeWordDetector.WakeWordDetected += OnWakeWordDetected;
    }

    /// <summary>
    /// Starts the hands-free voice loop.
    /// If hands-free is enabled, enters WaitingForWakeWord; otherwise enters Listening.
    /// </summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SparkVoiceAssistantLoop));
            if (_state != VoiceAssistantState.Idle) return;

            _activeLoopCts = new CancellationTokenSource();
            _wakeWordDetector.Reset();

            var nextState = _config.HandsFreeEnabled
                ? VoiceAssistantState.WaitingForWakeWord
                : VoiceAssistantState.Listening;

            TransitionTo(nextState);
        }
    }

    /// <summary>
    /// Stops the hands-free voice loop and cancels any pending audio/chat turn.
    /// </summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_disposed || _state == VoiceAssistantState.Idle) return;

            CancelSpeakingLocked();
            _activeLoopCts?.Cancel();
            _activeLoopCts?.Dispose();
            _activeLoopCts = null;

            _stopPlayback();
            TransitionTo(VoiceAssistantState.Idle);
        }
    }

    /// <summary>
    /// Triggered when external audio capture delivers raw audio samples to the loop.
    /// Feeds samples to the wake word detector when in WaitingForWakeWord state.
    /// </summary>
    public void ProcessAudioSamples(ReadOnlySpan<float> samples)
    {
        lock (_gate)
        {
            if (_disposed || _state != VoiceAssistantState.WaitingForWakeWord) return;
        }

        _wakeWordDetector.ProcessSamples(samples);
    }

    /// <summary>
    /// Informs the loop that user speech was detected (e.g. from VAD).
    /// Used for barge-in interruption during AssistantSpeaking.
    /// </summary>
    public void NotifyUserSpeechStarted()
    {
        lock (_gate)
        {
            if (_disposed) return;

            if (_state == VoiceAssistantState.AssistantSpeaking && _config.EnableBargeIn)
            {
                // BARGE-IN DETECTED: Abort assistant TTS playback immediately!
                CancelSpeakingLocked();
                _stopPlayback();
                TransitionTo(VoiceAssistantState.Interrupted);
                BargeInDetected?.Invoke();

                // Seamlessly switch to Listening to capture the interrupting user query
                TransitionTo(VoiceAssistantState.Listening);
            }
        }
    }

    /// <summary>
    /// Called when speech-to-text finishes transcribing an utterance.
    /// Normalizes the text via SparkPersonalVocabulary and dispatches it to the Chat Engine.
    /// </summary>
    public async Task ProcessCompletedUtteranceAsync(string rawUtterance)
    {
        if (string.IsNullOrWhiteSpace(rawUtterance))
        {
            lock (_gate)
            {
                if (_state == VoiceAssistantState.Listening)
                {
                    TransitionTo(_config.HandsFreeEnabled ? VoiceAssistantState.WaitingForWakeWord : VoiceAssistantState.Idle);
                }
            }
            return;
        }

        // Apply personal vocabulary, aliases, and technical term normalization
        var normalizedUtterance = _vocabulary.NormalizeTranscript(rawUtterance);
        UtteranceTranscribed?.Invoke(normalizedUtterance);

        CancellationToken loopToken;
        lock (_gate)
        {
            if (_disposed || _state == VoiceAssistantState.Idle) return;
            TransitionTo(VoiceAssistantState.ProcessingTurn);
            loopToken = _activeLoopCts?.Token ?? CancellationToken.None;
        }

        try
        {
            // Send user message to Chat Engine
            var responseText = await _sendMessageAsync(normalizedUtterance).ConfigureAwait(false);
            AssistantResponseReceived?.Invoke(responseText);

            if (loopToken.IsCancellationRequested) return;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                ResumeListeningOrWakeWord();
                return;
            }

            // Begin assistant speech playback
            CancellationToken speakingToken;
            lock (_gate)
            {
                if (_disposed || loopToken.IsCancellationRequested) return;
                CancelSpeakingLocked();
                _speakingCts = CancellationTokenSource.CreateLinkedTokenSource(loopToken);
                speakingToken = _speakingCts.Token;
                TransitionTo(VoiceAssistantState.AssistantSpeaking);
            }

            try
            {
                await _playSpeechAsync(responseText, speakingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Speaking was canceled by barge-in or stop; do not fault
                return;
            }

            // Assistant playback finished naturally
            lock (_gate)
            {
                if (_disposed || loopToken.IsCancellationRequested) return;
                if (_state == VoiceAssistantState.AssistantSpeaking)
                {
                    if (_config.AutoResumeListening)
                    {
                        TransitionTo(_config.HandsFreeEnabled ? VoiceAssistantState.WaitingForWakeWord : VoiceAssistantState.Idle);
                    }
                    else
                    {
                        TransitionTo(VoiceAssistantState.Idle);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                TransitionTo(VoiceAssistantState.Faulted);
            }
            LoopFaulted?.Invoke(ex);
        }
    }

    private void OnWakeWordDetected(WakeWordDetectionResult result)
    {
        lock (_gate)
        {
            if (_disposed || _state != VoiceAssistantState.WaitingForWakeWord) return;

            WakeWordTriggered?.Invoke(result.DetectedPhrase);
            TransitionTo(VoiceAssistantState.Listening);
        }
    }

    private void ResumeListeningOrWakeWord()
    {
        lock (_gate)
        {
            if (_disposed) return;
            TransitionTo(_config.HandsFreeEnabled ? VoiceAssistantState.WaitingForWakeWord : VoiceAssistantState.Idle);
        }
    }

    private void CancelSpeakingLocked()
    {
        if (_speakingCts != null)
        {
            try
            {
                _speakingCts.Cancel();
                _speakingCts.Dispose();
            }
            catch { }
            _speakingCts = null;
        }
    }

    private void TransitionTo(VoiceAssistantState newState)
    {
        if (_state == newState) return;
        _state = newState;
        StateChanged?.Invoke(newState);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _wakeWordDetector.WakeWordDetected -= OnWakeWordDetected;
            CancelSpeakingLocked();
            _activeLoopCts?.Cancel();
            _activeLoopCts?.Dispose();
            _activeLoopCts = null;
            _stopPlayback();
            _state = VoiceAssistantState.Idle;
        }
    }
}
