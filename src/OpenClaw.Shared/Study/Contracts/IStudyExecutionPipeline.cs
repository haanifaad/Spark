using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Unified execution pipeline uniting Slash Commands and Natural Language inquiries
/// through a single, permission-gated, deterministic learning intelligence workflow.
/// </summary>
public interface IStudyExecutionPipeline
{
    Task<PipelineExecutionResult> ProcessInputAsync(PipelineExecutionRequest request, CancellationToken ct = default);
}
