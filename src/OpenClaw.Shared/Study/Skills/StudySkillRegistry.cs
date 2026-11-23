using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Skills;

/// <summary>
/// Persistent registry and lifecycle manager for Study Division skills.
/// Backed by the thread-safe, ACID-atomic StudyDataStore.
/// </summary>
public sealed class StudySkillRegistry : IStudySkillRegistry
{
    private readonly IStudyDataStore _dataStore;

    public StudySkillRegistry(IStudyDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    public async Task RegisterSkillAsync(StudySkillDefinition skill, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skill);
        var errors = await ValidateSkillDefinitionAsync(skill);
        if (errors.Count > 0)
        {
            throw new ArgumentException($"Invalid skill definition: {string.Join("; ", errors)}");
        }

        skill.UpdatedAtUtc = DateTime.UtcNow;
        await _dataStore.SaveSkillAsync(skill, userId, ct);
    }

    public async Task<StudySkillDefinition?> GetSkillAsync(string skillId, string userId = "default", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(skillId)) return null;
        var skill = await _dataStore.GetSkillAsync(skillId, userId, ct);
        if (skill != null) return skill;

        var normalized = skillId.StartsWith("skill-", StringComparison.OrdinalIgnoreCase)
            ? skillId["skill-".Length..]
            : skillId;
        skill = await _dataStore.GetSkillAsync(normalized, userId, ct);
        if (skill != null) return skill;

        var builtIn = StudyBuiltInSkills.GetAll().FirstOrDefault(s =>
            string.Equals(s.Id, skillId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.Id, normalized, StringComparison.OrdinalIgnoreCase));
        if (builtIn != null)
        {
            await _dataStore.SaveSkillAsync(builtIn, userId, ct);
            return builtIn;
        }

        return null;
    }

    public async Task<IReadOnlyList<StudySkillDefinition>> ListSkillsAsync(
        SkillCategory? category = null,
        bool includeDisabled = false,
        string userId = "default",
        CancellationToken ct = default)
    {
        var skills = await _dataStore.ListSkillsAsync(userId, ct);

        // If data store is newly initialized or empty, seed from built-ins
        if (skills.Count == 0)
        {
            foreach (var b in StudyBuiltInSkills.GetAll())
            {
                await _dataStore.SaveSkillAsync(b, userId, ct);
            }
            skills = await _dataStore.ListSkillsAsync(userId, ct);
        }

        var query = skills.AsEnumerable();
        if (!includeDisabled)
        {
            query = query.Where(s => s.IsEnabled && s.Status != SkillStatus.Disabled);
        }

        if (category.HasValue)
        {
            query = query.Where(s => s.Category == category.Value);
        }

        return query.OrderBy(s => s.Category).ThenBy(s => s.Name).ToList();
    }

    public async Task<IReadOnlyList<StudySkillDefinition>> SearchSkillsAsync(string searchTerm, string userId = "default", CancellationToken ct = default)
    {
        var all = await ListSkillsAsync(null, includeDisabled: true, userId, ct);
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return all.Where(s => s.IsEnabled).ToList();
        }

        var term = searchTerm.Trim();
        return all
            .Where(s => s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        s.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        s.Id.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        s.TriggerConditions.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        s.Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<bool> SetSkillEnabledAsync(string skillId, bool isEnabled, string userId = "default", CancellationToken ct = default)
    {
        var skill = await GetSkillAsync(skillId, userId, ct);
        if (skill == null) return false;

        skill.IsEnabled = isEnabled;
        skill.Status = isEnabled ? SkillStatus.Active : SkillStatus.Disabled;
        skill.UpdatedAtUtc = DateTime.UtcNow;

        await _dataStore.SaveSkillAsync(skill, userId, ct);
        return true;
    }

    public Task<IReadOnlyList<string>> ValidateSkillDefinitionAsync(StudySkillDefinition skill)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(skill.Id))
        {
            errors.Add("Skill Id is required.");
        }
        else if (skill.Id.Any(char.IsWhiteSpace))
        {
            errors.Add("Skill Id must not contain whitespace.");
        }

        if (string.IsNullOrWhiteSpace(skill.Name))
        {
            errors.Add("Skill Name is required.");
        }

        if (string.IsNullOrWhiteSpace(skill.Description))
        {
            errors.Add("Skill Description is required.");
        }

        if (string.IsNullOrWhiteSpace(skill.Instructions))
        {
            errors.Add("Skill Instructions are required.");
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    public async Task RecordSkillExecutionAsync(SkillExecutionRecord record, string userId = "default", CancellationToken ct = default)
    {
        await _dataStore.SaveSkillExecutionAsync(record, userId, ct);
    }

    public async Task<IReadOnlyList<SkillExecutionRecord>> GetSkillExecutionHistoryAsync(
        string? skillId = null,
        string userId = "default",
        int limit = 50,
        CancellationToken ct = default)
    {
        return await _dataStore.ListSkillExecutionsAsync(skillId, userId, limit, ct);
    }
}
