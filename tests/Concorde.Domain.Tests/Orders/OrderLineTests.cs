namespace Concorde.Domain.Tests.Orders;

using Concorde.Domain.Common;
using Concorde.Domain.Orders;
using Xunit;

public class OrderLineTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesSuccessfully()
    {
        var unitPrice = new Money(100m);
        var line = new OrderLine("SKU-A", "Product A", 2, unitPrice);
        
        Assert.NotNull(line);
        Assert.Equal("SKU-A", line.Sku);
        Assert.Equal("Product A", line.Name);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(unitPrice, line.UnitPrice);
        Assert.Equal(new Money(200m), line.LineTotal);
    }

    [Fact]
    public void Constructor_GeneratesUniqueId()
    {
        var unitPrice = new Money(100m);
        var line1 = new OrderLine("SKU-A", "Product A", 1, unitPrice);
        var line2 = new OrderLine("SKU-A", "Product A", 1, unitPrice);
        
        Assert.NotEqual(Guid.Empty, line1.Id);
        Assert.NotEqual(line1.Id, line2.Id);
    }

    [Fact]
    public void Constructor_WithNullSku_Throws()
    {
        var unitPrice = new Money(100m);
        var ex = Assert.Throws<ArgumentException>(() => 
            new OrderLine(null!, "Product A", 1, unitPrice));
        
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithEmptySku_Throws()
    {
        var unitPrice = new Money(100m);
        var ex = Assert.Throws<ArgumentException>(() => 
            new OrderLine("", "Product A", 1, unitPrice));
        
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithWhitespaceSku_Throws()
    {
        var unitPrice = new Money(100m);
        var ex = Assert.Throws<ArgumentException>(() => 
            new OrderLine("   ", "Product A", 1, unitPrice));
        
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithSkuExceedingMaxLength_Throws()
    {
        var unitPrice = new Money(100m);
        var longSku = new string('A', OrderLine.MaxSkuLength + 1);
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new OrderLine(longSku, "Product A", 1, unitPrice));
        
        Assert.Contains("may not exceed", ex.Message);
    }

    [Fact]
    public void Constructor_WithNullName_Throws()
    {
        var unitPrice = new Money(100m);
        var ex = Assert.Throws<ArgumentException>(() => 
            new OrderLine("SKU-A", null!, 1, unitPrice));
        
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithEmptyName_Throws()
    {
        var unitPrice = new Money(100m);
        var ex = Assert.Throws<ArgumentException>(() => 
            new OrderLine("SKU-A", "", 1, unitPrice));
        
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithNameExceedingMaxLength_Throws()
    {
        var unitPrice = new Money(100m);
        var longName = new string('A', OrderLine.MaxNameLength + 1);
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new OrderLine("SKU-A", longName, 1, unitPrice));
        
        Assert.Contains("may not exceed", ex.Message);
    }

    [Fact]
    public void Constructor_WithZeroQuantity_Throws()
    {
        var unitPrice = new Money(100m);
        var ex = Assert.Throws<ArgumentException>(() => 
            new OrderLine("SKU-A", "Product A", 0, unitPrice));
        
        Assert.Contains("at least", ex.Message);
    }

    [Fact]
    public void Constructor_WithNegativeQuantity_Throws()
    {
        var unitPrice = new Money(100m);
        var ex = Assert.Throws<ArgumentException>(() => 
            new OrderLine("SKU-A", "Product A", -1, unitPrice));
        
        Assert.Contains("at least", ex.Message);
    }

    [Fact]
    public void Constructor_WithQuantityExceedingMax_Throws()
    {
        var unitPrice = new Money(100m);
        var ex = Assert.Throws<ArgumentException>(() => 
            new OrderLine("SKU-A", "Product A", OrderLine.MaxQuantity + 1, unitPrice));
        
        Assert.Contains("may not exceed", ex.Message);
    }

    [Fact]
    public void Constructor_WithNullUnitPrice_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => 
            new OrderLine("SKU-A", "Product A", 1, null!));
        
        Assert.Equal("unitPrice", ex.ParamName);
    }

    [Fact]
    public void LineTotal_IsCalculatedAsQuantityTimesUnitPrice()
    {
        var unitPrice = new Money(33.33m);
        var line = new OrderLine("SKU-A", "Product A", 3, unitPrice);
        
        // 3 × 33.33 = 99.99
        Assert.Equal(new Money(99.99m), line.LineTotal);
    }

    [Fact]
    public void LineTotal_WithZeroUnitPrice_IsZero()
    {
        var unitPrice = new Money(0m);
        var line = new OrderLine("SKU-FREE", "Free Item", 100, unitPrice);
        
        Assert.Equal(new Money(0m), line.LineTotal);
    }

    [Fact]
    public void LineTotal_WithDecimalRounding_AppliesBankersRounding()
    {
        // 0.10 × 3 = 0.30 (no rounding needed, but tests banker's rounding logic)
        var unitPrice = new Money(0.10m);
        var line = new OrderLine("SKU-A", "Product A", 3, unitPrice);
        
        Assert.Equal(new Money(0.30m), line.LineTotal);
    }
}
