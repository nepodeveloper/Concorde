namespace Concorde.Application.Orders.Get;

using Concorde.Application.Abstractions;
using Concorde.Application.Common;

public sealed record GetOrderQuery(Guid OrderId);

public class GetOrderHandler
{
    private readonly IOrderRepository _repository;

    public GetOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrderDto> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(query.OrderId, cancellationToken)
            ?? throw new OrderNotFoundException(query.OrderId);

        return OrderDto.FromDomain(order);
    }
}
