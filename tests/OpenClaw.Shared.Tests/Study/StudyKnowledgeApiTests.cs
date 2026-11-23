using System;
using System.IO;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Application;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyKnowledgeApiTests : IDisposable
{
    private readonly string _testRoot;
    private readonly StudyDataStore _store;
    private readonly StudyApiService _api;

    public StudyKnowledgeApiTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SparkStudyKnowledgeApiTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _store = new StudyDataStore(_testRoot);
        _api = new StudyApiService(_store);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task CollectionsEndpoints_CreateAndList_WorksViaApi()
    {
        await _store.InitializeAsync();

        var col = new KnowledgeCollection(
            Id: "col_api_1",
            UserId: "api_user",
            Title: "Neuroscience",
            Description: "Neural pathways");

        var createResp = await _api.CreateCollectionAsync(col, userId: "api_user");
        Assert.True(createResp.Success);
        Assert.NotNull(createResp.Data);
        Assert.Equal("Neuroscience", createResp.Data.Title);

        var listResp = await _api.GetCollectionsAsync(userId: "api_user");
        Assert.True(listResp.Success);
        Assert.NotNull(listResp.Data);
        Assert.Contains(listResp.Data, c => c.Id == col.Id);
    }

    [Fact]
    public async Task IngestionAndSearchEndpoints_WorkViaApi()
    {
        await _store.InitializeAsync();

        var sampleFile = Path.Combine(_testRoot, "relativity.txt");
        await File.WriteAllTextAsync(sampleFile, "Albert Einstein published the general theory of relativity in 1915 describing curvature of spacetime.");

        var ingestResp = await _api.IngestDocumentFileAsync(sampleFile, userId: "einstein_user");
        Assert.True(ingestResp.Success);
        Assert.NotNull(ingestResp.Data);
        Assert.Equal("relativity.txt", ingestResp.Data.FileName);
        Assert.Equal("relativity", ingestResp.Data.Title);

        var searchResp = await _api.SearchKnowledgeAsync(new StudySearchRequest(
            Query: "spacetime curvature",
            UserId: "einstein_user"));

        Assert.True(searchResp.Success);
        Assert.NotNull(searchResp.Data);
        Assert.True(searchResp.Data.TotalMatches >= 1);
        Assert.Contains("spacetime", searchResp.Data.Results[0].Snippet);
    }

    [Fact]
    public async Task ResearchAndCitationEndpoints_WorkViaApi()
    {
        await _store.InitializeAsync();

        var proj = new ResearchProject(
            Id: "proj_api_1",
            UserId: "research_user",
            Title: "Quantum Computing Algorithms",
            Description: "Shor and Grover algorithms");

        var createProjResp = await _api.CreateResearchProjectAsync(proj, userId: "research_user");
        Assert.True(createProjResp.Success);
        Assert.NotNull(createProjResp.Data);

        var listProjResp = await _api.GetResearchProjectsAsync(userId: "research_user");
        Assert.True(listProjResp.Success);
        Assert.Contains(listProjResp.Data!, p => p.Id == proj.Id);

        var citation = new ResearchCitation(
            Id: "cit_api_1",
            ProjectId: proj.Id,
            UserId: "research_user",
            SourceTitle: "Polynomial-Time Algorithms for Prime Factorization and Discrete Logarithms on a Quantum Computer",
            Author: "Shor, Peter W.",
            Year: 1997,
            Style: "IEEE",
            FormattedCitation: "[1] P. W. Shor, 'Polynomial-Time Algorithms for Prime Factorization,' 1997.");

        var createCitResp = await _api.CreateCitationAsync(citation, userId: "research_user");
        Assert.True(createCitResp.Success);

        var listCitResp = await _api.GetCitationsAsync(projectId: proj.Id, userId: "research_user");
        Assert.True(listCitResp.Success);
        Assert.Contains(listCitResp.Data!, c => c.Id == citation.Id);
    }

    [Fact]
    public async Task GroundedAnswerEndpoint_WorksViaApi()
    {
        await _store.InitializeAsync();

        var sampleFile = Path.Combine(_testRoot, "photosynthesis.txt");
        await File.WriteAllTextAsync(sampleFile, "Photosynthesis in green plants converts sunlight into glucose and releases oxygen.");

        await _api.IngestDocumentFileAsync(sampleFile, userId: "bio_student");

        var answerResp = await _api.GetGroundedAnswerAsync(
            query: "How does photosynthesis produce oxygen?",
            userId: "bio_student");

        Assert.True(answerResp.Success);
        Assert.NotNull(answerResp.Data);
        Assert.True(answerResp.Data.IsGroundedInSources);
        Assert.Contains("Source: photosynthesis, Page 1", answerResp.Data.Answer);
    }
}
