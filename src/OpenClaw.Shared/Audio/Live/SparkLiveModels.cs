using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Audio.Live;

/// <summary>
/// Lifecycle states for a realtime bidirectional SPARK Live session.
/// </summary>
public enum SparkLiveState
{
    Disconnected,
    Connecting,
    ConnectedIdle,
    UserSpeaking,
    ProcessingTurn,
    AssistantSpeaking,
    Interrupted,
    Reconnecting,
    Closed,
    Faulted
}

/// <summary>
/// Configuration options for SPARK Live session.
/// </summary>
public sealed class SparkLiveSessionOptions
{
    public int SampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1;
    public int FrameDurationMs { get; set; } = 100;
    public bool EnableBargeIn { get; set; } = true;
    public int MaxReconnectAttempts { get; set; } = 3;
    public TimeSpan ReconnectBackoff { get; set; } = TimeSpan.FromMilliseconds(800);
    public string ModelId { get; set; } = "gemini-2.0-flash-realtime";
}

/// <summary>
/// An audio chunk in the realtime live bidirectional stream.
/// </summary>
public sealed record SparkLiveAudioChunk(
    byte[] Data,
    long TimestampMs,
    int SampleRate = 16000,
    int Channels = 1,
    bool IsFinal = false);

/// <summary>
/// A conversational turn in the live session (committed to history).
/// </summary>
public sealed class SparkLiveTurn
{
    public required string TurnId { get; init; }
    public required string Role { get; init; } // "user" or "assistant"
    public string Text { get; set; } = "";
    public DateTime StartTime { get; init; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public bool IsComplete { get; set; }
}
