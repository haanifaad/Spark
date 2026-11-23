using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Tools.Development;

/// <summary>
/// Helper for running controlled subprocess commands with bounded timeouts and streaming output capture.
/// </summary>
public static class SparkProcessRunner
{
    public static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunProcessAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, $"Failed to start process '{fileName}': {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            return (process.ExitCode, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process may have exited concurrently
            }

            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return (-1, stdout.ToString().TrimEnd(), $"Process execution timed out after {effectiveTimeout.TotalSeconds:0.##}s limit.");
            }

            return (-1, stdout.ToString().TrimEnd(), "Process execution was canceled.");
        }
    }
}

/// <summary>
/// Tool to inspect git working tree status and branch info.
/// </summary>
public class SparkGitStatusTool : ISparkTool
{
    public SparkGitStatusTool()
    {
        Metadata = new SparkToolMetadata
        {
            Id = "dev_git_status",
            Name = "Git Status",
            Description = "Retrieves the current git branch, commit HEAD, and working tree modification status.",
            Category = SparkToolCategory.Development,
            RiskLevel = SparkToolRiskLevel.Low,
            RequiredPermissions = new List<string> { "git:read" },
            DefaultTimeout = TimeSpan.FromSeconds(15),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"repoDirectory\":{\"type\":\"string\"}}}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var repoDir = request.GetArgument<string>("repoDirectory") ?? Directory.GetCurrentDirectory();

        var (code, stdout, stderr) = await SparkProcessRunner.RunProcessAsync(
            "git",
            "status --porcelain -b",
            repoDir,
            Metadata.DefaultTimeout,
            cancellationToken).ConfigureAwait(false);

        if (code != 0)
        {
            return SparkToolExecutionResponse.Fail(
                Metadata.Id,
                string.IsNullOrWhiteSpace(stderr) ? $"Git command failed with exit code {code}." : stderr,
                Metadata.RiskLevel,
                TimeSpan.Zero);
        }

        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var branchLine = lines.Length > 0 ? lines[0] : string.Empty;
        var modifiedFiles = lines.Skip(1).ToList();

        return SparkToolExecutionResponse.Ok(Metadata.Id, new
        {
            repoDirectory = repoDir,
            branch = branchLine.TrimStart('#').Trim(),
            modifiedCount = modifiedFiles.Count,
            isClean = modifiedFiles.Count == 0,
            modifications = modifiedFiles
        }, Metadata.RiskLevel, TimeSpan.Zero);
    }
}

/// <summary>
/// Tool to inspect git diff of current changes.
/// </summary>
public class SparkGitDiffTool : ISparkTool
{
    public SparkGitDiffTool()
    {
        Metadata = new SparkToolMetadata
        {
            Id = "dev_git_diff",
            Name = "Git Diff",
            Description = "Retrieves unstaged or staged git diff output.",
            Category = SparkToolCategory.Development,
            RiskLevel = SparkToolRiskLevel.Low,
            RequiredPermissions = new List<string> { "git:read" },
            DefaultTimeout = TimeSpan.FromSeconds(20),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"staged\":{\"type\":\"boolean\"},\"repoDirectory\":{\"type\":\"string\"}}}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var staged = request.GetArgument<bool>("staged", false);
        var repoDir = request.GetArgument<string>("repoDirectory") ?? Directory.GetCurrentDirectory();

        var args = staged ? "diff --cached" : "diff";
        var (code, stdout, stderr) = await SparkProcessRunner.RunProcessAsync(
            "git",
            args,
            repoDir,
            Metadata.DefaultTimeout,
            cancellationToken).ConfigureAwait(false);

        if (code != 0)
        {
            return SparkToolExecutionResponse.Fail(
                Metadata.Id,
                string.IsNullOrWhiteSpace(stderr) ? $"Git diff failed with exit code {code}." : stderr,
                Metadata.RiskLevel,
                TimeSpan.Zero);
        }

        return SparkToolExecutionResponse.Ok(Metadata.Id, new
        {
            staged = staged,
            diffLength = stdout.Length,
            diff = stdout
        }, Metadata.RiskLevel, TimeSpan.Zero);
    }
}

/// <summary>
/// Tool for controlled command execution with strict permission requirements.
/// </summary>
public class SparkRunCommandTool : ISparkTool
{
    public SparkRunCommandTool()
    {
        Metadata = new SparkToolMetadata
        {
            Id = "dev_run_command",
            Name = "Run Command",
            Description = "Executes a controlled command line process with bounded timeout and captured streams.",
            Category = SparkToolCategory.Development,
            RiskLevel = SparkToolRiskLevel.Critical,
            RequiredPermissions = new List<string> { "shell:execute" },
            DefaultTimeout = TimeSpan.FromSeconds(60),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"command\":{\"type\":\"string\"},\"arguments\":{\"type\":\"string\"},\"workingDirectory\":{\"type\":\"string\"}},\"required\":[\"command\"]}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = request.GetArgument<string>("command");
        var arguments = request.GetArgument<string>("arguments") ?? string.Empty;
        var workingDir = request.GetArgument<string>("workingDirectory") ?? Directory.GetCurrentDirectory();

        if (string.IsNullOrWhiteSpace(command))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "Command argument is required.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        var (code, stdout, stderr) = await SparkProcessRunner.RunProcessAsync(
            command,
            arguments,
            workingDir,
            Metadata.DefaultTimeout,
            cancellationToken).ConfigureAwait(false);

        return SparkToolExecutionResponse.Ok(Metadata.Id, new
        {
            command = command,
            arguments = arguments,
            exitCode = code,
            success = code == 0,
            standardOutput = stdout,
            standardError = stderr
        }, Metadata.RiskLevel, TimeSpan.Zero);
    }
}

/// <summary>
/// Tool to build and run test suites with result evaluation.
/// </summary>
public class SparkBuildTestTool : ISparkTool
{
    public SparkBuildTestTool()
    {
        Metadata = new SparkToolMetadata
        {
            Id = "dev_build_test",
            Name = "Build and Test",
            Description = "Executes dotnet build or test on a target project/solution.",
            Category = SparkToolCategory.Development,
            RiskLevel = SparkToolRiskLevel.High,
            RequiredPermissions = new List<string> { "build:execute" },
            DefaultTimeout = TimeSpan.FromSeconds(120),
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"enum\":[\"build\",\"test\"]},\"projectPath\":{\"type\":\"string\"},\"filter\":{\"type\":\"string\"}},\"required\":[\"action\",\"projectPath\"]}"
        };
    }

    public SparkToolMetadata Metadata { get; }

    public async Task<SparkToolExecutionResponse> ExecuteAsync(
        SparkToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var action = request.GetArgument<string>("action", "build")?.ToLowerInvariant();
        var projectPath = request.GetArgument<string>("projectPath");
        var filter = request.GetArgument<string>("filter");

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return SparkToolExecutionResponse.Fail(Metadata.Id, "ProjectPath argument is required.", Metadata.RiskLevel, TimeSpan.Zero);
        }

        string arguments;
        if (action == "test")
        {
            arguments = string.IsNullOrWhiteSpace(filter)
                ? $"test \"{projectPath}\" -v normal"
                : $"test \"{projectPath}\" --filter \"{filter}\" -v normal";
        }
        else
        {
            arguments = $"build \"{projectPath}\" -c Debug";
        }

        var (code, stdout, stderr) = await SparkProcessRunner.RunProcessAsync(
            "dotnet",
            arguments,
            Directory.GetCurrentDirectory(),
            Metadata.DefaultTimeout,
            cancellationToken).ConfigureAwait(false);

        return SparkToolExecutionResponse.Ok(Metadata.Id, new
        {
            action = action,
            projectPath = projectPath,
            exitCode = code,
            success = code == 0,
            output = stdout,
            error = stderr
        }, Metadata.RiskLevel, TimeSpan.Zero);
    }
}
