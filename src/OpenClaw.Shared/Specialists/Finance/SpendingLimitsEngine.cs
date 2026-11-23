using System.Collections.Concurrent;
using System.Text.Json;

namespace OpenClaw.Shared.Specialists.Finance;

/// <summary>
/// Production backend engine enforcing financial safeguards, India-first payment workflows, and spending caps.
/// </summary>
public sealed class SpendingLimitsEngine : ISpendingLimitsEngine
{
    private readonly string _storageDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SparkSpendingLimitsConfig _config = new();
    private readonly ConcurrentDictionary<string, SparkFinancialTransactionRecord> _transactions = new();

    public SpendingLimitsEngine(string? customStorageDir = null)
    {
        _storageDirectory = customStorageDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Spark", "FinancialControl");
        Directory.CreateDirectory(_storageDirectory);

        LoadState();
    }

    public Task<SparkSpendingLimitsConfig> GetConfigAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_config);
    }

    public async Task UpdateConfigAsync(SparkSpendingLimitsConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _config = config;
            await SaveConfigInternalAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CheckTransactionAllowedAsync(decimal amount, string merchantName, string category, string currency = "INR", CancellationToken ct = default)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Transaction amount must be strictly positive.");

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 1. Per-transaction limit
            if (amount > _config.MaxPerTransactionLimit)
            {
                throw new SpendingLimitExceededException(
                    $"Proposed amount of {amount:N2} {currency} exceeds maximum per-transaction limit of {_config.MaxPerTransactionLimit:N2} {currency}.",
                    amount,
                    _config.MaxPerTransactionLimit,
                    "PerTransaction");
            }

            // Calculate current today and month totals
            var today = DateTimeOffset.UtcNow.Date;
            var month = new DateTime(today.Year, today.Month, 1);

            var todayTotal = _transactions.Values
                .Where(t => t.InitiatedAt.Date == today && t.Stage is SparkPaymentLifecycleStage.PaymentConfirmed or SparkPaymentLifecycleStage.PaymentProcessing or SparkPaymentLifecycleStage.WaitingForUserAuthorization)
                .Sum(t => t.Amount);

            var monthTotal = _transactions.Values
                .Where(t => t.InitiatedAt >= month && t.Stage is SparkPaymentLifecycleStage.PaymentConfirmed or SparkPaymentLifecycleStage.PaymentProcessing or SparkPaymentLifecycleStage.WaitingForUserAuthorization)
                .Sum(t => t.Amount);

            // 2. Daily limit
            if (todayTotal + amount > _config.DailySpendingLimit)
            {
                throw new SpendingLimitExceededException(
                    $"Transaction of {amount:N2} {currency} would exceed daily spending limit of {_config.DailySpendingLimit:N2} {currency} (current today: {todayTotal:N2} {currency}).",
                    amount,
                    _config.DailySpendingLimit,
                    "Daily");
            }

            // 3. Monthly limit
            if (monthTotal + amount > _config.MonthlySpendingLimit)
            {
                throw new SpendingLimitExceededException(
                    $"Transaction of {amount:N2} {currency} would exceed monthly spending limit of {_config.MonthlySpendingLimit:N2} {currency} (current month: {monthTotal:N2} {currency}).",
                    amount,
                    _config.MonthlySpendingLimit,
                    "Monthly");
            }

            // 4. Per-merchant limit if configured
            if (!string.IsNullOrWhiteSpace(merchantName) && _config.PerMerchantLimits.TryGetValue(merchantName, out var merchantLimit))
            {
                if (amount > merchantLimit)
                {
                    throw new SpendingLimitExceededException(
                        $"Transaction of {amount:N2} {currency} exceeds limit of {merchantLimit:N2} {currency} for merchant '{merchantName}'.",
                        amount,
                        merchantLimit,
                        "Merchant");
                }
            }

            // 5. Category-based limit if configured
            if (!string.IsNullOrWhiteSpace(category) && _config.CategoryLimits.TryGetValue(category, out var categoryLimit))
            {
                if (amount > categoryLimit)
                {
                    throw new SpendingLimitExceededException(
                        $"Transaction of {amount:N2} {currency} exceeds limit of {categoryLimit:N2} {currency} for category '{category}'.",
                        amount,
                        categoryLimit,
                        "Category");
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkFinancialTransactionRecord> InitiateTransactionAsync(string taskId, string merchantName, string category, decimal amount, string currency = "INR", CancellationToken ct = default)
    {
        // Enforce backend spending checks first
        await CheckTransactionAllowedAsync(amount, merchantName, category, currency, ct).ConfigureAwait(false);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var txId = Guid.NewGuid().ToString("N");
            var cleanMerchant = merchantName.Replace(" ", "").ToLowerInvariant();

            // Construct secure hosted checkout URL and UPI Intent
            var checkoutUrl = $"https://checkout.spark-agent.internal/pay/{txId}?merchant={cleanMerchant}&amount={amount:F2}&cu={currency}";
            var upiIntent = $"upi://pay?pa={cleanMerchant}@icici&pn={Uri.EscapeDataString(merchantName)}&am={amount:F2}&cu={currency}&tr={txId}";

            var record = new SparkFinancialTransactionRecord
            {
                TransactionId = txId,
                TaskId = taskId,
                MerchantName = merchantName,
                Category = category,
                Amount = amount,
                Currency = currency,
                Stage = SparkPaymentLifecycleStage.WaitingForUserAuthorization,
                HostedCheckoutUrl = checkoutUrl,
                UpiIntentString = upiIntent,
                InitiatedAt = DateTimeOffset.UtcNow
            };

            _transactions[txId] = record;
            await SaveTransactionInternalAsync(record, ct).ConfigureAwait(false);
            return record;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkFinancialTransactionRecord> UpdateTransactionStageAsync(string transactionId, SparkPaymentLifecycleStage stage, string? providerRef = null, string? failureReason = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_transactions.TryGetValue(transactionId, out var record))
                throw new KeyNotFoundException($"Transaction '{transactionId}' was not found.");

            record.Stage = stage;
            if (!string.IsNullOrWhiteSpace(providerRef))
                record.ProviderReference = providerRef;

            if (!string.IsNullOrWhiteSpace(failureReason))
                record.FailureReason = failureReason;

            if (stage is SparkPaymentLifecycleStage.PaymentConfirmed or SparkPaymentLifecycleStage.PaymentFailed or SparkPaymentLifecycleStage.RefundConfirmed)
            {
                record.CompletedAt = DateTimeOffset.UtcNow;
            }

            await SaveTransactionInternalAsync(record, ct).ConfigureAwait(false);
            return record;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<SparkSpendingSummary> GetSpendingSummaryAsync(CancellationToken ct = default)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var month = new DateTime(today.Year, today.Month, 1);

        var summary = new SparkSpendingSummary
        {
            TodayCompletedTotal = _transactions.Values
                .Where(t => t.InitiatedAt.Date == today && t.Stage == SparkPaymentLifecycleStage.PaymentConfirmed)
                .Sum(t => t.Amount),

            MonthCompletedTotal = _transactions.Values
                .Where(t => t.InitiatedAt >= month && t.Stage == SparkPaymentLifecycleStage.PaymentConfirmed)
                .Sum(t => t.Amount),

            PendingAuthorizedTotal = _transactions.Values
                .Where(t => t.Stage is SparkPaymentLifecycleStage.WaitingForUserAuthorization or SparkPaymentLifecycleStage.PaymentProcessing)
                .Sum(t => t.Amount),

            TotalRefunded = _transactions.Values
                .Where(t => t.Stage == SparkPaymentLifecycleStage.RefundConfirmed)
                .Sum(t => t.Amount),

            TotalTransactionCount = _transactions.Count
        };

        return Task.FromResult(summary);
    }

    private void LoadState()
    {
        try
        {
            var configPath = Path.Combine(_storageDirectory, "limits_config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var loaded = JsonSerializer.Deserialize<SparkSpendingLimitsConfig>(json);
                if (loaded != null)
                    _config = loaded;
            }

            var txDir = Path.Combine(_storageDirectory, "transactions");
            if (Directory.Exists(txDir))
            {
                foreach (var file in Directory.GetFiles(txDir, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var tx = JsonSerializer.Deserialize<SparkFinancialTransactionRecord>(json);
                        if (tx != null)
                            _transactions[tx.TransactionId] = tx;
                    }
                    catch
                    {
                        // Ignore individual corrupted files
                    }
                }
            }
        }
        catch
        {
            // Fallback to defaults
        }
    }

    private async Task SaveConfigInternalAsync(CancellationToken ct)
    {
        var configPath = Path.Combine(_storageDirectory, "limits_config.json");
        var tempPath = configPath + ".tmp";
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
        File.Move(tempPath, configPath, overwrite: true);
    }

    private async Task SaveTransactionInternalAsync(SparkFinancialTransactionRecord record, CancellationToken ct)
    {
        var txDir = Path.Combine(_storageDirectory, "transactions");
        Directory.CreateDirectory(txDir);
        var txPath = Path.Combine(txDir, $"{record.TransactionId}.json");
        var tempPath = txPath + ".tmp";
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
        File.Move(tempPath, txPath, overwrite: true);
    }
}
