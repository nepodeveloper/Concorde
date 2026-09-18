namespace Concorde.Application.Orders.Create;

/// <summary>
/// Request to create an order. Quantity and unit price are decimals so the
/// validator can report fractional quantities and excess decimal places
/// (AC-03.3, AC-03.5) instead of failing at deserialization.
/// Client-supplied totals are not part of this contract (FR-04.4).
/// </summary>
public sealed record CreateOrderCommand(
    string? ExternalReference,
    string? CustomerName,
    string? CustomerCode,
    string? Currency,
    string? Notes,
    IReadOnlyList<CreateOrderLine>? Lines);

public sealed record CreateOrderLine(
    string? Sku,
    string? Name,
    decimal Quantity,
    decimal UnitPrice);
