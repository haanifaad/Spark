using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Service contract managing personal knowledge collections, documents, and chunk provenance.
/// </summary>
public interface IStudyKnowledgeService
{
    // Collections
    Task<KnowledgeCollection> CreateCollectionAsync(KnowledgeCollection collection, CancellationToken ct = default);
    Task<KnowledgeCollection?> GetCollectionAsync(string collectionId, string userId = "default", CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeCollection>> ListCollectionsAsync(string userId = "default", CancellationToken ct = default);
    Task<KnowledgeCollection> UpdateCollectionAsync(KnowledgeCollection collection, CancellationToken ct = default);
    Task<bool> DeleteCollectionAsync(string collectionId, string userId = "default", CancellationToken ct = default);

    // Documents
    Task<StudyDocument> RegisterDocumentAsync(StudyDocument document, CancellationToken ct = default);
    Task<StudyDocument?> GetDocumentAsync(string documentId, string userId = "default", CancellationToken ct = default);
    Task<IReadOnlyList<StudyDocument>> ListDocumentsAsync(string? collectionId = null, string userId = "default", CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(string documentId, string userId = "default", CancellationToken ct = default);

    // Chunks
    Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(string documentId, string userId = "default", CancellationToken ct = default);
    Task<DocumentChunk?> GetChunkAsync(string chunkId, string userId = "default", CancellationToken ct = default);
}
