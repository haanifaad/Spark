using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Research;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyResearchWorkspaceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudyDataStore _dataStore;
    private readonly StudyResearchService _researchService;

    public StudyResearchWorkspaceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spark_study_research_" + Guid.NewGuid().ToString("N"));
        _dataStore = new StudyDataStore(_tempDir);
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
    public async Task ResearchProject_Lifecycle_CreateUpdateListDelete()
    {
        var proj = new ResearchProject(
            Id: "proj_astronomy_1",
            UserId: "astro_student",
            Title: "Black Hole Thermodynamics",
            Description: "Study of Hawking radiation and entropy bounds",
            Hypothesis: "Black hole event horizons emit thermal radiation.");

        // 1. Create
        var created = await _researchService.CreateProjectAsync(proj);
        Assert.NotNull(created);
        Assert.Equal("Black Hole Thermodynamics", created.Title);

        // 2. Get & List
        var retrieved = await _researchService.GetProjectAsync(created.Id, "astro_student");
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);

        var list = await _researchService.ListProjectsAsync("astro_student");
        Assert.Single(list);

        // 3. Update
        var updated = await _researchService.UpdateProjectAsync(retrieved with
        {
            Description = "Updated description with Kerr metric focus."
        });
        Assert.Equal("Updated description with Kerr metric focus.", updated.Description);

        // 4. Delete
        var deleted = await _researchService.DeleteProjectAsync(created.Id, "astro_student");
        Assert.True(deleted);

        var afterDelete = await _researchService.GetProjectAsync(created.Id, "astro_student");
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task ResearchQuestions_AddAndStatusProgression_UpdatesCorrectly()
    {
        var proj = await _researchService.CreateProjectAsync(new ResearchProject(
            Id: "proj_neuro",
            UserId: "neuro_student",
            Title: "Synaptic Plasticity",
            Description: "LTP and LTD mechanisms"));

        var q = await _researchService.AddQuestionAsync(new ResearchQuestion(
            Id: "q_ltp_1",
            ProjectId: proj.Id,
            UserId: "neuro_student",
            QuestionText: "What is the role of NMDA receptors in induction of LTP?"));

        Assert.Equal(ResearchQuestionStatus.Open, q.Status);

        // Progress to Investigating
        var investigatingQ = await _researchService.UpdateQuestionStatusAsync(q.Id, ResearchQuestionStatus.Investigating, userId: "neuro_student");
        Assert.Equal(ResearchQuestionStatus.Investigating, investigatingQ.Status);

        // Progress to Answered with findings
        var answeredQ = await _researchService.UpdateQuestionStatusAsync(
            q.Id,
            ResearchQuestionStatus.Answered,
            findings: "NMDA receptors act as coincidence detectors requiring both glutamate and depolarization to remove the magnesium block.",
            userId: "neuro_student");

        Assert.Equal(ResearchQuestionStatus.Answered, answeredQ.Status);
        Assert.NotNull(answeredQ.Findings);
        Assert.Contains("magnesium block", answeredQ.Findings);
    }

    [Fact]
    public async Task ResearchNotesAndEvidence_AddAndRetrieve_PreservesProvenance()
    {
        var proj = await _researchService.CreateProjectAsync(new ResearchProject(
            Id: "proj_genetics",
            UserId: "bio_student",
            Title: "CRISPR-Cas9 Editing",
            Description: "Targeted genomic modifications"));

        // Add Note
        var note = await _researchService.AddNoteAsync(new ResearchNote(
            Id: "note_1",
            ProjectId: proj.Id,
            UserId: "bio_student",
            Title: "Off-target cleavage",
            Content: "High-fidelity variants reduce non-specific guide RNA binding.",
            Tags: new() { "CRISPR", "Specificity" }));

        Assert.NotNull(note);
        var notes = await _researchService.ListNotesAsync(proj.Id, "bio_student");
        Assert.Single(notes);

        // Add Evidence
        var evidence = await _researchService.AddEvidenceAsync(new ResearchEvidence(
            Id: "ev_1",
            ProjectId: proj.Id,
            QuestionId: null,
            UserId: "bio_student",
            Excerpt: "Enhanced Cas9 enzymes demonstrate greater than 99% on-target specificity.",
            DocumentId: "doc_nature_2020",
            DocumentTitle: "Nature 2020",
            PageNumber: 14,
            CredibilityRating: "High",
            Analysis: "Confirms feasibility of clinical editing without broad off-target mutations."));

        Assert.NotNull(evidence);
        var evidenceList = await _researchService.ListEvidenceAsync(proj.Id, userId: "bio_student");
        Assert.Single(evidenceList);
        Assert.Equal(14, evidenceList[0].PageNumber);
        Assert.Equal("High", evidenceList[0].CredibilityRating);
    }

    [Fact]
    public async Task CitationFormatting_APA_MLA_Chicago_IEEE_Harvard_ProducesAccurateStrings()
    {
        // Test APA
        var apa = await _researchService.FormatCitationAsync(
            title: "Structure and Interpretation of Computer Programs",
            author: "Abelson, H. & Sussman, G. J.",
            year: 1996,
            style: "APA");
        Assert.Equal("APA", apa.Style);
        Assert.Contains("Abelson, H. & Sussman, G. J.", apa.FormattedCitation);
        Assert.Contains("(1996)", apa.FormattedCitation);

        // Test MLA
        var mla = await _researchService.FormatCitationAsync(
            title: "The Design of Everyday Things",
            author: "Norman, Donald A.",
            year: 2013,
            style: "MLA");
        Assert.Equal("MLA", mla.Style);
        Assert.Contains("\"The Design of Everyday Things.\"", mla.FormattedCitation);

        // Test IEEE
        var ieee = await _researchService.FormatCitationAsync(
            title: "A Mathematical Theory of Communication",
            author: "Shannon, C. E.",
            year: 1948,
            style: "IEEE");
        Assert.Equal("IEEE", ieee.Style);
        Assert.Contains("[1] Shannon, C. E.", ieee.FormattedCitation);

        // Test Chicago
        var chicago = await _researchService.FormatCitationAsync(
            title: "The Structure of Scientific Revolutions",
            author: "Kuhn, Thomas S.",
            year: 1962,
            style: "CHICAGO");
        Assert.Equal("CHICAGO", chicago.Style);
        Assert.Contains("Kuhn, Thomas S.", chicago.FormattedCitation);

        // Test Harvard
        var harvard = await _researchService.FormatCitationAsync(
            title: "Artificial Intelligence: A Modern Approach",
            author: "Russell, S. & Norvig, P.",
            year: 2020,
            style: "HARVARD");
        Assert.Equal("HARVARD", harvard.Style);
        Assert.Contains("Russell, S. & Norvig, P.", harvard.FormattedCitation);
    }

    [Fact]
    public async Task GenerateReportAsync_CompilesFullStructuredMarkdownReport()
    {
        var proj = await _researchService.CreateProjectAsync(new ResearchProject(
            Id: "proj_relativity",
            UserId: "einstein_fan",
            Title: "Gravitational Waves",
            Description: "Detection by laser interferometry",
            Hypothesis: "Spacetime ripples can be detected via interferometer strain."));

        await _researchService.AddQuestionAsync(new ResearchQuestion(
            Id: "q_gw_1",
            ProjectId: proj.Id,
            UserId: "einstein_fan",
            QuestionText: "How did LIGO detect the first binary black hole merger?",
            Status: ResearchQuestionStatus.Answered,
            Findings: "GW150914 observed strain amplitude of ~10^-21 using dual 4km Fabry-Pérot cavities."));

        await _researchService.AddNoteAsync(new ResearchNote(
            Id: "note_gw_1",
            ProjectId: proj.Id,
            UserId: "einstein_fan",
            Title: "Arm length modulation",
            Content: "Quadrupole metric perturbations cause differential phase shift in orthogonal arms."));

        await _researchService.AddCitationAsync(new ResearchCitation(
            Id: "cit_gw_1",
            UserId: "einstein_fan",
            ProjectId: proj.Id,
            SourceTitle: "Observation of Gravitational Waves from a Binary Black Hole Merger",
            Author: "Abbott, B. P. et al. (LIGO Scientific Collaboration)",
            Year: 2016,
            Style: "APA",
            FormattedCitation: "Abbott, B. P. et al. (2016). Observation of Gravitational Waves. Physical Review Letters, 116(6)."));

        var report = await _researchService.GenerateReportAsync(proj.Id, "einstein_fan");

        Assert.NotNull(report);
        Assert.Equal(proj.Id, report.ProjectId);
        Assert.Equal("Research Synthesis: Gravitational Waves", report.Title);
        Assert.Contains("Spacetime ripples", report.ExecutiveSummary);

        Assert.Contains("Inquiries & Findings", report.Sections.Keys);
        Assert.Contains("Empirical Evidence", report.Sections.Keys);
        Assert.Contains("Conceptual Synthesis", report.Sections.Keys);

        Assert.Contains("GW150914", report.Sections["Inquiries & Findings"]);
        Assert.Contains("Arm length modulation", report.Sections["Conceptual Synthesis"]);
        Assert.Contains(report.Citations, c => c.Contains("Abbott, B. P."));
    }
}
