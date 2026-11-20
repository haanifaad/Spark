namespace OpenClaw.Shared.Specialists.Finance;

/// <summary>
/// Exception thrown when a proposed purchase or booking violates configured backend spending caps.
/// </summary>
public sealed class SpendingLimitExceededException : InvalidOperationException
{
    public decimal RequestedAmount { get; }
    public decimal LimitAmount { get; }
    public string LimitType { get; }

    public SpendingLimitExceededException(string message, decimal requested, decimal limit, string limitType)
        : base(message)
    {
        RequestedAmount = requested;
        LimitAmount = limit;
        LimitType = limitType;
    }
}

/// <summary>
/// Backend engine enforcing India-first financial limits, hosted checkout sessions, and transaction lifecycles.
/// </summary>
public interface ISpendingLimitsEngine
{
    Task CheckTransactionAllowedAsync(decimal amount, string merchantName, string category, string currency = "INR", CancellationToken ct = default);
    Task<SparkFinancialTransactionRecord> InitiateTransactionAsync(string taskId, string merchantName, string category, decimal amount, string currency = "INR", CancellationToken ct = default);
    Task<SparkFinancialTransactionRecord> UpdateTransactionStageAsync(string transactionId, SparkPaymentLifecycleStage stage, string? providerRef = null, string? failureReason = null, CancellationToken ct = default);
    Task<SparkSpendingSummary> GetSpendingSummaryAsync(CancellationToken ct = default);
    Task<SparkSpendingLimitsConfig> GetConfigAsync(CancellationToken ct = default);
    Task UpdateConfigAsync(SparkSpendingLimitsConfig config, CancellationToken ct = default);
}
