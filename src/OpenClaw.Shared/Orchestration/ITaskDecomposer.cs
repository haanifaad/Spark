using OpenClaw.Shared.Tools;

namespace OpenClaw.Shared.Orchestration;

/// <summary>
/// Contract for decomposing complex bilingual user instructions into structured sequential steps.
/// </summary>
public interface ITaskDecomposer
{
    Task<List<SparkTaskStep>> DecomposeInstructionAsync(string taskId, string instruction, string language, ISparkToolRegistry? toolRegistry = null, CancellationToken ct = default);
}
