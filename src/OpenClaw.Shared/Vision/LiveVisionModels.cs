using System;

namespace OpenClaw.Shared.Vision;

/// <summary>
/// Information about a detected video camera device.
/// </summary>
public sealed record CameraDeviceInfo(
    string DeviceId,
    string Name,
    bool IsDefault = false,
    int PreferredWidth = 1280,
    int PreferredHeight = 720,
    int PreferredFps = 30);

/// <summary>
/// Desired resolution profile for camera capture.
/// </summary>
public enum CameraResolutionProfile
{
    StandardDef480p, // 640x480 - bandwidth-optimized
    HighDef720p,     // 1280x720 - standard AI multimodal
    FullHd1080p      // 1920x1080 - high detail
}

/// <summary>
/// Options for camera capture and frame throttling.
/// </summary>
public sealed class CameraCaptureOptions
{
    public string? PreferredDeviceId { get; set; }
    public CameraResolutionProfile Resolution { get; set; } = CameraResolutionProfile.HighDef720p;

    /// <summary>Target frame rate for model feeding (e.g. 1 frame / 1.5s = 0.67 fps).</summary>
    public float ModelInputFps { get; set; } = 0.67f;

    /// <summary>JPEG compression quality 1..100.</summary>
    public int JpegQuality { get; set; } = 80;
}

/// <summary>
/// A single video frame captured from camera or screen, timestamped and encoded for AI model consumption.
/// </summary>
public sealed class LiveVideoFrame
{
    public required byte[] JpegBytes { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required long TimestampMs { get; init; }
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Generates a data URL for inclusion in multimodal chat/live payloads.
    /// </summary>
    public string ToBase64DataUrl() => $"data:image/jpeg;base64,{Convert.ToBase64String(JpegBytes)}";

    /// <summary>
    /// Generates a base64 string without data scheme.
    /// </summary>
    public string ToBase64() => Convert.ToBase64String(JpegBytes);
}
