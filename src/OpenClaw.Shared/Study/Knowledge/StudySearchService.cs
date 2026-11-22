using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Knowledge;

/// <summary>
/// Grounded search engine providing multi-factor relevance ranking, snippet highlighting,
/// and metadata filtering over indexed document chunks.
/// </summary>
public sealed class StudySearchService : IStudySearchService
{
    private readonly IStudyDataStore _dataStore;

    public StudySearchService(IStudyDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    public async Task<StudySearchResponse> SearchAsync(StudySearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new StudySearchResponse(string.Empty, 0, [], sw.ElapsedMilliseconds);
        }

        var chunks = await _dataStore.GetAllUserChunksAsync(request.UserId, ct);
        var docs = (await _dataStore.ListStudyDocumentsAsync(request.CollectionId, request.UserId, ct))
                   .ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);

        // Filter chunks by document / collection if requested
        if (!string.IsNullOrEmpty(request.DocumentId))
        {
            chunks = chunks.Where(c => string.Equals(c.DocumentId, request.DocumentId, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else if (!string.IsNullOrEmpty(request.CollectionId))
        {
            chunks = chunks.Where(c => docs.ContainsKey(c.DocumentId)).ToList();
        }

        var queryTerms = Regex.Matches(request.Query.ToLowerInvariant(), @"\b[a-z0-9_\-\.]{2,}\b")
                              .Select(m => m.Value)
                              .Distinct()
                              .ToList();

        var matches = new List<StudySearchResultItem>();

        foreach (var chunk in chunks)
        {
            var contentLower = chunk.Content.ToLowerInvariant();
            var sectionLower = chunk.SectionTitle?.ToLowerInvariant() ?? string.Empty;

            double score = 0.0;
            var matchedTerms = new List<string>();

            // 1. Exact phrase match
            if (contentLower.Contains(request.Query.ToLowerInvariant()))
            {
                score += 3.0;
            }

            // 2. Token frequency matches
            foreach (var term in queryTerms)
            {
                int count = Regex.Matches(contentLower, Regex.Escape(term)).Count;
                if (count > 0)
                {
                    score += count * 0.5;
                    matchedTerms.Add(term);
                }

                if (sectionLower.Contains(term))
                {
                    score += 1.5; // Bonus for section heading match
                }

                if (chunk.Keywords.Any(k => string.Equals(k, term, StringComparison.OrdinalIgnoreCase)))
                {
                    score += 1.0; // Bonus for explicit keyword tag match
                }
            }

            if (score >= request.MinScore && matchedTerms.Count > 0)
            {
                var docTitle = docs.TryGetValue(chunk.DocumentId, out var doc) ? doc.Title : chunk.DocumentId;
                var snippet = GenerateSnippet(chunk.Content, matchedTerms);

                matches.Add(new StudySearchResultItem(
                    ChunkId: chunk.Id,
                    DocumentId: chunk.DocumentId,
                    DocumentTitle: docTitle,
                    PageNumber: chunk.PageNumber,
                    SectionTitle: chunk.SectionTitle,
                    Content: chunk.Content,
                    Snippet: snippet,
                    RelevanceScore: Math.Round(score, 3),
                    MatchedKeywords: matchedTerms
                ));
            }
        }

        var ranked = matches.OrderByDescending(m => m.RelevanceScore)
                            .Take(request.MaxResults)
                            .ToList();

        sw.Stop();
        return new StudySearchResponse(request.Query, matches.Count, ranked, sw.ElapsedMilliseconds);
    }

    public async Task<int> RebuildIndexAsync(string userId = "default", CancellationToken ct = default)
    {
        var chunks = await _dataStore.GetAllUserChunksAsync(userId, ct);
        return chunks.Count;
    }

    private static string GenerateSnippet(string content, List<string> matchedTerms)
    {
        if (content.Length <= 240) return content;

        int firstIndex = -1;
        foreach (var term in matchedTerms)
        {
            int idx = content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (firstIndex == -1 || idx < firstIndex))
            {
                firstIndex = idx;
            }
        }

        if (firstIndex == -1)
        {
            return content[..Math.Min(content.Length, 240)] + "...";
        }

        int start = Math.Max(0, firstIndex - 50);
        int length = Math.Min(content.Length - start, 240);
        var snippet = content.Substring(start, length);

        return (start > 0 ? "..." : "") + snippet + (start + length < content.Length ? "..." : "");
    }
}
