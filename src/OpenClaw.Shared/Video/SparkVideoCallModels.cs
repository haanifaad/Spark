using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Video;

/// <summary>
/// Lifecycle state of a dedicated AI Video Call.
/// </summary>
public enum VideoCallState
{
    Idle,
    Connecting,
    Connected,
    Reconnecting,
    Ended,
    Faulted
}

/// <summary>
/// Configuration options for launching an AI Video Call session.
/// </summary>
public sealed class SparkVideoCallSessionOptions
{
    public bool StartWithCameraOn { get; set; } = true;
    public bool StartWithMicMuted { get; set; } = false;
    public bool EnableScreenShare { get; set; } = false;
    public string ModelId { get; set; } = "gemini-2.0-flash-realtime";
}

/// <summary>
/// An entry in the live video call transcript rail.
/// </summary>
public sealed class VideoCallTranscriptEntry
{
    public required string Id { get; init; }
    public required string Speaker { get; init; } // "User" or "SPARK"
    public string Text { get; set; } = "";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public bool IsInterrupted { get; set; }
}
