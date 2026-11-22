using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Knowledge;

/// <summary>
/// Domain service implementing personal knowledge collection and document lifecycle management.
/// </summary>
public sealed class StudyKnowledgeService : IStudyKnowledgeService
{
    private readonly IStudyDataStore _dataStore;

    public StudyKnowledgeService(IStudyDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    public async Task<KnowledgeCollection> CreateCollectionAsync(KnowledgeCollection collection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        if (string.IsNullOrWhiteSpace(collection.Title))
        {
            throw new ArgumentException("Collection title cannot be empty.");
        }

        var col = collection with
        {
            Id = string.IsNullOrWhiteSpace(collection.Id) ? "col-" + Guid.NewGuid().ToString("N")[..10] : collection.Id,
            CreatedAtUtc = collection.CreatedAtUtc == default ? DateTime.UtcNow : collection.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow
        };

        return await _dataStore.SaveKnowledgeCollectionAsync(col, col.UserId, ct);
    }

    public async Task<KnowledgeCollection?> GetCollectionAsync(string collectionId, string userId = "default", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(collectionId)) return null;
        return await _dataStore.GetKnowledgeCollectionAsync(collectionId, userId, ct);
    }

    public async Task<IReadOnlyList<KnowledgeCollection>> ListCollectionsAsync(string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.ListKnowledgeCollectionsAsync(userId, ct);
    }

    public async Task<KnowledgeCollection> UpdateCollectionAsync(KnowledgeCollection collection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var col = collection with { UpdatedAtUtc = DateTime.UtcNow };
        return await _dataStore.SaveKnowledgeCollectionAsync(col, col.UserId, ct);
    }

    public async Task<bool> DeleteCollectionAsync(string collectionId, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.DeleteKnowledgeCollectionAsync(collectionId, userId, ct);
    }

    public async Task<StudyDocument> RegisterDocumentAsync(StudyDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var doc = document with
        {
            Id = string.IsNullOrWhiteSpace(document.Id) ? "doc-" + Guid.NewGuid().ToString("N")[..12] : document.Id,
            CreatedAtUtc = document.CreatedAtUtc == default ? DateTime.UtcNow : document.CreatedAtUtc
        };

        return await _dataStore.SaveStudyDocumentAsync(doc, doc.UserId, ct);
    }

    public async Task<StudyDocument?> GetDocumentAsync(string documentId, string userId = "default", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(documentId)) return null;
        return await _dataStore.GetStudyDocumentAsync(documentId, userId, ct);
    }

    public async Task<IReadOnlyList<StudyDocument>> ListDocumentsAsync(string? collectionId = null, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.ListStudyDocumentsAsync(collectionId, userId, ct);
    }

    public async Task<bool> DeleteDocumentAsync(string documentId, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.DeleteStudyDocumentAsync(documentId, userId, ct);
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(string documentId, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.GetDocumentChunksAsync(documentId, userId, ct);
    }

    public async Task<DocumentChunk?> GetChunkAsync(string chunkId, string userId = "default", CancellationToken ct = default)
    {
        var all = await _dataStore.GetAllUserChunksAsync(userId, ct);
        return all.FirstOrDefault(c => string.Equals(c.Id, chunkId, StringComparison.OrdinalIgnoreCase));
    }
}
