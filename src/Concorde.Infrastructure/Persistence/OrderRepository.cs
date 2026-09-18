namespace Concorde.Infrastructure.Persistence;

using Concorde.Application.Abstractions;
using Concorde.Domain.Orders;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public class OrderRepository : IOrderRepository
{
    private readonly ConcordeDbContext _db;

    public OrderRepository(ConcordeDbContext db)
    {
        _db = db;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Order?> GetByExternalReferenceAsync(string normalizedReference, CancellationToken cancellationToken = default) =>
        // The domain persists the reference upper-cased, so equality on the
        // normalized input is a case-insensitive match (CC-01).
        _db.Orders.FirstOrDefaultAsync(o => o.ExternalReference == normalizedReference, cancellationToken);

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        _db.Orders.Add(order);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Detach the failed aggregate so the context stays usable for the re-read.
            _db.ChangeTracker.Clear();
            throw new UniqueReferenceViolationException(order.ExternalReference);
        }
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (_db.Entry(order).State == EntityState.Detached)
            _db.Orders.Update(order);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        OrderStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Orders.AsNoTracking();

        if (status is not null)
            query = query.Where(o => o.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);

        // FR-05.2: newest first, Id as deterministic tie-breaker.
        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 }; // SQLITE_CONSTRAINT
}
