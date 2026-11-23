using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Registry for slash commands and execution audit tracking.
/// </summary>
public interface IStudyCommandRegistry
{
    void RegisterCommand(StudyCommandDefinition command);

    StudyCommandDefinition? FindCommand(string commandNameOrAlias);

    IReadOnlyList<StudyCommandDefinition> ListCommands(CommandCategory? category = null);

    IReadOnlyList<CommandAutocompleteItem> GetAutocompleteSuggestions(string partialInput);

    Task RecordCommandExecutionAsync(CommandExecutionRecord record, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<CommandExecutionRecord>> GetCommandExecutionHistoryAsync(
        string? commandName = null,
        string userId = "default",
        int limit = 50,
        CancellationToken ct = default);
}
