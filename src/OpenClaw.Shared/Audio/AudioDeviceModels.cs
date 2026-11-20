using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Audio;

/// <summary>Type of audio endpoint.</summary>
public enum AudioDeviceType
{
    Microphone = 0,
    Speaker = 1,
    Virtual = 2
}

/// <summary>Status of an audio device.</summary>
public enum AudioDeviceState
{
    Active = 0,
    Disabled = 1,
    Unplugged = 2,
    NotPresent = 3
}

/// <summary>Metadata representing an audio capture or render device.</summary>
public sealed record AudioDeviceInfo(
    string Id,
    string Name,
    AudioDeviceType DeviceType,
    bool IsDefault = false,
    AudioDeviceState State = AudioDeviceState.Active,
    int PreferredSampleRate = 16000,
    int Channels = 1)
{
    public override string ToString() => $"{Name} ({DeviceType}, {(IsDefault ? "Default" : "Custom")})";
}
