namespace Concorde.Application.Orders.List;

using Concorde.Application.Abstractions;
using Concorde.Application.Validation;
using Concorde.Domain.Orders;

/// <summary>
/// Paged order list per FR-05.1: page >= 1, pageSize 1-100 (default 20),
/// optional status filter (FR-05.6).
/// </summary>
public sealed record ListOrdersQuery(int Page = 1, int PageSize = 20, string? Status = null);

public class ListOrdersHandler
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private readonly IOrderRepository _repository;

    public ListOrdersHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<OrderSummaryDto>> HandleAsync(
        ListOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        if (query.Page < 1)
            errors.Add(new ValidationError("page", ValidationErrorCodes.InvalidFormat,
                "Page must be at least 1."));

        if (query.PageSize < 1)
            errors.Add(new ValidationError("pageSize", ValidationErrorCodes.InvalidFormat,
                "Page size must be at least 1."));

        OrderStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed))
                statusFilter = parsed;
            else
                errors.Add(new ValidationError("status", ValidationErrorCodes.InvalidStatus,
                    $"'{query.Status}' is not a valid order status."));
        }

        if (errors.Count > 0)
            throw new ValidationException(errors);

        var pageSize = Math.Min(query.PageSize, MaxPageSize);

        var (items, totalCount) = await _repository.ListAsync(
            query.Page, pageSize, statusFilter, cancellationToken);

        return new PagedResult<OrderSummaryDto>(
            items.Select(OrderSummaryDto.FromDomain).ToList(),
            query.Page,
            pageSize,
            totalCount);
    }
}
