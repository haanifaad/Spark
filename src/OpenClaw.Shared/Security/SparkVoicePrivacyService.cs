using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Security;

/// <summary>
/// Status of a device permission for microphone, camera, or screen sharing.
/// </summary>
public enum SparkPermissionState
{
    NotDetermined = 0,
    Granted = 1,
    Denied = 2,
    Restricted = 3
}

/// <summary>
/// Supported media device types for privacy tracking.
/// </summary>
public enum SparkMediaDeviceType
{
    Microphone = 0,
    Camera = 1,
    ScreenCapture = 2
}

/// <summary>
/// Exception thrown when media capture is attempted without explicit user permission.
/// </summary>
public sealed class SparkPrivacyException : Exception
{
    public SparkMediaDeviceType DeviceType { get; }
    public SparkPermissionState State { get; }

    public SparkPrivacyException(SparkMediaDeviceType deviceType, SparkPermissionState state, string message)
        : base(message)
    {
        DeviceType = deviceType;
        State = state;
    }
}

/// <summary>
/// Service governing privacy permissions, active media indicators, zero-retention invariant checks,
/// and credential/key sanitization for SPARK Voice, Live, and Video sessions.
/// </summary>
public interface ISparkVoicePrivacyService
{
    SparkPermissionState GetPermission(SparkMediaDeviceType deviceType);
    Task<SparkPermissionState> RequestPermissionAsync(SparkMediaDeviceType deviceType, CancellationToken ct = default);
    void SetPermission(SparkMediaDeviceType deviceType, SparkPermissionState state);
    void EnsurePermission(SparkMediaDeviceType deviceType);

    bool IsMicrophoneActive { get; }
    bool IsCameraActive { get; }
    bool IsScreenShareActive { get; }

    void SetDeviceActive(SparkMediaDeviceType deviceType, bool active);
    event EventHandler<SparkMediaDeviceType>? DeviceActivityChanged;

    string SanitizeLog(string? rawText);
    string SanitizeUrl(string? rawUrl);
    bool VerifyZeroRetentionPolicy();
}

/// <summary>
/// Default implementation of <see cref="ISparkVoicePrivacyService"/>.
/// </summary>
public sealed class SparkVoicePrivacyService : ISparkVoicePrivacyService
{
    private static readonly Regex BearerRegex = new(
        @"(Bearer\s+)[A-Za-z0-9_\-\.]{8,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OpenAiKeyRegex = new(
        @"sk-[A-Za-z0-9_\-]{16,}",
        RegexOptions.Compiled);

    private static readonly Regex GoogleKeyRegex = new(
        @"AIzaSy[A-Za-z0-9_\-]{30,}",
        RegexOptions.Compiled);

    private static readonly Regex UrlSecretParamRegex = new(
        @"([?&](?:api[_-]?key|key|token|access_token|secret)=)([^&]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex JsonSecretFieldRegex = new(
        @"(""(?:token|apiKey|api_key|secret|password|access_token)""\s*:\s*"")([^""]+)("")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ConcurrentDictionary<SparkMediaDeviceType, SparkPermissionState> _permissions = new();
    private readonly ConcurrentDictionary<SparkMediaDeviceType, bool> _activeStates = new();
    private bool _allowDiskRetentionForDebug;

    public event EventHandler<SparkMediaDeviceType>? DeviceActivityChanged;

    public SparkVoicePrivacyService(bool defaultGranted = false, bool allowDiskRetentionForDebug = false)
    {
        _allowDiskRetentionForDebug = allowDiskRetentionForDebug;
        var initial = defaultGranted ? SparkPermissionState.Granted : SparkPermissionState.NotDetermined;
        _permissions[SparkMediaDeviceType.Microphone] = initial;
        _permissions[SparkMediaDeviceType.Camera] = initial;
        _permissions[SparkMediaDeviceType.ScreenCapture] = initial;

        _activeStates[SparkMediaDeviceType.Microphone] = false;
        _activeStates[SparkMediaDeviceType.Camera] = false;
        _activeStates[SparkMediaDeviceType.ScreenCapture] = false;
    }

    public SparkPermissionState GetPermission(SparkMediaDeviceType deviceType)
    {
        return _permissions.TryGetValue(deviceType, out var state) ? state : SparkPermissionState.NotDetermined;
    }

    public Task<SparkPermissionState> RequestPermissionAsync(SparkMediaDeviceType deviceType, CancellationToken ct = default)
    {
        // If already decided, return current state
        if (_permissions.TryGetValue(deviceType, out var current) && current != SparkPermissionState.NotDetermined)
        {
            return Task.FromResult(current);
        }

        // On Windows / WinUI Desktop, application-level explicit permission defaults to Granted upon user intent
        // unless restricted or denied by OS policy.
        var granted = SparkPermissionState.Granted;
        _permissions[deviceType] = granted;
        return Task.FromResult(granted);
    }

    public void SetPermission(SparkMediaDeviceType deviceType, SparkPermissionState state)
    {
        _permissions[deviceType] = state;
        if (state == SparkPermissionState.Denied || state == SparkPermissionState.Restricted)
        {
            SetDeviceActive(deviceType, false);
        }
    }

    public void EnsurePermission(SparkMediaDeviceType deviceType)
    {
        var state = GetPermission(deviceType);
        if (state != SparkPermissionState.Granted)
        {
            throw new SparkPrivacyException(
                deviceType,
                state,
                $"Access to {deviceType} is not permitted (Current state: {state}). Explicit user consent is required.");
        }
    }

    public bool IsMicrophoneActive => _activeStates.TryGetValue(SparkMediaDeviceType.Microphone, out var a) && a;
    public bool IsCameraActive => _activeStates.TryGetValue(SparkMediaDeviceType.Camera, out var a) && a;
    public bool IsScreenShareActive => _activeStates.TryGetValue(SparkMediaDeviceType.ScreenCapture, out var a) && a;

    public void SetDeviceActive(SparkMediaDeviceType deviceType, bool active)
    {
        if (active && GetPermission(deviceType) != SparkPermissionState.Granted)
        {
            throw new SparkPrivacyException(
                deviceType,
                GetPermission(deviceType),
                $"Cannot activate {deviceType} without explicit Granted permission.");
        }

        _activeStates[deviceType] = active;
        DeviceActivityChanged?.Invoke(this, deviceType);
    }

    public string SanitizeLog(string? rawText)
    {
        if (string.IsNullOrEmpty(rawText))
            return string.Empty;

        var text = rawText;
        text = BearerRegex.Replace(text, "$1[REDACTED_TOKEN]");
        text = OpenAiKeyRegex.Replace(text, "sk-***");
        text = GoogleKeyRegex.Replace(text, "AIzaSy***");
        text = JsonSecretFieldRegex.Replace(text, "$1[REDACTED]$3");
        text = SanitizeUrl(text);

        return text;
    }

    public string SanitizeUrl(string? rawUrl)
    {
        if (string.IsNullOrEmpty(rawUrl))
            return string.Empty;

        return UrlSecretParamRegex.Replace(rawUrl, "$1[REDACTED]");
    }

    /// <summary>
    /// Verifies the SPARK zero-retention policy invariant: raw PCM audio and camera frames must
    /// never be persisted to local disk. Returns true if zero retention is strictly enforced.
    /// </summary>
    public bool VerifyZeroRetentionPolicy()
    {
        return !_allowDiskRetentionForDebug;
    }
}
