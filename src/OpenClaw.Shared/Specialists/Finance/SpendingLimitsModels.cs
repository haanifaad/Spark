namespace OpenClaw.Shared.Specialists.Finance;

/// <summary>
/// Distinct stages of payment and commerce authorization.
/// </summary>
public enum SparkPaymentLifecycleStage
{
    PreparingPurchase = 0,
    CreatingCheckoutSession = 1,
    WaitingForUserAuthorization = 2,
    PaymentProcessing = 3,
    PaymentConfirmed = 4,
    PaymentFailed = 5,
    RefundRequested = 6,
    RefundConfirmed = 7
}

/// <summary>
/// Configurable spending limits enforced by the backend before transactions can proceed.
/// </summary>
public sealed class SparkSpendingLimitsConfig
{
    public decimal MaxPerTransactionLimit { get; set; } = 5000.00m; // Default: 5,000 INR
    public decimal DailySpendingLimit { get; set; } = 15000.00m;    // Default: 15,000 INR
    public decimal MonthlySpendingLimit { get; set; } = 50000.00m;  // Default: 50,000 INR
    public string DefaultCurrency { get; set; } = "INR";
    public Dictionary<string, decimal> PerMerchantLimits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> CategoryLimits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Record of an autonomous financial transaction tracking authorization, state, and amounts.
/// </summary>
public sealed class SparkFinancialTransactionRecord
{
    public string TransactionId { get; init; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; init; } = string.Empty;
    public required string MerchantName { get; init; }
    public required string Category { get; init; } // e.g. Travel, Shopping, Utilities
    public required decimal Amount { get; init; }
    public string Currency { get; init; } = "INR";
    public SparkPaymentLifecycleStage Stage { get; set; } = SparkPaymentLifecycleStage.PreparingPurchase;
    public string? HostedCheckoutUrl { get; set; }
    public string? ProviderReference { get; set; }
    public string? UpiIntentString { get; set; }
    public bool RequiresUserPinConfirmation => true; // Always requires user confirmation
    public DateTimeOffset InitiatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}

/// <summary>
/// Aggregated spending totals tracking authorized, completed, and refunded amounts.
/// </summary>
public sealed class SparkSpendingSummary
{
    public decimal TodayCompletedTotal { get; set; }
    public decimal MonthCompletedTotal { get; set; }
    public decimal PendingAuthorizedTotal { get; set; }
    public decimal TotalRefunded { get; set; }
    public int TotalTransactionCount { get; set; }
}
