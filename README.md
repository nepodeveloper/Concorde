# Concorde — Order Intake & Lifecycle Platform

A production-minded order intake and tracking system demonstrating senior engineering principles: clean architecture, TDD, idempotency, domain-driven design, and enterprise-ready error handling.

## Quick Start

### Prerequisites
- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/en-us/download)
- **Node.js 20+** — [download](https://nodejs.org)
- **Git**
- A code editor (VS Code recommended)

### Backend Setup

```bash
cd src/Concorde.Api

# Restore dependencies
dotnet restore

# Create and migrate database
dotnet ef database update

# Run the API (http://localhost:5000)
dotnet run
```

OpenAPI/Swagger available at `http://localhost:5000/swagger`

### Frontend Setup

```bash
cd web/concorde-web

# Install dependencies
npm install

# Run development server (http://localhost:4200)
npm start
```

The app expects the API at `http://localhost:5000` (CORS is pre-configured for the Angular dev origin).

### Run Tests

```bash
# All tests
dotnet test

# Domain tests only (TDD focus)
dotnet test tests/Concorde.Domain.Tests

# Application tests
dotnet test tests/Concorde.Application.Tests

# Integration tests
dotnet test tests/Concorde.IntegrationTests
```

---

## Project Structure

```
Concorde/
│
├─ src/
│  ├─ Concorde.Domain/              # Core business rules (no frameworks)
│  ├─ Concorde.Application/         # Use cases & commands
│  ├─ Concorde.Infrastructure/      # EF Core, repositories, database
│  └─ Concorde.Api/                 # ASP.NET Core controllers & middleware
│
├─ tests/
│  ├─ Concorde.Domain.Tests/        # Unit tests for business rules
│  ├─ Concorde.Application.Tests/   # Application service tests
│  └─ Concorde.IntegrationTests/    # Full API + database tests
│
├─ web/
│  └─ concorde-web/                 # Angular 22 frontend (signals, standalone components)
│
├─ docs/
│  └─ ORDER_INTAKE_USER_STORIES.md  # Product backlog & acceptance criteria
│
├─ .github/
│  └─ workflows/                    # CI/CD pipelines (planned)
│
├─ README.md                        # This file
├─ SOLUTION.md                      # Architecture & design decisions
├─ CONCORDE_BUILD_BLUEPRINT.md      # Comprehensive engineering blueprint
└─ Concorde.sln                     # .NET solution file
```

---

## Architecture

### Clean Architecture with Vertical Slices

```
API (Controllers)
   ↓
Application (Commands/Queries)
   ↓
Domain (Entities/Rules)
   
Infrastructure → Database
```

**Key Principle:** Domain has **zero framework dependencies** — it's pure business logic.

### Layers

| Layer | Purpose | Dependencies |
|---|---|---|
| **Domain** | Business rules, entities, value objects | None (pure C#) |
| **Application** | Use cases, command/query handlers, validation | Domain |
| **Infrastructure** | Database access, EF Core mappings | Domain, Application |
| **API** | HTTP controllers, error handling, middleware | All |

---

## Key Features

✓ **Duplicate Prevention** — External reference uniqueness enforced at database & application level  
✓ **Idempotency** — Safe retries without creating duplicate orders  
✓ **Server-Side Totals** — All calculations happen on the server (untrusted client input)  
✓ **Status Lifecycle** — State machine validates all transitions  
✓ **Consistent Errors** — ProblemDetails envelope with field-level validation feedback  
✓ **Decimal Arithmetic** — No floating-point precision loss for money  
✓ **TDD-First** — Domain rules covered by unit tests  
✓ **Concurrency-Safe** — Handles concurrent duplicate submissions correctly  

---

## User Stories (MVP)

### US-01: Submit a Purchase Order
Create orders with external reference, customer, currency, and line items.

### US-02: Prevent Duplicate Orders
Safe handling of repeated submissions using external reference as idempotency key.

### US-03: Validate Order Data
Comprehensive validation with field-level error feedback.

### US-04: Calculate Order Totals
Server calculates all totals (client values ignored).

### US-05: View Orders
List orders (paginated, sorted newest first) or retrieve by ID.

### US-06: View Order Details
Full order details with line items and status history.

### US-07: Change Order Status
Status transitions through defined lifecycle (Pending → Confirmed → Fulfilled/Cancelled).

### US-08: Provide Consistent User Feedback
RFC 7807 ProblemDetails envelope for all errors.

See [ORDER_INTAKE_USER_STORIES.md](docs/ORDER_INTAKE_USER_STORIES.md) for detailed acceptance criteria and TDD test specifications.

---

## API Endpoints

| Method | Path | Purpose |
|---|---|---|
| **POST** | `/api/v1/orders` | Create order |
| **GET** | `/api/v1/orders` | List orders (paginated) |
| **GET** | `/api/v1/orders/{id}` | Get order by ID |
| **PATCH** | `/api/v1/orders/{id}/status` | Change order status |
| **GET** | `/health` | Health check |

**Example Request:**

```bash
curl -X POST http://localhost:5000/api/v1/orders \
  -H "Content-Type: application/json" \
  -d '{
    "externalReference": "PO-2026-00123",
    "customerName": "Acme Corp",
    "customerCode": "ACME-001",
    "currency": "ZAR",
    "lines": [
      {
        "sku": "SKU-A",
        "name": "Product A",
        "quantity": 2,
        "unitPrice": 100.00
      }
    ],
    "notes": "Deliver before month end"
  }'
```

**Example Response (201 Created):**

```json
{
  "id": "b56016ac-cc4c-49e1-a667-42c72e4a76cf",
  "externalReference": "PO-2026-00123",
  "customerName": "Acme Corp",
  "customerCode": "ACME-001",
  "currency": "ZAR",
  "status": "Pending",
  "subtotal": 200.00,
  "total": 200.00,
  "lines": [
    {
      "sku": "SKU-A",
      "name": "Product A",
      "quantity": 2,
      "unitPrice": 100.00,
      "lineTotal": 200.00
    }
  ],
  "createdAtUtc": "2026-09-18T12:00:00Z",
  "updatedAtUtc": "2026-09-18T12:00:00Z"
}
```

---

## Error Handling

All errors return RFC 7807 `ProblemDetails`:

```json
{
  "type": "https://concorde/problems/validation-failed",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "See errors property for details.",
  "traceId": "00-abc123...",
  "errors": {
    "customerName": ["Customer name is required."],
    "lines[0].quantity": ["Quantity must be greater than zero."]
  }
}
```

### HTTP Status Codes

- **201 Created** — Order successfully created
- **200 OK** — Successful read or idempotent replay
- **400 Bad Request** — Validation failure
- **404 Not Found** — Order not found
- **409 Conflict** — Invalid status transition or reference conflict
- **500 Internal Error** — Unexpected server error (no stack trace exposed)

---

## Development Notes

### Testing Approach

Tests are organized by layer:

1. **Domain Tests** — Pure business logic, no database, no mocks
2. **Application Tests** — Use cases with repository mocks
3. **Integration Tests** — Full API + real SQLite database

Example domain test:

```csharp
[Fact]
public void OrderLine_WithZeroQuantity_Fails()
{
    // Arrange & Act & Assert
    var ex = Assert.Throws<ArgumentException>(() =>
        new OrderLine("SKU-A", "Product A", quantity: 0, unitPrice: 100m));
    
    Assert.Contains("Quantity must be greater than zero", ex.Message);
}
```

### Database

**SQLite file:** `src/Concorde.Api/concorde.db`

**Migrations:**

```bash
cd src/Concorde.Infrastructure

# Create migration
dotnet ef migrations add AddOrdersTable

# Apply migration
cd ../Concorde.Api
dotnet ef database update
```

### Debugging

Set breakpoints in Visual Studio or VS Code and run:

```bash
dotnet run
```

API will pause at breakpoints. OpenAPI/Swagger provides interactive testing.

---

## Known Limitations (MVP)

- ❌ **No authentication** — Any user can perform any action
- ❌ **No authorization** — No role-based access control
- ❌ **No tax/discounts** — Total = Subtotal always
- ❌ **No async processing** — Synchronous only
- ❌ **Synchronous only** — No background jobs or messaging

See [SOLUTION.md](SOLUTION.md) for roadmap to enterprise features.

---

## Future Enhancements

- [ ] Azure SQL Server backend
- [ ] Microsoft Entra ID + OAuth 2.0 authentication
- [ ] Role-based authorization
- [ ] Azure Service Bus for async processing
- [ ] Order status history & audit logging
- [ ] Customer master data integration
- [ ] Tax & discount calculations
- [ ] Webhook integrations
- [ ] Azure Application Insights observability
- [ ] Rate limiting & throttling
- [ ] Containerization (Docker)

---

## Contributing

This project demonstrates TDD and clean architecture principles. When adding features:

1. Write tests first (TDD)
2. Implement domain rules
3. Add application use cases
4. Build persistence layer
5. Expose via API

All commits must:
- ✓ Pass all tests
- ✓ Maintain domain layer independence
- ✓ Follow C# style guide
- ✓ Include inline documentation for complex logic

---

## References

- [ORDER_INTAKE_USER_STORIES.md](docs/ORDER_INTAKE_USER_STORIES.md) — Product backlog
- [SOLUTION.md](SOLUTION.md) — Architecture & design decisions
- [CONCORDE_BUILD_BLUEPRINT.md](CONCORDE_BUILD_BLUEPRINT.md) — Engineering principles
- [.NET 10 Docs](https://learn.microsoft.com/en-us/dotnet/)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [RFC 7807 Problem Details](https://tools.ietf.org/html/rfc7807)
