using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

public sealed record StudySearchRequest(
    string Query,
    string UserId = "default",
    string? CollectionId = null,
    string? DocumentId = null,
    int MaxResults = 10,
    double MinScore = 0.05
);

public sealed record StudySearchResultItem(
    string ChunkId,
    string DocumentId,
    string DocumentTitle,
    int PageNumber,
    string? SectionTitle,
    string Content,
    string Snippet,
    double RelevanceScore,
    IReadOnlyList<string> MatchedKeywords
);

public sealed record StudySearchResponse(
    string Query,
    int TotalMatches,
    IReadOnlyList<StudySearchResultItem> Results,
    long DurationMs
);

/// <summary>
/// Service contract for grounded keyword, semantic, and hybrid retrieval across study documents.
/// </summary>
public interface IStudySearchService
{
    Task<StudySearchResponse> SearchAsync(StudySearchRequest request, CancellationToken ct = default);
    Task<int> RebuildIndexAsync(string userId = "default", CancellationToken ct = default);
}
