using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Security.Audit;

/// <summary>
/// Cryptographically protected, tamper-evident audit logger with automated secret redaction.
/// </summary>
public interface ISparkAuditLogger
{
    Task LogEventAsync(SparkAuditRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SparkAuditRecord>> QueryAuditTrailAsync(
        SparkAuditQueryFilter? filter = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyLogIntegrityAsync(CancellationToken cancellationToken = default);
}
