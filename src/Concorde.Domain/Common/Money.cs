namespace Concorde.Domain.Common;

/// <summary>
/// Represents a monetary amount in a specific currency.
/// Uses decimal (never double) for precision.
/// </summary>
public sealed class Money : IEquatable<Money>
{
    public const decimal MaxAmount = 1_000_000_000.00m; // 1 billion
    public const int DecimalPlaces = 2;

    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        if (amount > MaxAmount)
            throw new ArgumentException($"Amount cannot exceed {MaxAmount}.", nameof(amount));

        // Validate decimal places
        var scale = GetDecimalPlaces(amount);
        if (scale > DecimalPlaces)
            throw new ArgumentException(
                $"Amount may have at most {DecimalPlaces} decimal places.",
                nameof(amount));

        Amount = amount;
    }

    /// <summary>
    /// Round to 2 decimal places using banker's rounding (round-half-to-even).
    /// </summary>
    public static Money Round(decimal value)
    {
        var rounded = decimal.Round(value, DecimalPlaces, MidpointRounding.ToEven);
        return new Money(rounded);
    }

    /// <summary>
    /// Create Money from quantity and unit price (applies rounding).
    /// </summary>
    public static Money FromLineCalculation(int quantity, Money unitPrice)
    {
        ArgumentNullException.ThrowIfNull(unitPrice);
        var result = quantity * unitPrice.Amount;
        return Round(result);
    }

    /// <summary>
    /// Sum a collection of Money values.
    /// </summary>
    public static Money Sum(params Money[] amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        var total = amounts.Aggregate(0m, (acc, m) => acc + m.Amount);
        return new Money(total);
    }

    public override bool Equals(object? obj) => Equals(obj as Money);

    public bool Equals(Money? other) =>
        other is not null && Amount == other.Amount;

    public override int GetHashCode() => Amount.GetHashCode();

    public override string ToString() => Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    private static int GetDecimalPlaces(decimal value)
    {
        if (value == 0) return 0;

        var scale = decimal.GetBits(value)[3] >> 16 & 0xFF;
        return scale;
    }
}
