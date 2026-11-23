using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Memory;

/// <summary>
/// Contract for the unified SPARK memory system supporting long-term, semantic,
/// episodic, and project-level memory partitions with search, retrieval,
/// conflict resolution, and privacy controls.
/// </summary>
public interface ISparkMemoryStore
{
    /// <summary>
    /// Explicitly records a new memory entry. If a conflicting memory exists, resolves or versions it.
    /// </summary>
    Task<MemoryRecord> RememberAsync(MemoryRecord record, CancellationToken ct = default);

    /// <summary>
    /// Explicitly forgets/deletes a memory record by ID.
    /// </summary>
    Task<bool> ForgetAsync(string id, string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single memory record by ID.
    /// </summary>
    Task<MemoryRecord?> GetAsync(string id, string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Updates an existing memory record, archiving the prior state into revision history.
    /// </summary>
    Task<MemoryRecord> UpdateAsync(string id, string newContent, string reason, string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Retrieves the revision history of a memory record.
    /// </summary>
    Task<IReadOnlyList<MemoryRevision>> GetHistoryAsync(string id, string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Searches memories using keyword and semantic relevance scoring with filtering.
    /// </summary>
    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query,
        MemoryFilter? filter = null,
        int limit = 10,
        string userId = "default",
        CancellationToken ct = default);

    /// <summary>
    /// Contextual memory retriever used during conversation turn generation.
    /// Analyzes the user prompt and active project context to pull top relevant memories.
    /// </summary>
    Task<IReadOnlyList<MemoryRecord>> GetRelevantMemoriesAsync(
        string currentMessage,
        string? activeProjectId = null,
        int limit = 8,
        string userId = "default",
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all memories for a user, optionally filtered.
    /// </summary>
    Task<IReadOnlyList<MemoryRecord>> GetAllAsync(
        string userId = "default",
        MemoryFilter? filter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the user profile.
    /// </summary>
    Task<UserProfile> GetUserProfileAsync(string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Saves or updates the user profile.
    /// </summary>
    Task SaveUserProfileAsync(UserProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Retrieves project-level memory.
    /// </summary>
    Task<ProjectMemory?> GetProjectMemoryAsync(string projectId, string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Saves project-level memory.
    /// </summary>
    Task SaveProjectMemoryAsync(ProjectMemory project, string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Exports all memory records, profile, and project knowledge to a JSON backup package.
    /// </summary>
    Task<string> ExportBackupJsonAsync(string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Restores memory records from a JSON backup package with collision handling.
    /// </summary>
    Task<int> RestoreBackupJsonAsync(string json, bool overwrite = false, string userId = "default", CancellationToken ct = default);
}
