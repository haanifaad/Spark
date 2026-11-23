namespace OpenClaw.Shared.Browser;

/// <summary>
/// Contract for browser automation host with live visibility and user takeover coordination.
/// </summary>
public interface ISparkBrowserWorker
{
    Task<SparkBrowserSessionSnapshot> NavigateAsync(string url, CancellationToken ct = default);
    Task<SparkBrowserSessionSnapshot> FillFieldAsync(string fieldDescription, string value, bool isSensitive = false, CancellationToken ct = default);
    Task<SparkBrowserSessionSnapshot> CaptureLiveSnapshotAsync(CancellationToken ct = default);
    Task<SparkBrowserSessionSnapshot> InitiateUserTakeoverAsync(CancellationToken ct = default);
    Task<SparkBrowserSessionSnapshot> ReleaseUserTakeoverAsync(CancellationToken ct = default);
    Task<SparkBrowserSessionSnapshot> PauseAsync(CancellationToken ct = default);
    Task<SparkBrowserSessionSnapshot> ResumeAsync(CancellationToken ct = default);
    Task CloseAsync(CancellationToken ct = default);
    IReadOnlyList<SparkBrowserActionRecord> GetActionHistory();
}
