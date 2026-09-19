using System.Data;
using System.Text.Json.Serialization;
using Concorde.Api.Endpoints;
using Concorde.Api.Middleware;
using Concorde.Application.Orders.ChangeStatus;
using Concorde.Application.Orders.Create;
using Concorde.Application.Orders.Get;
using Concorde.Application.Orders.List;
using Concorde.Application.Orders.Update;
using Concorde.Infrastructure;
using Concorde.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

const string AngularDevCorsPolicy = "AngularDev";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Concorde") ?? "DataSource=concorde.db");

builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddScoped<GetOrderHandler>();
builder.Services.AddScoped<ListOrdersHandler>();
builder.Services.AddScoped<ChangeOrderStatusHandler>();
builder.Services.AddScoped<UpdateOrderHandler>();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// NFR-05: CORS restricted to the Angular dev origin in Development.
builder.Services.AddCors(options =>
    options.AddPolicy(AngularDevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

// MVP schema management: EnsureCreated instead of migrations (documented in SOLUTION.md).
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ConcordeDbContext>();
    dbContext.Database.EnsureCreated();
    EnsureStatusReasonColumnExists(dbContext);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // NFR-04: OpenAPI in Development at /openapi/v1.json
    app.UseCors(AngularDevCorsPolicy);
}

app.MapOrderEndpoints();

app.MapGet("/health", () => TypedResults.Ok(new { status = "Healthy" }))
    .WithName("HealthCheck")
    .WithSummary("Health check");

app.Run();

static void EnsureStatusReasonColumnExists(ConcordeDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != ConnectionState.Open;

    if (shouldClose)
    {
        connection.Open();
    }

    try
    {
        using var tableInfoCommand = connection.CreateCommand();
        tableInfoCommand.CommandText = "PRAGMA table_info('Orders');";

        using var reader = tableInfoCommand.ExecuteReader();
        var hasStatusReasonColumn = false;

        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), "StatusReason", StringComparison.OrdinalIgnoreCase))
            {
                hasStatusReasonColumn = true;
                break;
            }
        }

        if (!hasStatusReasonColumn)
        {
            using var alterTableCommand = connection.CreateCommand();
            alterTableCommand.CommandText = "ALTER TABLE Orders ADD COLUMN StatusReason TEXT NULL;";
            try
            {
                alterTableCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name: StatusReason", StringComparison.OrdinalIgnoreCase))
            {
                // Another startup path added the column concurrently.
            }
        }
    }
    finally
    {
        if (shouldClose)
        {
            connection.Close();
        }
    }
}

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program { }
