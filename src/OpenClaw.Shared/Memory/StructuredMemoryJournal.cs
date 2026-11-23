using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Watchdog;

namespace OpenClaw.Shared.Memory;

public sealed record JournalEntry
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string Actor { get; init; }
    public required string Kind { get; init; } // "created", "edited", "deleted"
    public string? BeforeHash { get; init; }
    public string? AfterHash { get; init; }
    public string? PreviousContent { get; init; }
    public required string Diff { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public bool CanUndo { get; init; } = true;
    public string? UndoBlockReason { get; init; }
}

public class MemoryRollbackException : Exception
{
    public MemoryRollbackException(string message) : base(message) { }
}

/// <summary>
/// Structured, audit-logged memory journal supporting transactional note history
/// and 1-click atomic rollback with secret scrubbing and lock-safe NTFS persistence.
/// </summary>
public sealed class StructuredMemoryJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _storageDirectory;
    private readonly string _journalFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<JournalEntry> _entries = new();

    public StructuredMemoryJournal(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "MemoryJournal");

        Directory.CreateDirectory(_storageDirectory);
        _journalFilePath = Path.Combine(_storageDirectory, "journal.json");
        LoadFromDisk();
    }

    public async Task<JournalEntry?> RecordChangeAsync(
        string relativePath,
        string? beforeContent,
        string? afterContent,
        string actor = "agent",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (string.Equals(beforeContent, afterContent, StringComparison.Ordinal))
            return null; // Nothing changed

        var kind = beforeContent == null ? "created" : afterContent == null ? "deleted" : "edited";
        var beforeHash = beforeContent == null ? null : ComputeHash(beforeContent);
        var afterHash = afterContent == null ? null : ComputeHash(afterContent);

        var diff = ComputeUnifiedDiff(relativePath, beforeContent ?? "", afterContent ?? "");

        var canUndo = true;
        string? blockReason = null;
        string? previousToKeep = null;

        if (beforeContent != null)
        {
            var redacted = TurnWatchdogMonitor.RedactSecrets(beforeContent);
            if (redacted.Contains("[REDACTED_SECRET]"))
            {
                canUndo = false;
                blockReason = "Previous version contained detected credentials or secrets.";
            }
            else
            {
                previousToKeep = beforeContent;
            }
        }

        var entry = new JournalEntry
        {
            Id = $"jrn_{Guid.NewGuid():N}",
            Path = relativePath.Trim(),
            Actor = actor,
            Kind = kind,
            BeforeHash = beforeHash,
            AfterHash = afterHash,
            PreviousContent = previousToKeep,
            Diff = diff,
            CanUndo = canUndo,
            UndoBlockReason = blockReason,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _entries.Add(entry);
            SaveToDisk();
            return entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<JournalEntry>> GetHistoryAsync(
        string? targetPath = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var query = _entries.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                query = query.Where(e => string.Equals(e.Path, targetPath.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            return query.OrderByDescending(e => e.Timestamp).Take(limit).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> RollbackAsync(
        string entryId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entry = _entries.FirstOrDefault(e => string.Equals(e.Id, entryId, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                throw new MemoryRollbackException($"Journal entry '{entryId}' was not found.");

            if (!entry.CanUndo)
                throw new MemoryRollbackException(entry.UndoBlockReason ?? "This entry cannot be rolled back.");

            var restored = entry.PreviousContent;
            var undoEntry = new JournalEntry
            {
                Id = $"jrn_{Guid.NewGuid():N}",
                Path = entry.Path,
                Actor = "undo",
                Kind = restored == null ? "deleted" : "restored",
                BeforeHash = entry.AfterHash,
                AfterHash = restored == null ? null : ComputeHash(restored),
                PreviousContent = null,
                Diff = ComputeUnifiedDiff(entry.Path, "rolled_back", restored ?? ""),
                Timestamp = DateTimeOffset.UtcNow
            };

            _entries.Add(undoEntry);
            SaveToDisk();

            return restored;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> RollbackAsync(
        string entryId,
        string currentContent,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entry = _entries.FirstOrDefault(e => string.Equals(e.Id, entryId, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                throw new MemoryRollbackException($"Journal entry '{entryId}' was not found.");

            if (!entry.CanUndo)
                throw new MemoryRollbackException(entry.UndoBlockReason ?? "This entry cannot be rolled back.");

            var currentHash = ComputeHash(currentContent);
            if (!string.Equals(currentHash, entry.AfterHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new MemoryRollbackException(
                    "The target file has changed since this revision was recorded. Undo newer revisions first.");
            }

            var restored = entry.PreviousContent;

            // Record the undo operation itself as a journal entry
            var undoEntry = new JournalEntry
            {
                Id = $"jrn_{Guid.NewGuid():N}",
                Path = entry.Path,
                Actor = "undo",
                Kind = restored == null ? "deleted" : "restored",
                BeforeHash = currentHash,
                AfterHash = restored == null ? null : ComputeHash(restored),
                PreviousContent = currentContent,
                Diff = ComputeUnifiedDiff(entry.Path, currentContent, restored ?? ""),
                Timestamp = DateTimeOffset.UtcNow
            };

            _entries.Add(undoEntry);
            SaveToDisk();

            return restored;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static string ComputeHash(string text)
    {
        if (text == null) return "";
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeUnifiedDiff(string path, string before, string after)
    {
        var beforeLines = before.Split('\n');
        var afterLines = after.Split('\n');

        var sb = new StringBuilder();
        sb.AppendLine($"--- a/{path}");
        sb.AppendLine($"+++ b/{path}");

        var max = Math.Max(beforeLines.Length, afterLines.Length);
        for (var i = 0; i < max; i++)
        {
            var bLine = i < beforeLines.Length ? beforeLines[i].TrimEnd('\r') : null;
            var aLine = i < afterLines.Length ? afterLines[i].TrimEnd('\r') : null;

            if (bLine != aLine)
            {
                if (bLine != null) sb.AppendLine($"- {bLine}");
                if (aLine != null) sb.AppendLine($"+ {aLine}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_journalFilePath))
            {
                var json = File.ReadAllText(_journalFilePath);
                var loaded = JsonSerializer.Deserialize<List<JournalEntry>>(json, JsonOptions);
                if (loaded != null)
                {
                    _entries.Clear();
                    _entries.AddRange(loaded);
                }
            }
        }
        catch
        {
            // Fallback to empty entries on parse error
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var tempPath = _journalFilePath + ".tmp";
            var json = JsonSerializer.Serialize(_entries, JsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _journalFilePath, overwrite: true);
        }
        catch
        {
            // Ignore non-fatal file write failures
        }
    }
}
