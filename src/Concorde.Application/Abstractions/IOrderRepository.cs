namespace Concorde.Application.Abstractions;

using Concorde.Domain.Orders;

/// <summary>
/// Persistence abstraction for the Order aggregate.
/// Implemented by the Infrastructure layer.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lookup by normalized (upper-cased) external reference. Case-insensitive per CC-01.
    /// </summary>
    Task<Order?> GetByExternalReferenceAsync(string normalizedReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new order. Throws <see cref="UniqueReferenceViolationException"/>
    /// if the unique index on the external reference is violated (race condition).
    /// </summary>
    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        OrderStatus? status,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Raised by the persistence layer when the unique external-reference index is violated.
/// The application handler catches this and re-reads the existing order (FR-02.6).
/// </summary>
public class UniqueReferenceViolationException : Exception
{
    public UniqueReferenceViolationException(string externalReference)
        : base($"An order with external reference '{externalReference}' already exists.")
    {
        ExternalReference = externalReference;
    }

    public string ExternalReference { get; }
}
