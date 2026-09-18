namespace Concorde.Application.Orders;

using Concorde.Domain.Orders;

/// <summary>
/// Read model returned by all order queries and commands.
/// </summary>
public sealed record OrderDto(
    Guid Id,
    string ExternalReference,
    string CustomerName,
    string? CustomerCode,
    string Currency,
    string? Notes,
    string Status,
    decimal Subtotal,
    decimal Total,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<OrderLineDto> Lines)
{
    public static OrderDto FromDomain(Order order) => new(
        order.Id,
        order.ExternalReference,
        order.CustomerName,
        order.CustomerCode,
        order.Currency.Code,
        order.Notes,
        order.Status.ToString(),
        order.Subtotal.Amount,
        order.Total.Amount,
        order.CreatedAtUtc,
        order.UpdatedAtUtc,
        order.Lines.Select(OrderLineDto.FromDomain).ToList());
}

public sealed record OrderLineDto(
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal)
{
    public static OrderLineDto FromDomain(OrderLine line) => new(
        line.Sku,
        line.Name,
        line.Quantity,
        line.UnitPrice.Amount,
        line.LineTotal.Amount);
}

/// <summary>
/// Compact list item per FR-05.3.
/// </summary>
public sealed record OrderSummaryDto(
    Guid Id,
    string ExternalReference,
    string CustomerName,
    DateTime CreatedAtUtc,
    string Status,
    string Currency,
    decimal Total)
{
    public static OrderSummaryDto FromDomain(Order order) => new(
        order.Id,
        order.ExternalReference,
        order.CustomerName,
        order.CreatedAtUtc,
        order.Status.ToString(),
        order.Currency.Code,
        order.Total.Amount);
}

/// <summary>
/// Paged envelope per FR-05.1.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
