using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Research;

/// <summary>
/// Domain service orchestrating academic research workflows, hypothesis scoping,
/// evidence extraction, citation management, and synthesis report compilation.
/// </summary>
public sealed class StudyResearchService : IStudyResearchService
{
    private readonly IStudyDataStore _dataStore;

    public StudyResearchService(IStudyDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    // ══════════════════════════════════════════════════════════════════
    // Research Projects
    // ══════════════════════════════════════════════════════════════════

    public async Task<ResearchProject> CreateProjectAsync(ResearchProject project, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(project.Title))
        {
            throw new ArgumentException("Research project title is required.");
        }

        var proj = project with
        {
            Id = string.IsNullOrWhiteSpace(project.Id) ? "proj-" + Guid.NewGuid().ToString("N")[..10] : project.Id,
            CreatedAtUtc = project.CreatedAtUtc == default ? DateTime.UtcNow : project.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow
        };

        return await _dataStore.SaveResearchProjectAsync(proj, proj.UserId, ct);
    }

    public async Task<ResearchProject?> GetProjectAsync(string projectId, string userId = "default", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return null;
        return await _dataStore.GetResearchProjectAsync(projectId, userId, ct);
    }

    public async Task<IReadOnlyList<ResearchProject>> ListProjectsAsync(string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.ListResearchProjectsAsync(userId, ct);
    }

    public async Task<ResearchProject> UpdateProjectAsync(ResearchProject project, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var proj = project with { UpdatedAtUtc = DateTime.UtcNow };
        return await _dataStore.SaveResearchProjectAsync(proj, proj.UserId, ct);
    }

    public async Task<bool> DeleteProjectAsync(string projectId, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.DeleteResearchProjectAsync(projectId, userId, ct);
    }

    // ══════════════════════════════════════════════════════════════════
    // Research Questions
    // ══════════════════════════════════════════════════════════════════

    public async Task<ResearchQuestion> AddQuestionAsync(ResearchQuestion question, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(question);
        var q = question with
        {
            Id = string.IsNullOrWhiteSpace(question.Id) ? "rq-" + Guid.NewGuid().ToString("N")[..10] : question.Id,
            CreatedAtUtc = question.CreatedAtUtc == default ? DateTime.UtcNow : question.CreatedAtUtc
        };

        var saved = await _dataStore.SaveResearchQuestionAsync(q, q.UserId, ct);

        // Update project questions list
        var project = await _dataStore.GetResearchProjectAsync(q.ProjectId, q.UserId, ct);
        if (project != null && !project.QuestionIds.Contains(saved.Id))
        {
            project.QuestionIds.Add(saved.Id);
            await _dataStore.SaveResearchProjectAsync(project, q.UserId, ct);
        }

        return saved;
    }

    public async Task<IReadOnlyList<ResearchQuestion>> ListQuestionsAsync(string projectId, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.ListResearchQuestionsAsync(projectId, userId, ct);
    }

    public async Task<ResearchQuestion> UpdateQuestionStatusAsync(
        string questionId,
        ResearchQuestionStatus status,
        string? findings = null,
        string userId = "default",
        CancellationToken ct = default)
    {
        var questions = await _dataStore.ListResearchQuestionsAsync(string.Empty, userId, ct);
        var target = questions.FirstOrDefault(q => string.Equals(q.Id, questionId, StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            throw new KeyNotFoundException($"Research question '{questionId}' not found.");
        }

        var updated = target with
        {
            Status = status,
            Findings = findings ?? target.Findings
        };

        return await _dataStore.SaveResearchQuestionAsync(updated, userId, ct);
    }

    // ══════════════════════════════════════════════════════════════════
    // Research Notes
    // ══════════════════════════════════════════════════════════════════

    public async Task<ResearchNote> AddNoteAsync(ResearchNote note, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(note);
        var n = note with
        {
            Id = string.IsNullOrWhiteSpace(note.Id) ? "rn-" + Guid.NewGuid().ToString("N")[..10] : note.Id,
            CreatedAtUtc = note.CreatedAtUtc == default ? DateTime.UtcNow : note.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var saved = await _dataStore.SaveResearchNoteAsync(n, n.UserId, ct);

        var project = await _dataStore.GetResearchProjectAsync(n.ProjectId, n.UserId, ct);
        if (project != null && !project.NoteIds.Contains(saved.Id))
        {
            project.NoteIds.Add(saved.Id);
            await _dataStore.SaveResearchProjectAsync(project, n.UserId, ct);
        }

        return saved;
    }

    public async Task<IReadOnlyList<ResearchNote>> ListNotesAsync(string projectId, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.ListResearchNotesAsync(projectId, userId, ct);
    }

    public async Task<ResearchNote> UpdateNoteAsync(ResearchNote note, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(note);
        var n = note with { UpdatedAtUtc = DateTime.UtcNow };
        return await _dataStore.SaveResearchNoteAsync(n, n.UserId, ct);
    }

    public async Task<bool> DeleteNoteAsync(string noteId, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.DeleteResearchNoteAsync(noteId, userId, ct);
    }

    // ══════════════════════════════════════════════════════════════════
    // Research Evidence
    // ══════════════════════════════════════════════════════════════════

    public async Task<ResearchEvidence> AddEvidenceAsync(ResearchEvidence evidence, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var ev = evidence with
        {
            Id = string.IsNullOrWhiteSpace(evidence.Id) ? "ev-" + Guid.NewGuid().ToString("N")[..10] : evidence.Id,
            CreatedAtUtc = evidence.CreatedAtUtc == default ? DateTime.UtcNow : evidence.CreatedAtUtc
        };

        var saved = await _dataStore.SaveResearchEvidenceAsync(ev, ev.UserId, ct);

        var project = await _dataStore.GetResearchProjectAsync(ev.ProjectId, ev.UserId, ct);
        if (project != null && !project.EvidenceIds.Contains(saved.Id))
        {
            project.EvidenceIds.Add(saved.Id);
            await _dataStore.SaveResearchProjectAsync(project, ev.UserId, ct);
        }

        return saved;
    }

    public async Task<IReadOnlyList<ResearchEvidence>> ListEvidenceAsync(string projectId, string? questionId = null, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.ListResearchEvidenceAsync(projectId, questionId, userId, ct);
    }

    // ══════════════════════════════════════════════════════════════════
    // Research Citations
    // ══════════════════════════════════════════════════════════════════

    public async Task<ResearchCitation> AddCitationAsync(ResearchCitation citation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(citation);
        var c = citation with
        {
            Id = string.IsNullOrWhiteSpace(citation.Id) ? "cite-" + Guid.NewGuid().ToString("N")[..10] : citation.Id,
            CreatedAtUtc = citation.CreatedAtUtc == default ? DateTime.UtcNow : citation.CreatedAtUtc
        };

        return await _dataStore.SaveResearchCitationAsync(c, c.UserId, ct);
    }

    public async Task<IReadOnlyList<ResearchCitation>> ListCitationsAsync(string? projectId = null, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.ListResearchCitationsAsync(projectId, userId, ct);
    }

    public async Task<ResearchCitation> FormatCitationAsync(
        string title,
        string author,
        int year,
        string style = "APA",
        string? url = null,
        string? projectId = null,
        string userId = "default",
        CancellationToken ct = default)
    {
        var formatted = (style.ToUpperInvariant()) switch
        {
            "MLA" => $"{author}. \"{title}.\" ({year}). {url}".Trim(),
            "CHICAGO" => $"{author}. {year}. {title}. {url}".Trim(),
            "IEEE" => $"[1] {author}, \"{title},\" {year}. {url}".Trim(),
            "HARVARD" => $"{author} ({year}) '{title}'. Available at: {url}".Trim(),
            _ => $"{author} ({year}). {title}. {url}".Trim() // Default APA
        };

        var citation = new ResearchCitation(
            Id: "cite-" + Guid.NewGuid().ToString("N")[..10],
            ProjectId: projectId,
            UserId: userId,
            SourceTitle: title,
            Author: author,
            Year: year,
            Style: style.ToUpperInvariant(),
            FormattedCitation: formatted,
            UrlOrDoi: url,
            CreatedAtUtc: DateTime.UtcNow
        );

        return await AddCitationAsync(citation, ct);
    }

    // ══════════════════════════════════════════════════════════════════
    // Synthesis & Reports
    // ══════════════════════════════════════════════════════════════════

    public async Task<ResearchReport> GenerateReportAsync(string projectId, string userId = "default", CancellationToken ct = default)
    {
        var project = await GetProjectAsync(projectId, userId, ct);
        if (project == null)
        {
            throw new KeyNotFoundException($"Project '{projectId}' not found.");
        }

        var questions = await ListQuestionsAsync(projectId, userId, ct);
        var notes = await ListNotesAsync(projectId, userId, ct);
        var evidence = await ListEvidenceAsync(projectId, null, userId, ct);
        var citations = await ListCitationsAsync(projectId, userId, ct);

        var sections = new Dictionary<string, string>();

        // 1. Objectives & Questions Section
        var qSb = new StringBuilder();
        foreach (var q in questions)
        {
            qSb.AppendLine($"- **Question:** {q.QuestionText} [{q.Status}]");
            if (!string.IsNullOrEmpty(q.Findings))
            {
                qSb.AppendLine($"  *Findings:* {q.Findings}");
            }
        }
        sections["Inquiries & Findings"] = qSb.ToString();

        // 2. Empirical Evidence Section
        var evSb = new StringBuilder();
        foreach (var ev in evidence)
        {
            evSb.AppendLine($"> \"{ev.Excerpt}\"");
            evSb.AppendLine($"  — Source: *{ev.DocumentTitle}*, Page {ev.PageNumber} ({ev.CredibilityRating})");
            if (!string.IsNullOrEmpty(ev.Analysis))
            {
                evSb.AppendLine($"  *Analysis:* {ev.Analysis}");
            }
            evSb.AppendLine();
        }
        sections["Empirical Evidence"] = evSb.ToString();

        // 3. Conceptual Synthesis
        var noteSb = new StringBuilder();
        foreach (var n in notes)
        {
            noteSb.AppendLine($"### {n.Title}");
            noteSb.AppendLine(n.Content);
            noteSb.AppendLine();
        }
        sections["Conceptual Synthesis"] = noteSb.ToString();

        var citationStrings = citations.Select(c => c.FormattedCitation).ToList();

        var summary = $"Research Project '{project.Title}'. Analyzed {questions.Count} inquiries and {evidence.Count} empirical citations.";
        if (!string.IsNullOrWhiteSpace(project.Hypothesis))
        {
            summary += $" Hypothesis: {project.Hypothesis}";
        }

        return new ResearchReport(
            ReportId: "rep-" + Guid.NewGuid().ToString("N")[..10],
            ProjectId: projectId,
            UserId: userId,
            Title: $"Research Synthesis: {project.Title}",
            ExecutiveSummary: summary,
            Sections: sections,
            Citations: citationStrings,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }
}
