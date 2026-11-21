using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Divisions;

namespace OpenClaw.Shared.VoiceMode;

/// <summary>
/// Implementation of <see cref="IVoiceModeDivisionService"/> coordinating the
/// Voice Mode runtime in Python and enforcing strict Voice Mode isolation.
/// </summary>
public sealed class VoiceModeDivisionService : IVoiceModeDivisionService, IDisposable
{
    private readonly IDivisionManager _divisionManager;
    private readonly VoiceModeCapabilityGuard _guard;
    private readonly SemaphoreSlim _runtimeLock = new(1, 1);
    private Process? _ipcProcess;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private readonly string _runtimeDirectory;

    public VoiceModeCapabilityGuard Guard => _guard;
    public bool IsRuntimeRunning => _ipcProcess != null && !_ipcProcess.HasExited;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    public event EventHandler<string>? LogMessageAppended;

    public VoiceModeDivisionService(IDivisionManager divisionManager)
    {
        _divisionManager = divisionManager ?? throw new ArgumentNullException(nameof(divisionManager));
        _guard = new VoiceModeCapabilityGuard(divisionManager);

        _divisionManager.DivisionChanged += OnDivisionChanged;

        // Resolve runtime directory
        var baseDir = AppContext.BaseDirectory;
        // Check local development path or relative path
        var candidate = Path.Combine(baseDir, "VoiceMode", "runtime");
        if (!Directory.Exists(candidate))
        {
            candidate = @"C:\Intel\Spark\Spark-main\src\OpenClaw.Shared\VoiceMode\runtime";
        }
        _runtimeDirectory = candidate;
    }

    private void OnDivisionChanged(object? sender, DivisionChangedEventArgs e)
    {
        _guard.HandleDivisionChanged(e.PreviousDivision, e.ActiveDivision);
        if (e.ActiveDivision != SparkDivision.VoiceMode)
        {
            // Update active division inside python process if alive
            if (IsRuntimeRunning)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendCommandRawAsync("set_division", new() { ["division"] = e.ActiveDivision.ToString().ToLowerInvariant() }, CancellationToken.None);
                    }
                    catch { }
                });
            }
        }
    }

    public async Task EnsureRuntimeStartedAsync(CancellationToken ct = default)
    {
        if (IsRuntimeRunning) return;

        await _runtimeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsRuntimeRunning) return;

            var scriptPath = Path.Combine(_runtimeDirectory, "ipc_server.py");
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"Voice Mode runtime script not found at {scriptPath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" --server",
                WorkingDirectory = _runtimeDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Propagate environment variables
            var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            if (!string.IsNullOrEmpty(geminiKey))
            {
                startInfo.EnvironmentVariables["GEMINI_API_KEY"] = geminiKey;
            }

            var openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            if (!string.IsNullOrEmpty(openRouterKey))
            {
                startInfo.EnvironmentVariables["OPENROUTER_API_KEY"] = openRouterKey;
            }

            _ipcProcess = new Process { StartInfo = startInfo };
            _ipcProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ErrorReceived?.Invoke(this, e.Data);
                    LogMessageAppended?.Invoke(this, $"[ERR] {e.Data}");
                }
            };

            _ipcProcess.Start();
            _ipcProcess.BeginErrorReadLine();
            _stdin = _ipcProcess.StandardInput;
            _stdout = _ipcProcess.StandardOutput;

            LogMessageAppended?.Invoke(this, "[VoiceMode] Runtime backend started.");
        }
        finally
        {
            _runtimeLock.Release();
        }
    }

    public async Task<VoiceModeCommandResult> SendCommandAsync(string command, Dictionary<string, object?>? parameters = null, CancellationToken ct = default)
    {
        // Enforce strict Voice Mode isolation barrier
        if (!_guard.CanExecute(command, out var rejectionReason))
        {
            LogMessageAppended?.Invoke(this, $"[VoiceModeCapabilityGuard] REJECTED: {rejectionReason}");
            return new VoiceModeCommandResult(false, null, rejectionReason, null);
        }

        // Validate destructive/safety actions
        var stringParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                stringParams[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
            }
        }

        var authorized = await _guard.VerifyActionSafetyAsync(command, stringParams, ct).ConfigureAwait(false);
        if (!authorized)
        {
            return new VoiceModeCommandResult(false, null, "User rejected or declined approval for high-impact action.", null);
        }

        return await SendCommandRawAsync(command, parameters, ct).ConfigureAwait(false);
    }

    private async Task<VoiceModeCommandResult> SendCommandRawAsync(string command, Dictionary<string, object?>? parameters, CancellationToken ct)
    {
        await EnsureRuntimeStartedAsync(ct).ConfigureAwait(false);

        var divisionKey = _divisionManager.ActiveDivision switch
        {
            SparkDivision.VoiceMode => "voice-mode",
            SparkDivision.Chat => "chat",
            SparkDivision.Work => "work",
            SparkDivision.Code => "code",
            SparkDivision.Study => "study",
            SparkDivision.Creative => "creative",
            _ => "unknown"
        };

        var payload = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["division"] = divisionKey,
            ["params"] = parameters ?? new Dictionary<string, object?>()
        };

        var json = JsonSerializer.Serialize(payload);

        await _runtimeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_stdin == null || _stdout == null)
            {
                return new VoiceModeCommandResult(false, null, "Runtime IPC stream unavailable", null);
            }

            await _stdin.WriteLineAsync(json.AsMemory(), ct).ConfigureAwait(false);
            await _stdin.FlushAsync(ct).ConfigureAwait(false);

            var responseLine = await _stdout.ReadLineAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                return new VoiceModeCommandResult(false, null, "Empty response from Voice Mode runtime", null);
            }

            OutputReceived?.Invoke(this, responseLine);

            using var doc = JsonDocument.Parse(responseLine);
            var root = doc.RootElement;
            bool success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
            string? result = root.TryGetProperty("result", out var rProp) ? rProp.ToString() : null;
            string? error = root.TryGetProperty("error", out var eProp) ? eProp.GetString() : null;

            return new VoiceModeCommandResult(success, result, error, responseLine);
        }
        catch (Exception ex)
        {
            return new VoiceModeCommandResult(false, null, ex.Message, null);
        }
        finally
        {
            _runtimeLock.Release();
        }
    }

    public Task<VoiceModeCommandResult> ScreenCaptureAsync(CancellationToken ct = default) =>
        SendCommandAsync("screen_capture", null, ct);

    public Task<VoiceModeCommandResult> VisualQuestionAnsweringAsync(string query, string mode = "screen", CancellationToken ct = default) =>
        SendCommandAsync("visual_qa", new() { ["query"] = query, ["mode"] = mode }, ct);

    public Task<VoiceModeCommandResult> ExecuteComputerSettingAsync(string action, object? value = null, CancellationToken ct = default) =>
        SendCommandAsync("computer_settings", new() { ["action"] = action, ["value"] = value }, ct);

    public Task<VoiceModeCommandResult> ExecuteComputerControlAsync(Dictionary<string, object?> parameters, CancellationToken ct = default) =>
        SendCommandAsync("computer_control", parameters, ct);

    public Task<VoiceModeCommandResult> OpenApplicationAsync(string appName, CancellationToken ct = default) =>
        SendCommandAsync("open_app", new() { ["app_name"] = appName }, ct);

    public Task<VoiceModeCommandResult> BrowserControlAsync(string action, Dictionary<string, object?>? parameters = null, CancellationToken ct = default)
    {
        var dict = parameters ?? new Dictionary<string, object?>();
        dict["action"] = action;
        return SendCommandAsync("browser_control", dict, ct);
    }

    public Task<VoiceModeCommandResult> FileControllerAsync(string action, string path, CancellationToken ct = default) =>
        SendCommandAsync("file_controller", new() { ["action"] = action, ["path"] = path }, ct);

    public Task<VoiceModeCommandResult> SubmitAutonomousTaskAsync(string goal, CancellationToken ct = default) =>
        SendCommandAsync("submit_task", new() { ["goal"] = goal }, ct);

    public Task<VoiceModeCommandResult> GetAutonomousTaskStatusAsync(string taskId, CancellationToken ct = default) =>
        SendCommandAsync("get_task_status", new() { ["task_id"] = taskId }, ct);

    public Task<VoiceModeCommandResult> LoadMemoryAsync(CancellationToken ct = default) =>
        SendCommandAsync("load_memory", null, ct);

    public Task<VoiceModeCommandResult> UpdateMemoryAsync(Dictionary<string, object?> memoryData, CancellationToken ct = default) =>
        SendCommandAsync("update_memory", new() { ["data"] = memoryData }, ct);

    public Task<VoiceModeCommandResult> ProcessFileAsync(string filePath, string query = "analyze", CancellationToken ct = default) =>
        SendCommandAsync("process_file", new() { ["file_path"] = filePath, ["query"] = query }, ct);

    public Task LaunchVoiceModeUiAsync(CancellationToken ct = default)
    {
        _guard.AssertVoiceModeActive("LaunchVoiceModeUi");

        var scriptPath = Path.Combine(_runtimeDirectory, "main.py");
        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{scriptPath}\"",
            WorkingDirectory = _runtimeDirectory,
            UseShellExecute = true
        };

        Process.Start(psi);
        LogMessageAppended?.Invoke(this, "[VoiceMode] Adaptive UI launched.");
        return Task.CompletedTask;
    }

    public void ShutdownRuntime()
    {
        try
        {
            if (_ipcProcess != null && !_ipcProcess.HasExited)
            {
                _ipcProcess.Kill(entireProcessTree: true);
                _ipcProcess.Dispose();
                _ipcProcess = null;
            }
        }
        catch { }
    }

    public void Dispose()
    {
        ShutdownRuntime();
        _runtimeLock.Dispose();
    }
}
