namespace OpenClaw.Shared.Tools.Reversal;

/// <summary>
/// Reversibility classification of an autonomous tool operation.
/// </summary>
public enum SparkToolReversibility
{
    Reversible = 0,
    PartiallyReversible = 1,
    Irreversible = 2,
    ReversibleOnlyThroughProviderWorkflow = 3
}

/// <summary>
/// Metadata and state snapshot captured before executing an operation to allow rollback.
/// </summary>
public sealed class SparkReversalMetadata
{
    public string OperationId { get; init; } = Guid.NewGuid().ToString("N");
    public required string ToolName { get; init; }
    public SparkToolReversibility Reversibility { get; init; } = SparkToolReversibility.Irreversible;
    public string? BackupFilePath { get; init; }
    public string? PreviousStateJson { get; init; }
    public string? CompensationToolName { get; init; }
    public Dictionary<string, object?> CompensationArguments { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Contract for managing pre-execution recovery metadata and compensating rollback actions.
/// </summary>
public interface ISparkReversalManager
{
    Task<SparkReversalMetadata> CapturePreExecutionStateAsync(string toolName, Dictionary<string, object?> arguments, SparkToolReversibility reversibility, CancellationToken ct = default);
    Task<bool> AttemptReversalAsync(string operationId, ISparkToolRegistry toolRegistry, CancellationToken ct = default);
    SparkReversalMetadata? GetReversalMetadata(string operationId);
    string GenerateIdempotencyKey(string toolName, string uniqueEntityId);
}
