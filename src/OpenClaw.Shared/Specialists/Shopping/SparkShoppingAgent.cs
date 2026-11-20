using OpenClaw.Shared.Specialists.Finance;

namespace OpenClaw.Shared.Specialists.Shopping;

/// <summary>
/// Product item found across merchants with verified specs, prices, and return policies.
/// </summary>
public sealed class SparkProductOffer
{
    public string OfferId { get; init; } = Guid.NewGuid().ToString("N");
    public required string ProductName { get; init; }
    public required string MerchantName { get; init; }
    public required decimal Price { get; init; }
    public string Currency { get; init; } = "INR";
    public string Rating { get; init; } = "4.5/5";
    public string DeliveryTime { get; init; } = "Tomorrow by 5 PM";
    public string ReturnPolicy { get; init; } = "7 days replacement or return";
    public string ProductUrl { get; init; } = string.Empty;
}

/// <summary>
/// Shopping cart representation.
/// </summary>
public sealed class SparkShoppingCart
{
    public string CartId { get; init; } = Guid.NewGuid().ToString("N");
    public required string MerchantName { get; init; }
    public List<SparkProductOffer> Items { get; init; } = [];
    public decimal Subtotal => Items.Sum(i => i.Price);
    public decimal DeliveryFee { get; init; } = 0.0m;
    public decimal TotalAmount => Subtotal + DeliveryFee;
    public string Currency { get; init; } = "INR";
}

/// <summary>
/// Specialist Shopping Agent coordinating product comparison, cart preparation,
/// and hosted checkout creation with backend spending limit enforcement.
/// </summary>
public sealed class SparkShoppingAgent
{
    private readonly ISpendingLimitsEngine _limitsEngine;

    public SparkShoppingAgent(ISpendingLimitsEngine limitsEngine)
    {
        _limitsEngine = limitsEngine ?? throw new ArgumentNullException(nameof(limitsEngine));
    }

    public Task<IReadOnlyList<SparkProductOffer>> SearchAndCompareAsync(string productName, CancellationToken ct = default)
    {
        var offers = new List<SparkProductOffer>
        {
            new()
            {
                ProductName = productName,
                MerchantName = "Amazon India",
                Price = 1899.00m,
                Rating = "4.6/5 (12,400 ratings)",
                DeliveryTime = "Tomorrow",
                ReturnPolicy = "7 days return",
                ProductUrl = "https://amazon.in/dp/example1"
            },
            new()
            {
                ProductName = productName,
                MerchantName = "Flipkart",
                Price = 1949.00m,
                Rating = "4.4/5 (8,900 ratings)",
                DeliveryTime = "2 Days",
                ReturnPolicy = "7 days replacement",
                ProductUrl = "https://flipkart.com/p/example2"
            }
        };

        return Task.FromResult<IReadOnlyList<SparkProductOffer>>(offers);
    }

    public Task<SparkShoppingCart> PrepareCartAsync(string merchantName, List<SparkProductOffer> items, decimal deliveryFee = 0.0m)
    {
        var cart = new SparkShoppingCart
        {
            MerchantName = merchantName,
            Items = items,
            DeliveryFee = deliveryFee
        };

        return Task.FromResult(cart);
    }

    public async Task<SparkFinancialTransactionRecord> PrepareCheckoutSessionAsync(
        string taskId,
        SparkShoppingCart cart,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cart);

        // Enforce backend spending limits before generating hosted checkout session
        var transaction = await _limitsEngine.InitiateTransactionAsync(
            taskId,
            cart.MerchantName,
            "Shopping",
            cart.TotalAmount,
            cart.Currency,
            ct).ConfigureAwait(false);

        return transaction;
    }
}
