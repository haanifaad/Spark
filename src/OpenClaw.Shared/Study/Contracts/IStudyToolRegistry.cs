using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Registry, validator, and execution engine for typed Study Division tools.
/// </summary>
public interface IStudyToolRegistry
{
    void RegisterTool(StudyToolDefinition definition, IStudyToolHandler handler);

    StudyToolDefinition? GetTool(string toolId);

    IReadOnlyList<StudyToolDefinition> ListTools(ToolCategory? category = null);

    IReadOnlyList<StudyToolDefinition> SearchTools(string searchTerm);

    Task<StudyToolExecutionResult> ExecuteToolAsync(StudyToolExecutionRequest request, CancellationToken ct = default);

    Task<StudyToolExecutionResult> ApproveAndExecutePendingAsync(string requestId, string approvedBy, string userId = "default", CancellationToken ct = default);

    Task<StudyToolExecutionResult> RejectPendingAsync(string requestId, string reason, string rejectedBy, string userId = "default", CancellationToken ct = default);

    Task<bool> CancelExecutionAsync(string requestId, string userId = "default", CancellationToken ct = default);

    Task<IReadOnlyList<StudyToolExecutionResult>> GetToolExecutionHistoryAsync(string? toolId = null, string userId = "default", int limit = 50, CancellationToken ct = default);

    Task<StudyToolExecutionResult?> GetExecutionResultAsync(string requestId, string userId = "default", CancellationToken ct = default);
}
