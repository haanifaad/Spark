using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Knowledge;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyKnowledgeIngestionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudyDataStore _dataStore;
    private readonly StudyKnowledgeService _knowledgeService;
    private readonly StudyDocumentIngestionService _ingestionService;

    public StudyKnowledgeIngestionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spark_study_ingestion_" + Guid.NewGuid().ToString("N"));
        _dataStore = new StudyDataStore(_tempDir);
        _knowledgeService = new StudyKnowledgeService(_dataStore);
        _ingestionService = new StudyDocumentIngestionService(_dataStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task IngestContentAsync_MarkdownWithHeadingsAndPages_CreatesChunksWithKeywords()
    {
        var mdContent = @"# Quantum Mechanics Fundamentals
Quantum mechanics is a fundamental theory in physics that describes the physical properties of nature.
---page---
## Wave-Particle Duality
Light and matter exhibit both wave-like and particle-like properties. De Broglie wavelength relates momentum to wavelength.
---page---
## Schrödinger Equation
The Schrödinger equation is a linear partial differential equation that governs the wave function of a quantum-mechanical system.";

        var doc = await _ingestionService.IngestContentAsync(
            fileName: "quantum_physics.md",
            format: "md",
            content: mdContent,
            userId: "student_1");

        Assert.NotNull(doc);
        Assert.Equal("quantum_physics.md", doc.FileName);
        Assert.Equal("quantum physics", doc.Title);
        Assert.Equal(".md", doc.Format);
        Assert.Equal("student_1", doc.UserId);
        Assert.Equal(3, doc.PageCount);
        Assert.NotNull(doc.Sha256Checksum);
        Assert.NotEmpty(doc.Sha256Checksum);

        // Verify chunks in data store
        var chunks = await _knowledgeService.GetDocumentChunksAsync(doc.Id, "student_1");
        Assert.True(chunks.Count >= 3, $"Expected at least 3 chunks, got {chunks.Count}");

        var firstChunk = chunks.FirstOrDefault(c => c.PageNumber == 1);
        Assert.NotNull(firstChunk);
        Assert.Contains("Quantum Mechanics Fundamentals", firstChunk.Content);
        Assert.Contains("quantum", firstChunk.Keywords);

        var waveChunk = chunks.FirstOrDefault(c => c.PageNumber == 2);
        Assert.NotNull(waveChunk);
        Assert.Contains("Wave-Particle Duality", waveChunk.SectionTitle ?? waveChunk.Content);
        Assert.Contains("wavelength", waveChunk.Keywords);
    }

    [Fact]
    public async Task IngestFileAsync_TextFileFromDisk_ParsesAndChunksCorrectly()
    {
        var filePath = Path.Combine(_tempDir, "sample_notes.txt");
        await File.WriteAllTextAsync(filePath, "Computer Science principles: Operating Systems, Compilers, Distributed Databases.", Encoding.UTF8);

        var doc = await _ingestionService.IngestFileAsync(filePath, userId: "user_a");

        Assert.NotNull(doc);
        Assert.Equal("sample_notes.txt", doc.FileName);
        Assert.Equal("sample notes", doc.Title);
        Assert.Equal(".txt", doc.Format);
        Assert.True(doc.FileSizeBytes > 0);
        Assert.Equal(1, doc.PageCount);

        var chunks = await _knowledgeService.GetDocumentChunksAsync(doc.Id, "user_a");
        Assert.NotEmpty(chunks);
        Assert.Contains("Distributed", chunks[0].Content);
    }

    [Fact]
    public async Task IngestFileAsync_DuplicateContent_UpdatesExistingDocumentWithoutDuplicatingChunks()
    {
        var filePath = Path.Combine(_tempDir, "duplicate_test.txt");
        await File.WriteAllTextAsync(filePath, "Calculus 101: Limits, derivatives, integrals.", Encoding.UTF8);

        // First ingestion
        var doc1 = await _ingestionService.IngestFileAsync(filePath, userId: "math_student");
        Assert.NotNull(doc1);

        // Second ingestion with same file
        var doc2 = await _ingestionService.IngestFileAsync(filePath, userId: "math_student");
        Assert.NotNull(doc2);
        Assert.Equal(doc1.Id, doc2.Id); // Preserves existing document ID

        var chunks = await _knowledgeService.GetDocumentChunksAsync(doc1.Id, "math_student");
        var distinctChunks = chunks.Select(c => c.Id).Distinct().Count();
        Assert.Equal(chunks.Count, distinctChunks);
    }

    [Fact]
    public async Task IngestContentAsync_JsonAndCsvFormats_HandlesGracefully()
    {
        var jsonContent = @"{ ""course"": ""Biology"", ""module"": ""Cell Division"", ""mitosis"": true }";
        var docJson = await _ingestionService.IngestContentAsync("cell_bio.json", "json", jsonContent, "bio_user");
        Assert.Equal(".json", docJson.Format);
        var jsonChunks = await _knowledgeService.GetDocumentChunksAsync(docJson.Id, "bio_user");
        Assert.NotEmpty(jsonChunks);

        var csvContent = "Id,Concept,Formula\n1,Kinematics,v=u+at\n2,Newton2,F=ma\n";
        var docCsv = await _ingestionService.IngestContentAsync("physics_formula.csv", "csv", csvContent, "bio_user");
        Assert.Equal(".csv", docCsv.Format);
        var csvChunks = await _knowledgeService.GetDocumentChunksAsync(docCsv.Id, "bio_user");
        Assert.NotEmpty(csvChunks);
    }

    [Fact]
    public async Task ReprocessDocumentAsync_RebuildsChunksSuccessfully()
    {
        var doc = await _ingestionService.IngestContentAsync(
            "ai_ethics.md",
            "md",
            "# AI Governance\nTransparency and accountability in neural systems.\n---page---\nSafety alignments and human oversight.",
            "researcher");

        var initialChunks = await _knowledgeService.GetDocumentChunksAsync(doc.Id, "researcher");
        Assert.Equal(2, initialChunks.Count);

        var reprocessedDoc = await _ingestionService.ReprocessDocumentAsync(doc.Id, "researcher");
        Assert.NotNull(reprocessedDoc);
        Assert.Equal(doc.Id, reprocessedDoc.Id);

        var reprocessedChunks = await _knowledgeService.GetDocumentChunksAsync(doc.Id, "researcher");
        Assert.Equal(2, reprocessedChunks.Count);
    }

    [Fact]
    public async Task IngestFileAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _ingestionService.IngestFileAsync(Path.Combine(_tempDir, "does_not_exist.pdf"), "user1"));
    }

    [Fact]
    public async Task IngestContentAsync_InvalidArguments_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _ingestionService.IngestContentAsync("", "md", "content", "user1"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _ingestionService.IngestContentAsync("doc.md", "md", "", "user1"));
    }
}
