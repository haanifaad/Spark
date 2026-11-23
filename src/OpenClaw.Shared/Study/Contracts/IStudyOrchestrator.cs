using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Orchestration;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Educational AI Orchestration contract coordinating intent routing, context assembly,
/// and SPARK model routing.
/// </summary>
public interface IStudyOrchestrator
{
    Task<StudyInteractionResult> ProcessTurnAsync(
        StudyInteractionRequest request,
        CancellationToken ct = default);
}
