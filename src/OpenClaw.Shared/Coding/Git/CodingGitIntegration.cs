using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.Coding.Terminal;

namespace OpenClaw.Shared.Coding.Git;

/// <summary>
/// Git integration engine for status inspection, diff generation, branch inspection,
/// commit drafting, and safe change management.
/// </summary>
public sealed class CodingGitIntegration
{
    private readonly CodingTerminalEngine _terminal;
    private readonly string _workspaceRoot;

    public CodingGitIntegration(CodingTerminalEngine terminal)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        _workspaceRoot = terminal.WorkspaceRoot;
    }

    /// <summary>
    /// Checks if the workspace is inside a valid git repository.
    /// </summary>
    public bool IsGitRepository => Directory.Exists(Path.Combine(_workspaceRoot, ".git"));

    /// <summary>
    /// Retrieves current git branch name.
    /// </summary>
    public async Task<string> GetCurrentBranchAsync(CancellationToken ct = default)
    {
        if (!IsGitRepository) return "(not a git repo)";
        var res = await _terminal.ExecuteAsync("git branch --show-current", timeoutSeconds: 10, ct: ct);
        return res.ExitCode == 0 ? res.CombinedOutput.Trim() : "HEAD";
    }

    /// <summary>
    /// Retrieves current git status (modified, untracked, staged files).
    /// </summary>
    public async Task<(IReadOnlyList<string> Modified, IReadOnlyList<string> Untracked, IReadOnlyList<string> Staged, string RawStatus)> GetStatusAsync(
        CancellationToken ct = default)
    {
        if (!IsGitRepository)
            return (Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), "Not a git repository");

        var res = await _terminal.ExecuteAsync("git status --porcelain", timeoutSeconds: 15, ct: ct);
        if (res.ExitCode != 0)
        {
            return (Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), res.CombinedOutput);
        }

        var modified = new List<string>();
        var untracked = new List<string>();
        var staged = new List<string>();

        var lines = res.CombinedOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Length < 3) continue;
            var indexChar = line[0];
            var workTreeChar = line[1];
            var path = line[3..].Trim().Trim('"');

            if (indexChar is 'M' or 'A' or 'D' or 'R')
            {
                staged.Add(path);
            }

            if (workTreeChar == 'M' || workTreeChar == 'D')
            {
                modified.Add(path);
            }
            else if (indexChar == '?' && workTreeChar == '?')
            {
                untracked.Add(path);
            }
        }

        return (modified, untracked, staged, res.CombinedOutput);
    }

    /// <summary>
    /// Retrieves git diff (staged, unstaged, or for a specific file).
    /// </summary>
    public async Task<string> GetDiffAsync(bool stagedOnly = false, string? filePath = null, CancellationToken ct = default)
    {
        if (!IsGitRepository) return string.Empty;

        var cmd = new StringBuilder("git diff");
        if (stagedOnly) cmd.Append(" --staged");
        if (!string.IsNullOrEmpty(filePath)) cmd.Append($" -- \"{filePath}\"");

        var res = await _terminal.ExecuteAsync(cmd.ToString(), timeoutSeconds: 30, ct: ct);
        return res.ExitCode == 0 ? res.CombinedOutput : string.Empty;
    }

    /// <summary>
    /// Retrieves recent git commit history.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetRecentCommitsAsync(int count = 5, CancellationToken ct = default)
    {
        if (!IsGitRepository) return Array.Empty<string>();

        var res = await _terminal.ExecuteAsync($"git log --oneline -n {count}", timeoutSeconds: 10, ct: ct);
        if (res.ExitCode != 0) return Array.Empty<string>();

        return res.CombinedOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Stages files for commit.
    /// </summary>
    public async Task<bool> StageFilesAsync(IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        if (!IsGitRepository) return false;

        var paths = string.Join(" ", filePaths.Select(p => $"\"{p}\""));
        if (string.IsNullOrWhiteSpace(paths)) return true;

        var res = await _terminal.ExecuteAsync($"git add {paths}", timeoutSeconds: 15, ct: ct);
        return res.ExitCode == 0;
    }

    /// <summary>
    /// Creates a commit with the specified message. Requires explicit approval.
    /// </summary>
    public async Task<(bool Success, string Output)> CreateCommitAsync(string message, CancellationToken ct = default)
    {
        if (!IsGitRepository) return (false, "Not a git repository");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        // Escape double quotes in message
        var escapedMessage = message.Replace("\"", "\\\"").Replace("\r\n", "\n");

        var res = await _terminal.ExecuteAsync($"git commit -m \"{escapedMessage}\"", timeoutSeconds: 20, ct: ct);
        return (res.ExitCode == 0, res.CombinedOutput);
    }
}
