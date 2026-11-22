using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Execution permission categories defined by the Study Division security policy.
/// </summary>
public enum StudyPermissionKind
{
    ReadOnly,
    FileAccess,
    ExternalApiAccess,
    FileModification,
    CodeExecution,
    DestructiveOperation,
    WriteFilesystem = FileModification
}

/// <summary>
/// Result of a Study permission verification check.
/// </summary>
public sealed record StudyPermissionDecision(
    bool IsAllowed,
    bool RequiredUserApproval,
    string? Reason = null);

/// <summary>
/// Enforces Study Division execution boundaries and manual approval gates.
/// </summary>
public interface IStudyPermissionManager
{
    Task<StudyPermissionDecision> AuthorizeAsync(
        StudyPermissionKind permission,
        string targetResource,
        string description,
        string userId = "default",
        CancellationToken ct = default);
}
