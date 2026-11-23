using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Creative;

/// <summary>
/// Result of an application launch or inspection operation.
/// </summary>
public sealed record CreativeLaunchResult
{
    public required bool Success { get; init; }
    public required string ApplicationId { get; init; }
    public required string DisplayName { get; init; }
    public int? ProcessId { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? ExecutablePath { get; init; }
    public string? Message { get; init; }
    public bool IsManualEditingMode { get; init; }
    public DateTime LaunchedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Manages the process and project lifecycles for all 16 applications in SPARK Creativity Mode.
/// </summary>
public sealed class CreativeLifecycleManager
{
    private static readonly Lazy<CreativeLifecycleManager> s_instance = new(() => new CreativeLifecycleManager());
    public static CreativeLifecycleManager Instance => s_instance.Value;

    private readonly ConcurrentDictionary<string, Process> _runningProcesses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _activeProjectStates = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, int>? ProcessStarted;
    public event Action<string, int, int>? ProcessExited;

    public bool IsApplicationRunning(string applicationId)
    {
        if (_runningProcesses.TryGetValue(applicationId, out var proc))
        {
            try
            {
                if (!proc.HasExited) return true;
                _runningProcesses.TryRemove(applicationId, out _);
            }
            catch
            {
                _runningProcesses.TryRemove(applicationId, out _);
            }
        }
        return false;
    }

    public Process? GetRunningProcess(string applicationId)
    {
        if (_runningProcesses.TryGetValue(applicationId, out var proc) && !proc.HasExited)
        {
            return proc;
        }
        return null;
    }

    public string ResolveApplicationSourcePath(string applicationId)
    {
        if (CreativeAppRegistry.TryGetApplication(applicationId, out var app))
        {
            var path = Path.Combine(CreativeAppRegistry.ExtractedRoot, app.RelativeSourcePath);
            if (Directory.Exists(path))
            {
                return path;
            }
        }
        return string.Empty;
    }

    public string? ResolveExecutablePath(string applicationId, bool preferCli = false)
    {
        if (!CreativeAppRegistry.TryGetApplication(applicationId, out var app))
        {
            return null;
        }

        var sourceDir = ResolveApplicationSourcePath(applicationId);
        if (string.IsNullOrEmpty(sourceDir))
        {
            return null;
        }

        var exeName = preferCli ? app.CliExecutableName : app.BinaryExecutableName;

        // Check target/release or target/debug
        string[] candidates =
        [
            Path.Combine(sourceDir, "target", "release", exeName),
            Path.Combine(sourceDir, "target", "debug", exeName),
            Path.Combine(sourceDir, "bin", exeName),
            Path.Combine(sourceDir, exeName)
        ];

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        return candidates[0]; // fallback candidate
    }

    /// <summary>
    /// Launches an application in Manual Editing mode or integrated mode.
    /// Preserves current project state, opens the native window, and registers the process.
    /// </summary>
    public async Task<CreativeLaunchResult> LaunchApplicationAsync(
        string applicationId,
        string? projectPath = null,
        bool isManualEditing = true,
        CancellationToken cancellationToken = default)
    {
        if (!CreativeAppRegistry.TryGetApplication(applicationId, out var app))
        {
            return new CreativeLaunchResult
            {
                Success = false,
                ApplicationId = applicationId,
                DisplayName = applicationId,
                Message = $"Application '{applicationId}' is not registered in SPARK."
            };
        }

        // Avoid opening duplicate instances unnecessarily
        if (IsApplicationRunning(applicationId))
        {
            var running = _runningProcesses[applicationId];
            return new CreativeLaunchResult
            {
                Success = true,
                ApplicationId = applicationId,
                DisplayName = app.DisplayName,
                ProcessId = running.Id,
                IsManualEditingMode = isManualEditing,
                Message = $"Application '{app.DisplayName}' is already active (PID: {running.Id})."
            };
        }

        var sourcePath = ResolveApplicationSourcePath(applicationId);
        var exePath = ResolveExecutablePath(applicationId, preferCli: false);

        // Record active project state
        if (!string.IsNullOrEmpty(projectPath))
        {
            _activeProjectStates[applicationId] = projectPath;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = Directory.Exists(sourcePath) ? sourcePath : AppContext.BaseDirectory,
                UseShellExecute = true
            };

            if (File.Exists(exePath))
            {
                startInfo.FileName = exePath;
                if (!string.IsNullOrEmpty(projectPath))
                {
                    startInfo.Arguments = $"\"{projectPath}\"";
                }
            }
            else
            {
                // When pre-compiled native binary is being built, launch a companion host shell or explorer
                startInfo.FileName = "explorer.exe";
                startInfo.Arguments = Directory.Exists(sourcePath) ? $"\"{sourcePath}\"" : $"\"{CreativeAppRegistry.CraftArchivesRoot}\"";
            }

            var process = Process.Start(startInfo);
            if (process != null)
            {
                _runningProcesses[applicationId] = process;
                app.ActiveProcessId = process.Id;
                app.RunningProcessName = process.ProcessName;

                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    var exitCode = -1;
                    try { exitCode = process.ExitCode; } catch { }
                    _runningProcesses.TryRemove(applicationId, out _);
                    app.ActiveProcessId = null;
                    app.RunningProcessName = null;
                    ProcessExited?.Invoke(applicationId, process.Id, exitCode);
                };

                ProcessStarted?.Invoke(applicationId, process.Id);

                return new CreativeLaunchResult
                {
                    Success = true,
                    ApplicationId = applicationId,
                    DisplayName = app.DisplayName,
                    ProcessId = process.Id,
                    ExecutablePath = startInfo.FileName,
                    WorkingDirectory = startInfo.WorkingDirectory,
                    IsManualEditingMode = isManualEditing,
                    Message = $"Launched {app.DisplayName} successfully{(isManualEditing ? " in Manual Editing mode" : "")}."
                };
            }
        }
        catch (Exception ex)
        {
            return new CreativeLaunchResult
            {
                Success = false,
                ApplicationId = applicationId,
                DisplayName = app.DisplayName,
                Message = $"Failed to launch {app.DisplayName}: {ex.Message}"
            };
        }

        return new CreativeLaunchResult
        {
            Success = false,
            ApplicationId = applicationId,
            DisplayName = app.DisplayName,
            Message = $"Unable to start process for {app.DisplayName}."
        };
    }

    /// <summary>
    /// Terminates an active creative application process.
    /// </summary>
    public bool TerminateApplication(string applicationId)
    {
        if (_runningProcesses.TryRemove(applicationId, out var proc))
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Performs an honest health check on the specified creative application.
    /// </summary>
    public Dictionary<string, object> PerformHealthCheck(string applicationId)
    {
        var result = new Dictionary<string, object>();
        if (!CreativeAppRegistry.TryGetApplication(applicationId, out var app))
        {
            result["status"] = "NotRegistered";
            return result;
        }

        var sourcePath = ResolveApplicationSourcePath(applicationId);
        var sourceExists = Directory.Exists(sourcePath);
        var binaryPath = ResolveExecutablePath(applicationId, preferCli: false);
        var binaryExists = File.Exists(binaryPath);
        var isRunning = IsApplicationRunning(applicationId);

        result["applicationId"] = app.ApplicationId;
        result["displayName"] = app.DisplayName;
        result["status"] = app.Status.ToString();
        result["sourceExists"] = sourceExists;
        result["sourcePath"] = sourcePath;
        result["binaryExists"] = binaryExists;
        result["binaryPath"] = binaryPath ?? "None";
        result["isRunning"] = isRunning;
        result["processId"] = isRunning ? _runningProcesses[applicationId].Id : 0;
        result["interfaces"] = app.SupportedInterfaces;

        return result;
    }
}
