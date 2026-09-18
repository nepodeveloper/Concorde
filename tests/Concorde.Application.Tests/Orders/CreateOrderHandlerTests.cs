namespace Concorde.Application.Tests.Orders;

using Concorde.Application.Orders.Create;
using Concorde.Application.Validation;
using Xunit;

public class CreateOrderHandlerTests
{
    private readonly InMemoryOrderRepository _repository = new();
    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerTests()
    {
        _handler = new CreateOrderHandler(_repository);
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_CreatesOrder()
    {
        var result = await _handler.HandleAsync(TestData.ValidCommand());

        Assert.True(result.WasCreated);
        Assert.NotEqual(Guid.Empty, result.Order.Id);
        Assert.Equal("PO-10001", result.Order.ExternalReference);
    }

    [Fact]
    public async Task CreateOrder_SetsStatusToPending()
    {
        var result = await _handler.HandleAsync(TestData.ValidCommand());
        Assert.Equal("Pending", result.Order.Status);
    }

    [Fact]
    public async Task CreateOrder_CalculatesTotalsOnServer()
    {
        var result = await _handler.HandleAsync(TestData.ValidCommand());

        // 2 × 100.00 + 3 × 50.00 = 350.00
        Assert.Equal(350.00m, result.Order.Subtotal);
        Assert.Equal(350.00m, result.Order.Total);
        Assert.Equal(200.00m, result.Order.Lines[0].LineTotal);
        Assert.Equal(150.00m, result.Order.Lines[1].LineTotal);
    }

    [Fact]
    public async Task CreateOrder_WithExistingReference_DoesNotCreateDuplicate()
    {
        var first = await _handler.HandleAsync(TestData.ValidCommand());
        var second = await _handler.HandleAsync(TestData.ValidCommand());

        Assert.True(first.WasCreated);
        Assert.False(second.WasCreated);
        Assert.Equal(first.Order.Id, second.Order.Id);
    }

    [Fact]
    public async Task CreateOrder_WithExistingReferenceDifferentCase_DoesNotCreateDuplicate()
    {
        var first = await _handler.HandleAsync(TestData.ValidCommand("PO-10001"));
        var second = await _handler.HandleAsync(TestData.ValidCommand("po-10001"));

        Assert.False(second.WasCreated);
        Assert.Equal(first.Order.Id, second.Order.Id);
    }

    [Fact]
    public async Task CreateOrder_WhenInsertRaceLost_ReturnsExistingOrder()
    {
        // Seed the "winning" order, then force the unique violation path.
        var winner = await _handler.HandleAsync(TestData.ValidCommand("PO-RACE"));
        _repository.SimulateUniqueViolationOnNextAdd = true;

        // Bypass the pre-check by using a repository state where GetByExternalReference
        // initially misses: simulate by removing then re-adding is complex, so instead
        // verify the handler resolves the violation to the existing order.
        var handler = new CreateOrderHandler(new RaceSimulatingRepository(_repository));
        var result = await handler.HandleAsync(TestData.ValidCommand("PO-RACE"));

        Assert.False(result.WasCreated);
        Assert.Equal(winner.Order.Id, result.Order.Id);
    }

    [Fact]
    public async Task CreateOrder_ConcurrentRequests_ProducesSingleOrder()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _handler.HandleAsync(TestData.ValidCommand("PO-CONCURRENT")))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var ids = results.Select(r => r.Order.Id).Distinct().ToList();
        Assert.Single(ids);
        Assert.Equal(1, results.Count(r => r.WasCreated));
    }

    [Fact]
    public async Task CreateOrder_WithoutExternalReference_ThrowsValidation()
    {
        var command = TestData.ValidCommand() with { ExternalReference = null };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _handler.HandleAsync(command));
        Assert.Contains(ex.Errors, e => e.Field == "externalReference");
    }

    [Fact]
    public async Task CreateOrder_WithoutLineItems_ThrowsValidation()
    {
        var command = TestData.ValidCommand() with { Lines = Array.Empty<CreateOrderLine>() };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _handler.HandleAsync(command));
        Assert.Contains(ex.Errors, e =>
            e.Field == "lines" && e.Message == "At least one line item is required.");
    }

    [Fact]
    public async Task CreateOrder_WithInvalidRequest_DoesNotPersistOrder()
    {
        var command = TestData.ValidCommand() with { CustomerName = "" };

        await Assert.ThrowsAsync<ValidationException>(() => _handler.HandleAsync(command));

        var (items, totalCount) = await _repository.ListAsync(1, 10, null);
        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    /// <summary>
    /// Wraps the shared repository but reports "not found" on the pre-insert
    /// duplicate check, so the handler takes the unique-violation path.
    /// </summary>
    private class RaceSimulatingRepository : Concorde.Application.Abstractions.IOrderRepository
    {
        private readonly InMemoryOrderRepository _inner;
        private bool _firstLookup = true;

        public RaceSimulatingRepository(InMemoryOrderRepository inner) => _inner = inner;

        public Task<Domain.Orders.Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _inner.GetByIdAsync(id, ct);

        public Task<Domain.Orders.Order?> GetByExternalReferenceAsync(string reference, CancellationToken ct = default)
        {
            if (_firstLookup)
            {
                _firstLookup = false;
                return Task.FromResult<Domain.Orders.Order?>(null);
            }
            return _inner.GetByExternalReferenceAsync(reference, ct);
        }

        public Task AddAsync(Domain.Orders.Order order, CancellationToken ct = default)
            => _inner.AddAsync(order, ct);

        public Task UpdateAsync(Domain.Orders.Order order, CancellationToken ct = default)
            => _inner.UpdateAsync(order, ct);

        public Task<(IReadOnlyList<Domain.Orders.Order> Items, int TotalCount)> ListAsync(
            int page, int pageSize, Domain.Orders.OrderStatus? status, CancellationToken ct = default)
            => _inner.ListAsync(page, pageSize, status, ct);
    }
}
