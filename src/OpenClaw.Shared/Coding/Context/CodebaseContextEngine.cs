using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Files;
using OpenClaw.Shared.Coding.Models;

namespace OpenClaw.Shared.Coding.Context;

/// <summary>
/// Intelligent context retrieval engine that performs glob search, regex content search,
/// and selective context window assembly without dumping entire codebases.
/// </summary>
public sealed class CodebaseContextEngine
{
    private readonly string _workspaceRoot;
    private readonly CodingFileOperationsEngine _fileOps;

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".vscode", ".idea", "bin", "obj", "node_modules",
        "dist", "build", "out", "target", "venv", ".venv", "env",
        "__pycache__", ".next", ".nuxt", "coverage", "vendor"
    };

    public CodebaseContextEngine(string workspaceRoot, CodingFileOperationsEngine? fileOps = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _fileOps = fileOps ?? new CodingFileOperationsEngine(_workspaceRoot);
    }

    /// <summary>
    /// Searches files matching a glob pattern (e.g. "*.cs", "src/**/*.ts").
    /// </summary>
    public Task<IReadOnlyList<string>> GlobSearchAsync(
        string pattern,
        int maxResults = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        var results = new List<string>();
        var cleanPattern = pattern.Replace("/", "\\").TrimStart('\\');

        string searchPattern = cleanPattern.Contains('\\')
            ? Path.GetFileName(cleanPattern)
            : cleanPattern;

        if (string.IsNullOrEmpty(searchPattern)) searchPattern = "*.*";

        try
        {
            var matchedFiles = Directory.EnumerateFiles(_workspaceRoot, searchPattern, SearchOption.AllDirectories);
            foreach (var file in matchedFiles)
            {
                if (ct.IsCancellationRequested || results.Count >= maxResults) break;

                var rel = Path.GetRelativePath(_workspaceRoot, file);
                var segments = rel.Split(Path.DirectorySeparatorChar);

                if (segments.Any(s => IgnoredDirectories.Contains(s))) continue;

                results.Add(rel);
            }
        }
        catch
        {
            // Fallback on enumeration error
        }

        return Task.FromResult<IReadOnlyList<string>>(results);
    }

    /// <summary>
    /// Searches for text or regex across workspace files (like ripgrep / GrepTool).
    /// </summary>
    public async Task<IReadOnlyList<(string FilePath, int LineNumber, string LineText)>> GrepSearchAsync(
        string query,
        string? filePattern = null,
        bool isRegex = false,
        bool caseSensitive = false,
        int maxResults = 50,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var matches = new List<(string FilePath, int LineNumber, string LineText)>();
        Regex? regex = null;

        if (isRegex)
        {
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            regex = new Regex(query, options);
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var candidateFiles = await GlobSearchAsync(filePattern ?? "*.*", maxResults: 500, ct: ct);

        foreach (var relPath in candidateFiles)
        {
            if (ct.IsCancellationRequested || matches.Count >= maxResults) break;

            var fullPath = Path.Combine(_workspaceRoot, relPath);
            if (!File.Exists(fullPath)) continue;

            try
            {
                var lines = await File.ReadAllLinesAsync(fullPath, Encoding.UTF8, ct);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (ct.IsCancellationRequested || matches.Count >= maxResults) break;

                    var line = lines[i];
                    bool isMatch = isRegex
                        ? regex!.IsMatch(line)
                        : line.Contains(query, comparison);

                    if (isMatch)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Length > 200) trimmed = string.Concat(trimmed.AsSpan(0, 200), "...");
                        matches.Add((relPath, i + 1, trimmed));
                    }
                }
            }
            catch
            {
                // Skip unreadable files
            }
        }

        return matches;
    }

    /// <summary>
    /// Builds focused repository context relevant to the user request.
    /// </summary>
    public async Task<string> AssembleContextAsync(
        string userPrompt,
        RepositoryAnalysis analysis,
        int maxTokensEstimate = 4000,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== REPOSITORY CONTEXT ===");
        sb.AppendLine($"Project: {analysis.ProjectName} ({analysis.ProjectRoot})");
        sb.AppendLine($"Build System: {analysis.BuildSystem}");
        if (analysis.DetectedLanguages.Count > 0)
            sb.AppendLine($"Languages: {string.Join(", ", analysis.DetectedLanguages)}");
        if (analysis.PrimaryFrameworks.Count > 0)
            sb.AppendLine($"Frameworks: {string.Join(", ", analysis.PrimaryFrameworks)}");
        if (!string.IsNullOrEmpty(analysis.GitBranch))
            sb.AppendLine($"Git Branch: {analysis.GitBranch} {(analysis.GitIsDirty ? "[uncommitted changes]" : "[clean]")}");

        // Project instructions (e.g. CLAUDE.md, AGENTS.md, README.md snippet)
        foreach (var instFile in analysis.InstructionFiles.Take(2))
        {
            var fullPath = Path.Combine(_workspaceRoot, instFile);
            if (File.Exists(fullPath))
            {
                try
                {
                    var lines = await File.ReadAllLinesAsync(fullPath, Encoding.UTF8, ct);
                    sb.AppendLine($"\n--- Project Instructions ({instFile}) ---");
                    foreach (var line in lines.Take(50))
                    {
                        sb.AppendLine(line);
                    }
                }
                catch { }
            }
        }

        // Identify files mentioned or relevant to the prompt
        var relevantFiles = IdentifyCandidateFiles(userPrompt);
        if (relevantFiles.Count > 0)
        {
            sb.AppendLine("\n--- Potentially Relevant Files ---");
            foreach (var rel in relevantFiles.Take(5))
            {
                sb.AppendLine($"- {rel}");
            }
        }

        return sb.ToString().Trim();
    }

    private List<string> IdentifyCandidateFiles(string prompt)
    {
        var candidates = new List<string>();
        // Extract words ending in file extensions
        var fileRegex = new Regex(@"[a-zA-Z0-9_\-\.\/\\]+\.(cs|ts|tsx|js|jsx|py|rs|go|xaml|json|md)", RegexOptions.IgnoreCase);
        foreach (Match match in fileRegex.Matches(prompt))
        {
            var path = match.Value;
            var fullPath = Path.Combine(_workspaceRoot, path);
            if (File.Exists(fullPath))
            {
                candidates.Add(Path.GetRelativePath(_workspaceRoot, fullPath));
            }
        }

        return candidates;
    }
}
