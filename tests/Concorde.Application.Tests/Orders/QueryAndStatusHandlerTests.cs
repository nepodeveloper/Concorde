namespace Concorde.Application.Tests.Orders;

using Concorde.Application.Common;
using Concorde.Application.Orders.ChangeStatus;
using Concorde.Application.Orders.Create;
using Concorde.Application.Orders.Get;
using Concorde.Application.Orders.List;
using Concorde.Application.Validation;
using Xunit;

public class GetOrderHandlerTests
{
    private readonly InMemoryOrderRepository _repository = new();

    [Fact]
    public async Task GetOrder_WithExistingId_ReturnsOrder()
    {
        var created = await new CreateOrderHandler(_repository).HandleAsync(TestData.ValidCommand());
        var handler = new GetOrderHandler(_repository);

        var order = await handler.HandleAsync(new GetOrderQuery(created.Order.Id));

        Assert.Equal(created.Order.Id, order.Id);
        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(350.00m, order.Total);
    }

    [Fact]
    public async Task GetOrder_WithUnknownId_ThrowsNotFound()
    {
        var handler = new GetOrderHandler(_repository);

        await Assert.ThrowsAsync<OrderNotFoundException>(() =>
            handler.HandleAsync(new GetOrderQuery(Guid.NewGuid())));
    }
}

public class ListOrdersHandlerTests
{
    private readonly InMemoryOrderRepository _repository = new();
    private readonly CreateOrderHandler _createHandler;
    private readonly ListOrdersHandler _handler;

    public ListOrdersHandlerTests()
    {
        _createHandler = new CreateOrderHandler(_repository);
        _handler = new ListOrdersHandler(_repository);
    }

    private async Task SeedOrdersAsync(int count)
    {
        for (var i = 0; i < count; i++)
            await _createHandler.HandleAsync(TestData.ValidCommand($"PO-{i:D5}"));
    }

    [Fact]
    public async Task ListOrders_ReturnsNewestFirst()
    {
        await SeedOrdersAsync(3);

        var result = await _handler.HandleAsync(new ListOrdersQuery());

        var timestamps = result.Items.Select(i => i.CreatedAtUtc).ToList();
        Assert.Equal(timestamps.OrderByDescending(t => t), timestamps);
    }

    [Fact]
    public async Task ListOrders_RespectsPageAndPageSize()
    {
        await SeedOrdersAsync(25);

        var page1 = await _handler.HandleAsync(new ListOrdersQuery(Page: 1, PageSize: 10));
        var page2 = await _handler.HandleAsync(new ListOrdersQuery(Page: 2, PageSize: 10));

        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(25, page1.TotalCount);
        Assert.Empty(page1.Items.Select(i => i.Id).Intersect(page2.Items.Select(i => i.Id)));
    }

    [Fact]
    public async Task ListOrders_ClampsPageSizeToMax()
    {
        await SeedOrdersAsync(1);

        var result = await _handler.HandleAsync(new ListOrdersQuery(PageSize: 500));

        Assert.Equal(ListOrdersHandler.MaxPageSize, result.PageSize);
    }

    [Fact]
    public async Task ListOrders_FiltersByStatus()
    {
        await SeedOrdersAsync(3);
        var changeHandler = new ChangeOrderStatusHandler(_repository);
        var all = await _handler.HandleAsync(new ListOrdersQuery());
        await changeHandler.HandleAsync(new ChangeOrderStatusCommand(all.Items[0].Id, "Confirmed"));

        var confirmed = await _handler.HandleAsync(new ListOrdersQuery(Status: "Confirmed"));
        var pending = await _handler.HandleAsync(new ListOrdersQuery(Status: "Pending"));

        Assert.Equal(1, confirmed.TotalCount);
        Assert.Equal(2, pending.TotalCount);
    }

    [Fact]
    public async Task ListOrders_WithUnknownStatus_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(new ListOrdersQuery(Status: "Shipped")));

        Assert.Contains(ex.Errors, e => e.Field == "status");
    }

    [Fact]
    public async Task ListOrders_WithInvalidPage_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(new ListOrdersQuery(Page: 0)));

        Assert.Contains(ex.Errors, e => e.Field == "page");
    }
}

public class ChangeOrderStatusHandlerTests
{
    private readonly InMemoryOrderRepository _repository = new();
    private readonly ChangeOrderStatusHandler _handler;
    private readonly CreateOrderHandler _createHandler;

    public ChangeOrderStatusHandlerTests()
    {
        _handler = new ChangeOrderStatusHandler(_repository);
        _createHandler = new CreateOrderHandler(_repository);
    }

    private async Task<Guid> CreatePendingOrderAsync()
    {
        var result = await _createHandler.HandleAsync(TestData.ValidCommand());
        return result.Order.Id;
    }

    [Fact]
    public async Task ChangeStatus_PendingToConfirmed_Succeeds()
    {
        var id = await CreatePendingOrderAsync();

        var order = await _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Confirmed"));

        Assert.Equal("Confirmed", order.Status);
    }

    [Fact]
    public async Task ChangeStatus_PendingToCancelled_Succeeds()
    {
        var id = await CreatePendingOrderAsync();

        var order = await _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Cancelled"));

        Assert.Equal("Cancelled", order.Status);
    }

    [Fact]
    public async Task ChangeStatus_ConfirmedToFulfilled_Succeeds()
    {
        var id = await CreatePendingOrderAsync();
        await _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Confirmed"));

        var order = await _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Fulfilled"));

        Assert.Equal("Fulfilled", order.Status);
    }

    [Fact]
    public async Task ChangeStatus_PendingToFulfilled_ThrowsInvalidTransition()
    {
        var id = await CreatePendingOrderAsync();

        await Assert.ThrowsAsync<InvalidStatusTransitionException>(() =>
            _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Fulfilled")));
    }

    [Fact]
    public async Task ChangeStatus_FulfilledToPending_ThrowsWithBothStatusesNamed()
    {
        var id = await CreatePendingOrderAsync();
        await _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Confirmed"));
        await _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Fulfilled"));

        var ex = await Assert.ThrowsAsync<InvalidStatusTransitionException>(() =>
            _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Pending")));

        Assert.Equal("Order cannot transition from Fulfilled to Pending.", ex.Message);
    }

    [Fact]
    public async Task ChangeStatus_SameStatus_ThrowsInvalidTransition()
    {
        var id = await CreatePendingOrderAsync();
        await _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Confirmed"));

        await Assert.ThrowsAsync<InvalidStatusTransitionException>(() =>
            _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Confirmed")));
    }

    [Fact]
    public async Task ChangeStatus_WithUnknownStatusValue_ThrowsValidation()
    {
        var id = await CreatePendingOrderAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Shipped")));

        Assert.Contains(ex.Errors, e => e.Code == "INVALID_STATUS");
    }

    [Fact]
    public async Task ChangeStatus_OnUnknownOrder_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<OrderNotFoundException>(() =>
            _handler.HandleAsync(new ChangeOrderStatusCommand(Guid.NewGuid(), "Confirmed")));
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_DoesNotModifyOrder()
    {
        var id = await CreatePendingOrderAsync();

        await Assert.ThrowsAsync<InvalidStatusTransitionException>(() =>
            _handler.HandleAsync(new ChangeOrderStatusCommand(id, "Fulfilled")));

        var order = await new GetOrderHandler(_repository).HandleAsync(new GetOrderQuery(id));
        Assert.Equal("Pending", order.Status);
    }
}
