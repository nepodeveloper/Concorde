namespace Concorde.Api.Contracts;

/// <summary>Payload for submitting a purchase order (US-01). Totals are server-calculated and ignored if supplied.</summary>
public sealed record CreateOrderRequest(
    string? ExternalReference,
    string? CustomerName,
    string? CustomerCode,
    string? Currency,
    string? Notes,
    List<OrderLineRequest>? Lines);

/// <summary>A single line item in an order submission.</summary>
public sealed record OrderLineRequest(
    string? Sku,
    string? Name,
    decimal Quantity,
    decimal UnitPrice);

/// <summary>Payload for changing an order's lifecycle status (US-07).</summary>
public sealed record ChangeOrderStatusRequest(string? Status);
