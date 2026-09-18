namespace Concorde.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public class OrdersApiTests : IClassFixture<ConcordeApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public OrdersApiTests(ConcordeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static object ValidOrder(string reference) => new
    {
        externalReference = reference,
        customerName = "Acme Corp",
        customerCode = "ACME-001",
        currency = "ZAR",
        notes = "Deliver before month end",
        lines = new[]
        {
            new { sku = "SKU-A", name = "Product A", quantity = 2, unitPrice = 100.00m },
            new { sku = "SKU-B", name = "Product B", quantity = 3, unitPrice = 50.00m },
        },
    };

    private async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonOptions);

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/orders", ValidOrder("PO-API-001"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var id = body.GetProperty("id").GetString();
        Assert.Equal($"/api/v1/orders/{id}", response.Headers.Location!.ToString());
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(350.00m, body.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task CreateOrder_RepeatedSubmission_Returns200WithSameId()
    {
        var first = await _client.PostAsJsonAsync("/api/v1/orders", ValidOrder("PO-API-DUP"));
        var second = await _client.PostAsJsonAsync("/api/v1/orders", ValidOrder("PO-API-DUP"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstId = (await ReadJsonAsync(first)).GetProperty("id").GetString();
        var secondId = (await ReadJsonAsync(second)).GetProperty("id").GetString();
        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidData_Returns400Envelope()
    {
        var invalid = new
        {
            externalReference = "",
            customerName = "",
            currency = "XXX1",
            lines = new[] { new { sku = "SKU-A", name = "Product A", quantity = 0, unitPrice = 100.00m } },
        };

        var response = await _client.PostAsJsonAsync("/api/v1/orders", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));

        var fields = body.GetProperty("details").EnumerateArray()
            .Select(d => d.GetProperty("field").GetString())
            .ToList();
        Assert.Contains("externalReference", fields);
        Assert.Contains("customerName", fields);
        Assert.Contains("currency", fields);
        Assert.Contains("lines[0].quantity", fields);
    }

    [Fact]
    public async Task GetOrder_WithUnknownId_Returns404Envelope()
    {
        var response = await _client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("ORDER_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetOrder_WithMalformedId_Returns404Envelope()
    {
        var response = await _client.GetAsync("/api/v1/orders/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("ORDER_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetOrder_AfterCreation_ReturnsFullDetail()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/orders", ValidOrder("PO-API-GET"));
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/v1/orders/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("PO-API-GET", body.GetProperty("externalReference").GetString());
        Assert.Equal(2, body.GetProperty("lines").GetArrayLength());
        Assert.Equal(350.00m, body.GetProperty("subtotal").GetDecimal());
    }

    [Fact]
    public async Task ListOrders_ReturnsPagedEnvelope()
    {
        await _client.PostAsJsonAsync("/api/v1/orders", ValidOrder("PO-API-LIST"));

        var response = await _client.GetAsync("/api/v1/orders?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(5, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);
        Assert.True(body.GetProperty("items").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ListOrders_WithUnknownStatus_Returns400Envelope()
    {
        var response = await _client.GetAsync("/api/v1/orders?status=Shipped");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangeStatus_PendingToConfirmed_Returns200()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/orders", ValidOrder("PO-API-CONFIRM"));
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetString();

        var response = await _client.PatchAsJsonAsync($"/api/v1/orders/{id}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_Returns409EnvelopeWithMessage()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/orders", ValidOrder("PO-API-BADTRANS"));
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetString();

        var response = await _client.PatchAsJsonAsync($"/api/v1/orders/{id}/status", new { status = "Fulfilled" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("INVALID_STATUS_TRANSITION", body.GetProperty("code").GetString());
        Assert.Equal("Order cannot transition from Pending to Fulfilled.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ChangeStatus_UnknownStatusValue_Returns400Envelope()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/orders", ValidOrder("PO-API-BADSTATUS"));
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetString();

        var response = await _client.PatchAsJsonAsync($"/api/v1/orders/{id}/status", new { status = "Shipped" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangeStatus_OnUnknownOrder_Returns404Envelope()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/orders/{Guid.NewGuid()}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("ORDER_NOT_FOUND", body.GetProperty("code").GetString());
    }
}
