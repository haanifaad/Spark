using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Knowledge;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudySearchAndRetrievalTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudyDataStore _dataStore;
    private readonly StudyKnowledgeService _knowledgeService;
    private readonly StudyDocumentIngestionService _ingestionService;
    private readonly StudySearchService _searchService;

    public StudySearchAndRetrievalTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spark_study_search_" + Guid.NewGuid().ToString("N"));
        _dataStore = new StudyDataStore(_tempDir);
        _knowledgeService = new StudyKnowledgeService(_dataStore);
        _ingestionService = new StudyDocumentIngestionService(_dataStore);
        _searchService = new StudySearchService(_dataStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task SearchAsync_KeywordsMatchingDocument_ReturnsRankedResultsWithSnippets()
    {
        // 1. Ingest test documents
        await _ingestionService.IngestContentAsync(
            "transformers.md",
            "md",
            "# Attention Mechanism\nThe Transformer architecture relies entirely on self-attention mechanisms without recurrent connections.\n---page---\n# Positional Encoding\nSince transformers contain no recurrence, positional encodings are injected to represent order.",
            "student_ai");

        await _ingestionService.IngestContentAsync(
            "cnn.md",
            "md",
            "# Convolutional Networks\nCNN architectures use convolutional filters for image feature maps.",
            "student_ai");

        // 2. Search for self-attention
        var response = await _searchService.SearchAsync(new StudySearchRequest(
            Query: "self-attention mechanism",
            UserId: "student_ai"));

        Assert.NotNull(response);
        Assert.True(response.TotalMatches >= 1, "Expected at least 1 match");
        Assert.NotEmpty(response.Results);

        var topResult = response.Results[0];
        Assert.Equal("transformers", topResult.DocumentTitle);
        Assert.Equal(1, topResult.PageNumber);
        Assert.Contains("Attention", topResult.Snippet);
        Assert.True(topResult.RelevanceScore > 0);
        Assert.True(response.DurationMs >= 0);
    }

    [Fact]
    public async Task SearchAsync_WithCollectionFilter_IsolatesResultsToRequestedCollection()
    {
        var col1 = await _knowledgeService.CreateCollectionAsync(new KnowledgeCollection(
            Id: "col_physics",
            UserId: "learner_x",
            Title: "Physics",
            Description: "Physics notes"));

        var col2 = await _knowledgeService.CreateCollectionAsync(new KnowledgeCollection(
            Id: "col_chemistry",
            UserId: "learner_x",
            Title: "Chemistry",
            Description: "Chemistry notes"));

        await _ingestionService.IngestContentAsync("optics.md", "md", "Laser diffraction and photon wavelengths.", "learner_x", collectionId: col1.Id);
        await _ingestionService.IngestContentAsync("reactions.md", "md", "Chemical reaction equilibrium and laser excitation.", "learner_x", collectionId: col2.Id);

        // Search specifically in Physics collection
        var physResponse = await _searchService.SearchAsync(new StudySearchRequest(
            Query: "laser",
            UserId: "learner_x",
            CollectionId: col1.Id));

        Assert.Single(physResponse.Results);
        Assert.Equal("optics", physResponse.Results[0].DocumentTitle);

        // Search specifically in Chemistry collection
        var chemResponse = await _searchService.SearchAsync(new StudySearchRequest(
            Query: "laser",
            UserId: "learner_x",
            CollectionId: col2.Id));

        Assert.Single(chemResponse.Results);
        Assert.Equal("reactions", chemResponse.Results[0].DocumentTitle);
    }

    [Fact]
    public async Task SearchAsync_MinScoreThreshold_FiltersOutLowRelevanceChunks()
    {
        await _ingestionService.IngestContentAsync(
            "algorithms.md",
            "md",
            "Sorting algorithms like quicksort and mergesort run in O(n log n) expected time.",
            "algo_user");

        // High MinScore that shouldn't match tangential text
        var response = await _searchService.SearchAsync(new StudySearchRequest(
            Query: "quicksort mergesort algorithms",
            UserId: "algo_user",
            MinScore: 10.0)); // artificially high threshold

        Assert.Empty(response.Results);
    }

    [Fact]
    public async Task SearchAsync_EmptyOrWhitespaceQuery_ReturnsEmptyResponseQuickly()
    {
        var response = await _searchService.SearchAsync(new StudySearchRequest(
            Query: "   ",
            UserId: "any_user"));

        Assert.NotNull(response);
        Assert.Equal(0, response.TotalMatches);
        Assert.Empty(response.Results);
    }

    [Fact]
    public async Task RebuildIndexAsync_ReturnsIndexedChunkCount()
    {
        await _ingestionService.IngestContentAsync("doc1.txt", "txt", "Alpha Beta Gamma", "index_user");
        await _ingestionService.IngestContentAsync("doc2.txt", "txt", "Delta Epsilon Zeta", "index_user");

        var count = await _searchService.RebuildIndexAsync("index_user");
        Assert.True(count >= 2, $"Expected at least 2 chunks indexed, got {count}");
    }
}
