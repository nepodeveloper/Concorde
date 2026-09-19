namespace Concorde.Domain.Tests.Orders;

using Concorde.Domain.Common;
using Concorde.Domain.Orders;
using Xunit;

public class OrderTests
{
    private Money CreatePrice(decimal amount) => new(amount);
    
    private OrderLine CreateLine(string sku, string name, int qty, decimal price) =>
        new(sku, name, qty, CreatePrice(price));

    [Fact]
    public void Constructor_WithValidData_CreatesSuccessfully()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 2, 100m) };
        var currency = new Currency("ZAR");
        
        var order = new Order("PO-001", "Customer Inc", "CUST-001", currency, lines);
        
        Assert.NotNull(order);
        Assert.Equal("PO-001".ToUpperInvariant(), order.ExternalReference);
        Assert.Equal("Customer Inc", order.CustomerName);
        Assert.Equal("CUST-001", order.CustomerCode);
        Assert.Equal(currency, order.Currency);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Single(order.Lines);
    }

    [Fact]
    public void Constructor_GeneratesUniqueId()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        
        var order1 = new Order("PO-001", "Customer", "CUST", currency, lines);
        var order2 = new Order("PO-002", "Customer", "CUST", currency, lines);
        
        Assert.NotEqual(Guid.Empty, order1.Id);
        Assert.NotEqual(order1.Id, order2.Id);
    }

    [Fact]
    public void Constructor_StartsWithPendingStatus()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        
        var order = new Order("PO-001", "Customer", null, currency, lines);
        
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void Constructor_RecordsCreatedTimestampInUtc()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        var beforeCreation = DateTime.UtcNow;
        
        var order = new Order("PO-001", "Customer", null, currency, lines);
        
        var afterCreation = DateTime.UtcNow;
        Assert.InRange(order.CreatedAtUtc, beforeCreation.AddSeconds(-1), afterCreation.AddSeconds(1));
    }

    [Fact]
    public void Constructor_NormalizesExternalReferenceToUppercase()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        
        var order = new Order("po-001", "Customer", null, currency, lines);
        
        Assert.Equal("PO-001", order.ExternalReference);
    }

    [Fact]
    public void Constructor_NormalizesExternalReferenceTrimWhitespace()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        
        var order = new Order("  PO-001  ", "Customer", null, currency, lines);
        
        Assert.Equal("PO-001", order.ExternalReference);
    }

    [Fact]
    public void Constructor_WithNullExternalReference_Throws()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new Order(null!, "Customer", null, currency, lines));
        
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithEmptyExternalReference_Throws()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new Order("", "Customer", null, currency, lines));
        
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithExternalReferenceExceedingMaxLength_Throws()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        var longRef = new string('A', Order.MaxExternalReferenceLength + 1);
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new Order(longRef, "Customer", null, currency, lines));
        
        Assert.Contains("may not exceed", ex.Message);
    }

    [Fact]
    public void Constructor_WithInvalidExternalReferenceFormat_Throws()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        
        // Must match [A-Za-z0-9._-]+
        var ex = Assert.Throws<ArgumentException>(() => 
            new Order("PO@001", "Customer", null, currency, lines));
        
        Assert.Contains("alphanumeric", ex.Message);
    }

    [Fact]
    public void Constructor_WithNullCustomerName_Throws()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new Order("PO-001", null!, null, currency, lines));
        
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithEmptyCustomerName_Throws()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new Order("PO-001", "", null, currency, lines));
        
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithCustomerNameExceedingMaxLength_Throws()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        var longName = new string('A', Order.MaxCustomerNameLength + 1);
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new Order("PO-001", longName, null, currency, lines));
        
        Assert.Contains("may not exceed", ex.Message);
    }

    [Fact]
    public void Constructor_WithNullCurrency_Throws()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        
        var ex = Assert.Throws<ArgumentNullException>(() => 
            new Order("PO-001", "Customer", null, null!, lines));
        
        Assert.Equal("currency", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNoLineItems_Throws()
    {
        var currency = new Currency("ZAR");
        var lines = Array.Empty<OrderLine>();
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new Order("PO-001", "Customer", null, currency, lines));
        
        Assert.Contains("At least one line", ex.Message);
    }

    [Fact]
    public void Constructor_WithMoreThan100Lines_Throws()
    {
        var currency = new Currency("ZAR");
        var lines = Enumerable.Range(0, Order.MaxLineItems + 1)
            .Select(i => CreateLine($"SKU-{i}", $"Product {i}", 1, 100m))
            .ToArray();
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new Order("PO-001", "Customer", null, currency, lines));
        
        Assert.Contains("may not contain more than", ex.Message);
    }

    [Fact]
    public void Constructor_CalculatesSubtotalFromLineItems()
    {
        var lines = new[]
        {
            CreateLine("SKU-A", "Product A", 2, 100m),  // 200
            CreateLine("SKU-B", "Product B", 3, 50m),   // 150
        };
        var currency = new Currency("ZAR");
        
        var order = new Order("PO-001", "Customer", null, currency, lines);
        
        Assert.Equal(new Money(350m), order.Subtotal);
    }

    [Fact]
    public void Constructor_SetsTotalEqualToSubtotalInMvp()
    {
        var lines = new[]
        {
            CreateLine("SKU-A", "Product A", 2, 100m),
            CreateLine("SKU-B", "Product B", 3, 50m),
        };
        var currency = new Currency("ZAR");
        
        var order = new Order("PO-001", "Customer", null, currency, lines);
        
        Assert.Equal(order.Subtotal, order.Total);
    }

    [Fact]
    public void ChangeStatus_FromPendingToConfirmed_Succeeds()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        var order = new Order("PO-001", "Customer", null, currency, lines);
        
        order.ChangeStatus(OrderStatus.Confirmed);
        
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void ChangeStatus_FromPendingToCancelled_Succeeds()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        var order = new Order("PO-001", "Customer", null, currency, lines);
        
        order.ChangeStatus(OrderStatus.Cancelled);
        
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void ChangeStatus_FromConfirmedToFulfilled_Succeeds()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        var order = new Order("PO-001", "Customer", null, currency, lines);
        order.ChangeStatus(OrderStatus.Confirmed);
        
        order.ChangeStatus(OrderStatus.Fulfilled);
        
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
    }

    [Fact]
    public void ChangeStatus_ToSameStatus_Throws()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        var order = new Order("PO-001", "Customer", null, currency, lines);
        
        var ex = Assert.Throws<InvalidOperationException>(() => 
            order.ChangeStatus(OrderStatus.Pending));
        
        Assert.Contains("cannot transition", ex.Message);
    }

    [Fact]
    public void ChangeStatus_FromFulfilledToAnyState_Throws()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        var order = new Order("PO-001", "Customer", null, currency, lines);
        order.ChangeStatus(OrderStatus.Confirmed);
        order.ChangeStatus(OrderStatus.Fulfilled);
        
        var ex = Assert.Throws<InvalidOperationException>(() => 
            order.ChangeStatus(OrderStatus.Pending));
        
        Assert.Contains("cannot transition", ex.Message);
    }

    [Fact]
    public void ChangeStatus_UpdatesLastUpdatedTimestamp()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var currency = new Currency("ZAR");
        var order = new Order("PO-001", "Customer", null, currency, lines);
        var originalUpdatedAt = order.UpdatedAtUtc;
        
        System.Threading.Thread.Sleep(10); // Small delay
        order.ChangeStatus(OrderStatus.Confirmed);
        
        Assert.NotEqual(originalUpdatedAt, order.UpdatedAtUtc);
        Assert.True(order.UpdatedAtUtc > originalUpdatedAt);
    }

    [Fact]
    public void ChangeStatus_CancellingFulfilledOrderWithoutReason_Throws()
    {
        var order = CreateFulfilledOrder();

        var ex = Assert.Throws<ArgumentException>(() =>
            order.ChangeStatus(OrderStatus.Cancelled));

        Assert.Contains("reason is required", ex.Message);
    }

    [Fact]
    public void ChangeStatus_CancellingFulfilledOrderWithReason_SetsStatusReason()
    {
        var order = CreateFulfilledOrder();

        order.ChangeStatus(OrderStatus.Cancelled, "Customer requested refund");

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Customer requested refund", order.StatusReason);
    }

    [Fact]
    public void ChangeStatus_WithOverlongReason_Throws()
    {
        var order = CreateFulfilledOrder();
        var reason = new string('x', Order.MaxStatusReasonLength + 1);

        Assert.Throws<ArgumentException>(() =>
            order.ChangeStatus(OrderStatus.Cancelled, reason));
    }

    [Fact]
    public void Amend_WhilePending_UpdatesDetailsAndRecalculatesTotals()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var order = new Order("PO-001", "Customer", null, new Currency("ZAR"), lines);

        order.Amend(
            "New Customer",
            "NC-01",
            new Currency("USD"),
            new[] { CreateLine("SKU-B", "Product B", 3, 50m) },
            "amended");

        Assert.Equal("New Customer", order.CustomerName);
        Assert.Equal("NC-01", order.CustomerCode);
        Assert.Equal("USD", order.Currency.Code);
        Assert.Equal("amended", order.Notes);
        Assert.Single(order.Lines);
        Assert.Equal(150m, order.Total.Amount);
    }

    [Fact]
    public void Amend_WhileConfirmed_IsAllowed()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var order = new Order("PO-001", "Customer", null, new Currency("ZAR"), lines);
        order.ChangeStatus(OrderStatus.Confirmed);

        order.Amend("New Customer", null, new Currency("ZAR"),
            new[] { CreateLine("SKU-B", "Product B", 2, 25m) });

        Assert.Equal("New Customer", order.CustomerName);
        Assert.Equal(50m, order.Total.Amount);
    }

    [Fact]
    public void Amend_WhenFulfilledOrCancelled_Throws()
    {
        var order = CreateFulfilledOrder();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            order.Amend("New Customer", null, new Currency("ZAR"),
                new[] { CreateLine("SKU-A", "Product A", 1, 100m) }));

        Assert.Contains("no longer be edited", ex.Message);
    }

    private Order CreateFulfilledOrder()
    {
        var lines = new[] { CreateLine("SKU-A", "Product A", 1, 100m) };
        var order = new Order("PO-001", "Customer", null, new Currency("ZAR"), lines);
        order.ChangeStatus(OrderStatus.Confirmed);
        order.ChangeStatus(OrderStatus.Fulfilled);
        return order;
    }
}
