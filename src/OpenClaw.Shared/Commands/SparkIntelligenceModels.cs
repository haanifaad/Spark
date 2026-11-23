using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClaw.Shared;

/// <summary>
/// Categories of permanent user memories.
/// </summary>
public enum SparkMemoryCategory
{
    Preference,
    Project,
    Learning,
    Technical,
    Goal,
    Decision
}

/// <summary>
/// A single persistent user memory record.
/// </summary>
public sealed record SparkMemoryEntry(
    string Id,
    string Content,
    SparkMemoryCategory Category,
    DateTimeOffset CreatedAt,
    string? SourceSessionKey = null,
    double RelevanceScore = 1.0);

/// <summary>
/// Context budget and token estimation for a conversation thread.
/// </summary>
public sealed record SparkContextBudget(
    long UsedTokens,
    long MaxContextTokens,
    double UsagePercent,
    bool IsNearLimit,
    long MessageTokens,
    long AttachmentTokens,
    long MemoryTokens)
{
    public static SparkContextBudget Estimate(
        long messageTokens,
        long attachmentTokens,
        long memoryTokens,
        long maxContextTokens)
    {
        var used = messageTokens + attachmentTokens + memoryTokens;
        var limit = maxContextTokens > 0 ? maxContextTokens : 128_000;
        var pct = Math.Min(100.0, Math.Round((double)used / limit * 100.0, 1));
        var near = pct >= 80.0;
        return new SparkContextBudget(used, limit, pct, near, messageTokens, attachmentTokens, memoryTokens);
    }
}

/// <summary>
/// A web research citation or source item.
/// </summary>
public sealed record SparkResearchSource(
    string Id,
    string Title,
    string Url,
    string? Excerpt = null,
    string? SiteName = null);

/// <summary>
/// Multi-source research result container.
/// </summary>
public sealed record SparkResearchReport(
    string Query,
    IReadOnlyList<SparkResearchSource> Sources,
    string Summary,
    DateTimeOffset RetrievedAt);
