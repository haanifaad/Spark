using OpenClaw.Shared.Memory;

namespace OpenClaw.Shared.Orchestration;

/// <summary>
/// Contract for formulating, risk-evaluating, and dynamically replanning autonomous task execution plans.
/// </summary>
public interface ITaskPlanner
{
    Task<SparkTaskPlan> CreatePlanAsync(SparkTask task, ISparkMemoryStore? memoryStore = null, CancellationToken ct = default);
    Task<SparkTaskPlan> ReplanAsync(SparkTask task, string modificationReason = "Instruction modified", ISparkMemoryStore? memoryStore = null, CancellationToken ct = default);
}
