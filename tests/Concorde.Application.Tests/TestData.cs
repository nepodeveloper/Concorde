namespace Concorde.Application.Tests;

using Concorde.Application.Orders.Create;

/// <summary>Builders for valid commands that individual tests mutate.</summary>
public static class TestData
{
    public static CreateOrderCommand ValidCommand(string externalReference = "PO-10001") => new(
        ExternalReference: externalReference,
        CustomerName: "Acme Corp",
        CustomerCode: "ACME-001",
        Currency: "ZAR",
        Notes: "Deliver before month end",
        Lines: new[]
        {
            new CreateOrderLine("SKU-A", "Product A", 2, 100.00m),
            new CreateOrderLine("SKU-B", "Product B", 3, 50.00m),
        });
}
