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

public sealed class StudyGroundedAiTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudyDataStore _dataStore;
    private readonly StudyKnowledgeService _knowledgeService;
    private readonly StudyDocumentIngestionService _ingestionService;
    private readonly StudySearchService _searchService;
    private readonly StudyGroundedAnswerSynthesizer _synthesizer;

    public StudyGroundedAiTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spark_study_grounded_ai_" + Guid.NewGuid().ToString("N"));
        _dataStore = new StudyDataStore(_tempDir);
        _knowledgeService = new StudyKnowledgeService(_dataStore);
        _ingestionService = new StudyDocumentIngestionService(_dataStore);
        _searchService = new StudySearchService(_dataStore);
        _synthesizer = new StudyGroundedAnswerSynthesizer(_searchService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task GenerateGroundedAnswerAsync_WithMatchingDocuments_IncludesSourceBadges()
    {
        // 1. Ingest document
        await _ingestionService.IngestContentAsync(
            "mitochondria.md",
            "md",
            "# Mitochondria Biology\nMitochondria generate most of the chemical energy needed to power biochemical reactions via ATP synthesis.\n---page---\n# Cellular Respiration\nOxidative phosphorylation occurs within the inner mitochondrial membrane.",
            "biology_student");

        // 2. Synthesize answer
        var result = await _synthesizer.GenerateGroundedAnswerAsync(
            query: "What is the function of mitochondria?",
            userId: "biology_student",
            mode: TeachingMode.Standard,
            language: LearningLanguage.English);

        Assert.NotNull(result);
        Assert.True(result.IsGroundedInSources);
        Assert.NotEmpty(result.CitedSources);
        Assert.Contains("mitochondria", result.CitedSources.Select(s => s.DocumentTitle));

        // Verify source badge format [Source: {doc}, Page {page}]
        Assert.Contains("Source: mitochondria, Page 1", result.Answer);
        Assert.Contains("ATP synthesis", result.Answer);
    }

    [Fact]
    public async Task GenerateGroundedAnswerAsync_NoMatchingDocuments_ProvidesTransparentFallbackDisclaimer()
    {
        // No documents ingested for this user
        var result = await _synthesizer.GenerateGroundedAnswerAsync(
            query: "Explain string theory M-branes",
            userId: "empty_user",
            language: LearningLanguage.English);

        Assert.NotNull(result);
        Assert.False(result.IsGroundedInSources);
        Assert.Empty(result.CitedSources);

        // Verify transparent disclaimer
        Assert.Contains("No relevant passages were found in your indexed study documents", result.Answer);
    }

    [Fact]
    public async Task GenerateGroundedAnswerAsync_MalayalamLanguage_ProducesMalayalamDisclaimerAndGuidance()
    {
        var result = await _synthesizer.GenerateGroundedAnswerAsync(
            query: "ക്വാണ്ടം ഭൗതികശാസ്ത്രം എന്താണ്?",
            userId: "malayalam_user",
            language: LearningLanguage.Malayalam);

        Assert.NotNull(result);
        Assert.False(result.IsGroundedInSources);

        // Verify Malayalam disclaimer
        Assert.Contains("നിങ്ങളുടെ അപ്‌ലോഡ് ചെയ്ത രേഖകളിൽ ഈ ചോദ്യത്തിനാവശ്യമായ വിവരങ്ങൾ ലഭ്യമായിട്ടില്ല", result.Answer);
        Assert.Contains("അടിസ്ഥാന തത്വങ്ങൾ വിശകലനം", result.Answer);
    }

    [Fact]
    public async Task GenerateGroundedAnswerAsync_SocraticMode_AddsReflectionPrompts()
    {
        await _ingestionService.IngestContentAsync(
            "calculus.md",
            "md",
            "Derivatives represent the instantaneous rate of change of a function.",
            "math_student");

        var result = await _synthesizer.GenerateGroundedAnswerAsync(
            query: "What is a derivative?",
            userId: "math_student",
            mode: TeachingMode.Socratic,
            language: LearningLanguage.English);

        Assert.NotNull(result);
        Assert.True(result.IsGroundedInSources);
        Assert.Contains("Reflective Question", result.Answer);
    }

    [Fact]
    public async Task GenerateGroundedAnswerAsync_EmptyQuery_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _synthesizer.GenerateGroundedAnswerAsync("   ", "user1"));
    }
}
