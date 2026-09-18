namespace Concorde.IntegrationTests;

using Concorde.Application.Orders.Create;
using Concorde.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Exercises the CreateOrder use case against real SQLite persistence,
/// proving idempotency survives the actual unique constraint.
/// </summary>
public class CreateOrderIntegrationTests : IDisposable
{
    private readonly SqliteDatabaseFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static CreateOrderCommand Command(string reference) => new(
        reference, "Acme Corp", "ACME-001", "ZAR", null,
        new[] { new CreateOrderLine("SKU-A", "Product A", 2, 100.00m) });

    [Fact]
    public async Task CreateOrder_PersistsAndIsReadableFromFreshContext()
    {
        CreateOrderResult result;
        using (var context = _fixture.CreateContext())
        {
            result = await new CreateOrderHandler(new OrderRepository(context)).HandleAsync(Command("PO-10001"));
        }

        Assert.True(result.WasCreated);

        using (var context = _fixture.CreateContext())
        {
            var loaded = await new OrderRepository(context).GetByIdAsync(result.Order.Id);
            Assert.NotNull(loaded);
            Assert.Equal(200.00m, loaded.Total.Amount);
        }
    }

    [Fact]
    public async Task CreateOrder_RepeatedSubmission_ReturnsSameOrderWithoutDuplicate()
    {
        Guid firstId;
        using (var context = _fixture.CreateContext())
        {
            var handler = new CreateOrderHandler(new OrderRepository(context));
            firstId = (await handler.HandleAsync(Command("PO-10001"))).Order.Id;
        }

        // Second submission via a completely fresh context (new request simulation).
        using (var context = _fixture.CreateContext())
        {
            var handler = new CreateOrderHandler(new OrderRepository(context));
            var replay = await handler.HandleAsync(Command("PO-10001"));

            Assert.False(replay.WasCreated);
            Assert.Equal(firstId, replay.Order.Id);
        }

        using (var context = _fixture.CreateContext())
        {
            var (_, totalCount) = await new OrderRepository(context).ListAsync(1, 10, null);
            Assert.Equal(1, totalCount);
        }
    }

    [Fact]
    public async Task CreateOrder_DifferentCaseReference_ReturnsExistingOrder()
    {
        Guid firstId;
        using (var context = _fixture.CreateContext())
        {
            firstId = (await new CreateOrderHandler(new OrderRepository(context))
                .HandleAsync(Command("PO-10001"))).Order.Id;
        }

        using (var context = _fixture.CreateContext())
        {
            var replay = await new CreateOrderHandler(new OrderRepository(context))
                .HandleAsync(Command("po-10001"));

            Assert.False(replay.WasCreated);
            Assert.Equal(firstId, replay.Order.Id);
        }
    }
}
