namespace Concorde.IntegrationTests;

using Concorde.Application.Abstractions;
using Concorde.Domain.Common;
using Concorde.Domain.Orders;
using Concorde.Infrastructure.Persistence;
using Xunit;

public class OrderRepositoryTests : IDisposable
{
    private readonly SqliteDatabaseFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static Order NewOrder(string reference, params (string Sku, int Qty, decimal Price)[] lines)
    {
        var orderLines = (lines.Length == 0 ? new[] { ("SKU-A", 2, 100.00m) } : lines)
            .Select(l => new OrderLine(l.Item1, $"Product {l.Item1}", l.Item2, new Money(l.Item3)))
            .ToList();

        return new Order(reference, "Acme Corp", "ACME-001", new Currency("ZAR"), orderLines, "notes");
    }

    [Fact]
    public async Task AddAsync_PersistsOrderAndLines()
    {
        var order = NewOrder("PO-10001", ("SKU-A", 2, 100.00m), ("SKU-B", 3, 50.00m));

        using (var context = _fixture.CreateContext())
        {
            await new OrderRepository(context).AddAsync(order);
        }

        // Re-read with a fresh context to prove real persistence.
        using (var context = _fixture.CreateContext())
        {
            var loaded = await new OrderRepository(context).GetByIdAsync(order.Id);

            Assert.NotNull(loaded);
            Assert.Equal("PO-10001", loaded.ExternalReference);
            Assert.Equal("Acme Corp", loaded.CustomerName);
            Assert.Equal("ZAR", loaded.Currency.Code);
            Assert.Equal(OrderStatus.Pending, loaded.Status);
            Assert.Equal(2, loaded.Lines.Count);
            Assert.Equal(350.00m, loaded.Total.Amount);
            Assert.Equal(200.00m, loaded.Lines.Single(l => l.Sku == "SKU-A").LineTotal.Amount);
        }
    }

    [Fact]
    public async Task AddAsync_WithDuplicateReference_ThrowsUniqueViolation()
    {
        using var context = _fixture.CreateContext();
        var repository = new OrderRepository(context);

        await repository.AddAsync(NewOrder("PO-10001"));

        await Assert.ThrowsAsync<UniqueReferenceViolationException>(() =>
            repository.AddAsync(NewOrder("PO-10001")));
    }

    [Fact]
    public async Task AddAsync_WithDuplicateReferenceDifferentCase_ThrowsUniqueViolation()
    {
        using var context = _fixture.CreateContext();
        var repository = new OrderRepository(context);

        await repository.AddAsync(NewOrder("PO-10001"));

        // Domain normalizes to upper-case, so the unique index catches this too.
        await Assert.ThrowsAsync<UniqueReferenceViolationException>(() =>
            repository.AddAsync(NewOrder("po-10001")));
    }

    [Fact]
    public async Task AddAsync_AfterUniqueViolation_ContextRemainsUsable()
    {
        using var context = _fixture.CreateContext();
        var repository = new OrderRepository(context);

        await repository.AddAsync(NewOrder("PO-10001"));
        await Assert.ThrowsAsync<UniqueReferenceViolationException>(() =>
            repository.AddAsync(NewOrder("PO-10001")));

        // FR-02.6: after the violation the existing order must be readable.
        var existing = await repository.GetByExternalReferenceAsync("PO-10001");
        Assert.NotNull(existing);
    }

    [Fact]
    public async Task GetByExternalReferenceAsync_WithUnknownReference_ReturnsNull()
    {
        using var context = _fixture.CreateContext();

        var result = await new OrderRepository(context).GetByExternalReferenceAsync("PO-UNKNOWN");

        Assert.Null(result);
    }

    [Fact]
    public async Task ListAsync_ReturnsNewestFirst()
    {
        using var context = _fixture.CreateContext();
        var repository = new OrderRepository(context);

        for (var i = 0; i < 3; i++)
        {
            await repository.AddAsync(NewOrder($"PO-{i}"));
            await Task.Delay(15); // distinct CreatedAtUtc values
        }

        var (items, _) = await repository.ListAsync(1, 10, null);

        var timestamps = items.Select(o => o.CreatedAtUtc).ToList();
        Assert.Equal(timestamps.OrderByDescending(t => t), timestamps);
        Assert.Equal("PO-2", items[0].ExternalReference);
    }

    [Fact]
    public async Task ListAsync_RespectsPagination()
    {
        using var context = _fixture.CreateContext();
        var repository = new OrderRepository(context);

        for (var i = 0; i < 25; i++)
            await repository.AddAsync(NewOrder($"PO-{i:D3}"));

        var (page1, totalCount) = await repository.ListAsync(1, 10, null);
        var (page2, _) = await repository.ListAsync(2, 10, null);
        var (page3, _) = await repository.ListAsync(3, 10, null);

        Assert.Equal(25, totalCount);
        Assert.Equal(10, page1.Count);
        Assert.Equal(10, page2.Count);
        Assert.Equal(5, page3.Count);
        Assert.Empty(page1.Select(o => o.Id).Intersect(page2.Select(o => o.Id)));
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus()
    {
        using var context = _fixture.CreateContext();
        var repository = new OrderRepository(context);

        var order = NewOrder("PO-CONFIRM");
        await repository.AddAsync(order);
        await repository.AddAsync(NewOrder("PO-PENDING"));

        order.ChangeStatus(OrderStatus.Confirmed);
        await repository.UpdateAsync(order);

        var (confirmed, confirmedCount) = await repository.ListAsync(1, 10, OrderStatus.Confirmed);
        var (pending, pendingCount) = await repository.ListAsync(1, 10, OrderStatus.Pending);

        Assert.Equal(1, confirmedCount);
        Assert.Equal("PO-CONFIRM", confirmed[0].ExternalReference);
        Assert.Equal(1, pendingCount);
        Assert.Equal("PO-PENDING", pending[0].ExternalReference);
    }

    [Fact]
    public async Task UpdateAsync_PersistsStatusChange()
    {
        var order = NewOrder("PO-10001");

        using (var context = _fixture.CreateContext())
        {
            await new OrderRepository(context).AddAsync(order);
        }

        using (var context = _fixture.CreateContext())
        {
            var repository = new OrderRepository(context);
            var loaded = await repository.GetByIdAsync(order.Id);
            loaded!.ChangeStatus(OrderStatus.Confirmed);
            await repository.UpdateAsync(loaded);
        }

        using (var context = _fixture.CreateContext())
        {
            var reloaded = await new OrderRepository(context).GetByIdAsync(order.Id);
            Assert.Equal(OrderStatus.Confirmed, reloaded!.Status);
        }
    }

    [Fact]
    public async Task InvalidTransition_DoesNotAlterPersistedOrder()
    {
        var order = NewOrder("PO-10001");

        using (var context = _fixture.CreateContext())
        {
            await new OrderRepository(context).AddAsync(order);
        }

        using (var context = _fixture.CreateContext())
        {
            var loaded = await new OrderRepository(context).GetByIdAsync(order.Id);
            Assert.Throws<InvalidOperationException>(() => loaded!.ChangeStatus(OrderStatus.Fulfilled));
            // No SaveChanges — nothing persisted.
        }

        using (var context = _fixture.CreateContext())
        {
            var reloaded = await new OrderRepository(context).GetByIdAsync(order.Id);
            Assert.Equal(OrderStatus.Pending, reloaded!.Status);
        }
    }
}
