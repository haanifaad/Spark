using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Service contract for document parsing, format detection, text extraction, and chunk indexing.
/// </summary>
public interface IStudyDocumentIngestionService
{
    /// <summary>
    /// Ingests a local document file from disk into the user's knowledge base.
    /// </summary>
    Task<StudyDocument> IngestFileAsync(string filePath, string userId = "default", string? collectionId = null, CancellationToken ct = default);

    /// <summary>
    /// Ingests in-memory text/markdown/json content as a registered document.
    /// </summary>
    Task<StudyDocument> IngestContentAsync(string fileName, string format, string content, string userId = "default", string? collectionId = null, CancellationToken ct = default);

    /// <summary>
    /// Reprocesses an existing document (e.g. after extraction failure or updated chunking rules).
    /// </summary>
    Task<StudyDocument> ReprocessDocumentAsync(string documentId, string userId = "default", CancellationToken ct = default);

    /// <summary>
    /// Retrieves status of an asynchronous processing job.
    /// </summary>
    Task<DocumentProcessingJob?> GetProcessingJobStatusAsync(string jobId, string userId = "default", CancellationToken ct = default);
}
