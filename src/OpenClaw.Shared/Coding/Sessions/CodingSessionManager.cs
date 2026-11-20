using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Files;
using OpenClaw.Shared.Coding.Models;

namespace OpenClaw.Shared.Coding.Sessions;

/// <summary>
/// Session manager that persists sessions, coordinates checkpoints,
/// and enables multi-level rollback and session resumption.
/// </summary>
public sealed class CodingSessionManager
{
    private readonly string _storageDirectory;
    private readonly ConcurrentDictionary<string, CodingSession> _activeSessions = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public CodingSessionManager(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClawTray",
            "coding_sessions");

        Directory.CreateDirectory(_storageDirectory);
    }

    /// <summary>
    /// Creates a new coding session for a project.
    /// </summary>
    public CodingSession CreateSession(string projectRoot, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var id = "cs_" + Guid.NewGuid().ToString("N")[..12];
        var session = new CodingSession
        {
            SessionId = id,
            ProjectRoot = Path.GetFullPath(projectRoot),
            Title = title ?? $"Session {DateTime.Now:MMM d, HH:mm}",
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow,
            Status = "Idle"
        };

        _activeSessions[id] = session;
        return session;
    }

    /// <summary>
    /// Retrieves a session by ID (from memory or persistent storage).
    /// </summary>
    public async Task<CodingSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (_activeSessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        var path = GetSessionFilePath(sessionId);
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            var loaded = JsonSerializer.Deserialize<CodingSession>(json, JsonOptions);
            if (loaded != null)
            {
                _activeSessions[sessionId] = loaded;
                return loaded;
            }
        }
        catch
        {
            // Ignore deserialization errors
        }

        return null;
    }

    /// <summary>
    /// Persists session metadata, plan, and message history to disk.
    /// </summary>
    public async Task SaveSessionAsync(CodingSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        _activeSessions[session.SessionId] = session;
        var path = GetSessionFilePath(session.SessionId);

        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    /// <summary>
    /// Creates a checkpoint of currently modified files in the session for undo / rollback.
    /// </summary>
    public CodingSession CreateCheckpoint(
        CodingSession session,
        string description,
        IReadOnlyDictionary<string, string> currentFileContents)
    {
        var checkpoint = new CodingSessionCheckpoint
        {
            CheckpointId = "chk_" + Guid.NewGuid().ToString("N")[..8],
            Description = description,
            Timestamp = DateTimeOffset.UtcNow,
            FileSnapshots = new Dictionary<string, string>(currentFileContents)
        };

        session.Checkpoints.Add(checkpoint);
        return session;
    }

    /// <summary>
    /// Rolls back files to the state captured in a checkpoint.
    /// </summary>
    public async Task<bool> RollbackToCheckpointAsync(
        CodingSession session,
        string checkpointId,
        CodingFileOperationsEngine fileOps,
        CancellationToken ct = default)
    {
        var checkpoint = session.Checkpoints.FirstOrDefault(c => c.CheckpointId == checkpointId);
        if (checkpoint == null) return false;

        foreach (var (relPath, content) in checkpoint.FileSnapshots)
        {
            await fileOps.WriteFileAsync(relPath, content, createBackup: false, ct);
        }

        return true;
    }

    /// <summary>
    /// Lists all saved coding sessions across projects.
    /// </summary>
    public async Task<IReadOnlyList<CodingSession>> ListSavedSessionsAsync(CancellationToken ct = default)
    {
        var result = new List<CodingSession>();

        foreach (var file in Directory.EnumerateFiles(_storageDirectory, "cs_*.json"))
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var s = JsonSerializer.Deserialize<CodingSession>(json, JsonOptions);
                if (s != null) result.Add(s);
            }
            catch { }
        }

        return result.OrderByDescending(s => s.LastActiveAt).ToList();
    }

    /// <summary>
    /// Deletes a session and its persistent storage.
    /// </summary>
    public Task<bool> DeleteSessionAsync(string sessionId)
    {
        _activeSessions.TryRemove(sessionId, out _);
        var path = GetSessionFilePath(sessionId);
        if (File.Exists(path))
        {
            File.Delete(path);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private string GetSessionFilePath(string sessionId)
    {
        return Path.Combine(_storageDirectory, $"{sessionId}.json");
    }
}
