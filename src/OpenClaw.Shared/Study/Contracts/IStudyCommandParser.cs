using System.Collections.Generic;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Robust tokenizer and parser for Study slash commands.
/// Handles commands, quotes, key-value arguments, positional parameters, and autocomplete metadata.
/// </summary>
public interface IStudyCommandParser
{
    bool IsSlashCommand(string input);

    ParsedStudyCommand Parse(string input, IReadOnlyList<StudyCommandDefinition>? knownCommands = null);

    IReadOnlyList<CommandAutocompleteItem> GetAutocompleteSuggestions(string partialInput, IReadOnlyList<StudyCommandDefinition> knownCommands);
}
