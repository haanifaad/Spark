using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Models;

namespace OpenClaw.Shared.Coding.Files;

/// <summary>
/// Safe file operations engine enforcing workspace boundary containment,
/// path traversal protection, binary detection, windowed reading, and atomic backups.
/// </summary>
public sealed class CodingFileOperationsEngine
{
    private readonly string _workspaceRoot;
    private readonly string _backupDirectory;

    public const int MaxReadBytes = 100 * 1024; // 100 KB
    public const int MaxReadLines = 2000;
    public const int MaxLineLength = 2000;

    public string WorkspaceRoot => _workspaceRoot;

    public CodingFileOperationsEngine(string workspaceRoot, string? backupDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);

        _backupDirectory = backupDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClawTray",
            "coding_backups");

        Directory.CreateDirectory(_backupDirectory);
    }

    /// <summary>
    /// Resolves and asserts that a path is securely contained within the workspace root.
    /// Prevents path traversal and directory escape attacks.
    /// </summary>
    public string ResolveSecurePath(string relativeOrAbsolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeOrAbsolutePath);

        string fullPath = Path.IsPathRooted(relativeOrAbsolutePath)
            ? Path.GetFullPath(relativeOrAbsolutePath)
            : Path.GetFullPath(Path.Combine(_workspaceRoot, relativeOrAbsolutePath));

        // Ensure path starts with workspace root (with trailing separator check)
        var normalizedRoot = _workspaceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, _workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Access denied: Path '{relativeOrAbsolutePath}' escapes workspace boundary '{_workspaceRoot}'.");
        }

        return fullPath;
    }

    /// <summary>
    /// Reads a file with optional line windowing, binary detection, and line numbers.
    /// </summary>
    public async Task<(string Content, bool IsBinary, int TotalLines, bool IsTruncated)> ReadFileAsync(
        string path,
        int? lineOffset = null,
        int? lineCount = null,
        CancellationToken ct = default)
    {
        var securePath = ResolveSecurePath(path);

        if (!File.Exists(securePath))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        // Check if file is binary
        if (await IsBinaryFileAsync(securePath, ct))
        {
            var info = new FileInfo(securePath);
            return ($"[Binary file: {info.Length:N0} bytes, preview unavailable]", true, 0, false);
        }

        var lines = await File.ReadAllLinesAsync(securePath, Encoding.UTF8, ct);
        int totalLines = lines.Length;

        int startLine = Math.Max(1, lineOffset ?? 1);
        int count = lineCount ?? MaxReadLines;

        int startIndex = Math.Min(startLine - 1, totalLines);
        int takeCount = Math.Min(count, totalLines - startIndex);

        bool isTruncated = (startIndex + takeCount) < totalLines || takeCount < totalLines;

        var sb = new StringBuilder();
        for (int i = 0; i < takeCount; i++)
        {
            int currentLineNum = startIndex + i + 1;
            var line = lines[startIndex + i];

            // Truncate overly long single lines
            if (line.Length > MaxLineLength)
            {
                line = string.Concat(line.AsSpan(0, MaxLineLength), $"... [line truncated to {MaxLineLength} chars]");
            }

            sb.AppendLine($"{currentLineNum,5} | {line}");
        }

        if (isTruncated)
        {
            sb.AppendLine($"... [Showing lines {startLine}-{startLine + takeCount - 1} of {totalLines} total lines. Use lineOffset to read further]");
        }

        return (sb.ToString(), false, totalLines, isTruncated);
    }

    /// <summary>
    /// Writes content to a file, creating atomic backups and returning a FileChangeRecord.
    /// </summary>
    public async Task<FileChangeRecord> WriteFileAsync(
        string path,
        string newContent,
        bool createBackup = true,
        CancellationToken ct = default)
    {
        var securePath = ResolveSecurePath(path);
        var relPath = Path.GetRelativePath(_workspaceRoot, securePath);

        string? oldContent = null;
        string? backupPath = null;
        var changeType = FileChangeType.Created;

        if (File.Exists(securePath))
        {
            changeType = FileChangeType.Modified;
            oldContent = await File.ReadAllTextAsync(securePath, Encoding.UTF8, ct);

            if (createBackup)
            {
                backupPath = await CreateBackupAsync(securePath, oldContent, ct);
            }
        }
        else
        {
            var dir = Path.GetDirectoryName(securePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        await File.WriteAllTextAsync(securePath, newContent, Encoding.UTF8, ct);

        var diff = CodeEditingEngine.GenerateDiff(relPath, oldContent ?? string.Empty, newContent);

        return new FileChangeRecord
        {
            FilePath = relPath,
            ChangeType = changeType,
            Status = FileChangeStatus.Applied,
            OldContent = oldContent,
            NewContent = newContent,
            Diff = diff,
            BackupPath = backupPath,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Reverts a file change using its backup or prior content.
    /// </summary>
    public async Task<bool> RevertChangeAsync(FileChangeRecord change, CancellationToken ct = default)
    {
        var securePath = ResolveSecurePath(change.FilePath);

        if (change.ChangeType == FileChangeType.Created)
        {
            if (File.Exists(securePath))
            {
                File.Delete(securePath);
                return true;
            }
            return false;
        }

        if (change.OldContent != null)
        {
            await File.WriteAllTextAsync(securePath, change.OldContent, Encoding.UTF8, ct);
            return true;
        }

        if (change.BackupPath != null && File.Exists(change.BackupPath))
        {
            File.Copy(change.BackupPath, securePath, overwrite: true);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Lists directory contents safely.
    /// </summary>
    public Task<IReadOnlyList<string>> ListDirectoryAsync(
        string relativePath = "",
        bool recursive = false,
        CancellationToken ct = default)
    {
        var securePath = ResolveSecurePath(relativePath);

        if (!Directory.Exists(securePath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {relativePath}");
        }

        var results = new List<string>();
        var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var entry in Directory.EnumerateFileSystemEntries(securePath, "*", opt))
        {
            if (ct.IsCancellationRequested) break;
            var name = Path.GetFileName(entry);
            if (name.StartsWith('.') || name is "bin" or "obj" or "node_modules") continue;

            results.Add(Path.GetRelativePath(_workspaceRoot, entry));
        }

        return Task.FromResult<IReadOnlyList<string>>(results);
    }

    private async Task<string> CreateBackupAsync(string filePath, string content, CancellationToken ct)
    {
        var fileName = Path.GetFileName(filePath);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmssfff");
        var backupFile = Path.Combine(_backupDirectory, $"{stamp}_{fileName}.bak");

        await File.WriteAllTextAsync(backupFile, content, Encoding.UTF8, ct);
        return backupFile;
    }

    private static async Task<bool> IsBinaryFileAsync(string filePath, CancellationToken ct)
    {
        const int sampleSize = 8192;
        var buffer = new byte[sampleSize];

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        int read = await stream.ReadAsync(buffer.AsMemory(0, sampleSize), ct);

        for (int i = 0; i < read; i++)
        {
            if (buffer[i] == 0) // Null byte indicates binary
            {
                return true;
            }
        }

        return false;
    }
}
