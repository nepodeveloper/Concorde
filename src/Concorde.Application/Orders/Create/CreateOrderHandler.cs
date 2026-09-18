namespace Concorde.Application.Orders.Create;

using Concorde.Application.Abstractions;
using Concorde.Application.Validation;
using Concorde.Domain.Common;
using Concorde.Domain.Orders;

/// <summary>
/// Result of a create request. WasCreated distinguishes 201 (new) from
/// 200 (idempotent replay of an existing reference — FR-02.3).
/// </summary>
public sealed record CreateOrderResult(OrderDto Order, bool WasCreated);

public class CreateOrderHandler
{
    private readonly IOrderRepository _repository;

    public CreateOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<CreateOrderResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var errors = CreateOrderValidator.Validate(command);
        if (errors.Count > 0)
            throw new ValidationException(errors);

        // CC-01: uniqueness comparison is case-insensitive on the normalized reference.
        var normalizedReference = command.ExternalReference!.Trim().ToUpperInvariant();

        var existing = await _repository.GetByExternalReferenceAsync(normalizedReference, cancellationToken);
        if (existing is not null)
            return new CreateOrderResult(OrderDto.FromDomain(existing), WasCreated: false);

        var order = BuildOrder(command);

        try
        {
            await _repository.AddAsync(order, cancellationToken);
        }
        catch (UniqueReferenceViolationException)
        {
            // FR-02.6: lost the insert race — re-read and return the winner.
            var winner = await _repository.GetByExternalReferenceAsync(normalizedReference, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Unique constraint violated for '{normalizedReference}' but the existing order could not be read.");
            return new CreateOrderResult(OrderDto.FromDomain(winner), WasCreated: false);
        }

        return new CreateOrderResult(OrderDto.FromDomain(order), WasCreated: true);
    }

    private static Order BuildOrder(CreateOrderCommand command)
    {
        var currency = new Currency(command.Currency!);
        var lines = command.Lines!
            .Select(l => new OrderLine(
                l.Sku!,
                l.Name!,
                (int)l.Quantity,
                new Money(l.UnitPrice)))
            .ToList();

        return new Order(
            command.ExternalReference!,
            command.CustomerName!,
            command.CustomerCode,
            currency,
            lines,
            command.Notes);
    }
}
