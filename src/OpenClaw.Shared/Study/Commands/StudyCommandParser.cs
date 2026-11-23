using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Commands;

/// <summary>
/// Robust tokenizer and parser for Study slash commands.
/// Accurately parses commands, quoted strings, named key=value pairs, and positional arguments.
/// </summary>
public sealed class StudyCommandParser : IStudyCommandParser
{
    public bool IsSlashCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.TrimStart();
        return trimmed.StartsWith('/') && trimmed.Length > 1 && char.IsLetter(trimmed[1]);
    }

    public ParsedStudyCommand Parse(string input, IReadOnlyList<StudyCommandDefinition>? knownCommands = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ParsedStudyCommand(string.Empty, input ?? string.Empty, null, null, false, ["Input command was empty."]);
        }

        var trimmed = input.Trim();
        if (!trimmed.StartsWith('/'))
        {
            return new ParsedStudyCommand(string.Empty, input, null, null, false, ["Command must start with '/'."]);
        }

        var tokens = Tokenize(trimmed);
        if (tokens.Count == 0)
        {
            return new ParsedStudyCommand(string.Empty, input, null, null, false, ["No command token found."]);
        }

        var commandName = tokens[0].TrimStart('/');
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positional = new List<string>();
        var errors = new List<string>();

        for (int i = 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            int eqIdx = token.IndexOf('=');

            if (eqIdx > 0)
            {
                var key = token[..eqIdx].Trim();
                var val = token[(eqIdx + 1)..].Trim();

                // Strip outer quotes if still present
                if ((val.StartsWith('"') && val.EndsWith('"')) || (val.StartsWith('\'') && val.EndsWith('\'')))
                {
                    val = val[1..^1];
                }

                args[key] = val;
            }
            else
            {
                positional.Add(token);
            }
        }

        // Validate against command definition if available
        if (knownCommands != null)
        {
            var cmdDef = knownCommands.FirstOrDefault(c =>
                string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase) ||
                c.Aliases.Any(a => string.Equals(a.TrimStart('/'), commandName, StringComparison.OrdinalIgnoreCase)));

            if (cmdDef != null)
            {
                commandName = cmdDef.Name;
                // If positional arguments exist and required named arguments are missing, map positional args to arguments
                if (positional.Count > 0 && cmdDef.Arguments.Count > 0)
                {
                    int posIndex = 0;
                    foreach (var argDef in cmdDef.Arguments)
                    {
                        if (!args.ContainsKey(argDef.Name) && posIndex < positional.Count)
                        {
                            // If it's the last argument and more positional arguments remain, join them
                            if (posIndex == cmdDef.Arguments.Count - 1 && positional.Count > posIndex + 1)
                            {
                                args[argDef.Name] = string.Join(" ", positional.Skip(posIndex));
                                break;
                            }
                            else
                            {
                                args[argDef.Name] = positional[posIndex++];
                            }
                        }
                    }
                }

                // Verify required arguments
                foreach (var argDef in cmdDef.Arguments.Where(a => a.IsRequired))
                {
                    if (!args.ContainsKey(argDef.Name))
                    {
                        errors.Add($"Missing required argument '{argDef.Name}'. Usage: {cmdDef.UsageExample}");
                    }
                }

                // Verify allowed values
                foreach (var (k, v) in args)
                {
                    var def = cmdDef.Arguments.FirstOrDefault(a => string.Equals(a.Name, k, StringComparison.OrdinalIgnoreCase));
                    if (def?.AllowedValues != null && def.AllowedValues.Count > 0)
                    {
                        if (!def.AllowedValues.Contains(v, StringComparer.OrdinalIgnoreCase))
                        {
                            errors.Add($"Invalid value '{v}' for '{k}'. Allowed: {string.Join(", ", def.AllowedValues)}");
                        }
                    }
                }
            }
            else
            {
                errors.Add($"Unknown command '/{commandName}'. Type /help for available commands.");
            }
        }

        return new ParsedStudyCommand(commandName, input, args, positional, errors.Count == 0, errors);
    }

    public IReadOnlyList<CommandAutocompleteItem> GetAutocompleteSuggestions(string partialInput, IReadOnlyList<StudyCommandDefinition> knownCommands)
    {
        if (string.IsNullOrWhiteSpace(partialInput))
        {
            return knownCommands.Select(ToAutocompleteItem).ToList();
        }

        var trimmed = partialInput.TrimStart();
        if (!trimmed.StartsWith('/'))
        {
            return [];
        }

        var query = trimmed[1..].Trim();
        var spaceIdx = query.IndexOf(' ');

        if (spaceIdx < 0)
        {
            // Autocomplete command name
            return knownCommands
                .Where(c => c.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
                            c.Aliases.Any(a => a.TrimStart('/').StartsWith(query, StringComparison.OrdinalIgnoreCase)))
                .Select(ToAutocompleteItem)
                .ToList();
        }

        // Inside a command: autocomplete argument names
        var cmdName = query[..spaceIdx].Trim();
        var cmdDef = knownCommands.FirstOrDefault(c =>
            string.Equals(c.Name, cmdName, StringComparison.OrdinalIgnoreCase) ||
            c.Aliases.Any(a => string.Equals(a.TrimStart('/'), cmdName, StringComparison.OrdinalIgnoreCase)));

        if (cmdDef == null) return [];

        return cmdDef.Arguments.Select(arg => new CommandAutocompleteItem(
            cmdDef.Name,
            $"{arg.Name}=",
            arg.Description,
            cmdDef.Category.ToString(),
            $"{arg.Name}=\"\"",
            [arg.Name])).ToList();
    }

    private static CommandAutocompleteItem ToAutocompleteItem(StudyCommandDefinition cmd)
    {
        var argNames = cmd.Arguments.Select(a => a.Name).ToList();
        var template = cmd.Arguments.Count > 0
            ? $"/{cmd.Name} {string.Join(" ", cmd.Arguments.Select(a => $"{a.Name}=\"\""))}"
            : $"/{cmd.Name}";

        return new CommandAutocompleteItem(
            cmd.Name,
            $"/{cmd.Name}",
            cmd.Description,
            cmd.Category.ToString(),
            template,
            argNames);
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        char quoteChar = '\0';

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == quoteChar)
                {
                    inQuotes = false;
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c is '"' or '\'')
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        if (sb.Length > 0)
        {
            tokens.Add(sb.ToString());
        }

        return tokens;
    }
}
