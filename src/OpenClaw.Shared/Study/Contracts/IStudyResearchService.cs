using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Service contract managing research projects, research questions, evidence linking, and citations.
/// </summary>
public interface IStudyResearchService
{
    // Projects
    Task<ResearchProject> CreateProjectAsync(ResearchProject project, CancellationToken ct = default);
    Task<ResearchProject?> GetProjectAsync(string projectId, string userId = "default", CancellationToken ct = default);
    Task<IReadOnlyList<ResearchProject>> ListProjectsAsync(string userId = "default", CancellationToken ct = default);
    Task<ResearchProject> UpdateProjectAsync(ResearchProject project, CancellationToken ct = default);
    Task<bool> DeleteProjectAsync(string projectId, string userId = "default", CancellationToken ct = default);

    // Questions
    Task<ResearchQuestion> AddQuestionAsync(ResearchQuestion question, CancellationToken ct = default);
    Task<IReadOnlyList<ResearchQuestion>> ListQuestionsAsync(string projectId, string userId = "default", CancellationToken ct = default);
    Task<ResearchQuestion> UpdateQuestionStatusAsync(string questionId, ResearchQuestionStatus status, string? findings = null, string userId = "default", CancellationToken ct = default);

    // Notes
    Task<ResearchNote> AddNoteAsync(ResearchNote note, CancellationToken ct = default);
    Task<IReadOnlyList<ResearchNote>> ListNotesAsync(string projectId, string userId = "default", CancellationToken ct = default);
    Task<ResearchNote> UpdateNoteAsync(ResearchNote note, CancellationToken ct = default);
    Task<bool> DeleteNoteAsync(string noteId, string userId = "default", CancellationToken ct = default);

    // Evidence
    Task<ResearchEvidence> AddEvidenceAsync(ResearchEvidence evidence, CancellationToken ct = default);
    Task<IReadOnlyList<ResearchEvidence>> ListEvidenceAsync(string projectId, string? questionId = null, string userId = "default", CancellationToken ct = default);

    // Citations
    Task<ResearchCitation> AddCitationAsync(ResearchCitation citation, CancellationToken ct = default);
    Task<IReadOnlyList<ResearchCitation>> ListCitationsAsync(string? projectId = null, string userId = "default", CancellationToken ct = default);
    Task<ResearchCitation> FormatCitationAsync(string title, string author, int year, string style = "APA", string? url = null, string? projectId = null, string userId = "default", CancellationToken ct = default);

    // Reports
    Task<ResearchReport> GenerateReportAsync(string projectId, string userId = "default", CancellationToken ct = default);
}
