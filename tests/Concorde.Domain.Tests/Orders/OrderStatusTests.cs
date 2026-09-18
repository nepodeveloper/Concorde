namespace Concorde.Domain.Tests.Orders;

using Concorde.Domain.Orders;
using Xunit;

public class OrderStatusTransitionPolicyTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Fulfilled, false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Fulfilled, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Fulfilled, false)]
    public void IsTransitionAllowed_ReturnsExpectedResult(
        OrderStatus from, OrderStatus to, bool expected)
    {
        var result = OrderStatusTransitionPolicy.IsTransitionAllowed(from, to);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Fulfilled, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Cancelled, false)]
    public void IsTransitionAllowed_WithSameStatus_ReturnsFalse(
        OrderStatus status, OrderStatus sameStatus, bool _)
    {
        var result = OrderStatusTransitionPolicy.IsTransitionAllowed(status, sameStatus);
        Assert.False(result);
    }

    [Fact]
    public void GetValidTransitions_FromPending_ReturnsTwoOptions()
    {
        var validTransitions = OrderStatusTransitionPolicy.GetValidTransitions(OrderStatus.Pending);
        
        Assert.Equal(2, validTransitions.Count);
        Assert.Contains(OrderStatus.Confirmed, validTransitions);
        Assert.Contains(OrderStatus.Cancelled, validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromConfirmed_ReturnsTwoOptions()
    {
        var validTransitions = OrderStatusTransitionPolicy.GetValidTransitions(OrderStatus.Confirmed);
        
        Assert.Equal(2, validTransitions.Count);
        Assert.Contains(OrderStatus.Fulfilled, validTransitions);
        Assert.Contains(OrderStatus.Cancelled, validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromFulfilled_ReturnsEmpty()
    {
        var validTransitions = OrderStatusTransitionPolicy.GetValidTransitions(OrderStatus.Fulfilled);
        
        Assert.Empty(validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromCancelled_ReturnsEmpty()
    {
        var validTransitions = OrderStatusTransitionPolicy.GetValidTransitions(OrderStatus.Cancelled);
        
        Assert.Empty(validTransitions);
    }
}
