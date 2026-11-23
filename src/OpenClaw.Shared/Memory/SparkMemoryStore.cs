using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Memory;

/// <summary>
/// Unified thread-safe persistent memory store for the SPARK autonomous agent.
/// Supports multi-tier memory partitioning, versioned records, BM25-style relevance retrieval,
/// automatic conflict detection, user isolation, and atomic file persistence.
/// </summary>
public sealed class SparkMemoryStore : ISparkMemoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex TokenizerRegex = new(@"\w+", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "in", "on", "at", "to", "for", "of", "with", "by", "from",
        "and", "or", "is", "are", "was", "were", "be", "been", "being", "have", "has",
        "had", "do", "does", "did", "i", "you", "he", "she", "it", "we", "they", "my",
        "your", "his", "her", "its", "our", "their", "this", "that", "these", "those"
    };

    private readonly string _baseStoragePath;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _userLocks = new(StringComparer.OrdinalIgnoreCase);

    public SparkMemoryStore(string? baseStoragePath = null)
    {
        if (!string.IsNullOrWhiteSpace(baseStoragePath))
        {
            _baseStoragePath = baseStoragePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baseStoragePath = Path.Combine(appData, "Spark", "Memory");
        }

        Directory.CreateDirectory(_baseStoragePath);
    }

    private SemaphoreSlim GetUserGate(string userId) =>
        _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

    private string GetUserDirectory(string userId)
    {
        var cleanUserId = string.IsNullOrWhiteSpace(userId) ? "default" : userId.Trim();
        var safeName = string.Join("_", cleanUserId.Split(Path.GetInvalidFileNameChars()));
        var dir = Path.Combine(_baseStoragePath, safeName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string GetRecordsFilePath(string userId) =>
        Path.Combine(GetUserDirectory(userId), "records.json");

    private string GetProfileFilePath(string userId) =>
        Path.Combine(GetUserDirectory(userId), "profile.json");

    private string GetProjectsFilePath(string userId) =>
        Path.Combine(GetUserDirectory(userId), "projects.json");

    #region Internal File I/O with Atomic Write

    private async Task<List<MemoryRecord>> LoadRecordsInternalAsync(string userId, CancellationToken ct)
    {
        var file = GetRecordsFilePath(userId);
        if (!File.Exists(file)) return new List<MemoryRecord>();

        try
        {
            await using var stream = File.OpenRead(file);
            var records = await JsonSerializer.DeserializeAsync<List<MemoryRecord>>(stream, JsonOptions, ct).ConfigureAwait(false);
            return records ?? new List<MemoryRecord>();
        }
        catch (Exception)
        {
            // Attempt to restore from backup if present
            var backupFile = file + ".bak";
            if (File.Exists(backupFile))
            {
                try
                {
                    await using var stream = File.OpenRead(backupFile);
                    var records = await JsonSerializer.DeserializeAsync<List<MemoryRecord>>(stream, JsonOptions, ct).ConfigureAwait(false);
                    if (records != null) return records;
                }
                catch { /* ignore */ }
            }
            return new List<MemoryRecord>();
        }
    }

    private async Task SaveRecordsInternalAsync(string userId, List<MemoryRecord> records, CancellationToken ct)
    {
        var file = GetRecordsFilePath(userId);
        var tempFile = file + ".tmp";
        var backupFile = file + ".bak";

        var json = JsonSerializer.Serialize(records, JsonOptions);
        await File.WriteAllTextAsync(tempFile, json, ct).ConfigureAwait(false);

        if (File.Exists(file))
        {
            File.Copy(file, backupFile, overwrite: true);
        }

        File.Move(tempFile, file, overwrite: true);
    }

    private async Task<UserProfile> LoadProfileInternalAsync(string userId, CancellationToken ct)
    {
        var file = GetProfileFilePath(userId);
        if (!File.Exists(file)) return new UserProfile { UserId = userId };

        try
        {
            await using var stream = File.OpenRead(file);
            var profile = await JsonSerializer.DeserializeAsync<UserProfile>(stream, JsonOptions, ct).ConfigureAwait(false);
            return profile ?? new UserProfile { UserId = userId };
        }
        catch
        {
            return new UserProfile { UserId = userId };
        }
    }

    private async Task SaveProfileInternalAsync(string userId, UserProfile profile, CancellationToken ct)
    {
        var file = GetProfileFilePath(userId);
        var tempFile = file + ".tmp";
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(tempFile, json, ct).ConfigureAwait(false);
        File.Move(tempFile, file, overwrite: true);
    }

    private async Task<Dictionary<string, ProjectMemory>> LoadProjectsInternalAsync(string userId, CancellationToken ct)
    {
        var file = GetProjectsFilePath(userId);
        if (!File.Exists(file)) return new Dictionary<string, ProjectMemory>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var stream = File.OpenRead(file);
            var list = await JsonSerializer.DeserializeAsync<List<ProjectMemory>>(stream, JsonOptions, ct).ConfigureAwait(false);
            var dict = new Dictionary<string, ProjectMemory>(StringComparer.OrdinalIgnoreCase);
            if (list != null)
            {
                foreach (var p in list) dict[p.ProjectId] = p;
            }
            return dict;
        }
        catch
        {
            return new Dictionary<string, ProjectMemory>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveProjectsInternalAsync(string userId, Dictionary<string, ProjectMemory> projects, CancellationToken ct)
    {
        var file = GetProjectsFilePath(userId);
        var tempFile = file + ".tmp";
        var json = JsonSerializer.Serialize(projects.Values.ToList(), JsonOptions);
        await File.WriteAllTextAsync(tempFile, json, ct).ConfigureAwait(false);
        File.Move(tempFile, file, overwrite: true);
    }

    #endregion

    #region ISparkMemoryStore Implementation

    public async Task<MemoryRecord> RememberAsync(MemoryRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.Content))
            throw new ArgumentException("Memory content cannot be empty.", nameof(record));

        var userId = string.IsNullOrWhiteSpace(record.UserId) ? "default" : record.UserId.Trim();
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var records = await LoadRecordsInternalAsync(userId, ct).ConfigureAwait(false);

            // Check if record with same ID exists
            var existingIndex = records.FindIndex(r => string.Equals(r.Id, record.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                var existing = records[existingIndex];
                var updated = existing.WithUpdate(record.Content, "Updated via RememberAsync", "User");
                records[existingIndex] = updated;
                await SaveRecordsInternalAsync(userId, records, ct).ConfigureAwait(false);
                return updated;
            }

            // Conflict detection & deduplication within same category and project
            var normalizedNewContent = NormalizeForComparison(record.Content);
            var candidate = records.FirstOrDefault(r =>
                !r.IsArchived &&
                r.Scope == record.Scope &&
                r.Category == record.Category &&
                string.Equals(r.ProjectId, record.ProjectId, StringComparison.OrdinalIgnoreCase) &&
                CalculateJaccardSimilarity(normalizedNewContent, NormalizeForComparison(r.Content)) > 0.70);

            if (candidate != null)
            {
                // Supersede / Update the existing memory rather than creating an ambiguous clone
                var updated = candidate.WithUpdate(
                    record.Content,
                    $"Superseded by newer observation: {record.Provenance ?? "conversation"}",
                    "System");

                var candidateIdx = records.FindIndex(r => r.Id == candidate.Id);
                records[candidateIdx] = updated;
                await SaveRecordsInternalAsync(userId, records, ct).ConfigureAwait(false);
                return updated;
            }

            // Generate clean ID if none or default supplied
            var finalRecord = record with
            {
                Id = string.IsNullOrWhiteSpace(record.Id) ? $"mem_{Guid.NewGuid():N}"[..16] : record.Id,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            records.Add(finalRecord);
            await SaveRecordsInternalAsync(userId, records, ct).ConfigureAwait(false);
            return finalRecord;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> ForgetAsync(string id, string userId = "default", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var records = await LoadRecordsInternalAsync(userId, ct).ConfigureAwait(false);
            var countBefore = records.Count;
            records.RemoveAll(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

            if (records.Count < countBefore)
            {
                await SaveRecordsInternalAsync(userId, records, ct).ConfigureAwait(false);
                return true;
            }
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<MemoryRecord?> GetAsync(string id, string userId = "default", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var records = await LoadRecordsInternalAsync(userId, ct).ConfigureAwait(false);
            return records.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<MemoryRecord> UpdateAsync(string id, string newContent, string reason, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(newContent);

        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var records = await LoadRecordsInternalAsync(userId, ct).ConfigureAwait(false);
            var index = records.FindIndex(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new KeyNotFoundException($"Memory record '{id}' not found for user '{userId}'.");
            }

            var existing = records[index];
            var updated = existing.WithUpdate(newContent, reason, "User");
            records[index] = updated;
            await SaveRecordsInternalAsync(userId, records, ct).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<MemoryRevision>> GetHistoryAsync(string id, string userId = "default", CancellationToken ct = default)
    {
        var record = await GetAsync(id, userId, ct).ConfigureAwait(false);
        return record?.Revisions ?? Array.Empty<MemoryRevision>();
    }

    public async Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query,
        MemoryFilter? filter = null,
        int limit = 10,
        string userId = "default",
        CancellationToken ct = default)
    {
        if (limit <= 0) limit = 10;

        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var records = await LoadRecordsInternalAsync(userId, ct).ConfigureAwait(false);
            var filtered = ApplyFilter(records, filter);

            if (string.IsNullOrWhiteSpace(query))
            {
                return filtered
                    .OrderByDescending(r => r.IsPinned)
                    .ThenByDescending(r => r.UpdatedAt)
                    .Take(limit)
                    .Select(r => new MemorySearchResult(r, 1.0, Array.Empty<string>()))
                    .ToList();
            }

            var queryTokens = ExtractTokens(query);
            if (queryTokens.Count == 0)
            {
                return filtered.Take(limit).Select(r => new MemorySearchResult(r, 1.0, Array.Empty<string>())).ToList();
            }

            var results = new List<MemorySearchResult>();
            foreach (var record in filtered)
            {
                var recordTokens = ExtractTokens(record.Content);
                var matched = queryTokens.Intersect(recordTokens, StringComparer.OrdinalIgnoreCase).ToList();

                if (matched.Count > 0)
                {
                    double score = (double)matched.Count / queryTokens.Count;

                    // Boost for exact phrase match
                    if (record.Content.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.5;
                    }

                    // Boost for pinned records
                    if (record.IsPinned)
                    {
                        score += 0.3;
                    }

                    // Boost for recency (within last 7 days)
                    var daysOld = (DateTimeOffset.UtcNow - record.UpdatedAt).TotalDays;
                    if (daysOld <= 7)
                    {
                        score += Math.Max(0.0, 0.2 * (1.0 - daysOld / 7.0));
                    }

                    results.Add(new MemorySearchResult(record, score, matched));
                }
            }

            return results
                .OrderByDescending(r => r.RelevanceScore)
                .ThenByDescending(r => r.Record.UpdatedAt)
                .Take(limit)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<MemoryRecord>> GetRelevantMemoriesAsync(
        string currentMessage,
        string? activeProjectId = null,
        int limit = 8,
        string userId = "default",
        CancellationToken ct = default)
    {
        if (limit <= 0) limit = 8;

        var searchResults = await SearchAsync(
            currentMessage,
            new MemoryFilter
            {
                IncludeArchived = false,
                ProjectId = activeProjectId
            },
            limit: limit,
            userId: userId,
            ct: ct).ConfigureAwait(false);

        return searchResults.Select(r => r.Record).ToList();
    }

    public async Task<IReadOnlyList<MemoryRecord>> GetAllAsync(
        string userId = "default",
        MemoryFilter? filter = null,
        CancellationToken ct = default)
    {
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var records = await LoadRecordsInternalAsync(userId, ct).ConfigureAwait(false);
            return ApplyFilter(records, filter);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<UserProfile> GetUserProfileAsync(string userId = "default", CancellationToken ct = default)
    {
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await LoadProfileInternalAsync(userId, ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveUserProfileAsync(UserProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var userId = string.IsNullOrWhiteSpace(profile.UserId) ? "default" : profile.UserId.Trim();
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var updated = profile with { LastUpdatedAt = DateTimeOffset.UtcNow };
            await SaveProfileInternalAsync(userId, updated, ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ProjectMemory?> GetProjectMemoryAsync(string projectId, string userId = "default", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return null;

        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var projects = await LoadProjectsInternalAsync(userId, ct).ConfigureAwait(false);
            return projects.TryGetValue(projectId, out var project) ? project : null;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveProjectMemoryAsync(ProjectMemory project, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var projects = await LoadProjectsInternalAsync(userId, ct).ConfigureAwait(false);
            projects[project.ProjectId] = project with { LastUpdatedAt = DateTimeOffset.UtcNow };
            await SaveProjectsInternalAsync(userId, projects, ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string> ExportBackupJsonAsync(string userId = "default", CancellationToken ct = default)
    {
        var gate = GetUserGate(userId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var records = await LoadRecordsInternalAsync(userId, ct).ConfigureAwait(false);
            var profile = await LoadProfileInternalAsync(userId, ct).ConfigureAwait(false);
            var projects = await LoadProjectsInternalAsync(userId, ct).ConfigureAwait(false);

            var backup = new MemoryBackupPackage
            {
                UserId = userId,
                ExportedAt = DateTimeOffset.UtcNow,
                Profile = profile,
                Projects = projects.Values.ToList(),
                Records = records
            };

            return JsonSerializer.Serialize(backup, JsonOptions);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> RestoreBackupJsonAsync(string json, bool overwrite = false, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        var backup = JsonSerializer.Deserialize<MemoryBackupPackage>(json, JsonOptions)
            ?? throw new ArgumentException("Invalid backup package JSON.", nameof(json));

        var targetUserId = string.IsNullOrWhiteSpace(userId) ? (backup.UserId ?? "default") : userId;
        var gate = GetUserGate(targetUserId);
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var existingRecords = overwrite
                ? new List<MemoryRecord>()
                : await LoadRecordsInternalAsync(targetUserId, ct).ConfigureAwait(false);

            int restoredCount = 0;
            foreach (var rec in backup.Records)
            {
                var idx = existingRecords.FindIndex(r => r.Id == rec.Id);
                if (idx >= 0)
                {
                    if (overwrite)
                    {
                        existingRecords[idx] = rec;
                        restoredCount++;
                    }
                }
                else
                {
                    existingRecords.Add(rec with { UserId = targetUserId });
                    restoredCount++;
                }
            }

            await SaveRecordsInternalAsync(targetUserId, existingRecords, ct).ConfigureAwait(false);

            if (backup.Profile != null)
            {
                await SaveProfileInternalAsync(targetUserId, backup.Profile with { UserId = targetUserId }, ct).ConfigureAwait(false);
            }

            if (backup.Projects.Count > 0)
            {
                var existingProjects = overwrite
                    ? new Dictionary<string, ProjectMemory>(StringComparer.OrdinalIgnoreCase)
                    : await LoadProjectsInternalAsync(targetUserId, ct).ConfigureAwait(false);

                foreach (var p in backup.Projects)
                {
                    existingProjects[p.ProjectId] = p;
                }
                await SaveProjectsInternalAsync(targetUserId, existingProjects, ct).ConfigureAwait(false);
            }

            return restoredCount;
        }
        finally
        {
            gate.Release();
        }
    }

    #endregion

    #region Helper Methods

    private static List<MemoryRecord> ApplyFilter(List<MemoryRecord> records, MemoryFilter? filter)
    {
        if (filter == null)
            return records.Where(r => !r.IsArchived).ToList();

        var query = records.AsEnumerable();

        if (!filter.IncludeArchived)
            query = query.Where(r => !r.IsArchived);

        if (filter.Scope.HasValue)
            query = query.Where(r => r.Scope == filter.Scope.Value);

        if (filter.Category.HasValue)
            query = query.Where(r => r.Category == filter.Category.Value);

        if (!string.IsNullOrWhiteSpace(filter.ProjectId))
            query = query.Where(r => string.Equals(r.ProjectId, filter.ProjectId, StringComparison.OrdinalIgnoreCase));

        if (filter.RequiredTags is { Count: > 0 })
            query = query.Where(r => filter.RequiredTags.All(t => r.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));

        if (filter.CreatedAfter.HasValue)
            query = query.Where(r => r.CreatedAt >= filter.CreatedAfter.Value);

        if (filter.CreatedBefore.HasValue)
            query = query.Where(r => r.CreatedAt <= filter.CreatedBefore.Value);

        if (filter.MinConfidence > 0)
            query = query.Where(r => r.Confidence >= filter.MinConfidence);

        return query.ToList();
    }

    private static HashSet<string> ExtractTokens(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return tokens;

        var matches = TokenizerRegex.Matches(text);
        foreach (Match match in matches)
        {
            var word = match.Value.ToLowerInvariant();
            if (word.Length >= 2 && !StopWords.Contains(word))
            {
                tokens.Add(word);
            }
        }
        return tokens;
    }

    private static string NormalizeForComparison(string text) =>
        string.Join(" ", ExtractTokens(text).OrderBy(t => t));

    private static double CalculateJaccardSimilarity(string normA, string normB)
    {
        var setA = new HashSet<string>(normA.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var setB = new HashSet<string>(normB.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (setA.Count == 0 || setB.Count == 0) return 0.0;

        int intersection = setA.Intersect(setB).Count();
        int union = setA.Union(setB).Count();

        return union == 0 ? 0.0 : (double)intersection / union;
    }

    #endregion
}
