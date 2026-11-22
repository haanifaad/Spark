using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Persistent registry and lifecycle manager for Study Division skills.
/// </summary>
public interface IStudySkillRegistry
{
    Task RegisterSkillAsync(StudySkillDefinition skill, string userId = "default", CancellationToken ct = default);

    Task<StudySkillDefinition?> GetSkillAsync(string skillId, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<StudySkillDefinition>> ListSkillsAsync(
        SkillCategory? category = null,
        bool includeDisabled = false,
        string userId = "default",
        CancellationToken ct = default);

    Task<IReadOnlyList<StudySkillDefinition>> SearchSkillsAsync(string searchTerm, string userId = "default", CancellationToken ct = default);

    Task<bool> SetSkillEnabledAsync(string skillId, bool isEnabled, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<string>> ValidateSkillDefinitionAsync(StudySkillDefinition skill);

    Task RecordSkillExecutionAsync(SkillExecutionRecord record, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<SkillExecutionRecord>> GetSkillExecutionHistoryAsync(
        string? skillId = null,
        string userId = "default",
        int limit = 50,
        CancellationToken ct = default);
}
