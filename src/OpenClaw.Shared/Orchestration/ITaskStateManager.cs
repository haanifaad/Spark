namespace OpenClaw.Shared.Orchestration;

/// <summary>
/// Contract for atomic, thread-safe persistence and lifecycle transitions of SPARK autonomous tasks.
/// </summary>
public interface ITaskStateManager
{
    Task<SparkTask> CreateTaskAsync(string instruction, string? title = null, SparkTaskPriority priority = SparkTaskPriority.Normal, string userId = "default", CancellationToken ct = default);
    Task<SparkTask?> GetTaskAsync(string taskId, string userId = "default", CancellationToken ct = default);
    Task<IReadOnlyList<SparkTask>> GetAllTasksAsync(string userId = "default", CancellationToken ct = default);
    Task<SparkTask> UpdateTaskStateAsync(string taskId, SparkTaskState newState, string? statusReason = null, string userId = "default", CancellationToken ct = default);
    Task<SparkTask> SaveTaskAsync(SparkTask task, CancellationToken ct = default);
    Task<SparkTask> UpdateTaskStepAsync(string taskId, SparkTaskStep step, string userId = "default", CancellationToken ct = default);
    Task<SparkTask> ModifyTaskInstructionAsync(string taskId, string newInstruction, string reason = "User edit", string userId = "default", CancellationToken ct = default);
    Task<int> RecoverInterruptedTasksAsync(string userId = "default", CancellationToken ct = default);
    Task<bool> DeleteTaskAsync(string taskId, string userId = "default", CancellationToken ct = default);
}
