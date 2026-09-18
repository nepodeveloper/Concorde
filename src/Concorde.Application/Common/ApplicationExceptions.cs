namespace Concorde.Application.Common;

/// <summary>
/// Thrown when an order does not exist. Maps to 404 ORDER_NOT_FOUND.
/// </summary>
public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(Guid orderId)
        : base($"Order '{orderId}' was not found.")
    {
        OrderId = orderId;
    }

    public Guid OrderId { get; }
}

/// <summary>
/// Thrown on a disallowed status transition. Maps to 409 INVALID_STATUS_TRANSITION.
/// </summary>
public class InvalidStatusTransitionException : Exception
{
    public InvalidStatusTransitionException(string fromStatus, string toStatus)
        : base($"Order cannot transition from {fromStatus} to {toStatus}.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }

    public string FromStatus { get; }
    public string ToStatus { get; }
}
