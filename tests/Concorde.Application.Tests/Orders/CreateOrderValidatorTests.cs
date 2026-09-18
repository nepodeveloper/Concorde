namespace Concorde.Application.Tests.Orders;

using Concorde.Application.Orders.Create;
using Concorde.Application.Validation;
using Xunit;

public class CreateOrderValidatorTests
{
    [Fact]
    public void Validate_WithValidCommand_ReturnsNoErrors()
    {
        var errors = CreateOrderValidator.Validate(TestData.ValidCommand());
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithZeroQuantity_ReportsInvalidQuantity()
    {
        var command = TestData.ValidCommand() with
        {
            Lines = new[] { new CreateOrderLine("SKU-A", "Product A", 0, 100m) },
        };

        var errors = CreateOrderValidator.Validate(command);

        var error = Assert.Single(errors);
        Assert.Equal("lines[0].quantity", error.Field);
        Assert.Equal(ValidationErrorCodes.InvalidQuantity, error.Code);
        Assert.Equal("Quantity must be greater than zero.", error.Message);
    }

    [Fact]
    public void Validate_WithNegativeQuantity_ReportsInvalidQuantity()
    {
        var command = TestData.ValidCommand() with
        {
            Lines = new[] { new CreateOrderLine("SKU-A", "Product A", -1, 100m) },
        };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Contains(errors, e => e.Field == "lines[0].quantity");
    }

    [Fact]
    public void Validate_WithFractionalQuantity_ReportsWholeNumberMessage()
    {
        var command = TestData.ValidCommand() with
        {
            Lines = new[] { new CreateOrderLine("SKU-A", "Product A", 1.5m, 100m) },
        };

        var errors = CreateOrderValidator.Validate(command);

        var error = Assert.Single(errors);
        Assert.Equal("lines[0].quantity", error.Field);
        Assert.Equal("Quantity must be a whole number.", error.Message);
    }

    [Fact]
    public void Validate_WithQuantityAboveMax_ReportsInvalidQuantity()
    {
        var command = TestData.ValidCommand() with
        {
            Lines = new[] { new CreateOrderLine("SKU-A", "Product A", 1_000_001, 100m) },
        };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Contains(errors, e => e.Field == "lines[0].quantity");
    }

    [Fact]
    public void Validate_WithNegativePrice_ReportsInvalidPrice()
    {
        var command = TestData.ValidCommand() with
        {
            Lines = new[] { new CreateOrderLine("SKU-A", "Product A", 1, -0.01m) },
        };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Contains(errors, e =>
            e.Field == "lines[0].unitPrice" && e.Code == ValidationErrorCodes.InvalidPrice);
    }

    [Fact]
    public void Validate_WithPriceMoreThanTwoDecimals_ReportsInvalidPrice()
    {
        var command = TestData.ValidCommand() with
        {
            Lines = new[] { new CreateOrderLine("SKU-A", "Product A", 1, 10.005m) },
        };

        var errors = CreateOrderValidator.Validate(command);

        var error = Assert.Single(errors);
        Assert.Equal("lines[0].unitPrice", error.Field);
        Assert.Equal("Unit price may have at most 2 decimal places.", error.Message);
    }

    [Fact]
    public void Validate_WithZeroPrice_IsValid()
    {
        var command = TestData.ValidCommand() with
        {
            Lines = new[] { new CreateOrderLine("SKU-FREE", "Free Item", 5, 0m) },
        };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithBlankSku_ReportsRequired()
    {
        var command = TestData.ValidCommand() with
        {
            Lines = new[] { new CreateOrderLine("  ", "Product A", 1, 100m) },
        };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Contains(errors, e =>
            e.Field == "lines[0].sku" && e.Code == ValidationErrorCodes.Required);
    }

    [Fact]
    public void Validate_WithInvalidCurrency_ReportsInvalidCurrency()
    {
        var command = TestData.ValidCommand() with { Currency = "XXX1" };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Contains(errors, e =>
            e.Field == "currency" && e.Code == ValidationErrorCodes.InvalidCurrency);
    }

    [Fact]
    public void Validate_WithMissingCurrency_ReportsRequired()
    {
        var command = TestData.ValidCommand() with { Currency = " " };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Contains(errors, e =>
            e.Field == "currency" && e.Code == ValidationErrorCodes.Required);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReportsAll()
    {
        var command = TestData.ValidCommand() with
        {
            CustomerName = "",
            Lines = new[] { new CreateOrderLine("SKU-A", "Product A", 0, 100m) },
        };

        var errors = CreateOrderValidator.Validate(command);

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Field == "customerName");
        Assert.Contains(errors, e => e.Field == "lines[0].quantity");
    }

    [Fact]
    public void Validate_WithExternalReferenceTooLong_ReportsTooLong()
    {
        var command = TestData.ValidCommand() with { ExternalReference = new string('A', 65) };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Contains(errors, e =>
            e.Field == "externalReference" && e.Code == ValidationErrorCodes.TooLong);
    }

    [Fact]
    public void Validate_WithInvalidExternalReferenceFormat_ReportsInvalidFormat()
    {
        var command = TestData.ValidCommand() with { ExternalReference = "PO 10001!" };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Contains(errors, e =>
            e.Field == "externalReference" && e.Code == ValidationErrorCodes.InvalidFormat);
    }

    [Fact]
    public void Validate_WithMoreThan100Lines_ReportsError()
    {
        var lines = Enumerable.Range(0, 101)
            .Select(i => new CreateOrderLine($"SKU-{i}", $"Product {i}", 1, 10m))
            .ToArray();
        var command = TestData.ValidCommand() with { Lines = lines };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Contains(errors, e => e.Field == "lines");
    }

    [Fact]
    public void Validate_WithNotesTooLong_ReportsTooLong()
    {
        var command = TestData.ValidCommand() with { Notes = new string('N', 1001) };

        var errors = CreateOrderValidator.Validate(command);
        Assert.Contains(errors, e =>
            e.Field == "notes" && e.Code == ValidationErrorCodes.TooLong);
    }
}
