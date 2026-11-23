using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenClaw.Shared.Memory;

/// <summary>
/// Scopes of persistent memory in SPARK.
/// </summary>
public enum MemoryScope
{
    /// <summary>Explicit user profile facts, preferences, and personal details.</summary>
    UserProfile,
    /// <summary>Long-term semantic facts, learned concepts, and world knowledge.</summary>
    Semantic,
    /// <summary>Episodic conversation summaries, past decisions, and milestone insights.</summary>
    Episodic,
    /// <summary>Project-specific context, conventions, repository rules, and architectures.</summary>
    Project,
    /// <summary>Active session context and short-term dialogue memory.</summary>
    SessionContext,
    /// <summary>Temporary scratchpad and tool-execution working memory.</summary>
    WorkingMemory
}

/// <summary>
/// Functional category of the remembered information.
/// </summary>
public enum MemoryCategory
{
    Preference,
    Identity,
    ProjectConvention,
    TechnicalDecision,
    ActiveGoal,
    Constraint,
    Workflow,
    GeneralFact
}

/// <summary>
/// A versioned revision record capturing previous contents and reasons for modification.
/// </summary>
public sealed record MemoryRevision(
    int Version,
    string Content,
    DateTimeOffset ChangedAt,
    string? Reason = null,
    string? Author = null);

/// <summary>
/// A single versioned persistent memory record in the SPARK autonomous agent system.
/// </summary>
public sealed record MemoryRecord
{
    public required string Id { get; init; }
    public string UserId { get; init; } = "default";
    public MemoryScope Scope { get; init; } = MemoryScope.Semantic;
    public MemoryCategory Category { get; init; } = MemoryCategory.GeneralFact;
    public required string Content { get; init; }
    public int Version { get; init; } = 1;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? SourceSessionId { get; init; }
    public string? ProjectId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? Provenance { get; init; }
    public double Confidence { get; init; } = 1.0;
    public bool IsPinned { get; init; }
    public bool IsArchived { get; init; }
    public IReadOnlyList<MemoryRevision> Revisions { get; init; } = Array.Empty<MemoryRevision>();

    /// <summary>
    /// Creates a new revision record of this memory with updated content.
    /// </summary>
    public MemoryRecord WithUpdate(string newContent, string reason, string? author = null)
    {
        var revision = new MemoryRevision(Version, Content, DateTimeOffset.UtcNow, reason, author);
        var updatedRevisions = new List<MemoryRevision>(Revisions) { revision };

        return this with
        {
            Content = newContent,
            Version = Version + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            Revisions = updatedRevisions
        };
    }
}

/// <summary>
/// Memory search result item with calculated relevance score and matched terms.
/// </summary>
public sealed record MemorySearchResult(
    MemoryRecord Record,
    double RelevanceScore,
    IReadOnlyList<string> MatchedTerms);

/// <summary>
/// Filter criteria for querying the memory store.
/// </summary>
public sealed record MemoryFilter
{
    public MemoryScope? Scope { get; init; }
    public MemoryCategory? Category { get; init; }
    public string? ProjectId { get; init; }
    public IReadOnlyList<string>? RequiredTags { get; init; }
    public bool IncludeArchived { get; init; }
    public DateTimeOffset? CreatedAfter { get; init; }
    public DateTimeOffset? CreatedBefore { get; init; }
    public double MinConfidence { get; init; } = 0.0;
}

/// <summary>
/// User profile representation storing personal facts, preferences, and communication habits.
/// </summary>
public sealed record UserProfile
{
    public string UserId { get; init; } = "default";
    public string? DisplayName { get; init; }
    public string? PreferredLanguage { get; init; } = "en";
    public string? Timezone { get; init; }
    public string? RoleOrOccupation { get; init; }
    public IReadOnlyList<string> WorkingConventions { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> CustomFields { get; init; } = new Dictionary<string, string>();
    public DateTimeOffset LastUpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Project-level memory partition storing technical stack, architectural conventions, and constraints.
/// </summary>
public sealed record ProjectMemory
{
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public string? RootDirectory { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> TechStack { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ArchitecturalRules { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ActiveGoals { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();
    public DateTimeOffset LastUpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Complete backup package for SPARK memory export and restoration.
/// </summary>
public sealed record MemoryBackupPackage
{
    public int BackupVersion { get; init; } = 1;
    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.UtcNow;
    public string UserId { get; init; } = "default";
    public UserProfile? Profile { get; init; }
    public IReadOnlyList<ProjectMemory> Projects { get; init; } = Array.Empty<ProjectMemory>();
    public IReadOnlyList<MemoryRecord> Records { get; init; } = Array.Empty<MemoryRecord>();
}
