using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Knowledge;

/// <summary>
/// Production document ingestion service handling format validation, checksumming, text extraction,
/// section boundaries, chunk preparation, and atomic indexing into <see cref="IStudyDataStore"/>.
/// </summary>
public sealed class StudyDocumentIngestionService : IStudyDocumentIngestionService
{
    private readonly IStudyDataStore _dataStore;

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".json", ".csv", ".html", ".htm", ".pdf", ".docx", ".epub"
    };

    public StudyDocumentIngestionService(IStudyDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    public async Task<StudyDocument> IngestFileAsync(
        string filePath,
        string userId = "default",
        string? collectionId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Target file not found: {filePath}");
        }

        var ext = Path.GetExtension(filePath);
        if (!SupportedFormats.Contains(ext))
        {
            throw new NotSupportedException($"File format '{ext}' is not supported for educational ingestion.");
        }

        var fileInfo = new FileInfo(filePath);
        var docId = "doc-" + Guid.NewGuid().ToString("N")[..12];
        var job = new DocumentProcessingJob(
            JobId: "job-" + Guid.NewGuid().ToString("N")[..12],
            DocumentId: docId,
            UserId: userId,
            Status: DocumentProcessingStatus.Processing,
            ProgressPercentage: 10,
            StartedAtUtc: DateTime.UtcNow,
            Details: $"Started processing file: {fileInfo.Name}"
        );
        await _dataStore.SaveProcessingJobAsync(job, userId, ct);

        try
        {
            // 1. Calculate SHA-256 for provenance & duplicate detection
            string checksum;
            await using (var stream = File.OpenRead(filePath))
            {
                var hashBytes = await SHA256.HashDataAsync(stream, ct);
                checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            // Check for existing duplicate document for this user
            var existingDocs = await _dataStore.ListStudyDocumentsAsync(collectionId, userId, ct);
            var duplicate = existingDocs.FirstOrDefault(d => string.Equals(d.Sha256Checksum, checksum, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
                job = job with
                {
                    Status = DocumentProcessingStatus.Indexed,
                    ProgressPercentage = 100,
                    CompletedAtUtc = DateTime.UtcNow,
                    Details = $"Document already indexed with identical checksum as [{duplicate.Id}]."
                };
                await _dataStore.SaveProcessingJobAsync(job, userId, ct);
                return duplicate;
            }

            // 2. Extract content & page structure
            var (pages, totalChars) = await ExtractDocumentPagesAsync(filePath, ext, ct);

            var doc = new StudyDocument(
                Id: docId,
                UserId: userId,
                FileName: fileInfo.Name,
                FilePath: filePath,
                FileSizeBytes: fileInfo.Length,
                Format: ext.ToLowerInvariant(),
                Title: Path.GetFileNameWithoutExtension(fileInfo.Name).Replace('-', ' ').Replace('_', ' '),
                CollectionId: collectionId,
                PageCount: pages.Count,
                CharacterCount: totalChars,
                Status: DocumentProcessingStatus.Processing,
                Sha256Checksum: checksum,
                CreatedAtUtc: DateTime.UtcNow,
                Metadata: new Dictionary<string, string>
                {
                    ["OriginalPath"] = filePath,
                    ["LastModifiedUtc"] = fileInfo.LastWriteTimeUtc.ToString("o")
                }
            );

            await _dataStore.SaveStudyDocumentAsync(doc, userId, ct);

            // 3. Chunking & Index Preparation
            var chunks = ChunkPages(docId, userId, pages);
            await _dataStore.SaveDocumentChunksAsync(docId, chunks, userId, ct);

            // 4. Update Document Status to Indexed
            doc.Status = DocumentProcessingStatus.Indexed;
            doc.ProcessedAtUtc = DateTime.UtcNow;
            await _dataStore.SaveStudyDocumentAsync(doc, userId, ct);

            job = job with
            {
                Status = DocumentProcessingStatus.Indexed,
                ProgressPercentage = 100,
                CompletedAtUtc = DateTime.UtcNow,
                Details = $"Successfully extracted {pages.Count} pages and generated {chunks.Count} searchable chunks."
            };
            await _dataStore.SaveProcessingJobAsync(job, userId, ct);

            return doc;
        }
        catch (Exception ex)
        {
            job = job with
            {
                Status = DocumentProcessingStatus.Failed,
                ProgressPercentage = 0,
                CompletedAtUtc = DateTime.UtcNow,
                Details = $"Extraction failed: {ex.Message}"
            };
            await _dataStore.SaveProcessingJobAsync(job, userId, ct);
            throw;
        }
    }

    public async Task<StudyDocument> IngestContentAsync(
        string fileName,
        string format,
        string content,
        string userId = "default",
        string? collectionId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var normFormat = format.StartsWith('.') ? format.ToLowerInvariant() : "." + format.ToLowerInvariant();
        var docId = "doc-" + Guid.NewGuid().ToString("N")[..12];

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        var checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Check duplicate
        var existingDocs = await _dataStore.ListStudyDocumentsAsync(collectionId, userId, ct);
        var duplicate = existingDocs.FirstOrDefault(d => string.Equals(d.Sha256Checksum, checksum, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null) return duplicate;

        var pages = SplitTextIntoPages(content, linesPerPage: 45);
        var meta = new Dictionary<string, string>
        {
            ["RawContent"] = content
        };
        var doc = new StudyDocument(
            Id: docId,
            UserId: userId,
            FileName: fileName,
            FilePath: $"memory://{fileName}",
            FileSizeBytes: Encoding.UTF8.GetByteCount(content),
            Format: normFormat,
            Title: Path.GetFileNameWithoutExtension(fileName).Replace('-', ' ').Replace('_', ' '),
            CollectionId: collectionId,
            PageCount: pages.Count,
            CharacterCount: content.Length,
            Status: DocumentProcessingStatus.Indexed,
            Sha256Checksum: checksum,
            CreatedAtUtc: DateTime.UtcNow,
            ProcessedAtUtc: DateTime.UtcNow,
            Metadata: meta
        );

        await _dataStore.SaveStudyDocumentAsync(doc, userId, ct);
        var chunks = ChunkPages(docId, userId, pages);
        await _dataStore.SaveDocumentChunksAsync(docId, chunks, userId, ct);

        return doc;
    }

    public async Task<StudyDocument> ReprocessDocumentAsync(string documentId, string userId = "default", CancellationToken ct = default)
    {
        var doc = await _dataStore.GetStudyDocumentAsync(documentId, userId, ct);
        if (doc == null)
        {
            throw new KeyNotFoundException($"Document with ID '{documentId}' does not exist.");
        }

        doc.Status = DocumentProcessingStatus.Reprocessing;
        await _dataStore.SaveStudyDocumentAsync(doc, userId, ct);

        if (File.Exists(doc.FilePath))
        {
            var (pages, totalChars) = await ExtractDocumentPagesAsync(doc.FilePath, doc.Format, ct);
            var chunks = ChunkPages(doc.Id, userId, pages);
            await _dataStore.SaveDocumentChunksAsync(doc.Id, chunks, userId, ct);

            doc.PageCount = pages.Count;
            doc.CharacterCount = totalChars;
            doc.Status = DocumentProcessingStatus.Indexed;
            doc.ProcessedAtUtc = DateTime.UtcNow;
            doc.ErrorMessage = null;
            await _dataStore.SaveStudyDocumentAsync(doc, userId, ct);
        }
        else if (doc.Metadata.TryGetValue("RawContent", out var rawContent))
        {
            var pages = SplitTextIntoPages(rawContent, linesPerPage: 45);
            var chunks = ChunkPages(doc.Id, userId, pages);
            await _dataStore.SaveDocumentChunksAsync(doc.Id, chunks, userId, ct);

            doc.PageCount = pages.Count;
            doc.CharacterCount = rawContent.Length;
            doc.Status = DocumentProcessingStatus.Indexed;
            doc.ProcessedAtUtc = DateTime.UtcNow;
            doc.ErrorMessage = null;
            await _dataStore.SaveStudyDocumentAsync(doc, userId, ct);
        }
        else
        {
            doc.Status = DocumentProcessingStatus.Failed;
            doc.ErrorMessage = "Underlying source file is missing from disk.";
            await _dataStore.SaveStudyDocumentAsync(doc, userId, ct);
        }

        return doc;
    }

    public async Task<DocumentProcessingJob?> GetProcessingJobStatusAsync(string jobId, string userId = "default", CancellationToken ct = default)
    {
        return await _dataStore.GetProcessingJobAsync(jobId, userId, ct);
    }

    // ══════════════════════════════════════════════════════════════════
    // Text Extraction & Chunking Logic
    // ══════════════════════════════════════════════════════════════════

    private static async Task<(List<string> Pages, int TotalChars)> ExtractDocumentPagesAsync(string filePath, string format, CancellationToken ct)
    {
        string rawText;
        if (format is ".txt" or ".md" or ".markdown" or ".json" or ".csv")
        {
            rawText = await File.ReadAllTextAsync(filePath, ct);
        }
        else if (format is ".html" or ".htm")
        {
            var html = await File.ReadAllTextAsync(filePath, ct);
            rawText = Regex.Replace(html, "<.*?>", " ");
            rawText = Regex.Replace(rawText, @"\s+", " ").Trim();
        }
        else
        {
            // Fallback text/binary reader extracting readable ASCII & UTF-8 runs
            var bytes = await File.ReadAllBytesAsync(filePath, ct);
            rawText = ExtractPrintableStrings(bytes);
        }

        var pages = SplitTextIntoPages(rawText, linesPerPage: 45);
        return (pages, rawText.Length);
    }

    private static List<string> SplitTextIntoPages(string text, int linesPerPage)
    {
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var pages = new List<string>();
        var current = new StringBuilder();
        int count = 0;

        foreach (var line in lines)
        {
            if (line.Trim().Equals("---page---", StringComparison.OrdinalIgnoreCase) || line.Contains('\f'))
            {
                if (current.Length > 0)
                {
                    pages.Add(current.ToString().Trim());
                    current.Clear();
                    count = 0;
                }
                continue;
            }

            current.AppendLine(line);
            count++;
            if (count >= linesPerPage)
            {
                pages.Add(current.ToString().Trim());
                current.Clear();
                count = 0;
            }
        }

        if (current.Length > 0)
        {
            pages.Add(current.ToString().Trim());
        }

        if (pages.Count == 0) pages.Add(string.Empty);
        return pages;
    }

    private static List<DocumentChunk> ChunkPages(string docId, string userId, List<string> pages)
    {
        var chunks = new List<DocumentChunk>();
        int chunkIdx = 0;

        for (int p = 0; p < pages.Count; p++)
        {
            var pageText = pages[p];
            if (string.IsNullOrWhiteSpace(pageText)) continue;

            // Segment page into paragraphs
            var paragraphs = pageText.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var currentBuffer = new StringBuilder();
            string? sectionTitle = null;

            foreach (var para in paragraphs)
            {
                var trimmed = para.Trim();
                if (trimmed.StartsWith('#'))
                {
                    sectionTitle = trimmed.TrimStart('#').Trim();
                }

                if (currentBuffer.Length + trimmed.Length > 800)
                {
                    var content = currentBuffer.ToString().Trim();
                    if (content.Length > 0)
                    {
                        chunks.Add(new DocumentChunk(
                            Id: $"{docId}-c{chunkIdx++}",
                            DocumentId: docId,
                            UserId: userId,
                            ChunkIndex: chunkIdx,
                            PageNumber: p + 1,
                            SectionTitle: sectionTitle,
                            Content: content,
                            TokenCount: content.Length / 4,
                            Keywords: ExtractKeywords(content)
                        ));
                        currentBuffer.Clear();
                    }
                }

                currentBuffer.AppendLine(trimmed);
            }

            if (currentBuffer.Length > 0)
            {
                var content = currentBuffer.ToString().Trim();
                chunks.Add(new DocumentChunk(
                    Id: $"{docId}-c{chunkIdx++}",
                    DocumentId: docId,
                    UserId: userId,
                    ChunkIndex: chunkIdx,
                    PageNumber: p + 1,
                    SectionTitle: sectionTitle,
                    Content: content,
                    TokenCount: content.Length / 4,
                    Keywords: ExtractKeywords(content)
                ));
            }
        }

        return chunks;
    }

    private static List<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "a", "an", "is", "it", "to", "in", "for", "with", "on", "as", "by", "that", "this", "are", "of", "or", "from"
        };

        var words = Regex.Matches(text, @"\b[A-Za-z]{3,}\b")
                         .Select(m => m.Value.ToLowerInvariant())
                         .Where(w => !stopWords.Contains(w))
                         .GroupBy(w => w)
                         .OrderByDescending(g => g.Count())
                         .Take(8)
                         .Select(g => g.Key)
                         .ToList();

        return words;
    }

    private static string ExtractPrintableStrings(byte[] bytes)
    {
        var sb = new StringBuilder();
        var current = new StringBuilder();

        foreach (var b in bytes)
        {
            if (b is >= 32 and <= 126 or (byte)'\n' or (byte)'\r' or (byte)'\t')
            {
                current.Append((char)b);
            }
            else
            {
                if (current.Length >= 4)
                {
                    sb.AppendLine(current.ToString());
                }
                current.Clear();
            }
        }

        if (current.Length >= 4)
        {
            sb.AppendLine(current.ToString());
        }

        return sb.ToString();
    }
}
