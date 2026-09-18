namespace Concorde.Application.Orders.Create;

using System.Text.RegularExpressions;
using Concorde.Domain.Common;
using Concorde.Domain.Orders;
using Concorde.Application.Validation;

/// <summary>
/// Validates a CreateOrderCommand, reporting ALL failures together (FR-03.6).
/// Field paths use the request contract shape, e.g. "lines[1].quantity".
/// </summary>
public static class CreateOrderValidator
{
    private static readonly Regex ExternalReferencePattern = new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    public static IReadOnlyList<ValidationError> Validate(CreateOrderCommand command)
    {
        var errors = new List<ValidationError>();

        ValidateExternalReference(command.ExternalReference, errors);
        ValidateCustomer(command, errors);
        ValidateCurrency(command.Currency, errors);
        ValidateNotes(command.Notes, errors);
        ValidateLines(command.Lines, errors);

        return errors;
    }

    private static void ValidateExternalReference(string? value, List<ValidationError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new ValidationError("externalReference", ValidationErrorCodes.Required,
                "External reference is required."));
            return;
        }

        if (trimmed.Length > Order.MaxExternalReferenceLength)
            errors.Add(new ValidationError("externalReference", ValidationErrorCodes.TooLong,
                $"External reference may not exceed {Order.MaxExternalReferenceLength} characters."));
        else if (!ExternalReferencePattern.IsMatch(trimmed))
            errors.Add(new ValidationError("externalReference", ValidationErrorCodes.InvalidFormat,
                "External reference must contain only alphanumeric characters, periods, hyphens, and underscores."));
    }

    private static void ValidateCustomer(CreateOrderCommand command, List<ValidationError> errors)
    {
        var name = command.CustomerName?.Trim();
        if (string.IsNullOrEmpty(name))
            errors.Add(new ValidationError("customerName", ValidationErrorCodes.Required,
                "Customer name is required."));
        else if (name.Length > Order.MaxCustomerNameLength)
            errors.Add(new ValidationError("customerName", ValidationErrorCodes.TooLong,
                $"Customer name may not exceed {Order.MaxCustomerNameLength} characters."));

        var code = command.CustomerCode?.Trim();
        if (!string.IsNullOrEmpty(code) && code.Length > Order.MaxCustomerCodeLength)
            errors.Add(new ValidationError("customerCode", ValidationErrorCodes.TooLong,
                $"Customer code may not exceed {Order.MaxCustomerCodeLength} characters."));
    }

    private static void ValidateCurrency(string? value, List<ValidationError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new ValidationError("currency", ValidationErrorCodes.Required,
                "Currency is required."));
            return;
        }

        if (!Currency.IsValid(trimmed))
            errors.Add(new ValidationError("currency", ValidationErrorCodes.InvalidCurrency,
                $"'{trimmed}' is not a recognized ISO 4217 currency code."));
    }

    private static void ValidateNotes(string? value, List<ValidationError> errors)
    {
        if (!string.IsNullOrEmpty(value?.Trim()) && value.Trim().Length > Order.MaxNotesLength)
            errors.Add(new ValidationError("notes", ValidationErrorCodes.TooLong,
                $"Notes may not exceed {Order.MaxNotesLength} characters."));
    }

    private static void ValidateLines(IReadOnlyList<CreateOrderLine>? lines, List<ValidationError> errors)
    {
        if (lines is null || lines.Count == 0)
        {
            errors.Add(new ValidationError("lines", ValidationErrorCodes.Required,
                "At least one line item is required."));
            return;
        }

        if (lines.Count > Order.MaxLineItems)
        {
            errors.Add(new ValidationError("lines", ValidationErrorCodes.InvalidFormat,
                $"Order may not contain more than {Order.MaxLineItems} line items."));
            return;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var prefix = $"lines[{i}]";

            var sku = line.Sku?.Trim();
            if (string.IsNullOrEmpty(sku))
                errors.Add(new ValidationError($"{prefix}.sku", ValidationErrorCodes.Required,
                    "SKU is required."));
            else if (sku.Length > OrderLine.MaxSkuLength)
                errors.Add(new ValidationError($"{prefix}.sku", ValidationErrorCodes.TooLong,
                    $"SKU may not exceed {OrderLine.MaxSkuLength} characters."));

            var name = line.Name?.Trim();
            if (string.IsNullOrEmpty(name))
                errors.Add(new ValidationError($"{prefix}.name", ValidationErrorCodes.Required,
                    "Item name is required."));
            else if (name.Length > OrderLine.MaxNameLength)
                errors.Add(new ValidationError($"{prefix}.name", ValidationErrorCodes.TooLong,
                    $"Item name may not exceed {OrderLine.MaxNameLength} characters."));

            ValidateQuantity(line.Quantity, prefix, errors);
            ValidateUnitPrice(line.UnitPrice, prefix, errors);
        }
    }

    private static void ValidateQuantity(decimal quantity, string prefix, List<ValidationError> errors)
    {
        if (quantity != decimal.Truncate(quantity))
        {
            errors.Add(new ValidationError($"{prefix}.quantity", ValidationErrorCodes.InvalidQuantity,
                "Quantity must be a whole number."));
            return;
        }

        if (quantity <= 0)
            errors.Add(new ValidationError($"{prefix}.quantity", ValidationErrorCodes.InvalidQuantity,
                "Quantity must be greater than zero."));
        else if (quantity > OrderLine.MaxQuantity)
            errors.Add(new ValidationError($"{prefix}.quantity", ValidationErrorCodes.InvalidQuantity,
                $"Quantity may not exceed {OrderLine.MaxQuantity}."));
    }

    private static void ValidateUnitPrice(decimal unitPrice, string prefix, List<ValidationError> errors)
    {
        if (unitPrice < 0)
        {
            errors.Add(new ValidationError($"{prefix}.unitPrice", ValidationErrorCodes.InvalidPrice,
                "Unit price cannot be negative."));
            return;
        }

        if (unitPrice > Money.MaxAmount)
        {
            errors.Add(new ValidationError($"{prefix}.unitPrice", ValidationErrorCodes.InvalidPrice,
                $"Unit price may not exceed {Money.MaxAmount}."));
            return;
        }

        var scale = (decimal.GetBits(unitPrice)[3] >> 16) & 0xFF;
        if (scale > Money.DecimalPlaces && unitPrice != decimal.Round(unitPrice, Money.DecimalPlaces))
            errors.Add(new ValidationError($"{prefix}.unitPrice", ValidationErrorCodes.InvalidPrice,
                "Unit price may have at most 2 decimal places."));
    }
}
