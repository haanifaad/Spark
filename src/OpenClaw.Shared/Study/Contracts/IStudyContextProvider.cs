using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Orchestration;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Educational context aggregation contract.
/// </summary>
public interface IStudyContextProvider
{
    Task<StudyContextEnvelope> BuildContextAsync(
        string query,
        string userId = "default",
        string? courseId = null,
        string? lessonId = null,
        CancellationToken ct = default);
}
