namespace Concorde.Application.Tests;

using Concorde.Application.Abstractions;
using Concorde.Domain.Orders;

/// <summary>
/// In-memory test double mirroring the real repository's contract,
/// including the unique-reference constraint.
/// </summary>
public class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = new();
    private readonly object _lock = new();

    /// <summary>When set, the next AddAsync throws to simulate an insert race.</summary>
    public bool SimulateUniqueViolationOnNextAdd { get; set; }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult(_orders.GetValueOrDefault(id));
    }

    public Task<Order?> GetByExternalReferenceAsync(string normalizedReference, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult(_orders.Values.FirstOrDefault(o =>
                o.ExternalReference.Equals(normalizedReference, StringComparison.OrdinalIgnoreCase)));
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (SimulateUniqueViolationOnNextAdd)
            {
                SimulateUniqueViolationOnNextAdd = false;
                throw new UniqueReferenceViolationException(order.ExternalReference);
            }

            if (_orders.Values.Any(o =>
                    o.ExternalReference.Equals(order.ExternalReference, StringComparison.OrdinalIgnoreCase)))
                throw new UniqueReferenceViolationException(order.ExternalReference);

            _orders[order.Id] = order;
            return Task.CompletedTask;
        }
    }

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _orders[order.Id] = order;
            return Task.CompletedTask;
        }
    }

    public Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(
        int page, int pageSize, OrderStatus? status, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var filtered = _orders.Values
                .Where(o => status is null || o.Status == status)
                .OrderByDescending(o => o.CreatedAtUtc)
                .ThenByDescending(o => o.Id)
                .ToList();

            var items = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(((IReadOnlyList<Order>)items, filtered.Count));
        }
    }

    /// <summary>Seed the repository directly (bypasses uniqueness simulation flags).</summary>
    public void Seed(Order order)
    {
        lock (_lock)
            _orders[order.Id] = order;
    }
}
