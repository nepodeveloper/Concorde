namespace Concorde.Application.Orders.ChangeStatus;

using Concorde.Application.Abstractions;
using Concorde.Application.Common;
using Concorde.Application.Validation;
using Concorde.Domain.Orders;

public sealed record ChangeOrderStatusCommand(Guid OrderId, string? NewStatus, string? Reason = null);

public class ChangeOrderStatusHandler
{
    private readonly IOrderRepository _repository;

    public ChangeOrderStatusHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrderDto> HandleAsync(
        ChangeOrderStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        // FR-07.1: unknown status values are a 400, not a 409.
        if (string.IsNullOrWhiteSpace(command.NewStatus)
            || !Enum.TryParse<OrderStatus>(command.NewStatus, ignoreCase: true, out var newStatus)
            || !Enum.IsDefined(newStatus))
        {
            throw new ValidationException(new[]
            {
                new ValidationError("status", ValidationErrorCodes.InvalidStatus,
                    $"'{command.NewStatus}' is not a valid order status."),
            });
        }

        var order = await _repository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new OrderNotFoundException(command.OrderId);

        if (!OrderStatusTransitionPolicy.IsTransitionAllowed(order.Status, newStatus))
            throw new InvalidStatusTransitionException(order.Status.ToString(), newStatus.ToString());

        var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();

        if (reason is { Length: > Order.MaxStatusReasonLength })
        {
            throw new ValidationException(new[]
            {
                new ValidationError("reason", ValidationErrorCodes.TooLong,
                    $"Reason may not exceed {Order.MaxStatusReasonLength} characters."),
            });
        }

        // Cancelling a fulfilled order must be justified.
        if (order.Status == OrderStatus.Fulfilled && newStatus == OrderStatus.Cancelled && reason is null)
        {
            throw new ValidationException(new[]
            {
                new ValidationError("reason", ValidationErrorCodes.Required,
                    "A reason is required when cancelling a fulfilled order."),
            });
        }

        order.ChangeStatus(newStatus, reason);
        await _repository.UpdateAsync(order, cancellationToken);

        return OrderDto.FromDomain(order);
    }
}
