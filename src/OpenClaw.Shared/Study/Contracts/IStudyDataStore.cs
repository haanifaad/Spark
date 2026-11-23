using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Data persistence contract for the Study Division.
/// Guarantees thread-safe, isolated, atomic reads and writes.
/// </summary>
public interface IStudyDataStore
{
    string SchemaVersion { get; }

    Task InitializeAsync(CancellationToken ct = default);

    Task<IReadOnlyList<StudyCourse>> ListCoursesAsync(string userId = "default", CancellationToken ct = default);

    Task<StudyCourse?> GetCourseAsync(string courseId, string userId = "default", CancellationToken ct = default);

    Task<StudyCourse> SaveCourseAsync(StudyCourse course, string userId = "default", CancellationToken ct = default);

    Task<bool> DeleteCourseAsync(string courseId, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<StudySession>> ListSessionsAsync(string userId = "default", string? courseId = null, CancellationToken ct = default);

    Task<StudySession?> GetSessionAsync(string sessionId, string userId = "default", CancellationToken ct = default);

    Task<StudySession> SaveSessionAsync(StudySession session, string userId = "default", CancellationToken ct = default);

    Task<StudyProgress> GetProgressAsync(string courseId, string userId = "default", CancellationToken ct = default);

    Task<StudyProgress> SaveProgressAsync(StudyProgress progress, string userId = "default", CancellationToken ct = default);

    // Phase 2: Learning Objectives & Learning Paths
    Task<IReadOnlyList<LearningObjective>> ListObjectivesAsync(string? lessonId = null, string userId = "default", CancellationToken ct = default);

    Task<LearningObjective?> GetObjectiveAsync(string objectiveId, string userId = "default", CancellationToken ct = default);

    Task<LearningObjective> SaveObjectiveAsync(LearningObjective objective, string userId = "default", CancellationToken ct = default);

    Task<bool> MarkObjectiveMasteredAsync(string objectiveId, bool isMastered = true, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<LearningPath>> ListLearningPathsAsync(EducationLevel? level = null, string userId = "default", CancellationToken ct = default);

    Task<LearningPath?> GetLearningPathAsync(string pathId, string userId = "default", CancellationToken ct = default);

    Task<LearningPath> SaveLearningPathAsync(LearningPath path, string userId = "default", CancellationToken ct = default);

    // Phase 3: Skills, Tools & Slash Commands
    Task<IReadOnlyList<StudySkillDefinition>> ListSkillsAsync(string userId = "default", CancellationToken ct = default);

    Task<StudySkillDefinition?> GetSkillAsync(string skillId, string userId = "default", CancellationToken ct = default);

    Task<StudySkillDefinition> SaveSkillAsync(StudySkillDefinition skill, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<SkillExecutionRecord>> ListSkillExecutionsAsync(string? skillId = null, string userId = "default", int limit = 50, CancellationToken ct = default);

    Task SaveSkillExecutionAsync(SkillExecutionRecord record, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<StudyToolDefinition>> ListToolsAsync(string userId = "default", CancellationToken ct = default);

    Task<StudyToolDefinition?> GetToolAsync(string toolId, string userId = "default", CancellationToken ct = default);

    Task<StudyToolDefinition> SaveToolAsync(StudyToolDefinition tool, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<StudyToolExecutionResult>> ListToolExecutionsAsync(string? toolId = null, string userId = "default", int limit = 50, CancellationToken ct = default);

    Task<StudyToolExecutionResult?> GetToolExecutionAsync(string requestId, string userId = "default", CancellationToken ct = default);

    Task SaveToolExecutionAsync(StudyToolExecutionResult result, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<StudyCommandDefinition>> ListCommandsAsync(string userId = "default", CancellationToken ct = default);

    Task<StudyCommandDefinition?> GetCommandAsync(string name, string userId = "default", CancellationToken ct = default);

    Task<StudyCommandDefinition> SaveCommandAsync(StudyCommandDefinition command, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<CommandExecutionRecord>> ListCommandExecutionsAsync(string? commandName = null, string userId = "default", int limit = 50, CancellationToken ct = default);

    Task SaveCommandExecutionAsync(CommandExecutionRecord record, string userId = "default", CancellationToken ct = default);

    // Phase 4: Knowledge Collections & Documents
    Task<IReadOnlyList<KnowledgeCollection>> ListKnowledgeCollectionsAsync(string userId = "default", CancellationToken ct = default);
    Task<KnowledgeCollection?> GetKnowledgeCollectionAsync(string collectionId, string userId = "default", CancellationToken ct = default);
    Task<KnowledgeCollection> SaveKnowledgeCollectionAsync(KnowledgeCollection collection, string userId = "default", CancellationToken ct = default);
    Task<bool> DeleteKnowledgeCollectionAsync(string collectionId, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<StudyDocument>> ListStudyDocumentsAsync(string? collectionId = null, string userId = "default", CancellationToken ct = default);
    Task<StudyDocument?> GetStudyDocumentAsync(string documentId, string userId = "default", CancellationToken ct = default);
    Task<StudyDocument> SaveStudyDocumentAsync(StudyDocument document, string userId = "default", CancellationToken ct = default);
    Task<bool> DeleteStudyDocumentAsync(string documentId, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(string documentId, string userId = "default", CancellationToken ct = default);
    Task<IReadOnlyList<DocumentChunk>> GetAllUserChunksAsync(string userId = "default", CancellationToken ct = default);
    Task SaveDocumentChunksAsync(string documentId, IEnumerable<DocumentChunk> chunks, string userId = "default", CancellationToken ct = default);
    Task<bool> DeleteDocumentChunksAsync(string documentId, string userId = "default", CancellationToken ct = default);

    // Phase 4: Research Projects & Evidence
    Task<IReadOnlyList<ResearchProject>> ListResearchProjectsAsync(string userId = "default", CancellationToken ct = default);
    Task<ResearchProject?> GetResearchProjectAsync(string projectId, string userId = "default", CancellationToken ct = default);
    Task<ResearchProject> SaveResearchProjectAsync(ResearchProject project, string userId = "default", CancellationToken ct = default);
    Task<bool> DeleteResearchProjectAsync(string projectId, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<ResearchQuestion>> ListResearchQuestionsAsync(string projectId, string userId = "default", CancellationToken ct = default);
    Task<ResearchQuestion> SaveResearchQuestionAsync(ResearchQuestion question, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<ResearchNote>> ListResearchNotesAsync(string projectId, string userId = "default", CancellationToken ct = default);
    Task<ResearchNote?> GetResearchNoteAsync(string noteId, string userId = "default", CancellationToken ct = default);
    Task<ResearchNote> SaveResearchNoteAsync(ResearchNote note, string userId = "default", CancellationToken ct = default);
    Task<bool> DeleteResearchNoteAsync(string noteId, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<ResearchEvidence>> ListResearchEvidenceAsync(string projectId, string? questionId = null, string userId = "default", CancellationToken ct = default);
    Task<ResearchEvidence> SaveResearchEvidenceAsync(ResearchEvidence evidence, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<ResearchCitation>> ListResearchCitationsAsync(string? projectId = null, string userId = "default", CancellationToken ct = default);
    Task<ResearchCitation> SaveResearchCitationAsync(ResearchCitation citation, string userId = "default", CancellationToken ct = default);

    Task<DocumentProcessingJob?> GetProcessingJobAsync(string jobId, string userId = "default", CancellationToken ct = default);
    Task SaveProcessingJobAsync(DocumentProcessingJob job, string userId = "default", CancellationToken ct = default);
}
