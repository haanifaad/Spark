using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OpenClaw.Shared.Coding.Models;

namespace OpenClaw.Shared.Coding.Agents;

/// <summary>
/// Autonomous code review agent analyzing proposed diffs and changed files
/// for security flaws, bugs, performance bottlenecks, and architectural violations.
/// </summary>
public sealed class CodeReviewAgent
{
    /// <summary>
    /// Reviews a list of modified files and diffs.
    /// </summary>
    public CodeReviewReport ReviewChanges(IReadOnlyList<FileChangeRecord> changes)
    {
        if (changes == null || changes.Count == 0)
        {
            return new CodeReviewReport
            {
                Summary = "No changes to review.",
                Findings = Array.Empty<CodeReviewFinding>(),
                QualityScore = 100.0
            };
        }

        var findings = new List<CodeReviewFinding>();

        foreach (var change in changes)
        {
            var content = change.NewContent ?? string.Empty;
            var file = change.FilePath;

            // 1. Security check: Hardcoded API keys or credentials
            if (Regex.IsMatch(content, @"sk-[a-zA-Z0-9_\-]{20,}") ||
                Regex.IsMatch(content, @"ghp_[a-zA-Z0-9]{20,}") ||
                Regex.IsMatch(content, @"password\s*=\s*[""'][^""'\s]{4,}[""']", RegexOptions.IgnoreCase))
            {
                findings.Add(new CodeReviewFinding
                {
                    Severity = CodingDiagnosticSeverity.Error,
                    Category = "Security",
                    FilePath = file,
                    Title = "Potential Hardcoded Secret or API Key",
                    Description = "Found pattern matching an API key, personal access token, or hardcoded password.",
                    Recommendation = "Use secure configuration (e.g. environment variables or DPAPI key store) instead of committing credentials."
                });
            }

            // 2. Security check: SQL Injection
            if (Regex.IsMatch(content, @"SELECT\s+.*\s+FROM\s+.*\s+WHERE\s+.*[\+\$]", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(content, @"ExecuteSqlRaw\(.*[\+\$]", RegexOptions.IgnoreCase))
            {
                findings.Add(new CodeReviewFinding
                {
                    Severity = CodingDiagnosticSeverity.Error,
                    Category = "Security",
                    FilePath = file,
                    Title = "Possible SQL Injection Risk",
                    Description = "SQL query string appears to be constructed using string interpolation or concatenation.",
                    Recommendation = "Use parameterized SQL queries with bind variables."
                });
            }

            // 3. Reliability: Empty catch blocks
            if (Regex.IsMatch(content, @"catch\s*(\([^\)]*\))?\s*\{\s*\}"))
            {
                findings.Add(new CodeReviewFinding
                {
                    Severity = CodingDiagnosticSeverity.Warning,
                    Category = "Reliability",
                    FilePath = file,
                    Title = "Empty Catch Block",
                    Description = "Exceptions are swallowed silently without logging or error recovery.",
                    Recommendation = "Log the exception or rethrow with contextual information."
                });
            }

            // 4. Resource Management: Unclosed streams / IDisposable without using
            if (content.Contains("new FileStream(") && !content.Contains("using ") && !content.Contains("await using"))
            {
                findings.Add(new CodeReviewFinding
                {
                    Severity = CodingDiagnosticSeverity.Warning,
                    Category = "Reliability",
                    FilePath = file,
                    Title = "Possible Unclosed Disposable Resource",
                    Description = "FileStream allocated without a 'using' declaration may leak OS handles.",
                    Recommendation = "Wrap IDisposable resources with 'using var' or 'await using var'."
                });
            }

            // 5. Code Quality: TODO comments left in code
            var todoMatches = Regex.Matches(content, @"\/\/\s*TODO[:\s]+(.*)", RegexOptions.IgnoreCase);
            foreach (Match match in todoMatches.Take(2))
            {
                findings.Add(new CodeReviewFinding
                {
                    Severity = CodingDiagnosticSeverity.Information,
                    Category = "Quality",
                    FilePath = file,
                    Title = "Pending TODO Item",
                    Description = $"Unresolved comment: {match.Groups[1].Value.Trim()}",
                    Recommendation = "Implement required functionality or track in task backlog."
                });
            }
        }

        // Calculate score (100 - 20 per error - 5 per warning)
        int errors = findings.Count(f => f.Severity == CodingDiagnosticSeverity.Error);
        int warnings = findings.Count(f => f.Severity == CodingDiagnosticSeverity.Warning);
        double score = Math.Max(0.0, 100.0 - (errors * 20.0) - (warnings * 5.0));

        var summary = errors > 0
            ? $"Review completed with {errors} critical security/correctness issue(s) and {warnings} warning(s)."
            : warnings > 0
                ? $"Review passed with {warnings} non-blocking quality suggestion(s)."
                : "Review passed cleanly. No security, correctness, or style issues detected.";

        return new CodeReviewReport
        {
            Summary = summary,
            Findings = findings,
            QualityScore = score,
            ReviewedAt = DateTimeOffset.UtcNow
        };
    }
}
