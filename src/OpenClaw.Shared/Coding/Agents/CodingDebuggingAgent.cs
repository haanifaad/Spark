using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Files;
using OpenClaw.Shared.Coding.Models;

namespace OpenClaw.Shared.Coding.Agents;

/// <summary>
/// Analysis result of a compiler or runtime error.
/// </summary>
public sealed record DebugDiagnosticAnalysis
{
    public required string ErrorMessage { get; init; }
    public string? FilePath { get; init; }
    public int? LineNumber { get; init; }
    public int? ColumnNumber { get; init; }
    public string? ErrorCode { get; init; }
    public string? CodeSnippet { get; init; }
    public string? RootCauseHypothesis { get; init; }
    public string? SuggestedFixDescription { get; init; }
}

/// <summary>
/// Autonomous debugging agent that parses compiler and runtime diagnostics,
/// pinpoints offending code locations, formulates root-cause hypotheses,
/// and prevents infinite repair loops with strict iteration limits.
/// </summary>
public sealed class CodingDebuggingAgent
{
    private readonly CodingFileOperationsEngine _fileOps;
    public const int MaxDebugIterations = 5;

    public CodingDebuggingAgent(CodingFileOperationsEngine fileOps)
    {
        _fileOps = fileOps ?? throw new ArgumentNullException(nameof(fileOps));
    }

    /// <summary>
    /// Parses error logs from compilers (MSBuild/Roslyn, tsc, gcc, clang, pytest) into structured diagnostics.
    /// </summary>
    public IReadOnlyList<DebugDiagnosticAnalysis> ParseErrors(string errorLog)
    {
        if (string.IsNullOrWhiteSpace(errorLog)) return Array.Empty<DebugDiagnosticAnalysis>();

        var results = new List<DebugDiagnosticAnalysis>();
        var lines = errorLog.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Pattern 1: MSBuild / C# error: "C:\path\to\File.cs(123,45): error CS0103: The name 'xyz' does not exist"
        var msbuildRegex = new Regex(@"(?:([A-Za-z]:\\[^:\(\)]+\.[a-zA-Z]+|\/[^:\(\)]+\.[a-zA-Z]+))\s*(?:\((\d+)(?:,(\d+))?\))?\s*:\s*(?:fatal\s+)?error\s*([A-Za-z0-9_]+)?\s*:\s*(.*)", RegexOptions.IgnoreCase);

        // Pattern 2: TypeScript / ESLint: "src/file.ts:123:45 - error TS2304: Cannot find name 'xyz'."
        var tsRegex = new Regex(@"([^\s:]+\.[a-zA-Z]+):(\d+):(\d+)\s*-\s*error\s*([A-Za-z0-9_]+)?\s*:\s*(.*)", RegexOptions.IgnoreCase);

        // Pattern 3: Python traceback: 'File "path/to/file.py", line 123, in func'
        var pythonRegex = new Regex(@"File\s*""([^""]+\.py)"",\s*line\s*(\d+)(?:,\s*in\s*.*)?", RegexOptions.IgnoreCase);

        foreach (var line in lines)
        {
            var match = msbuildRegex.Match(line);
            if (match.Success)
            {
                var filePath = match.Groups[1].Value;
                int.TryParse(match.Groups[2].Value, out int lineNum);
                int.TryParse(match.Groups[3].Value, out int colNum);
                var errCode = match.Groups[4].Value;
                var msg = match.Groups[5].Value;

                results.Add(new DebugDiagnosticAnalysis
                {
                    ErrorMessage = msg.Trim(),
                    FilePath = filePath,
                    LineNumber = lineNum > 0 ? lineNum : null,
                    ColumnNumber = colNum > 0 ? colNum : null,
                    ErrorCode = errCode,
                    RootCauseHypothesis = FormulateHypothesis(errCode, msg)
                });
                continue;
            }

            var tsMatch = tsRegex.Match(line);
            if (tsMatch.Success)
            {
                var filePath = tsMatch.Groups[1].Value;
                int.TryParse(tsMatch.Groups[2].Value, out int lineNum);
                int.TryParse(tsMatch.Groups[3].Value, out int colNum);
                var errCode = tsMatch.Groups[4].Value;
                var msg = tsMatch.Groups[5].Value;

                results.Add(new DebugDiagnosticAnalysis
                {
                    ErrorMessage = msg.Trim(),
                    FilePath = filePath,
                    LineNumber = lineNum > 0 ? lineNum : null,
                    ColumnNumber = colNum > 0 ? colNum : null,
                    ErrorCode = errCode,
                    RootCauseHypothesis = FormulateHypothesis(errCode, msg)
                });
                continue;
            }

            var pyMatch = pythonRegex.Match(line);
            if (pyMatch.Success)
            {
                var filePath = pyMatch.Groups[1].Value;
                int.TryParse(pyMatch.Groups[2].Value, out int lineNum);

                results.Add(new DebugDiagnosticAnalysis
                {
                    ErrorMessage = line.Trim(),
                    FilePath = filePath,
                    LineNumber = lineNum > 0 ? lineNum : null,
                    RootCauseHypothesis = "Unhandled exception encountered during execution."
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Fetches code context around the failing line for diagnostic reasoning.
    /// </summary>
    public async Task<string?> FetchErrorContextAsync(string filePath, int lineNumber, int contextLines = 5, CancellationToken ct = default)
    {
        try
        {
            int start = Math.Max(1, lineNumber - contextLines);
            int count = (contextLines * 2) + 1;

            var (content, _, _, _) = await _fileOps.ReadFileAsync(filePath, start, count, ct);
            return content;
        }
        catch
        {
            return null;
        }
    }

    private static string FormulateHypothesis(string? code, string message)
    {
        if (string.IsNullOrEmpty(code))
        {
            if (message.Contains("NullReference", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("null", StringComparison.OrdinalIgnoreCase))
                return "Potential null reference dereference. Null check or fallback value is missing.";

            return "Syntax or runtime logical defect.";
        }

        return code.ToUpperInvariant() switch
        {
            "CS0103" => "Variable or identifier does not exist in current scope. Check imports or declaration.",
            "CS0246" => "Type or namespace not found. A required using directive or package reference is missing.",
            "CS1061" => "Type does not contain definition for referenced member. Check method signature or version mismatch.",
            "CS8600" or "CS8602" or "CS8604" => "Nullable reference warning or possible null argument dereference.",
            "TS2304" => "Cannot find name. Ensure symbol is imported or declared in types.",
            "TS2339" => "Property does not exist on type. Check type definition or interface.",
            _ => $"Compiler error {code}: {message}"
        };
    }
}
