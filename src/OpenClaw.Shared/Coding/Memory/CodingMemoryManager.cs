using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.Memory;

namespace OpenClaw.Shared.Coding.Memory;

/// <summary>
/// Project-scoped memory coordinator integrating directly with SPARK's unified ISparkMemoryStore.
/// Preserves architectural knowledge, technical decisions, and conventions across coding tasks
/// without leaking context between unrelated repositories.
/// </summary>
public sealed class CodingMemoryManager
{
    private readonly ISparkMemoryStore _memoryStore;
    private readonly string _projectRoot;
    private readonly string _projectId;

    public string ProjectId => _projectId;
    public string ProjectRoot => _projectRoot;

    public CodingMemoryManager(ISparkMemoryStore memoryStore, string projectRoot)
    {
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        _projectRoot = Path.GetFullPath(projectRoot);
        _projectId = ComputeProjectId(_projectRoot);
    }

    /// <summary>
    /// Computes a stable, deterministic project ID from the normalized root path.
    /// </summary>
    public static string ComputeProjectId(string rootPath)
    {
        var normalized = Path.GetFullPath(rootPath).ToLowerInvariant().TrimEnd('\\', '/');
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "proj_" + Convert.ToHexString(hashBytes)[..12].ToLowerInvariant();
    }

    /// <summary>
    /// Retrieves existing project memory from SPARK shared memory store.
    /// </summary>
    public Task<ProjectMemory?> GetProjectMemoryAsync(CancellationToken ct = default)
    {
        return _memoryStore.GetProjectMemoryAsync(_projectId, ct: ct);
    }

    /// <summary>
    /// Updates or saves project memory from repository analysis and user conventions.
    /// </summary>
    public async Task SaveProjectAnalysisAsync(RepositoryAnalysis analysis, CancellationToken ct = default)
    {
        var existing = await GetProjectMemoryAsync(ct);

        var techStack = analysis.DetectedLanguages.Concat(analysis.PrimaryFrameworks).Distinct().ToList();
        var rules = new List<string>(existing?.ArchitecturalRules ?? Array.Empty<string>());

        if (analysis.BuildSystem != null && !rules.Contains($"Build: {analysis.BuildSystem}"))
        {
            rules.Add($"Build System: {analysis.BuildSystem}");
        }

        var memory = new ProjectMemory
        {
            ProjectId = _projectId,
            Name = analysis.ProjectName,
            RootDirectory = _projectRoot,
            Description = analysis.ArchitectureOverview,
            TechStack = techStack,
            ArchitecturalRules = rules,
            ActiveGoals = existing?.ActiveGoals ?? Array.Empty<string>(),
            Constraints = existing?.Constraints ?? Array.Empty<string>(),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await _memoryStore.SaveProjectMemoryAsync(memory, ct: ct);
    }

    /// <summary>
    /// Records a technical implementation decision made during a coding session.
    /// </summary>
    public async Task RecordDecisionAsync(
        string decisionTitle,
        string rationale,
        string? affectedComponent = null,
        CancellationToken ct = default)
    {
        var record = new MemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Scope = MemoryScope.Project,
            Category = MemoryCategory.TechnicalDecision,
            ProjectId = _projectId,
            Content = $"Decision: {decisionTitle}\nRationale: {rationale}" +
                      (string.IsNullOrEmpty(affectedComponent) ? "" : $"\nAffects: {affectedComponent}"),
            Confidence = 1.0,
            CreatedAt = DateTimeOffset.UtcNow,
            Tags = new[] { "coding", "decision", _projectId }
        };

        await _memoryStore.RememberAsync(record, ct);
    }

    /// <summary>
    /// Records a known issue or constraint discovered in the codebase.
    /// </summary>
    public async Task RecordKnownIssueAsync(
        string issueDescription,
        string? workaround = null,
        CancellationToken ct = default)
    {
        var record = new MemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Scope = MemoryScope.Project,
            Category = MemoryCategory.Constraint,
            ProjectId = _projectId,
            Content = $"Known Issue: {issueDescription}" +
                      (string.IsNullOrEmpty(workaround) ? "" : $"\nWorkaround: {workaround}"),
            Confidence = 0.9,
            CreatedAt = DateTimeOffset.UtcNow,
            Tags = new[] { "coding", "issue", _projectId }
        };

        await _memoryStore.RememberAsync(record, ct);
    }

    /// <summary>
    /// Builds a formatted summary of project memories for injecting into coding agent context.
    /// </summary>
    public async Task<string> BuildMemoryContextSummaryAsync(CancellationToken ct = default)
    {
        var projectMem = await GetProjectMemoryAsync(ct);
        var recentDecisions = await _memoryStore.SearchAsync(
            "coding",
            new MemoryFilter { ProjectId = _projectId, Scope = MemoryScope.Project },
            limit: 5,
            ct: ct);

        var sb = new StringBuilder();
        if (projectMem != null)
        {
            sb.AppendLine("=== PROJECT MEMORY ===");
            if (projectMem.TechStack.Count > 0)
                sb.AppendLine($"Tech Stack: {string.Join(", ", projectMem.TechStack)}");
            if (projectMem.ArchitecturalRules.Count > 0)
            {
                sb.AppendLine("Architectural Conventions:");
                foreach (var rule in projectMem.ArchitecturalRules.Take(4))
                {
                    sb.AppendLine($"  - {rule}");
                }
            }
        }

        if (recentDecisions.Count > 0)
        {
            sb.AppendLine("\nPrevious Decisions & Known Constraints:");
            foreach (var r in recentDecisions.Take(3))
            {
                sb.AppendLine($"  - {r.Record.Content.Replace("\n", " ")}");
            }
        }

        return sb.ToString().Trim();
    }
}
