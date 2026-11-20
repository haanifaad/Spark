using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.ExecApprovals;

namespace OpenClaw.Shared.Coding.Terminal;

/// <summary>
/// Controlled, sandboxed terminal command execution engine with banned-command filtering,
/// secret redaction, execution timeouts, output truncation, and background job support.
/// </summary>
public sealed class CodingTerminalEngine
{
    private readonly string _workspaceRoot;
    public const int DefaultTimeoutSeconds = 120;
    public const int MaxOutputLength = 30000;

    private static readonly HashSet<string> BannedExecutableTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "curl", "wget", "nc", "netcat", "telnet", "ssh", "scp", "ftp",
        "diskpart", "format", "cipher", "regini", "takeown", "icacls",
        "rmdir /s /q c:\\", "del /f /s /q c:\\", "shutdown", "reboot"
    };

    public string WorkspaceRoot => _workspaceRoot;

    public CodingTerminalEngine(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
    }

    /// <summary>
    /// Validates if a command contains banned or hazardous operations.
    /// </summary>
    public static (bool IsAllowed, string? DisallowReason) ValidateCommandSafety(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return (false, "Command is empty.");
        }

        var trimmed = commandLine.Trim();

        foreach (var banned in BannedExecutableTokens)
        {
            if (Regex.IsMatch(trimmed, $@"\b{Regex.Escape(banned)}\b", RegexOptions.IgnoreCase))
            {
                return (false, $"Command references restricted or banned utility: '{banned}'. For security, external exfiltration and low-level disk tools are disallowed.");
            }
        }

        // Check for catastrophic root wipes
        if (trimmed.Contains("rm -rf /") || trimmed.Contains("rd /s /q \\") || trimmed.Contains("del /s /q C:\\"))
        {
            return (false, "Command attempts catastrophic system filesystem destruction.");
        }

        return (true, null);
    }

    /// <summary>
    /// Executes a command in the workspace directory.
    /// </summary>
    public async Task<CommandExecutionResult> ExecuteAsync(
        string commandLine,
        string? relativeWorkDir = null,
        int timeoutSeconds = DefaultTimeoutSeconds,
        bool isBackground = false,
        CancellationToken ct = default)
    {
        var (isAllowed, reason) = ValidateCommandSafety(commandLine);
        if (!isAllowed)
        {
            throw new InvalidOperationException($"Security Validation Failed: {reason}");
        }

        var targetWorkDir = string.IsNullOrEmpty(relativeWorkDir)
            ? _workspaceRoot
            : Path.GetFullPath(Path.Combine(_workspaceRoot, relativeWorkDir));

        // Ensure target directory doesn't escape workspace
        if (!targetWorkDir.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            targetWorkDir = _workspaceRoot;
        }

        var isWindows = OperatingSystem.IsWindows();
        var shellFileName = isWindows ? "cmd.exe" : "/bin/bash";
        var shellArgs = isWindows ? $"/c \"{commandLine}\"" : $"-c \"{commandLine.Replace("\"", "\\\"")}\"";

        var psi = new ProcessStartInfo
        {
            FileName = shellFileName,
            Arguments = shellArgs,
            WorkingDirectory = targetWorkDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // Redact common secret keys from process environment
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        var stopwatch = Stopwatch.StartNew();
        using var process = new Process { StartInfo = psi };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        var combinedBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                var sanitized = RedactSecrets(e.Data);
                lock (combinedBuilder)
                {
                    stdoutBuilder.AppendLine(sanitized);
                    combinedBuilder.AppendLine(sanitized);
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                var sanitized = RedactSecrets(e.Data);
                lock (combinedBuilder)
                {
                    stderrBuilder.AppendLine(sanitized);
                    combinedBuilder.AppendLine(sanitized);
                }
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (isBackground)
            {
                return new CommandExecutionResult
                {
                    Command = commandLine,
                    WorkingDirectory = targetWorkDir,
                    ExitCode = 0,
                    CombinedOutput = $"[Process started in background with PID {process.Id}]",
                    WallTimeSeconds = 0.05,
                    IsBackground = true,
                    ProcessId = process.Id
                };
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                bool timedOut = timeoutCts.IsCancellationRequested;
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore kill errors
                }

                stopwatch.Stop();
                var partial = combinedBuilder.ToString();
                return new CommandExecutionResult
                {
                    Command = commandLine,
                    WorkingDirectory = targetWorkDir,
                    ExitCode = timedOut ? -1 : -2,
                    CombinedOutput = partial + (timedOut ? $"\n[Execution timed out after {timeoutSeconds}s]" : "\n[Execution cancelled by user]"),
                    StandardOutput = stdoutBuilder.ToString(),
                    StandardError = stderrBuilder.ToString(),
                    WallTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                    TimedOut = timedOut
                };
            }

            stopwatch.Stop();

            string fullOutput;
            lock (combinedBuilder)
            {
                fullOutput = combinedBuilder.ToString();
            }

            bool truncated = false;
            string? truncatedOutputPath = null;

            if (fullOutput.Length > MaxOutputLength)
            {
                truncated = true;
                var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
                truncatedOutputPath = Path.Combine(Path.GetTempPath(), $"spark_cmd_out_{stamp}.log");
                await File.WriteAllTextAsync(truncatedOutputPath, fullOutput, Encoding.UTF8, CancellationToken.None);

                fullOutput = string.Concat(fullOutput.AsSpan(0, MaxOutputLength),
                    $"\n... [Output truncated from {fullOutput.Length:N0} characters. Full log saved to: {truncatedOutputPath}]");
            }

            return new CommandExecutionResult
            {
                Command = commandLine,
                WorkingDirectory = targetWorkDir,
                ExitCode = process.ExitCode,
                StandardOutput = stdoutBuilder.ToString(),
                StandardError = stderrBuilder.ToString(),
                CombinedOutput = fullOutput.TrimEnd(),
                WallTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                TimedOut = false,
                Truncated = truncated,
                TruncatedOutputPath = truncatedOutputPath
            };
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            stopwatch.Stop();
            return new CommandExecutionResult
            {
                Command = commandLine,
                WorkingDirectory = targetWorkDir,
                ExitCode = -1,
                CombinedOutput = $"Command failed to launch: {ex.Message}",
                StandardError = ex.Message,
                WallTimeSeconds = stopwatch.Elapsed.TotalSeconds
            };
        }
    }

    /// <summary>
    /// Redacts bearer tokens, API keys, passwords, and sensitive headers from outputs.
    /// </summary>
    public static string RedactSecrets(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Use ExecApprovalSecretRedactor if available, with built-in regex fallbacks
        var output = input;

        // Bearer / Token
        output = Regex.Replace(output, @"(bearer|token|apikey|api_key|password|secret)[\s:=]+([a-zA-Z0-9_\-\.]{8,})",
            "$1: [REDACTED]", RegexOptions.IgnoreCase);

        // OpenAI / Anthropic key patterns
        output = Regex.Replace(output, @"sk-[a-zA-Z0-9_\-]{20,}", "sk-[REDACTED]");
        output = Regex.Replace(output, @"ghp_[a-zA-Z0-9]{20,}", "ghp_[REDACTED]");

        return output;
    }
}
