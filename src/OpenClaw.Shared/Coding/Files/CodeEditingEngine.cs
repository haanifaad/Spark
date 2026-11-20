using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Models;

namespace OpenClaw.Shared.Coding.Files;

/// <summary>
/// Precise code editing engine implementing exact string replacement,
/// multi-line fuzzy tolerance, multiplicity guards, and unified diff generation.
/// </summary>
public sealed class CodeEditingEngine
{
    private readonly CodingFileOperationsEngine _fileOps;

    public CodeEditingEngine(CodingFileOperationsEngine fileOps)
    {
        _fileOps = fileOps ?? throw new ArgumentNullException(nameof(fileOps));
    }

    /// <summary>
    /// Applies a targeted string replacement edit to a file.
    /// </summary>
    public async Task<FileChangeRecord> ApplyEditAsync(
        string filePath,
        string oldString,
        string newString,
        bool replaceAll = false,
        CancellationToken ct = default)
    {
        var securePath = _fileOps.ResolveSecurePath(filePath);
        var relPath = Path.GetRelativePath(_fileOps.WorkspaceRoot, securePath);

        string originalContent;
        if (!File.Exists(securePath))
        {
            if (string.IsNullOrEmpty(oldString))
            {
                // Create new file
                return await _fileOps.WriteFileAsync(relPath, newString, createBackup: true, ct);
            }
            throw new FileNotFoundException($"Cannot edit non-existent file: {filePath}. Use empty oldString to create.");
        }

        originalContent = await File.ReadAllTextAsync(securePath, Encoding.UTF8, ct);

        // Perform edit in memory
        string updatedContent = ReplaceText(originalContent, oldString, newString, replaceAll);

        if (string.Equals(originalContent, updatedContent, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Failed to apply edit: Original and edited content match exactly (no-op).");
        }

        // Write the updated file through file operations engine (with backup)
        return await _fileOps.WriteFileAsync(relPath, updatedContent, createBackup: true, ct);
    }

    /// <summary>
    /// Replaces oldString with newString using exact or normalized multi-line matching.
    /// </summary>
    public static string ReplaceText(string content, string oldString, string newString, bool replaceAll = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(oldString);
        ArgumentNullException.ThrowIfNull(newString);

        if (oldString.Length == 0)
        {
            // Empty old string means append or prepend
            return content + newString;
        }

        // 1. Try direct exact match
        int firstIndex = content.IndexOf(oldString, StringComparison.Ordinal);
        if (firstIndex != -1)
        {
            if (!replaceAll)
            {
                int secondIndex = content.IndexOf(oldString, firstIndex + oldString.Length, StringComparison.Ordinal);
                if (secondIndex != -1)
                {
                    throw new InvalidOperationException(
                        $"Ambiguous edit: target string appears multiple times in file. Provide more surrounding context lines, or set replaceAll to true.");
                }
                return string.Concat(content.AsSpan(0, firstIndex), newString, content.AsSpan(firstIndex + oldString.Length));
            }
            return content.Replace(oldString, newString, StringComparison.Ordinal);
        }

        // 2. Try line-ending normalized match (CRLF vs LF)
        var normalizedContent = content.Replace("\r\n", "\n");
        var normalizedOld = oldString.Replace("\r\n", "\n");
        var normalizedNew = newString.Replace("\r\n", "\n");

        int normIndex = normalizedContent.IndexOf(normalizedOld, StringComparison.Ordinal);
        if (normIndex != -1)
        {
            if (!replaceAll)
            {
                int nextIndex = normalizedContent.IndexOf(normalizedOld, normIndex + normalizedOld.Length, StringComparison.Ordinal);
                if (nextIndex != -1)
                {
                    throw new InvalidOperationException(
                        $"Ambiguous edit: target string appears multiple times in file. Provide more surrounding context lines.");
                }
            }

            var replacedNormalized = replaceAll
                ? normalizedContent.Replace(normalizedOld, normalizedNew, StringComparison.Ordinal)
                : string.Concat(normalizedContent.AsSpan(0, normIndex), normalizedNew, normalizedContent.AsSpan(normIndex + normalizedOld.Length));

            // Restore CRLF if original content predominantly used CRLF
            if (content.Contains("\r\n"))
            {
                return replacedNormalized.Replace("\n", "\r\n");
            }
            return replacedNormalized;
        }

        // 3. Try fuzzy quote and dash normalization
        var fuzzyContent = NormalizeQuotesAndDashes(normalizedContent);
        var fuzzyOld = NormalizeQuotesAndDashes(normalizedOld);

        int fuzzyIndex = fuzzyContent.IndexOf(fuzzyOld, StringComparison.Ordinal);
        if (fuzzyIndex != -1)
        {
            var replacedFuzzy = string.Concat(normalizedContent.AsSpan(0, fuzzyIndex), normalizedNew, normalizedContent.AsSpan(fuzzyIndex + fuzzyOld.Length));
            if (content.Contains("\r\n"))
            {
                return replacedFuzzy.Replace("\n", "\r\n");
            }
            return replacedFuzzy;
        }

        throw new InvalidOperationException(
            $"Target text not found in file. Ensure exact matching of whitespace, indentation, and characters.\n" +
            $"Search string snippet:\n{GetSnippet(oldString)}");
    }

    private static string NormalizeQuotesAndDashes(string text)
    {
        return text
            .Replace('’', '\'')
            .Replace('‘', '\'')
            .Replace('”', '"')
            .Replace('“', '"')
            .Replace('–', '-')
            .Replace('—', '-');
    }

    private static string GetSnippet(string text, int maxChars = 200)
    {
        if (text.Length <= maxChars) return text;
        return string.Concat(text.AsSpan(0, maxChars), "...");
    }

    /// <summary>
    /// Generates a standard unified diff between old and new text.
    /// </summary>
    public static UnifiedDiff GenerateDiff(string filePath, string oldText, string newText)
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);

        var hunks = new List<UnifiedDiffHunk>();
        int additions = 0;
        int deletions = 0;

        var sb = new StringBuilder();
        sb.AppendLine($"--- a/{filePath}");
        sb.AppendLine($"+++ b/{filePath}");

        // Compute LCS diff
        var lcs = ComputeLCS(oldLines, newLines);
        int oldIdx = 0, newIdx = 0, lcsIdx = 0;

        var currentHunkLines = new List<string>();
        int hunkOldStart = 1, hunkNewStart = 1;
        int hunkOldCount = 0, hunkNewCount = 0;

        void FlushHunk()
        {
            if (currentHunkLines.Count > 0)
            {
                var header = $"@@ -{hunkOldStart},{Math.Max(1, hunkOldCount)} +{hunkNewStart},{Math.Max(1, hunkNewCount)} @@";
                hunks.Add(new UnifiedDiffHunk(hunkOldStart, hunkOldCount, hunkNewStart, hunkNewCount, currentHunkLines.ToList(), header));
                sb.AppendLine(header);
                foreach (var line in currentHunkLines)
                {
                    sb.AppendLine(line);
                }
                currentHunkLines.Clear();
            }
        }

        while (oldIdx < oldLines.Length || newIdx < newLines.Length)
        {
            if (lcsIdx < lcs.Count && oldIdx < oldLines.Length && oldLines[oldIdx] == lcs[lcsIdx] &&
                newIdx < newLines.Length && newLines[newIdx] == lcs[lcsIdx])
            {
                // Context line
                if (currentHunkLines.Count > 0)
                {
                    currentHunkLines.Add($" {oldLines[oldIdx]}");
                    hunkOldCount++;
                    hunkNewCount++;
                }
                oldIdx++;
                newIdx++;
                lcsIdx++;
            }
            else
            {
                if (currentHunkLines.Count == 0)
                {
                    hunkOldStart = Math.Max(1, oldIdx + 1);
                    hunkNewStart = Math.Max(1, newIdx + 1);
                    hunkOldCount = 0;
                    hunkNewCount = 0;

                    // Include up to 3 lines of prior context
                    int ctxStart = Math.Max(0, oldIdx - 3);
                    for (int c = ctxStart; c < oldIdx; c++)
                    {
                        currentHunkLines.Add($" {oldLines[c]}");
                        hunkOldCount++;
                        hunkNewCount++;
                    }
                    hunkOldStart = ctxStart + 1;
                    hunkNewStart = Math.Max(1, newIdx - (oldIdx - ctxStart) + 1);
                }

                // Deletions from old
                while (oldIdx < oldLines.Length && (lcsIdx >= lcs.Count || oldLines[oldIdx] != lcs[lcsIdx]))
                {
                    currentHunkLines.Add($"-{oldLines[oldIdx]}");
                    hunkOldCount++;
                    deletions++;
                    oldIdx++;
                }

                // Additions from new
                while (newIdx < newLines.Length && (lcsIdx >= lcs.Count || newLines[newIdx] != lcs[lcsIdx]))
                {
                    currentHunkLines.Add($"+{newLines[newIdx]}");
                    hunkNewCount++;
                    additions++;
                    newIdx++;
                }
            }
        }

        FlushHunk();

        return new UnifiedDiff
        {
            FilePath = filePath,
            Additions = additions,
            Deletions = deletions,
            Hunks = hunks,
            RawDiffText = sb.ToString().TrimEnd()
        };
    }

    private static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        return text.Replace("\r\n", "\n").Split('\n');
    }

    private static List<string> ComputeLCS(string[] a, string[] b)
    {
        int m = a.Length;
        int n = b.Length;
        int[,] dp = new int[m + 1, n + 1];

        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (a[i] == b[j])
                    dp[i + 1, j + 1] = dp[i, j] + 1;
                else
                    dp[i + 1, j + 1] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var result = new List<string>();
        int x = m, y = n;
        while (x > 0 && y > 0)
        {
            if (a[x - 1] == b[y - 1])
            {
                result.Add(a[x - 1]);
                x--;
                y--;
            }
            else if (dp[x - 1, y] >= dp[x, y - 1])
            {
                x--;
            }
            else
            {
                y--;
            }
        }

        result.Reverse();
        return result;
    }
}
