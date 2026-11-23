using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Memory;

public class MemoryConflictException : Exception
{
    public MemoryConflictException(string message) : base(message) { }
}

public sealed record TopicDocument
{
    public required string Topic { get; init; } // e.g. "root" (for MEMORY.md), "coding", "finance", "travel"
    public required string RelativePath { get; init; }
    public required string Content { get; init; }
    public required string Sha256Hash { get; init; }
    public DateTimeOffset LastModified { get; init; }
}

public sealed record TopicSearchResult
{
    public required string Topic { get; init; }
    public required string RelativePath { get; init; }
    public required string MatchedSnippet { get; init; }
    public int LineNumber { get; init; }
}

/// <summary>
/// Topic-scoped Markdown memory store for SPARK.
/// Organizes persistent notes into MEMORY.md (core preferences) and memory/{topic}.md files.
/// Guarantees transactionality, optimistic concurrency checking, and audit-journaling with 1-click rollback.
/// </summary>
public sealed class TopicMemoryStore
{
    private readonly string _storageDir;
    private readonly StructuredMemoryJournal _journal;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TopicMemoryStore(string? storageDir = null, StructuredMemoryJournal? journal = null)
    {
        _storageDir = storageDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "MemoryTopics");

        Directory.CreateDirectory(_storageDir);
        Directory.CreateDirectory(Path.Combine(_storageDir, "topics"));

        _journal = journal ?? new StructuredMemoryJournal(Path.Combine(_storageDir, "journal"));
    }

    public StructuredMemoryJournal Journal => _journal;

    private string GetPathForTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic) || string.Equals(topic, "root", StringComparison.OrdinalIgnoreCase) || string.Equals(topic, "MEMORY", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(_storageDir, "MEMORY.md");
        }

        var clean = Regex.Replace(topic.Trim().ToLowerInvariant(), @"[^a-z0-9_\-]", "_");
        return Path.Combine(_storageDir, "topics", $"{clean}.md");
    }

    private string GetRelativePath(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic) || string.Equals(topic, "root", StringComparison.OrdinalIgnoreCase) || string.Equals(topic, "MEMORY", StringComparison.OrdinalIgnoreCase))
        {
            return "MEMORY.md";
        }

        var clean = Regex.Replace(topic.Trim().ToLowerInvariant(), @"[^a-z0-9_\-]", "_");
        return $"memory/{clean}.md";
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task<TopicDocument?> ReadTopicAsync(string topic, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var filePath = GetPathForTopic(topic);
            if (!File.Exists(filePath))
                return null;

            var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            var hash = ComputeHash(content);
            var info = new FileInfo(filePath);

            return new TopicDocument
            {
                Topic = topic,
                RelativePath = GetRelativePath(topic),
                Content = content,
                Sha256Hash = hash,
                LastModified = info.LastWriteTimeUtc
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TopicDocument> WriteTopicAsync(
        string topic,
        string content,
        string? expectedBeforeHash = null,
        string actor = "user",
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var filePath = GetPathForTopic(topic);
            var relPath = GetRelativePath(topic);

            string? beforeContent = null;
            if (File.Exists(filePath))
            {
                beforeContent = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
                var currentHash = ComputeHash(beforeContent);

                if (expectedBeforeHash != null && !string.Equals(currentHash, expectedBeforeHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new MemoryConflictException(
                        $"Conflict detected on topic '{topic}'. File has been modified by another process. Reload before saving.");
                }
            }

            // Write to disk atomically using temporary file
            var tempPath = filePath + $".{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, ct).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);

            // Record into journal
            await _journal.RecordChangeAsync(relPath, beforeContent, content, actor, ct).ConfigureAwait(false);

            return new TopicDocument
            {
                Topic = topic,
                RelativePath = relPath,
                Content = content,
                Sha256Hash = ComputeHash(content),
                LastModified = DateTimeOffset.UtcNow
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TopicDocument> AppendSectionAsync(
        string topic,
        string sectionTitle,
        string sectionBody,
        string actor = "user",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionTitle);
        sectionBody ??= string.Empty;

        var existing = await ReadTopicAsync(topic, ct).ConfigureAwait(false);
        var current = existing?.Content ?? $"# {topic.ToUpperInvariant()} NOTES\n\n";

        var builder = new StringBuilder(current.TrimEnd());
        builder.AppendLine("\n");
        builder.AppendLine($"## {sectionTitle.Trim()}");
        builder.AppendLine(sectionBody.Trim());
        builder.AppendLine();

        return await WriteTopicAsync(topic, builder.ToString(), existing?.Sha256Hash, actor, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListTopicsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var list = new List<string>();
            if (File.Exists(Path.Combine(_storageDir, "MEMORY.md")))
                list.Add("root");

            var topicsDir = Path.Combine(_storageDir, "topics");
            if (Directory.Exists(topicsDir))
            {
                foreach (var file in Directory.GetFiles(topicsDir, "*.md"))
                {
                    list.Add(Path.GetFileNameWithoutExtension(file));
                }
            }

            return list;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<TopicSearchResult>> SearchTopicsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<TopicSearchResult>();

        var topics = await ListTopicsAsync(ct).ConfigureAwait(false);
        var results = new List<TopicSearchResult>();

        foreach (var t in topics)
        {
            var doc = await ReadTopicAsync(t, ct).ConfigureAwait(false);
            if (doc == null) continue;

            var lines = doc.Content.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new TopicSearchResult
                    {
                        Topic = t,
                        RelativePath = doc.RelativePath,
                        MatchedSnippet = line.Trim(),
                        LineNumber = i + 1
                    });
                }
            }
        }

        return results;
    }

    public async Task<string?> RollbackTopicAsync(string topic, string entryId, CancellationToken ct = default)
    {
        var restored = await _journal.RollbackAsync(entryId, ct).ConfigureAwait(false);
        if (restored != null)
        {
            var filePath = GetPathForTopic(topic);
            await File.WriteAllTextAsync(filePath, restored, Encoding.UTF8, ct).ConfigureAwait(false);
        }
        return restored;
    }
}
