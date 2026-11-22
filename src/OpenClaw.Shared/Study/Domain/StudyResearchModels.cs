using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Study.Domain;

public enum ResearchProjectStatus
{
    Active = 0,
    Archived = 1,
    Completed = 2
}

public enum ResearchQuestionStatus
{
    Open = 0,
    Investigating = 1,
    Answered = 2
}

/// <summary>
/// Dedicated academic research project tracking inquiries, evidence, and citations.
/// </summary>
public sealed record ResearchProject(
    string Id,
    string UserId,
    string Title,
    string Description,
    string? Hypothesis = null,
    ResearchProjectStatus Status = ResearchProjectStatus.Active,
    List<string>? QuestionIds = null,
    List<string>? NoteIds = null,
    List<string>? SourceDocumentIds = null,
    List<string>? EvidenceIds = null,
    DateTime CreatedAtUtc = default,
    DateTime UpdatedAtUtc = default
)
{
    public ResearchProjectStatus Status { get; set; } = Status;
    public string? Hypothesis { get; set; } = Hypothesis;
    public List<string> QuestionIds { get; init; } = QuestionIds ?? [];
    public List<string> NoteIds { get; init; } = NoteIds ?? [];
    public List<string> SourceDocumentIds { get; init; } = SourceDocumentIds ?? [];
    public List<string> EvidenceIds { get; init; } = EvidenceIds ?? [];
}

/// <summary>
/// Specific research inquiry within a research project.
/// </summary>
public sealed record ResearchQuestion(
    string Id,
    string ProjectId,
    string UserId,
    string QuestionText,
    ResearchQuestionStatus Status = ResearchQuestionStatus.Open,
    string? Findings = null,
    int EvidenceCount = 0,
    DateTime CreatedAtUtc = default
)
{
    public ResearchQuestionStatus Status { get; set; } = Status;
    public string? Findings { get; set; } = Findings;
    public int EvidenceCount { get; set; } = EvidenceCount;
}

/// <summary>
/// Working research note or conceptual synthesis.
/// </summary>
public sealed record ResearchNote(
    string Id,
    string ProjectId,
    string UserId,
    string Title,
    string Content,
    List<string>? Tags = null,
    string? RelatedDocumentId = null,
    DateTime CreatedAtUtc = default,
    DateTime UpdatedAtUtc = default
)
{
    public string Title { get; set; } = Title;
    public string Content { get; set; } = Content;
    public List<string> Tags { get; init; } = Tags ?? [];
}

/// <summary>
/// Empirical excerpt extracted from a verified document source.
/// </summary>
public sealed record ResearchEvidence(
    string Id,
    string ProjectId,
    string? QuestionId,
    string UserId,
    string Excerpt,
    string DocumentId,
    string DocumentTitle,
    int PageNumber,
    string CredibilityRating = "Primary Source",
    string? Analysis = null,
    DateTime CreatedAtUtc = default
);

/// <summary>
/// Bibliographic citation record referencing academic sources.
/// </summary>
public sealed record ResearchCitation(
    string Id,
    string? ProjectId,
    string UserId,
    string SourceTitle,
    string Author,
    int Year,
    string Style,
    string FormattedCitation,
    string? UrlOrDoi = null,
    DateTime CreatedAtUtc = default
);

/// <summary>
/// Synthesized academic research report ready for export.
/// </summary>
public sealed record ResearchReport(
    string ReportId,
    string ProjectId,
    string UserId,
    string Title,
    string ExecutiveSummary,
    Dictionary<string, string> Sections,
    List<string> Citations,
    DateTime GeneratedAtUtc
);
