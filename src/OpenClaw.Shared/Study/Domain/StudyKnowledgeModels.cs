using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// Status of document parsing, text extraction, and chunk indexing.
/// </summary>
public enum DocumentProcessingStatus
{
    Pending = 0,
    Processing = 1,
    Indexed = 2,
    Failed = 3,
    Reprocessing = 4
}

/// <summary>
/// Logical collection grouping study materials, notes, and course textbooks.
/// </summary>
public sealed record KnowledgeCollection(
    string Id,
    string UserId,
    string Title,
    string Description,
    string? SubjectId = null,
    string? CourseId = null,
    List<string>? Tags = null,
    List<string>? DocumentIds = null,
    DateTime CreatedAtUtc = default,
    DateTime UpdatedAtUtc = default
)
{
    public List<string> Tags { get; init; } = Tags ?? [];
    public List<string> DocumentIds { get; init; } = DocumentIds ?? [];
}

/// <summary>
/// Registered document inside the learner's knowledge repository.
/// </summary>
public sealed record StudyDocument(
    string Id,
    string UserId,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    string Format,
    string Title,
    string? Author = null,
    string? CollectionId = null,
    int PageCount = 0,
    int CharacterCount = 0,
    DocumentProcessingStatus Status = DocumentProcessingStatus.Pending,
    string? ErrorMessage = null,
    string? Sha256Checksum = null,
    DateTime CreatedAtUtc = default,
    DateTime? ProcessedAtUtc = null,
    Dictionary<string, string>? Metadata = null
)
{
    public DocumentProcessingStatus Status { get; set; } = Status;
    public string? ErrorMessage { get; set; } = ErrorMessage;
    public int PageCount { get; set; } = PageCount;
    public int CharacterCount { get; set; } = CharacterCount;
    public DateTime? ProcessedAtUtc { get; set; } = ProcessedAtUtc;
    public Dictionary<string, string> Metadata { get; init; } = Metadata ?? [];
}

/// <summary>
/// Semantically coherent document segment retaining source provenance and page location.
/// </summary>
public sealed record DocumentChunk(
    string Id,
    string DocumentId,
    string UserId,
    int ChunkIndex,
    int PageNumber,
    string? SectionTitle,
    string Content,
    int TokenCount,
    List<string>? Keywords = null
)
{
    public List<string> Keywords { get; init; } = Keywords ?? [];
}

/// <summary>
/// Background asynchronous ingestion tracking record.
/// </summary>
public sealed record DocumentProcessingJob(
    string JobId,
    string DocumentId,
    string UserId,
    DocumentProcessingStatus Status,
    int ProgressPercentage,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc = null,
    string? Details = null
);
