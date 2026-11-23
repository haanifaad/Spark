namespace OpenClaw.Shared.Background;

/// <summary>
/// Contract for the persistent background worker orchestrating unattended autonomous tasks.
/// </summary>
public interface ISparkBackgroundWorker
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    SparkBackgroundWorkerStatus GetStatus();
    Task<int> PerformWorkerCycleAsync(CancellationToken ct = default);
    Task UpdateConfigAsync(SparkBackgroundWorkerConfig config, CancellationToken ct = default);
}
