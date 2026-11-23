using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Architectural contract placeholder for future Study Division custom skills (Phase 2+).
/// </summary>
public interface IStudySkill
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    IReadOnlyList<string> SupportedTopics { get; }
    Task<string> ExecuteSkillAsync(string prompt, IDictionary<string, object> parameters, CancellationToken ct = default);
}

/// <summary>
/// Architectural contract placeholder for future Study Division tools (Phase 2+).
/// </summary>
public interface IStudyTool
{
    string ToolName { get; }
    string Description { get; }
    StudyPermissionKind RequiredPermission { get; }
    Task<object?> InvokeToolAsync(IDictionary<string, object> args, CancellationToken ct = default);
}

/// <summary>
/// Architectural contract placeholder for future Study Division commands (Phase 2+).
/// </summary>
public interface IStudyCommand
{
    string CommandTrigger { get; }
    string HelpText { get; }
    Task ExecuteCommandAsync(string argument, CancellationToken ct = default);
}

/// <summary>
/// Architectural contract placeholder for future Study Division autonomous learning agents (Phase 3+).
/// </summary>
public interface IStudyAgent
{
    string AgentId { get; }
    string RoleTitle { get; }
    Task RunAgentCycleAsync(string goal, CancellationToken ct = default);
}

/// <summary>
/// Architectural contract placeholder for future Study Division No-Code extensions (Phase 4+).
/// </summary>
public interface IStudyExtension
{
    string ManifestId { get; }
    string DisplayName { get; }
    string Version { get; }
    bool IsEnabled { get; }
}
