using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Knowledge;
using OpenClaw.Shared.Study.Research;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyKnowledgeSecurityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudyDataStore _dataStore;
    private readonly StudyKnowledgeService _knowledgeService;
    private readonly StudyDocumentIngestionService _ingestionService;
    private readonly StudySearchService _searchService;
    private readonly StudyResearchService _researchService;

    public StudyKnowledgeSecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spark_study_sec_" + Guid.NewGuid().ToString("N"));
        _dataStore = new StudyDataStore(_tempDir);
        _knowledgeService = new StudyKnowledgeService(_dataStore);
        _ingestionService = new StudyDocumentIngestionService(_dataStore);
        _searchService = new StudySearchService(_dataStore);
        _researchService = new StudyResearchService(_dataStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CrossUserIsolation_DocumentsAndChunks_UserACannotAccessUserB()
    {
        // 1. User B ingests proprietary study notes
        var docB = await _ingestionService.IngestContentAsync(
            "user_b_secrets.md",
            "md",
            "# Confidential Thesis\nProprietary algorithm: Quantum-Resistant Homomorphic Lattice Cryptography.",
            userId: "user_b");

        // 2. User A attempts to list documents
        var userADocs = await _knowledgeService.ListDocumentsAsync(userId: "user_a");
        Assert.DoesNotContain(userADocs, d => d.Id == docB.Id);

        // 3. User A attempts to get Document B by ID
        var retrievedByA = await _knowledgeService.GetDocumentAsync(docB.Id, userId: "user_a");
        Assert.Null(retrievedByA);

        // 4. User A attempts to get chunks of Document B
        var chunksA = await _knowledgeService.GetDocumentChunksAsync(docB.Id, userId: "user_a");
        Assert.Empty(chunksA);

        // 5. User A attempts to delete Document B
        var deleteResult = await _knowledgeService.DeleteDocumentAsync(docB.Id, userId: "user_a");
        Assert.False(deleteResult);

        // 6. User B can still access their document
        var retrievedByB = await _knowledgeService.GetDocumentAsync(docB.Id, userId: "user_b");
        Assert.NotNull(retrievedByB);
    }

    [Fact]
    public async Task CrossUserIsolation_SearchEngine_NeverLeaksUserBDocumentsToUserA()
    {
        // User B has confidential document
        await _ingestionService.IngestContentAsync(
            "patent_draft.txt",
            "txt",
            "Breakthrough superconducting quantum qubit coherence at room temperature.",
            userId: "inventor_b");

        // User A searches for exact terms
        var searchRespA = await _searchService.SearchAsync(new StudySearchRequest(
            Query: "superconducting quantum qubit coherence",
            UserId: "researcher_a"));

        Assert.Equal(0, searchRespA.TotalMatches);
        Assert.Empty(searchRespA.Results);

        // User B searches and gets result
        var searchRespB = await _searchService.SearchAsync(new StudySearchRequest(
            Query: "superconducting quantum qubit coherence",
            UserId: "inventor_b"));

        Assert.True(searchRespB.TotalMatches >= 1);
        Assert.NotEmpty(searchRespB.Results);
    }

    [Fact]
    public async Task CrossUserIsolation_ResearchWorkspace_IsolatesProjectsNotesAndEvidence()
    {
        // User B creates project
        var projB = await _researchService.CreateProjectAsync(new ResearchProject(
            Id: "proj_secret_b",
            UserId: "user_b",
            Title: "Classified Aerospace Propulsion",
            Description: "Ion drive optimization"));

        var noteB = await _researchService.AddNoteAsync(new ResearchNote(
            Id: "note_b_1",
            ProjectId: projB.Id,
            UserId: "user_b",
            Title: "Specific Impulse Data",
            Content: "Isp exceeds 5000s."));

        // User A attempts to list projects
        var userAProjects = await _researchService.ListProjectsAsync(userId: "user_a");
        Assert.DoesNotContain(userAProjects, p => p.Id == projB.Id);

        // User A attempts to read project directly
        var retrievedProjA = await _researchService.GetProjectAsync(projB.Id, userId: "user_a");
        Assert.Null(retrievedProjA);

        // User A attempts to list notes
        var userANotes = await _researchService.ListNotesAsync(projB.Id, userId: "user_a");
        Assert.Empty(userANotes);

        // User A attempts to delete note
        var deletedNote = await _researchService.DeleteNoteAsync(noteB.Id, userId: "user_a");
        Assert.False(deletedNote);

        // User A attempts to delete project
        var deletedProj = await _researchService.DeleteProjectAsync(projB.Id, userId: "user_a");
        Assert.False(deletedProj);
    }

    [Fact]
    public async Task ConcurrentAccess_IngestionAndSearch_IsThreadSafeAndReliable()
    {
        // Concurrently ingest 10 documents
        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
        {
            await _ingestionService.IngestContentAsync(
                $"parallel_doc_{i}.txt",
                "txt",
                $"Concurrency test payload number {i} covering parallel processing and synchronization primitives.",
                userId: "concurrent_tester");
        }));

        await Task.WhenAll(tasks);

        var docs = await _knowledgeService.ListDocumentsAsync(userId: "concurrent_tester");
        Assert.Equal(10, docs.Count);

        var searchResp = await _searchService.SearchAsync(new StudySearchRequest(
            Query: "synchronization primitives",
            UserId: "concurrent_tester"));

        Assert.True(searchResp.TotalMatches >= 10);
    }
}
