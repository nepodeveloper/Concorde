namespace Concorde.Application.Orders.Update;

using Concorde.Application.Abstractions;
using Concorde.Application.Common;
using Concorde.Application.Orders.Create;
using Concorde.Application.Validation;
using Concorde.Domain.Common;
using Concorde.Domain.Orders;

/// <summary>
/// Request to amend an existing order's details and lines.
/// The external reference is immutable and not part of this contract.
/// </summary>
public sealed record UpdateOrderCommand(
    Guid OrderId,
    string? CustomerName,
    string? CustomerCode,
    string? Currency,
    string? Notes,
    IReadOnlyList<CreateOrderLine>? Lines);

public class UpdateOrderHandler
{
    private readonly IOrderRepository _repository;

    public UpdateOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrderDto> HandleAsync(
        UpdateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        // Reuse the create-order rules; the reference placeholder is never persisted.
        var errors = CreateOrderValidator
            .Validate(new CreateOrderCommand(
                "UPDATE-PLACEHOLDER",
                command.CustomerName,
                command.CustomerCode,
                command.Currency,
                command.Notes,
                command.Lines))
            .Where(e => e.Field != "externalReference")
            .ToList();

        if (errors.Count > 0)
            throw new ValidationException(errors);

        var order = await _repository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new OrderNotFoundException(command.OrderId);

        var lines = command.Lines!
            .Select(l => new OrderLine(l.Sku!, l.Name!, (int)l.Quantity, new Money(l.UnitPrice)))
            .ToList();

        try
        {
            order.Amend(
                command.CustomerName!,
                command.CustomerCode,
                new Currency(command.Currency!),
                lines,
                command.Notes);
        }
        catch (InvalidOperationException ex)
        {
            throw new OrderNotEditableException(order.Status.ToString(), ex.Message);
        }

        await _repository.UpdateAsync(order, cancellationToken);
        return OrderDto.FromDomain(order);
    }
}
