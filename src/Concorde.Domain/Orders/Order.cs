namespace Concorde.Domain.Orders;

using Common;

/// <summary>
/// Represents a customer purchase order.
/// This is the aggregate root for the Order domain.
/// </summary>
public class Order
{
    public const int MaxExternalReferenceLength = 64;
    public const int MinLineItems = 1;
    public const int MaxLineItems = 100;
    public const int MaxCustomerNameLength = 200;
    public const int MaxCustomerCodeLength = 50;
    public const int MaxNotesLength = 1000;
    public const int MaxStatusReasonLength = 500;

    public Guid Id { get; private set; }
    public string ExternalReference { get; private set; }
    public string CustomerName { get; private set; }
    public string? CustomerCode { get; private set; }
    public Currency Currency { get; private set; }
    public string? Notes { get; private set; }
    public OrderStatus Status { get; private set; }
    /// <summary>Reason supplied with the most recent status change, e.g. why a fulfilled order was cancelled.</summary>
    public string? StatusReason { get; private set; }
    public Money Subtotal { get; private set; } = new(0);
    public Money Total { get; private set; } = new(0);
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    // For EF Core
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    private Order() { }
#pragma warning restore CS8618

    /// <summary>
    /// Create a new order with validation.
    /// </summary>
    public Order(
        string externalReference,
        string customerName,
        string? customerCode,
        Currency currency,
        IEnumerable<OrderLine> lines,
        string? notes = null)
    {
        ValidateExternalReference(externalReference);
        ValidateCustomerName(customerName);
        ValidateCustomerCode(customerCode);
        ArgumentNullException.ThrowIfNull(currency);
        ValidateNotes(notes);

        var lineList = lines.ToList();
        ValidateLineItems(lineList);

        Id = Guid.NewGuid();
        ExternalReference = externalReference.Trim().ToUpperInvariant();
        CustomerName = customerName.Trim();
        CustomerCode = customerCode?.Trim();
        Currency = currency;
        Notes = notes?.Trim();
        _lines.AddRange(lineList);
        Status = OrderStatus.Pending;
        
        var now = DateTime.UtcNow;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;

        // Calculate totals
        CalculateTotals();
    }

    /// <summary>
    /// Attempt to change the order status.
    /// Throws if the transition is invalid, or if a fulfilled order is
    /// cancelled without a reason.
    /// </summary>
    public void ChangeStatus(OrderStatus newStatus, string? reason = null)
    {
        if (!OrderStatusTransitionPolicy.IsTransitionAllowed(Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Order cannot transition from {Status} to {newStatus}.");
        }

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (trimmedReason is { Length: > MaxStatusReasonLength })
            throw new ArgumentException(
                $"Status reason may not exceed {MaxStatusReasonLength} characters.", nameof(reason));

        // Cancelling a fulfilled order is exceptional and must be justified.
        if (Status == OrderStatus.Fulfilled && newStatus == OrderStatus.Cancelled && trimmedReason is null)
            throw new ArgumentException(
                "A reason is required when cancelling a fulfilled order.", nameof(reason));

        Status = newStatus;
        StatusReason = trimmedReason;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Amend order details and line items. Allowed until the order is
    /// fulfilled or cancelled; the external reference is immutable.
    /// Totals are recalculated.
    /// </summary>
    public void Amend(
        string customerName,
        string? customerCode,
        Currency currency,
        IEnumerable<OrderLine> lines,
        string? notes = null)
    {
        if (Status is OrderStatus.Fulfilled or OrderStatus.Cancelled)
            throw new InvalidOperationException(
                $"A {Status} order can no longer be edited.");

        ValidateCustomerName(customerName);
        ValidateCustomerCode(customerCode);
        ArgumentNullException.ThrowIfNull(currency);
        ValidateNotes(notes);

        var lineList = lines.ToList();
        ValidateLineItems(lineList);

        CustomerName = customerName.Trim();
        CustomerCode = customerCode?.Trim();
        Currency = currency;
        Notes = notes?.Trim();
        _lines.Clear();
        _lines.AddRange(lineList);
        UpdatedAtUtc = DateTime.UtcNow;

        CalculateTotals();
    }

    private void CalculateTotals()
    {
        // Calculate subtotal from all line items
        Subtotal = _lines.Any() ? Money.Sum(_lines.Select(l => l.LineTotal).ToArray()) : new Money(0);
        
        // In MVP, Total = Subtotal (no tax, discount, or shipping)
        Total = Subtotal;
    }

    private static void ValidateExternalReference(string externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
            throw new ArgumentException("External reference is required.", nameof(externalReference));

        var trimmed = externalReference.Trim();

        if (trimmed.Length > MaxExternalReferenceLength)
            throw new ArgumentException(
                $"External reference may not exceed {MaxExternalReferenceLength} characters.",
                nameof(externalReference));

        // CC-01: Must match ^[A-Za-z0-9._-]+$
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Za-z0-9._-]+$"))
            throw new ArgumentException(
                "External reference must contain only alphanumeric characters, periods, hyphens, and underscores.",
                nameof(externalReference));
    }

    private static void ValidateCustomerName(string customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name is required.", nameof(customerName));

        var trimmed = customerName.Trim();
        if (trimmed.Length > MaxCustomerNameLength)
            throw new ArgumentException(
                $"Customer name may not exceed {MaxCustomerNameLength} characters.",
                nameof(customerName));
    }

    private static void ValidateCustomerCode(string? customerCode)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
            return; // Optional

        var trimmed = customerCode.Trim();
        if (trimmed.Length > MaxCustomerCodeLength)
            throw new ArgumentException(
                $"Customer code may not exceed {MaxCustomerCodeLength} characters.",
                nameof(customerCode));
    }

    private static void ValidateNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return; // Optional

        var trimmed = notes.Trim();
        if (trimmed.Length > MaxNotesLength)
            throw new ArgumentException(
                $"Notes may not exceed {MaxNotesLength} characters.",
                nameof(notes));
    }

    private static void ValidateLineItems(List<OrderLine> lines)
    {
        if (!lines.Any())
            throw new ArgumentException("At least one line item is required.", nameof(lines));

        if (lines.Count > MaxLineItems)
            throw new ArgumentException(
                $"Order may not contain more than {MaxLineItems} line items.",
                nameof(lines));
    }
}
