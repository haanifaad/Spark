using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.VoiceMode;

/// <summary>
/// Service contract for the SPARK Voice Mode division.
/// Exposes real-time voice, visual awareness, computer control, autonomous tasks,
/// persistent memory, and file intelligence while strictly enforcing division boundary rules.
/// </summary>
public interface IVoiceModeDivisionService
{
    VoiceModeCapabilityGuard Guard { get; }
    bool IsRuntimeRunning { get; }

    string CurrentVisualMode { get; set; }
    string CurrentModel { get; set; }

    event EventHandler<string>? OutputReceived;
    event EventHandler<string>? ErrorReceived;
    event EventHandler<string>? LogMessageAppended;

    Task EnsureRuntimeStartedAsync(CancellationToken ct = default);
    Task<VoiceModeCommandResult> SendCommandAsync(string command, Dictionary<string, object?>? parameters = null, CancellationToken ct = default);

    Task<VoiceModeCommandResult> GetStateAsync(CancellationToken ct = default);
    Task<VoiceModeCommandResult> SetVisualModeAsync(string mode, CancellationToken ct = default);
    Task<VoiceModeCommandResult> SetModelAsync(string model, CancellationToken ct = default);
    Task<VoiceModeCommandResult> VoiceInteractAsync(string prompt, CancellationToken ct = default);
    Task<VoiceModeCommandResult> InterruptVoiceAsync(CancellationToken ct = default);

    Task<VoiceModeCommandResult> ScreenCaptureAsync(CancellationToken ct = default);
    Task<VoiceModeCommandResult> VisualQuestionAnsweringAsync(string query, string mode = "screen", CancellationToken ct = default);
    Task<VoiceModeCommandResult> ExecuteComputerSettingAsync(string action, object? value = null, CancellationToken ct = default);
    Task<VoiceModeCommandResult> ExecuteComputerControlAsync(Dictionary<string, object?> parameters, CancellationToken ct = default);
    Task<VoiceModeCommandResult> ExecuteUiTarsAsync(string instruction, CancellationToken ct = default);
    Task<VoiceModeCommandResult> OpenApplicationAsync(string appName, CancellationToken ct = default);
    Task<VoiceModeCommandResult> BrowserControlAsync(string action, Dictionary<string, object?>? parameters = null, CancellationToken ct = default);
    Task<VoiceModeCommandResult> FileControllerAsync(string action, string path, CancellationToken ct = default);
    Task<VoiceModeCommandResult> SubmitAutonomousTaskAsync(string goal, CancellationToken ct = default);
    Task<VoiceModeCommandResult> GetAutonomousTaskStatusAsync(string taskId, CancellationToken ct = default);
    Task<VoiceModeCommandResult> GetReasoningStateAsync(CancellationToken ct = default);
    Task<VoiceModeCommandResult> LoadMemoryAsync(CancellationToken ct = default);
    Task<VoiceModeCommandResult> UpdateMemoryAsync(Dictionary<string, object?> memoryData, CancellationToken ct = default);
    Task<VoiceModeCommandResult> ProcessFileAsync(string filePath, string query = "analyze", CancellationToken ct = default);

    Task<VoiceModeCommandResult> EmailAssistantAsync(string action, Dictionary<string, object?>? parameters = null, CancellationToken ct = default);
    Task<VoiceModeCommandResult> SendMessageAsync(string receiver, string messageText, string platform = "whatsapp", bool confirmed = false, string action = "prepare", CancellationToken ct = default);
    Task<VoiceModeCommandResult> ScheduleAssistantAsync(string action, Dictionary<string, object?>? parameters = null, CancellationToken ct = default);

    Task LaunchVoiceModeUiAsync(CancellationToken ct = default);
    void ShutdownRuntime();
}

public sealed record VoiceModeCommandResult(
    bool Success,
    string? Result,
    string? Error,
    string? RawJson);
