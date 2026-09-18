namespace Concorde.Domain.Orders;

/// <summary>
/// Represents the lifecycle status of an order.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order has been received but not confirmed.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Order has been confirmed and is in progress.
    /// </summary>
    Confirmed = 2,

    /// <summary>
    /// Order has been fulfilled (terminal state).
    /// </summary>
    Fulfilled = 3,

    /// <summary>
    /// Order has been cancelled (terminal state).
    /// </summary>
    Cancelled = 4,
}

/// <summary>
/// Enforces valid status transitions in the order lifecycle.
/// </summary>
public static class OrderStatusTransitionPolicy
{
    /// <summary>
    /// Determines if a transition from 'from' to 'to' is allowed.
    /// </summary>
    public static bool IsTransitionAllowed(OrderStatus from, OrderStatus to)
    {
        // Same status is never allowed
        if (from == to)
            return false;

        // Terminal states cannot transition anywhere
        if (from == OrderStatus.Fulfilled || from == OrderStatus.Cancelled)
            return false;

        return from switch
        {
            OrderStatus.Pending => to is OrderStatus.Confirmed or OrderStatus.Cancelled,
            OrderStatus.Confirmed => to is OrderStatus.Fulfilled or OrderStatus.Cancelled,
            _ => false,
        };
    }

    /// <summary>
    /// Get the valid next states for a given current state.
    /// </summary>
    public static IReadOnlyList<OrderStatus> GetValidTransitions(OrderStatus currentStatus)
    {
        return currentStatus switch
        {
            OrderStatus.Pending => new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
            OrderStatus.Confirmed => new[] { OrderStatus.Fulfilled, OrderStatus.Cancelled },
            OrderStatus.Fulfilled => Array.Empty<OrderStatus>(),
            OrderStatus.Cancelled => Array.Empty<OrderStatus>(),
            _ => Array.Empty<OrderStatus>(),
        };
    }
}
