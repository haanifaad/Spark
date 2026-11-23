using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenClaw.Shared.Creative;

/// <summary>
/// Metadata definition for a loaded creative skill.
/// </summary>
public sealed record CreativeSkillEntry
{
    public required string Id { get; init; }
    public required string SkillId { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string CategoryTitle { get; init; }
    public required string Application { get; init; }
    public required string ApplicationId { get; init; }
    public required string CliApp { get; init; }
    public required string Purpose { get; init; }
    public required string Path { get; init; }
    public bool IsEnabled { get; set; } = true;
    public int ExecutionCount { get; set; }
}

/// <summary>
/// Loads and indexes the 240 professional skills in the SPARK Creative Skills Engine.
/// </summary>
public sealed class CreativeSkillRegistry
{
    private static readonly Lazy<CreativeSkillRegistry> s_instance = new(() => new CreativeSkillRegistry());
    public static CreativeSkillRegistry Instance => s_instance.Value;

    private readonly List<CreativeSkillEntry> _skills = new();
    private readonly Dictionary<string, CreativeSkillEntry> _byId = new(StringComparer.OrdinalIgnoreCase);

    public int TotalSkillsCount => _skills.Count;
    public IReadOnlyList<CreativeSkillEntry> AllSkills => _skills.AsReadOnly();

    public CreativeSkillRegistry()
    {
        LoadSkillsCatalog();
    }

    private void LoadSkillsCatalog()
    {
        string[] candidatePaths =
        [
            Path.Combine(AppContext.BaseDirectory, "skills", "creativity", "skills_catalog.json"),
            @"C:\Intel\Spark\Spark-main\src\skills\creativity\skills_catalog.json"
        ];

        string? foundPath = candidatePaths.FirstOrDefault(File.Exists);
        if (foundPath != null)
        {
            try
            {
                var json = File.ReadAllText(foundPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("skills", out var skillsArray))
                {
                    foreach (var elem in skillsArray.EnumerateArray())
                    {
                        var entry = new CreativeSkillEntry
                        {
                            Id = elem.GetProperty("id").GetString() ?? "",
                            SkillId = elem.GetProperty("skillId").GetString() ?? "",
                            Name = elem.GetProperty("name").GetString() ?? "",
                            Category = elem.GetProperty("category").GetString() ?? "",
                            CategoryTitle = elem.GetProperty("categoryTitle").GetString() ?? "",
                            Application = elem.GetProperty("application").GetString() ?? "",
                            ApplicationId = elem.GetProperty("applicationId").GetString() ?? "",
                            CliApp = elem.GetProperty("cliApp").GetString() ?? "",
                            Purpose = elem.GetProperty("purpose").GetString() ?? "",
                            Path = elem.GetProperty("path").GetString() ?? ""
                        };
                        _skills.Add(entry);
                        _byId[entry.Id] = entry;
                        _byId[entry.SkillId] = entry;
                    }
                }
            }
            catch { }
        }
    }

    public IEnumerable<CreativeSkillEntry> SearchSkills(string? query = null, string? category = null, string? applicationId = null)
    {
        IEnumerable<CreativeSkillEntry> results = _skills;

        if (!string.IsNullOrWhiteSpace(category))
        {
            results = results.Where(s => string.Equals(s.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(applicationId))
        {
            results = results.Where(s => string.Equals(s.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(s.Application, applicationId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            results = results.Where(s =>
                s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.Purpose.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.CategoryTitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.SkillId.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return results;
    }

    public bool TryGetSkill(string skillId, out CreativeSkillEntry skill)
    {
        return _byId.TryGetValue(skillId, out skill!);
    }
}
