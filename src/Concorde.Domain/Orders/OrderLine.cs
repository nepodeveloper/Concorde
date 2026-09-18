namespace Concorde.Domain.Orders;

using Common;

/// <summary>
/// Represents a single line item in an order.
/// All validation happens at construction time (fail-fast).
/// </summary>
public class OrderLine
{
    public const int MinQuantity = 1;
    public const int MaxQuantity = 1_000_000;
    public const int MaxSkuLength = 50;
    public const int MaxNameLength = 200;

    public Guid Id { get; private set; }
    public string Sku { get; private set; }
    public string Name { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money LineTotal { get; private set; }

    // For EF Core
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    private OrderLine() { }
#pragma warning restore CS8618

    /// <summary>
    /// Create a new order line with validation.
    /// </summary>
    public OrderLine(string sku, string name, int quantity, Money unitPrice)
    {
        ValidateSku(sku);
        ValidateName(name);
        ValidateQuantity(quantity);
        ArgumentNullException.ThrowIfNull(unitPrice);

        Id = Guid.NewGuid();
        Sku = sku.Trim();
        Name = name.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = Money.FromLineCalculation(quantity, unitPrice);
    }

    private static void ValidateSku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU is required.", nameof(sku));

        var trimmed = sku.Trim();
        if (trimmed.Length > MaxSkuLength)
            throw new ArgumentException(
                $"SKU may not exceed {MaxSkuLength} characters.", nameof(sku));
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Item name is required.", nameof(name));

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException(
                $"Item name may not exceed {MaxNameLength} characters.", nameof(name));
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity < MinQuantity)
            throw new ArgumentException(
                $"Quantity must be at least {MinQuantity}.", nameof(quantity));

        if (quantity > MaxQuantity)
            throw new ArgumentException(
                $"Quantity may not exceed {MaxQuantity}.", nameof(quantity));
    }
}
