using OpenClaw.Shared.Security.Approvals;

namespace OpenClaw.Shared.Orchestration;

/// <summary>
/// Central coordinator orchestrating autonomous agent task intake, planning, execution, verification, and user takeover.
/// </summary>
public interface IAgentOrchestrator
{
    Task<SparkTask> SubmitTaskAsync(string instruction, string? title = null, SparkTaskPriority priority = SparkTaskPriority.Normal, string userId = "default", CancellationToken ct = default);
    Task<SparkTask> ExecuteTaskAsync(string taskId, string userId = "default", CancellationToken ct = default);
    Task<SparkTask> PauseTaskAsync(string taskId, string userId = "default", CancellationToken ct = default);
    Task<SparkTask> ResumeTaskAsync(string taskId, string userId = "default", CancellationToken ct = default);
    Task<SparkTask> CancelTaskAsync(string taskId, string reason = "User cancelled", string userId = "default", CancellationToken ct = default);
    Task<SparkTask> ModifyTaskAsync(string taskId, string newInstruction, string reason = "User edit", string userId = "default", CancellationToken ct = default);
    Task<SparkTask> SetUserTakeoverAsync(string taskId, bool isTakeoverActive, string userId = "default", CancellationToken ct = default);
    Task<SparkTask> ApproveStepAsync(string taskId, string stepId, string approverIdentity, string expectedBindingSignature, string userId = "default", CancellationToken ct = default);
}
