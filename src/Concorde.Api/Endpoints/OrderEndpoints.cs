namespace Concorde.Api.Endpoints;

using Concorde.Api.Contracts;
using Concorde.Application.Common;
using Concorde.Application.Orders;
using Concorde.Application.Orders.ChangeStatus;
using Concorde.Application.Orders.Create;
using Concorde.Application.Orders.Get;
using Concorde.Application.Orders.List;
using Concorde.Application.Orders.Update;
using Microsoft.AspNetCore.Http.HttpResults;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var orders = app.MapGroup("/api/v1/orders").WithTags("Orders");

        orders.MapPost("/", async Task<Results<Created<OrderDto>, Ok<OrderDto>>> (
            CreateOrderRequest request,
            CreateOrderHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateOrderCommand(
                request.ExternalReference,
                request.CustomerName,
                request.CustomerCode,
                request.Currency,
                request.Notes,
                request.Lines?.Select(l => new CreateOrderLine(l.Sku, l.Name, l.Quantity, l.UnitPrice)).ToList());

            var result = await handler.HandleAsync(command, cancellationToken);

            // FR-02.3: idempotent replay returns the existing order with 200, not 201.
            return result.WasCreated
                ? TypedResults.Created($"/api/v1/orders/{result.Order.Id}", result.Order)
                : TypedResults.Ok(result.Order);
        })
        .WithName("CreateOrder")
        .WithSummary("Submit a purchase order")
        .WithDescription("Creates an order, or returns the existing order when the external reference was already used (idempotent replay).")
        .Produces<OrderDto>(StatusCodes.Status201Created)
        .Produces<OrderDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        orders.MapGet("/", async (
            ListOrdersHandler handler,
            CancellationToken cancellationToken,
            int page = 1,
            int pageSize = 20,
            string? status = null) =>
        {
            var result = await handler.HandleAsync(new ListOrdersQuery(page, pageSize, status), cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithName("ListOrders")
        .WithSummary("List orders")
        .WithDescription("Returns a paginated list of orders sorted newest first, optionally filtered by status.")
        .Produces<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        orders.MapGet("/{id}", async Task<Ok<OrderDto>> (
            string id,
            GetOrderHandler handler,
            CancellationToken cancellationToken) =>
        {
            // FR-05.5: malformed IDs are indistinguishable from unknown orders (404).
            if (!Guid.TryParse(id, out var orderId))
                throw new OrderNotFoundException(Guid.Empty);

            var order = await handler.HandleAsync(new GetOrderQuery(orderId), cancellationToken);
            return TypedResults.Ok(order);
        })
        .WithName("GetOrder")
        .WithSummary("Get an order by ID")
        .Produces<OrderDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        orders.MapPatch("/{id}/status", async Task<Ok<OrderDto>> (
            string id,
            ChangeOrderStatusRequest request,
            ChangeOrderStatusHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(id, out var orderId))
                throw new OrderNotFoundException(Guid.Empty);

            var order = await handler.HandleAsync(
                new ChangeOrderStatusCommand(orderId, request.Status, request.Reason), cancellationToken);
            return TypedResults.Ok(order);
        })
        .WithName("ChangeOrderStatus")
        .WithSummary("Change an order's status")
        .WithDescription("Transitions the order through the lifecycle: Pending → Confirmed → Fulfilled. Cancelled is reachable from any active state; cancelling a fulfilled order requires a reason.")
        .Produces<OrderDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        orders.MapPut("/{id}", async Task<Ok<OrderDto>> (
            string id,
            UpdateOrderRequest request,
            UpdateOrderHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(id, out var orderId))
                throw new OrderNotFoundException(Guid.Empty);

            var command = new UpdateOrderCommand(
                orderId,
                request.CustomerName,
                request.CustomerCode,
                request.Currency,
                request.Notes,
                request.Lines?.Select(l => new CreateOrderLine(l.Sku, l.Name, l.Quantity, l.UnitPrice)).ToList());

            var order = await handler.HandleAsync(command, cancellationToken);
            return TypedResults.Ok(order);
        })
        .WithName("UpdateOrder")
        .WithSummary("Amend an order")
        .WithDescription("Updates customer details, notes, currency, and line items until the order is fulfilled or cancelled. The external reference is immutable; totals are recalculated on the server.")
        .Produces<OrderDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
