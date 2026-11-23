using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Execution delegate for a registered Study tool.
/// </summary>
public interface IStudyToolHandler
{
    Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default);
}
