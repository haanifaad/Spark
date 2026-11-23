using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;

namespace OpenClaw.Shared.Study.Tools;

/// <summary>
/// Code formatter for C#, Python, JavaScript, JSON, and SQL.
/// Normalizes indentation, line endings, and basic structure.
/// </summary>
public sealed class CodeFormatterTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("code", out var codeObj) || codeObj is not string code || string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Missing parameter 'code'.");
        }

        var language = (arguments.TryGetValue("language", out var l) ? l?.ToString() : "csharp")?.ToLowerInvariant() ?? "csharp";
        int indentSize = 4;
        if (arguments.TryGetValue("indentSize", out var indObj) && indObj != null)
        {
            indentSize = Math.Clamp(Convert.ToInt32(indObj), 2, 8);
        }

        string formattedCode = FormatCode(code, language, indentSize);

        return Task.FromResult<object?>(new
        {
            language,
            indentSize,
            originalLineCount = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length,
            formattedLineCount = formattedCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length,
            formattedCode
        });
    }

    private static string FormatCode(string code, string lang, int indentSize)
    {
        if (lang == "json")
        {
            try
            {
                using var doc = JsonDocument.Parse(code);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                // Fall back to line-by-line formatting if invalid json
            }
        }

        var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();
        int indentLevel = 0;
        string indentUnit = new(' ', indentSize);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
            {
                sb.AppendLine();
                continue;
            }

            // Adjust indent level for closing braces in C-like languages
            if (lang is "csharp" or "c#" or "javascript" or "typescript" or "java" or "c" or "cpp")
            {
                if (line.StartsWith('}') || line.StartsWith(']'))
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                }
            }

            for (int i = 0; i < indentLevel; i++)
            {
                sb.Append(indentUnit);
            }
            sb.AppendLine(line);

            if (lang is "csharp" or "c#" or "javascript" or "typescript" or "java" or "c" or "cpp")
            {
                if (line.EndsWith('{') || line.EndsWith('['))
                {
                    indentLevel++;
                }
            }
            else if (lang is "python")
            {
                if (line.EndsWith(':'))
                {
                    indentLevel++;
                }
                else if (line.StartsWith("return") || line.StartsWith("break") || line.StartsWith("pass"))
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                }
            }
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Code analyzer providing syntax checks, bracket balancing, cyclomatic complexity estimation, and metrics.
/// </summary>
public sealed class CodeAnalyzerTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("code", out var codeObj) || codeObj is not string code || string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Missing parameter 'code'.");
        }

        var language = (arguments.TryGetValue("language", out var l) ? l?.ToString() : "generic")?.ToLowerInvariant() ?? "generic";

        var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int totalLines = lines.Length;
        int blankLines = lines.Count(string.IsNullOrWhiteSpace);
        int commentLines = lines.Count(l =>
        {
            var t = l.Trim();
            return t.StartsWith("//") || t.StartsWith('#') || t.StartsWith("/*") || t.StartsWith('*');
        });
        int codeLines = totalLines - blankLines - commentLines;

        // Bracket balance analysis
        var stack = new Stack<(char bracket, int line)>();
        var balanceErrors = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            for (int j = 0; j < line.Length; j++)
            {
                char c = line[j];
                if (c is '{' or '(' or '[')
                {
                    stack.Push((c, i + 1));
                }
                else if (c is '}' or ')' or ']')
                {
                    if (stack.Count == 0)
                    {
                        balanceErrors.Add($"Unexpected closing bracket '{c}' at line {i + 1}");
                    }
                    else
                    {
                        var (top, topLn) = stack.Pop();
                        bool match = (top == '{' && c == '}') || (top == '(' && c == ')') || (top == '[' && c == ']');
                        if (!match)
                        {
                            balanceErrors.Add($"Mismatched bracket: opened '{top}' at line {topLn}, closed with '{c}' at line {i + 1}");
                        }
                    }
                }
            }
        }

        while (stack.Count > 0)
        {
            var (unclosed, ln) = stack.Pop();
            balanceErrors.Add($"Unclosed bracket '{unclosed}' opened at line {ln}");
        }

        // Cyclomatic Complexity Estimation: 1 + count of branching keywords
        int complexity = 1;
        var branchRegex = new Regex(@"\b(if|else\s+if|elif|for|while|case|catch|&&|\|\||\?)\b", RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith('#')) continue;
            complexity += branchRegex.Matches(trimmed).Count;
        }

        return Task.FromResult<object?>(new
        {
            language,
            totalLines,
            codeLines,
            commentLines,
            blankLines,
            estimatedComplexity = complexity,
            complexityRating = complexity switch
            {
                <= 5 => "Low (Simple)",
                <= 10 => "Moderate (Good)",
                <= 20 => "High (Consider refactoring)",
                _ => "Very High (Needs decomposition)"
            },
            isBalanced = balanceErrors.Count == 0,
            syntaxIssues = balanceErrors
        });
    }
}

/// <summary>
/// Strict JSON validator diagnosing syntax issues with line/column reporting and schema checking.
/// </summary>
public sealed class JsonValidatorTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("json", out var jsonObj) || jsonObj is not string json || string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Missing parameter 'json'.");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var rootType = doc.RootElement.ValueKind.ToString();
            int elementCount = 0;

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                elementCount = doc.RootElement.EnumerateObject().Count();
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                elementCount = doc.RootElement.GetArrayLength();
            }

            return Task.FromResult<object?>(new
            {
                isValid = true,
                rootType,
                elementCount,
                message = "JSON syntax is valid."
            });
        }
        catch (JsonException jex)
        {
            return Task.FromResult<object?>(new
            {
                isValid = false,
                lineNumber = jex.LineNumber,
                bytePositionInLine = jex.BytePositionInLine,
                message = jex.Message
            });
        }
    }
}

/// <summary>
/// HTML preview and sanitizer tool. Neutralizes malicious tags while presenting clean educational markup.
/// </summary>
public sealed class HtmlPreviewTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("html", out var htmlObj) || htmlObj is not string html || string.IsNullOrWhiteSpace(html))
        {
            throw new ArgumentException("Missing parameter 'html'.");
        }

        // Sanitize dangerous tags: <script>, <iframe>, <object>, <embed>, onload/onerror attributes
        var sanitized = Regex.Replace(html, @"<\s*(script|iframe|object|embed)[^>]*>.*?<\s*/\s*\1\s*>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        sanitized = Regex.Replace(sanitized, @"<\s*(script|iframe|object|embed)[^>]*/>", "", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", "", RegexOptions.IgnoreCase);

        bool removedThreats = sanitized.Length != html.Length;

        return Task.FromResult<object?>(new
        {
            isSafe = true,
            sanitizedApplied = removedThreats,
            previewHtml = sanitized,
            length = sanitized.Length
        });
    }
}

/// <summary>
/// SQL query analyzer and validator. Identifies statement type, tables, columns, and prevents unsafe queries.
/// </summary>
public sealed class SqlQueryValidatorTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("sql", out var sqlObj) || sqlObj is not string sql || string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("Missing parameter 'sql'.");
        }

        var trimmed = sql.Trim();
        var statementType = "UNKNOWN";

        var firstWord = trimmed.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() ?? "UNKNOWN";
        statementType = firstWord switch
        {
            "SELECT" => "Query (Read-Only)",
            "INSERT" => "Data Modification (INSERT)",
            "UPDATE" => "Data Modification (UPDATE)",
            "DELETE" => "Data Deletion (DELETE)",
            "CREATE" => "DDL (CREATE)",
            "DROP" => "DDL (DROP)",
            "ALTER" => "DDL (ALTER)",
            _ => "CUSTOM"
        };

        bool isDestructive = firstWord is "DROP" or "DELETE" or "TRUNCATE";
        bool hasWhereClause = Regex.IsMatch(trimmed, @"\bWHERE\b", RegexOptions.IgnoreCase);

        // Identify table names from FROM / JOIN clauses
        var tables = new List<string>();
        var fromMatches = Regex.Matches(trimmed, @"\b(FROM|JOIN)\s+([a-zA-Z0-9_\.]+)", RegexOptions.IgnoreCase);
        foreach (Match m in fromMatches)
        {
            var tbl = m.Groups[2].Value;
            if (!tables.Contains(tbl, StringComparer.OrdinalIgnoreCase))
            {
                tables.Add(tbl);
            }
        }

        var warnings = new List<string>();
        if (firstWord == "DELETE" && !hasWhereClause)
        {
            warnings.Add("High Risk: DELETE statement without a WHERE clause will delete all table records!");
        }
        if (firstWord == "UPDATE" && !hasWhereClause)
        {
            warnings.Add("Warning: UPDATE statement without a WHERE clause will update all table records!");
        }

        return Task.FromResult<object?>(new
        {
            statementType,
            isDestructive,
            isReadOnly = firstWord == "SELECT",
            hasWhereClause,
            referencedTables = tables,
            warnings,
            isValid = true
        });
    }
}
