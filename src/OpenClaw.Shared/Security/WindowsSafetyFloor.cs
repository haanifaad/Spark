using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenClaw.Shared.Security;

/// <summary>
/// Verdict returned by the Windows Safety Floor evaluation.
/// </summary>
public sealed record SafetyFloorVerdict
{
    public required bool IsAllowed { get; init; }
    public string? BlockedRule { get; init; }
    public string? Reason { get; init; }
    public bool IsReadable { get; init; } = true;

    public static SafetyFloorVerdict Allowed() => new() { IsAllowed = true };

    public static SafetyFloorVerdict Blocked(string rule, string reason) => new()
    {
        IsAllowed = false,
        BlockedRule = rule,
        Reason = reason
    };

    public static SafetyFloorVerdict Unreadable(string reason) => new()
    {
        IsAllowed = false,
        IsReadable = false,
        Reason = reason
    };
}

/// <summary>
/// Windows-native unbypassable safety floor for shell and PowerShell execution.
/// Inspects command tokens, resolves aliases, unpacks encoded commands, and enforces
/// an absolute never-list against destructive disk, root, and boot operations.
/// </summary>
public static class WindowsSafetyFloor
{
    private static readonly HashSet<string> DestructiveRemoveCmdlets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Remove-Item", "rm", "del", "erase", "rd", "ri", "rmdir"
    };

    private static readonly HashSet<string> DiskFormatCmdlets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Format-Volume", "format", "diskpart", "Clear-Disk", "Initialize-Disk"
    };

    private static readonly HashSet<string> RecursiveFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "-Recurse", "-r", "/s", "-Force", "-f", "/q"
    };

    private static readonly HashSet<string> ProtectedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        @"C:\", @"C:", @"\", @"/", "~",
        @"C:\Windows", @"C:\Windows\",
        @"C:\Users", @"C:\Users\",
        @"C:\Program Files", @"C:\Program Files\",
        @"C:\Program Files (x86)", @"C:\Program Files (x86)\",
        "$env:SystemRoot", "$env:windir", "$env:USERPROFILE", "$env:ProgramFiles",
        "%SystemRoot%", "%windir%", "%USERPROFILE%", "%ProgramFiles%"
    };

    private static readonly Regex EncodedCommandRegex = new(
        @"(?:-e|-enc|-encodedcommand)\s+([A-Za-z0-9+/=]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForkBombRegex = new(
        @":\(\)\s*\{\s*:\|:&\s*\};:|function\s+f\s*\{\s*f\|f&\s*\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GitForcePushRegex = new(
        @"\bgit\b.*\bpush\b.*(?:--force|-f|\+[a-zA-Z0-9_\-\/]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegistryRootDeleteRegex = new(
        @"\breg\s+delete\s+(?:HKLM|HKEY_LOCAL_MACHINE|HKCR|HKEY_CLASSES_ROOT)(?:\s|$|\\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BcdEditDeleteRegex = new(
        @"\bbcdedit\b.*(?:/delete|/deletevalue)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Evaluates whether a given shell or PowerShell command is permitted to execute.
    /// This gate is unbypassable and takes precedence over any user 'always allow' rules.
    /// </summary>
    public static SafetyFloorVerdict Evaluate(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return SafetyFloorVerdict.Allowed();

        var trimmed = command.Trim();

        // 1. Fork bomb check
        if (ForkBombRegex.IsMatch(trimmed))
        {
            return SafetyFloorVerdict.Blocked(
                "fork-bomb",
                "Command matches a known denial-of-service fork bomb pattern.");
        }

        // 2. Git force push check
        if (GitForcePushRegex.IsMatch(trimmed))
        {
            return SafetyFloorVerdict.Blocked(
                "force-push",
                "Destructive git force-push operation is prohibited.");
        }

        // 3. Registry root deletion
        if (RegistryRootDeleteRegex.IsMatch(trimmed))
        {
            return SafetyFloorVerdict.Blocked(
                "registry-system-delete",
                "Recursive deletion of system registry hives (HKLM/HKCR) is prohibited.");
        }

        // 4. BCD (Boot Configuration Data) destruction
        if (BcdEditDeleteRegex.IsMatch(trimmed))
        {
            return SafetyFloorVerdict.Blocked(
                "boot-configuration-tamper",
                "Modifying or deleting boot configuration data via bcdedit is prohibited.");
        }

        // 5. Inspect Base64 EncodedCommand payloads
        var encodedMatch = EncodedCommandRegex.Match(trimmed);
        if (encodedMatch.Success)
        {
            try
            {
                var base64 = encodedMatch.Groups[1].Value;
                var bytes = Convert.FromBase64String(base64);
                var decoded = Encoding.Unicode.GetString(bytes);
                var innerVerdict = Evaluate(decoded);
                if (!innerVerdict.IsAllowed)
                {
                    return SafetyFloorVerdict.Blocked(
                        $"encoded:{innerVerdict.BlockedRule}",
                        $"Encoded command contains prohibited action: {innerVerdict.Reason}");
                }
            }
            catch (Exception ex)
            {
                return SafetyFloorVerdict.Unreadable($"Failed to decode Base64 command: {ex.Message}");
            }
        }

        // 6. Tokenize command line into statements (handling ;, &&, ||, newlines)
        var statements = SplitStatements(trimmed);
        foreach (var statement in statements)
        {
            var tokens = Tokenize(statement);
            if (tokens.Count == 0) continue;

            var primaryVerb = Path.GetFileNameWithoutExtension(tokens[0]);

            // Check disk formatting commands
            if (DiskFormatCmdlets.Contains(primaryVerb))
            {
                return SafetyFloorVerdict.Blocked(
                    "format-disk",
                    $"Disk formatting command '{primaryVerb}' is strictly prohibited.");
            }

            // Check recursive removal of critical paths
            if (DestructiveRemoveCmdlets.Contains(primaryVerb))
            {
                var hasRecursive = tokens.Any(t =>
                    RecursiveFlags.Contains(t) ||
                    (t.StartsWith('-') && (t.Contains('r', StringComparison.OrdinalIgnoreCase) || t.Contains('s', StringComparison.OrdinalIgnoreCase))) ||
                    (t.StartsWith('/') && t.Contains('s', StringComparison.OrdinalIgnoreCase)));

                var operands = tokens.Skip(1).Where(t => !t.StartsWith('-') && !t.StartsWith('/')).ToList();

                foreach (var operand in operands)
                {
                    var normalized = NormalizePath(operand);
                    if (IsProtectedRoot(normalized))
                    {
                        if (hasRecursive || primaryVerb.Equals("rd", StringComparison.OrdinalIgnoreCase) || primaryVerb.Equals("rmdir", StringComparison.OrdinalIgnoreCase))
                        {
                            return SafetyFloorVerdict.Blocked(
                                "delete-root-system",
                                $"Recursive deletion of root or critical system directory '{operand}' is prohibited.");
                        }
                    }
                }
            }
        }

        return SafetyFloorVerdict.Allowed();
    }

    private static bool IsProtectedRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var clean = path.Trim().TrimEnd('\\', '/');

        foreach (var root in ProtectedRoots)
        {
            var cleanRoot = root.TrimEnd('\\', '/');
            if (clean.Equals(cleanRoot, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string NormalizePath(string raw)
    {
        var unquoted = raw.Trim('"', '\'', '`');
        try
        {
            if (unquoted.StartsWith("$env:", StringComparison.OrdinalIgnoreCase) || unquoted.StartsWith('%'))
                return unquoted;

            return Path.GetFullPath(unquoted);
        }
        catch
        {
            return unquoted;
        }
    }

    private static List<string> SplitStatements(string command)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;

        for (var i = 0; i < command.Length; i++)
        {
            var c = command[i];
            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                current.Append(c);
            }
            else if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                current.Append(c);
            }
            else if (!inSingleQuote && !inDoubleQuote && (c == ';' || c == '\n' || c == '\r'))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else if (!inSingleQuote && !inDoubleQuote && (c == '&' || c == '|'))
            {
                if (i + 1 < command.Length && command[i + 1] == c)
                {
                    i++; // Skip second operator character
                }
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private static List<string> Tokenize(string statement)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;
        var quoteChar = '\0';

        foreach (var c in statement.Trim())
        {
            if ((c == '"' || c == '\'') && (!inQuote || quoteChar == c))
            {
                inQuote = !inQuote;
                quoteChar = inQuote ? c : '\0';
            }
            else if (char.IsWhiteSpace(c) && !inQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
