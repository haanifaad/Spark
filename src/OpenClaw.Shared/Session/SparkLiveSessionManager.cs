using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Audio.Live;
using OpenClaw.Shared.Video;

namespace OpenClaw.Shared.Session;

/// <summary>
/// Sink interface for synchronizing live session turns into conversation history.
/// </summary>
public interface ISparkConversationTurnSyncTarget
{
    Task SyncTurnAsync(
        string conversationId,
        string role,
        string text,
        DateTime timestamp,
        bool isInterrupted,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Central manager for SPARK Live and Video Call sessions.
/// Enforces single-active-session invariant and ensures conversational turns are
/// faithfully synchronized to persistent Chat history.
/// </summary>
public sealed class SparkLiveSessionManager : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly ISparkConversationTurnSyncTarget? _syncTarget;
    private SparkLiveSession? _activeSession;
    private SparkVideoCallController? _activeVideoCall;
    private string? _activeConversationId;
    private bool _disposed;

    public bool HasActiveSession
    {
        get
        {
            lock (_gate)
            {
                return _activeSession != null || _activeVideoCall != null;
            }
        }
    }

    public string? ActiveConversationId
    {
        get
        {
            lock (_gate) return _activeConversationId;
        }
    }

    public SparkLiveSession? ActiveSession
    {
        get
        {
            lock (_gate) return _activeSession;
        }
    }

    public SparkVideoCallController? ActiveVideoCall
    {
        get
        {
            lock (_gate) return _activeVideoCall;
        }
    }

    public event Action? SessionStarted;
    public event Action? SessionEnded;
    public event Action<string, SparkLiveTurn>? TurnSynchronized;

    public SparkLiveSessionManager(ISparkConversationTurnSyncTarget? syncTarget = null)
    {
        _syncTarget = syncTarget;
    }

    /// <summary>
    /// Registers and activates a SPARK Live audio session.
    /// Enforces the single-active-session invariant.
    /// </summary>
    public async Task RegisterLiveSessionAsync(
        SparkLiveSession session,
        string? conversationId = null,
        bool forceSwap = false)
    {
        ArgumentNullException.ThrowIfNull(session);

        SparkLiveSession? oldSession = null;
        SparkVideoCallController? oldVideoCall = null;

        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SparkLiveSessionManager));

            if (HasActiveSession)
            {
                if (!forceSwap)
                {
                    throw new InvalidOperationException("Another SPARK Live or Video Call session is already active.");
                }

                oldSession = _activeSession;
                oldVideoCall = _activeVideoCall;
                _activeSession = null;
                _activeVideoCall = null;
            }

            _activeSession = session;
            _activeConversationId = conversationId ?? Guid.NewGuid().ToString("N");
            AttachLiveSessionEvents(session);
        }

        if (oldVideoCall != null)
        {
            await oldVideoCall.EndCallAsync().ConfigureAwait(false);
            await oldVideoCall.DisposeAsync().ConfigureAwait(false);
        }
        else if (oldSession != null)
        {
            await oldSession.DisconnectAsync().ConfigureAwait(false);
            await oldSession.DisposeAsync().ConfigureAwait(false);
        }

        SessionStarted?.Invoke();
    }

    /// <summary>
    /// Registers and activates a dedicated AI Video Call session.
    /// Enforces the single-active-session invariant.
    /// </summary>
    public async Task RegisterVideoCallAsync(
        SparkVideoCallController videoCall,
        string? conversationId = null,
        bool forceSwap = false)
    {
        ArgumentNullException.ThrowIfNull(videoCall);

        SparkLiveSession? oldSession = null;
        SparkVideoCallController? oldVideoCall = null;

        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SparkLiveSessionManager));

            if (HasActiveSession)
            {
                if (!forceSwap)
                {
                    throw new InvalidOperationException("Another SPARK Live or Video Call session is already active.");
                }

                oldSession = _activeSession;
                oldVideoCall = _activeVideoCall;
                _activeSession = null;
                _activeVideoCall = null;
            }

            _activeVideoCall = videoCall;
            _activeSession = videoCall.LiveSession;
            _activeConversationId = conversationId ?? Guid.NewGuid().ToString("N");
            AttachVideoCallEvents(videoCall);
        }

        if (oldVideoCall != null)
        {
            await oldVideoCall.EndCallAsync().ConfigureAwait(false);
            await oldVideoCall.DisposeAsync().ConfigureAwait(false);
        }
        else if (oldSession != null)
        {
            await oldSession.DisconnectAsync().ConfigureAwait(false);
            await oldSession.DisposeAsync().ConfigureAwait(false);
        }

        SessionStarted?.Invoke();
    }

    /// <summary>
    /// Cleanly ends the active live session or video call, flushing any turns to persistent history.
    /// </summary>
    public async Task EndActiveSessionAsync()
    {
        SparkLiveSession? sessionToEnd = null;
        SparkVideoCallController? videoToEnd = null;

        lock (_gate)
        {
            if (!HasActiveSession) return;

            sessionToEnd = _activeSession;
            videoToEnd = _activeVideoCall;

            _activeSession = null;
            _activeVideoCall = null;
            _activeConversationId = null;

            if (videoToEnd != null) DetachVideoCallEvents(videoToEnd);
            if (sessionToEnd != null) DetachLiveSessionEvents(sessionToEnd);
        }

        try
        {
            if (videoToEnd != null)
            {
                await videoToEnd.EndCallAsync().ConfigureAwait(false);
                await videoToEnd.DisposeAsync().ConfigureAwait(false);
            }
            else if (sessionToEnd != null)
            {
                await sessionToEnd.DisconnectAsync().ConfigureAwait(false);
                await sessionToEnd.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            SessionEnded?.Invoke();
        }
    }

    private void AttachLiveSessionEvents(SparkLiveSession session)
    {
        session.TurnCompleted += OnTurnCompleted;
    }

    private void DetachLiveSessionEvents(SparkLiveSession session)
    {
        session.TurnCompleted -= OnTurnCompleted;
    }

    private void AttachVideoCallEvents(SparkVideoCallController videoCall)
    {
        videoCall.CallEnded += OnCallEndedInternal;
        AttachLiveSessionEvents(videoCall.LiveSession);
    }

    private void DetachVideoCallEvents(SparkVideoCallController videoCall)
    {
        videoCall.CallEnded -= OnCallEndedInternal;
        DetachLiveSessionEvents(videoCall.LiveSession);
    }

    private void OnCallEndedInternal()
    {
        _ = EndActiveSessionAsync();
    }

    private void OnTurnCompleted(SparkLiveTurn turn)
    {
        string? targetConvoId;
        lock (_gate)
        {
            targetConvoId = _activeConversationId;
        }

        if (targetConvoId == null || string.IsNullOrWhiteSpace(turn.Text)) return;

        TurnSynchronized?.Invoke(targetConvoId, turn);

        if (_syncTarget != null)
        {
            _ = _syncTarget.SyncTurnAsync(
                targetConvoId,
                turn.Role,
                turn.Text,
                turn.StartTime,
                isInterrupted: false,
                CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        await EndActiveSessionAsync().ConfigureAwait(false);
    }
}
