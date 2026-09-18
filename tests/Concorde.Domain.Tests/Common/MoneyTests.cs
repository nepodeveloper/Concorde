namespace Concorde.Domain.Tests.Common;

using Concorde.Domain.Common;
using Xunit;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithValidAmount_CreatesSuccessfully()
    {
        var amount = 100.50m;
        var money = new Money(amount);
        Assert.Equal(amount, money.Amount);
    }

    [Fact]
    public void Constructor_WithZeroAmount_Succeeds()
    {
        var money = new Money(0m);
        Assert.Equal(0m, money.Amount);
    }

    [Fact]
    public void Constructor_WithNegativeAmount_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Money(-1m));
        Assert.Contains("cannot be negative", ex.Message);
    }

    [Fact]
    public void Constructor_WithAmountExceedingMax_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => 
            new Money(Money.MaxAmount + 0.01m));
        Assert.Contains("cannot exceed", ex.Message);
    }

    [Fact]
    public void Constructor_WithTooManyDecimalPlaces_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Money(10.999m));
        Assert.Contains("2 decimal places", ex.Message);
    }

    [Theory]
    [InlineData(10.00)]
    [InlineData(10.1)]
    [InlineData(10.12)]
    public void Constructor_WithValidDecimalPlaces_Succeeds(decimal amount)
    {
        var money = new Money(amount);
        Assert.NotNull(money);
    }

    [Fact]
    public void Round_WithThreeDecimalPlaces_RoundsCorrectly()
    {
        // 0.105 should round to 0.10 using banker's rounding (round-half-to-even)
        var result = Money.Round(0.105m);
        Assert.Equal(0.10m, result.Amount);
    }

    [Fact]
    public void Round_WithMoreDecimalPlaces_RoundsCorrectly()
    {
        var result = Money.Round(0.115m);
        Assert.Equal(0.12m, result.Amount);
    }

    [Fact]
    public void FromLineCalculation_WithValidQuantityAndPrice_CalculatesCorrectly()
    {
        var unitPrice = new Money(10.50m);
        var result = Money.FromLineCalculation(3, unitPrice);
        
        Assert.Equal(31.50m, result.Amount);
    }

    [Fact]
    public void FromLineCalculation_WithZeroUnitPrice_ReturnsZero()
    {
        var unitPrice = new Money(0m);
        var result = Money.FromLineCalculation(100, unitPrice);
        
        Assert.Equal(0m, result.Amount);
    }

    [Fact]
    public void Sum_WithMultipleAmounts_ReturnsTotalSum()
    {
        var amounts = new[]
        {
            new Money(100.00m),
            new Money(50.50m),
            new Money(25.25m)
        };

        var result = Money.Sum(amounts);
        Assert.Equal(175.75m, result.Amount);
    }

    [Fact]
    public void Sum_WithEmptyArray_ReturnsZero()
    {
        var result = Money.Sum();
        Assert.Equal(0m, result.Amount);
    }

    [Fact]
    public void Equals_WithSameAmount_ReturnsTrue()
    {
        var money1 = new Money(100m);
        var money2 = new Money(100m);
        
        Assert.Equal(money1, money2);
    }

    [Fact]
    public void Equals_WithDifferentAmount_ReturnsFalse()
    {
        var money1 = new Money(100m);
        var money2 = new Money(101m);
        
        Assert.NotEqual(money1, money2);
    }

    [Fact]
    public void ToString_FormatsWithTwoDecimalPlaces()
    {
        var money = new Money(100.5m);
        Assert.Equal("100.50", money.ToString());
    }
}
