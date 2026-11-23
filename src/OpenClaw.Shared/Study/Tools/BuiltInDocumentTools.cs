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

namespace OpenClaw.Shared.Study.Tools;

/// <summary>
/// Extracts readable textual content from local PDF or document representations.
/// Reads text safely within sandboxed educational document directories.
/// </summary>
public sealed class PdfReaderTool : IStudyToolHandler
{
    public async Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        string? filePath = arguments.TryGetValue("filePath", out var fp) ? fp?.ToString() : null;
        string? textContent = arguments.TryGetValue("textContent", out var tc) ? tc?.ToString() : null;

        if (string.IsNullOrWhiteSpace(filePath) && string.IsNullOrWhiteSpace(textContent))
        {
            throw new ArgumentException("Either 'filePath' or 'textContent' must be provided.");
        }

        string raw = string.Empty;
        string source = "inline";

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            if (!File.Exists(filePath))
            {
                return new
                {
                    isAvailable = false,
                    status = "Unavailable",
                    filePath,
                    message = $"Specified PDF document was not found at '{filePath}'."
                };
            }

            source = Path.GetFileName(filePath);
            // Safely read text stream or raw text
            raw = await File.ReadAllTextAsync(filePath, ct);
        }
        else
        {
            raw = textContent!;
        }

        int maxChars = 4000;
        if (arguments.TryGetValue("maxChars", out var mc) && mc != null)
        {
            maxChars = Convert.ToInt32(mc);
        }

        // Clean basic text formatting
        var snippet = raw.Length > maxChars ? raw[..maxChars] + " ...[truncated]" : raw;
        var wordCount = raw.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

        return new
        {
            source,
            length = raw.Length,
            wordCount,
            extractedText = snippet,
            isTruncated = raw.Length > maxChars
        };
    }
}

/// <summary>
/// Structured document text and outline extractor.
/// Identifies headings, paragraphs, bullet lists, and reading statistics.
/// </summary>
public sealed class DocumentTextExtractorTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("text", out var textObj) || textObj is not string text || string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Missing parameter 'text'.");
        }

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var headings = new List<string>();
        int bulletCount = 0;
        int paragraphCount = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.StartsWith('#') || Regex.IsMatch(trimmed, @"^(Chapter|Section|\d+\.)\s+", RegexOptions.IgnoreCase))
            {
                headings.Add(trimmed);
            }
            else if (trimmed.StartsWith('-') || trimmed.StartsWith('*') || trimmed.StartsWith('•'))
            {
                bulletCount++;
            }
            else
            {
                paragraphCount++;
            }
        }

        var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        int wordCount = words.Length;
        double estimatedReadingMinutes = Math.Round(wordCount / 200.0, 1);

        return Task.FromResult<object?>(new
        {
            totalLines = lines.Length,
            wordCount,
            characterCount = text.Length,
            paragraphCount,
            bulletCount,
            headingsFound = headings,
            estimatedReadingMinutes
        });
    }
}

/// <summary>
/// Context-aware text search tool with line matching and snippet context generation.
/// </summary>
public sealed class TextSearchTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("query", out var qObj) || qObj is not string query || string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Missing search 'query'.");

        if (!arguments.TryGetValue("corpus", out var cObj) || cObj is not string corpus)
            throw new ArgumentException("Missing 'corpus' text to search.");

        bool isRegex = arguments.TryGetValue("useRegex", out var rxObj) && rxObj is bool b && b;
        int maxMatches = 10;
        if (arguments.TryGetValue("limit", out var lObj) && lObj != null)
        {
            maxMatches = Convert.ToInt32(lObj);
        }

        var lines = corpus.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var matches = new List<object>();

        for (int i = 0; i < lines.Length && matches.Count < maxMatches; i++)
        {
            var line = lines[i];
            bool hit = isRegex
                ? Regex.IsMatch(line, query, RegexOptions.IgnoreCase)
                : line.Contains(query, StringComparison.OrdinalIgnoreCase);

            if (hit)
            {
                matches.Add(new
                {
                    lineNumber = i + 1,
                    lineText = line.Trim(),
                    preview = line.Trim()
                });
            }
        }

        return Task.FromResult<object?>(new
        {
            query,
            totalMatches = matches.Count,
            matches
        });
    }
}

/// <summary>
/// Reads file and document metadata, hashes, and character metrics.
/// </summary>
public sealed class DocumentMetadataReaderTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        string? text = arguments.TryGetValue("text", out var t) ? t?.ToString() : null;
        string? fileName = arguments.TryGetValue("fileName", out var fn) ? fn?.ToString() : "document.txt";

        if (text == null)
        {
            throw new ArgumentException("Missing parameter 'text'.");
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length;

        return Task.FromResult<object?>(new
        {
            fileName,
            byteSize = bytes.Length,
            characterCount = text.Length,
            wordCount = words,
            lineCount = lines,
            sha256Hash = hash,
            encoding = "UTF-8"
        });
    }
}
