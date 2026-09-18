# Concorde Solution Design & Implementation

**Project:** Concorde — Order Intake & Lifecycle Platform  
**Date:** 2026-09-18  
**Status:** MVP Development  
**Architecture:** Clean Architecture + Vertical Slice + Modular Monolith

---

## 1. Overview

Concorde is a production-minded order intake and tracking platform that demonstrates senior engineering principles applied to a small but non-trivial domain problem:

- Sales representatives submit customer purchase orders
- The system prevents accidental duplicate submissions
- Orders are tracked through a lifecycle (Pending → Confirmed → Fulfilled or Cancelled)
- All calculations happen server-side
- Domain invariants are enforced at multiple layers

### Key Business Invariant

**The same client-provided external order reference must never create duplicate orders, even under concurrent submission.**

This invariant is enforced through:
1. Unique constraint on the `ExternalReference` (normalized/upper-cased)
2. Application-level idempotency check
3. Request fingerprint to detect payload conflicts
4. Transaction rollback on constraint violations

---

## 2. Technology Stack

### Backend
- **.NET 10** with ASP.NET Core
- **C# 12+**
- **EF Core 10** for data access
- **SQLite** for local development (Azure SQL for production)
- **xUnit** for testing
- **FluentValidation** for declarative validation

### Frontend
- **Angular 22+** with standalone components
- **TypeScript**
- **RxJS** for async streams
- **Signals & Computed Signals** for reactive state

### Infrastructure (MVP)
- Local Git repository
- SQLite database file
- Single-tier deployment

---

## 3. Architecture Layers

### Dependency Flow

```
API (Controllers)
   ↓
Application (Use Cases / Commands / Queries)
   ↓
Domain (Rules / Entities / Value Objects)
   
Infrastructure → Application, Domain
```

**Key Constraint:** The Domain layer has **zero dependencies** on external frameworks. It knows nothing about:
- HTTP / REST
- ASP.NET Core
- EF Core
- Databases
- Angular
- Logging frameworks

### Layer Responsibilities

#### Domain (`Concorde.Domain`)
- **Order** entity with status lifecycle
- **OrderLine** entity
- **Money** value object (decimal, no float)
- **Currency** value object with ISO 4217 validation
- **OrderStatus** enumeration
- **OrderStatusTransitionPolicy** state machine
- Domain exceptions

#### Application (`Concorde.Application`)
- **CreateOrder** command
- **GetOrder** query
- **GetOrders** query (paginated)
- **ChangeOrderStatus** command
- Application-level validators
- Idempotency logic
- Use case orchestration

#### Infrastructure (`Concorde.Infrastructure`)
- **EF Core DbContext**
- **OrderRepository** implementation
- Database migrations
- Persistence mappings
- Connection string management

#### API (`Concorde.Api`)
- **OrdersController**
- **ExceptionMiddleware** (error envelope mapping)
- **Dependency Injection** configuration
- **OpenAPI/Swagger** setup
- CORS configuration
- Health checks

---

## 4. Core Domain Rules

These rules are enforced in the Domain layer regardless of the caller:

| Rule | Enforcement |
|---|---|
| New orders start as `Pending` | Order constructor |
| Quantity > 0 | OrderLine validation |
| Quantity is whole number | OrderLine validation |
| Unit price ≥ 0 | OrderLine validation |
| Unit price has ≤ 2 decimal places | OrderLine validation |
| Line total = Qty × UnitPrice | OrderLine calculation |
| Order subtotal = Σ line totals | Order calculation |
| Total = Subtotal (no tax/discount/shipping in MVP) | Order calculation |
| All money is `decimal` (never `float`) | Money value object |
| Status transitions follow the state machine | OrderStatusTransitionPolicy |
| External reference is required & unique | Order + unique constraint |
| At least 1 line item required | Order validation |
| Currency must be valid ISO 4217 | Currency validation |

---

## 5. Idempotency Strategy

### Business Idempotency: External Reference

The client provides an `externalReference` (e.g. "PO-10001"). This is the **business idempotency key**.

**Behavior:**

| Scenario | HTTP | Result |
|---|---|---|
| First submission of "PO-10001" | 201 Created | New order created |
| Resubmit "PO-10001" + same payload | 200 OK | Existing order returned |
| Resubmit "PO-10001" + different payload | 409 Conflict | Error: Reference already used with different data |

### Database-Level Protection

```sql
CREATE UNIQUE INDEX UX_Orders_ExternalReference
ON Orders(UPPER(ExternalReference));
```

Comparison is **case-insensitive** per CC-01.

### Race Condition Handling

```
Thread A: Check → not found
Thread B: Check → not found
Thread A: Insert ✓
Thread B: Insert ✗ (unique constraint)
       ↓
Application re-reads and returns Thread A's order
```

This is why the database constraint is essential — application-only checks race.

---

## 6. Order Status Lifecycle

```
Pending
├─→ Confirmed
│   ├─→ Fulfilled (terminal)
│   └─→ Cancelled (terminal)
└─→ Cancelled (terminal)

Any state
└─→ Same state = invalid (409)

Terminal states
└─→ Any other = invalid (409)
```

**Transitions are validated server-side.** The UI only displays valid options for the current status, but the API is the source of truth.

---

## 7. API Error Handling

All non-2xx responses use a consistent `ProblemDetails` envelope:

```json
{
  "type": "https://concorde/problems/validation-failed",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "See details array for field-level errors.",
  "instance": "POST /api/v1/orders",
  "traceId": "00-abc123def456-abc123def456-00",
  "errors": {
    "customerName": ["Customer name is required."],
    "lines[0].quantity": ["Quantity must be greater than zero."]
  }
}
```

### HTTP Status Mapping

| Status | Scenario | Code |
|---|---|---|
| **400** | Validation failure | `VALIDATION_FAILED` |
| **404** | Order not found | `ORDER_NOT_FOUND` |
| **409** | Invalid status transition | `INVALID_STATUS_TRANSITION` |
| **409** | Duplicate reference + different payload | `ORDER_REFERENCE_CONFLICT` |
| **412** | Optimistic concurrency conflict (future) | `CONCURRENCY_CONFLICT` |
| **500** | Unexpected exception | `INTERNAL_ERROR` (no stack trace) |

**Server does not expose:**
- Stack traces
- Exception messages
- Implementation details

All errors include a `traceId` for log correlation.

---

## 8. Persistence Model

### Order Table

| Column | Type | Constraints |
|---|---|---|
| `Id` | GUID | Primary key |
| `ExternalReference` | string(64) | Unique (case-insensitive) |
| `RequestFingerprint` | string | For conflict detection |
| `CustomerName` | string(200) | Required |
| `CustomerCode` | string(50) | Nullable |
| `Currency` | char(3) | Required, uppercase |
| `Notes` | string(1000) | Nullable |
| `Status` | varchar(20) | Required |
| `Subtotal` | decimal(19, 2) | Required |
| `Total` | decimal(19, 2) | Required |
| `CreatedAtUtc` | datetime2 | Required, UTC |
| `UpdatedAtUtc` | datetime2 | Required, UTC |
| `RowVersion` | rowversion | For optimistic concurrency |

### OrderLine Table

| Column | Type | Constraints |
|---|---|---|
| `Id` | GUID | Primary key |
| `OrderId` | GUID | Foreign key → Order |
| `Sku` | string(50) | Required |
| `Name` | string(200) | Required |
| `Quantity` | int | Required, > 0 |
| `UnitPrice` | decimal(19, 2) | Required, ≥ 0 |
| `LineTotal` | decimal(19, 2) | Calculated, persisted |

### OrderStatusHistory Table (Future)

| Column | Type |
|---|---|
| `Id` | GUID |
| `OrderId` | GUID |
| `FromStatus` | varchar(20) |
| `ToStatus` | varchar(20) |
| `ChangedAtUtc` | datetime2 |
| `ChangedBy` | string | (future: authenticated user) |

---

## 9. Known Trade-Offs & Future Enhancements

### MVP Limitations (Documented in Acceptance Criteria)

| Limitation | Reason | Future Evolution |
|---|---|---|
| No authentication | Assessment scope | Microsoft Entra ID + OAuth 2.0 |
| No authorization | All users can perform all actions | Role-based policies |
| No tax/discount/shipping | Simplifies initial domain | Extensible adjustment model |
| No customer master | Lightweight snapshot in order | Separate Customer service + integration |
| No order timeline/comments | Scope control | Activity/audit log + comments table |
| Synchronous only | MVP focus | Outbox + Service Bus for async side-effects |
| SQLite | Local development | Azure SQL for production |

### Design Preserved for Enterprise Evolution

✓ Unique constraint prevents duplicate orders at scale  
✓ Decimal arithmetic is precise  
✓ Idempotency at business and request levels  
✓ Status transitions are explicit & validated  
✓ Error envelope supports detailed diagnostics  
✓ Domain has no framework dependencies (testable in isolation)  
✓ Vertical slice structure supports feature isolation  
✓ Persistence layer is swappable (IOrderRepository)  

---

## 10. Testing Strategy

### Unit Tests (Domain)
- **Concorde.Domain.Tests** — Business rules, state transitions, value objects
- **Coverage:** All domain invariants
- **No mocks** — pure behavior testing

### Application Tests (Use Cases)
- **Concorde.Application.Tests** — Command handlers, idempotency, validation
- **Coverage:** Use case orchestration
- **Mock repositories** where needed

### Integration Tests (API + Database)
- **Concorde.IntegrationTests** — Full request/response cycles, database persistence
- **Real SQLite database** per test
- **WebApplicationFactory** for API testing
- **Coverage:** Duplicate prevention, concurrent submissions, error mapping

### TDD Sequence (Recommended)

1. Write OrderLine domain tests → implement OrderLine
2. Write Order domain tests → implement Order
3. Write OrderStatus & state machine tests → implement policies
4. Write CreateOrder use case tests → implement handler
5. Write idempotency tests → implement duplicate prevention
6. Write persistence tests → implement EF mappings
7. Write API integration tests → implement controller
8. Build Angular UI last

---

## 11. Development Workflow

### Local Setup

```bash
# Backend
cd src/Concorde.Api
dotnet restore
dotnet ef database update
dotnet run

# Frontend (when ready)
cd web/concorde-web
npm install
npm start
```

### Testing

```bash
# All tests
dotnet test

# Specific test project
dotnet test tests/Concorde.Domain.Tests

# With coverage
dotnet test /p:CollectCoverage=true
```

### Database

SQLite database file: `Concorde.Api/concorde.db`

Migrations tracked in `Concorde.Infrastructure/Migrations/`

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## 12. Security Considerations

### Current MVP

- **No authentication** — any user can perform any action (documented trade-off)
- **No authorization** — roles not yet defined
- **CORS restricted** to Angular dev origin in Development
- **HTTPS required** in Production
- **Input validation** at API boundary
- **Server-calculated totals** (untrusted client input ignored)
- **No secrets in Git** (.gitignore includes config files)

### Production Roadmap

- Microsoft Entra ID + OAuth 2.0
- Role-based access control
- Policy-based authorization (CanCreateOrder, CanViewOrder, etc.)
- Backend-for-Frontend pattern (BFF)
- Managed Identity for Azure resources
- Azure Key Vault for secrets
- Audit logging for compliance
- Rate limiting
- WAF / Front Door

---

## 13. Observability & Monitoring

### Structured Logging

```csharp
logger.LogInformation(
    "Order {OrderId} created for external reference {ExternalReference}",
    orderId, externalReference);
```

**Never log:**
- Customer credit cards
- Full customer names in some contexts
- Secrets / connection strings
- Complete payloads

### Traces & Correlation

Every request includes a `traceId` in responses.

**Future:** OpenTelemetry → Application Insights

### Business Metrics (Future)

```
orders.created
orders.duplicate_detected
orders.reference_conflict
orders.status_changed
orders.status_change_rejected
```

---

## 14. Deployment Architecture

### MVP (Current)

```
Developer Workstation
    |
    ├─ dotnet run (API on http://localhost:5000)
    ├─ npm start (Angular on http://localhost:4200)
    └─ SQLite (concorde.db)
```

### Production (Roadmap)

```
GitHub (CI/CD)
    ↓
[Azure Container Registry]
    ↓
[Azure Container Apps] - API
    ├─ [Azure SQL] - persistent storage
    ├─ [Key Vault] - secrets
    ├─ [Application Insights] - observability
    └─ [Azure Front Door] - CDN + WAF
       ↓
[Azure Static Web Apps] - Angular frontend
```

---

## 15. Next Steps

**Priority order (TDD-first):**

1. ✅ Project structure created
2. ⏳ **Create Money & Currency value objects** (next)
3. ⏳ **Create OrderLine entity** with tests
4. ⏳ **Create Order entity** with tests
5. ⏳ **Create status transition policy** with tests
6. ⏳ Implement CreateOrder use case
7. ⏳ Implement idempotency layer
8. ⏳ Implement EF Core persistence
9. ⏳ Implement API controllers & error handling
10. ⏳ Build Angular UI

---

## 16. Reference Documents

- **ORDER_INTAKE_USER_STORIES.md** — User stories, acceptance criteria, TDD tests
- **CONCORDE_BUILD_BLUEPRINT.md** — Comprehensive architecture & design patterns
- **README.md** — Setup instructions & quick start
- **This file (SOLUTION.md)** — Design decisions & trade-offs
