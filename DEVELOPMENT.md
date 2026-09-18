# Concorde MVP - Development Quick Start

## Building Locally

### 1. Restore Dependencies
```bash
cd c:\Users\27769\Documents\Nepo\Concorde
dotnet restore
```

### 2. Run Domain Tests (TDD Foundation)
```bash
dotnet test tests/Concorde.Domain.Tests -v normal
```

**Expected Results:**
- Total Tests: 78 tests across 5 test classes
- MoneyTests: 14 tests (decimal arithmetic, rounding, validation)
- CurrencyTests: 16 tests (ISO 4217 validation, case handling)
- OrderLineTests: 16 tests (line item creation, calculations, validation)
- OrderTests: 24 tests (order creation, lifecycle, status changes)
- OrderStatusTransitionPolicyTests: 8 tests (state machine transitions)

### 3. Run All Tests
```bash
dotnet test
```

### 4. Next: Build Persistence Layer
When you're ready to build the Application layer and persistence:

```bash
cd src/Concorde.Infrastructure
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## What's Been Implemented

### Domain Layer (src/Concorde.Domain/)

#### Common Folder
- **Money.cs** — Value object for monetary amounts
  - Uses `decimal` (never `double`) for precision
  - Supports banker's rounding (round-half-to-even)
  - Validates decimal places (max 2)
  - Validates amount range (0 to 1 billion)

- **Currency.cs** — Value object for ISO 4217 currency codes
  - Validates against recognized currency codes
  - Case-insensitive comparison
  - Always stored uppercase

- **DomainException.cs** — Base exception for domain invariants

#### Orders Folder
- **OrderLine.cs** — Line item entity
  - Validates SKU (1-50 chars), Name (1-200 chars)
  - Validates Quantity (1-1,000,000)
  - Calculates LineTotal = Quantity × UnitPrice
  - Applies banker's rounding

- **Order.cs** — Order aggregate root
  - Validates external reference (unique, 1-64 chars, alphanumeric + . _ -)
  - Normalizes external reference (uppercase)
  - Validates customer name (1-200 chars)
  - Optional customer code (1-50 chars)
  - Supports 1-100 line items
  - Optional notes (max 1000 chars)
  - Calculates Subtotal = Sum(line items)
  - Sets Total = Subtotal (MVP, no tax/discount/shipping)
  - Starts as Pending status
  - Supports status transitions via ChangeStatus()

- **OrderStatus.cs** — Status enumeration & transition policy
  - Pending → Confirmed, Cancelled
  - Confirmed → Fulfilled, Cancelled
  - Terminal states: Fulfilled, Cancelled (no further transitions)
  - Same-status transitions rejected

### Test Layer (tests/Concorde.Domain.Tests/)
Comprehensive TDD test suite covering all domain rules:
- Money arithmetic and rounding
- Currency validation
- Order line creation and validation
- Order lifecycle and status transitions
- State machine correctness

---

## Design Highlights

✅ **Fail-Fast Validation** — All constraints validated at construction time  
✅ **No External Dependencies** — Domain layer is pure C#, no frameworks  
✅ **Idempotency Ready** — External reference as natural business key  
✅ **Precision Money Handling** — Decimal arithmetic, banker's rounding  
✅ **State Machine** — Explicit transition policy, not implicit  
✅ **Comprehensive Tests** — TDD-first approach, 78 tests written  

---

## Next: Application Layer

The Application layer will contain:

```
Application/
├─ Orders/
│  ├─ Create/
│  │  ├─ CreateOrderCommand.cs
│  │  ├─ CreateOrderHandler.cs
│  │  └─ CreateOrderValidator.cs
│  ├─ Get/
│  │  ├─ GetOrderQuery.cs
│  │  └─ GetOrderHandler.cs
│  ├─ List/
│  │  ├─ ListOrdersQuery.cs
│  │  └─ ListOrdersHandler.cs
│  └─ ChangeStatus/
│     ├─ ChangeOrderStatusCommand.cs
│     └─ ChangeOrderStatusHandler.cs
├─ Validation/
│  └─ ValidationError.cs
└─ Idempotency/
   └─ IdempotencyService.cs
```

---

## Git Workflow

To commit these changes:

```bash
git add .
git commit -m "feat: implement domain layer with Money, Currency, OrderLine, Order, and comprehensive tests

- Add Money value object with decimal arithmetic and banker's rounding
- Add Currency value object with ISO 4217 validation
- Add OrderLine entity with line-item validation
- Add Order aggregate root with business rules
- Add OrderStatus enum and status transition policy
- Add 78 comprehensive TDD tests covering all domain invariants
- Ensure zero external dependencies in domain layer"
```

---

## Testing Commands

```bash
# Run specific test class
dotnet test tests/Concorde.Domain.Tests --filter "ClassName=MoneyTests"

# Run with coverage (requires coverlet)
dotnet test /p:CollectCoverage=true

# Watch mode (requires dotnet-watch)
dotnet watch test

# Verbose output
dotnet test -v detailed
```

---

## Architecture Verification

Domain layer follows Clean Architecture:

```
Domain (Concorde.Domain/)
├─ No references to: HTTP, ASP.NET Core, EF Core, Angular, frameworks
├─ Only references: System, System.Linq, System.Text.RegularExpressions
├─ Pure business logic: Money, Currency, Order, OrderLine, OrderStatus
└─ All invariants validated at construction (fail-fast)

Tests (Concorde.Domain.Tests/)
├─ No framework dependencies for domain logic testing
├─ xUnit for test runner only
├─ No mocks of domain objects (tests real behavior)
└─ 78 tests covering all rules
```

---

## Common Issues & Fixes

**Issue:** Tests don't run  
**Fix:** Ensure .NET SDK is installed: `dotnet --version`

**Issue:** Currency code validation too strict  
**Fix:** Currently validates against common ISO 4217 codes. Can expand HashSet in Currency.cs

**Issue:** Want to add custom currency code  
**Fix:** Add to ValidCurrencyCodes HashSet in Currency.cs, no changes to domain logic needed

---

## References

- [Concorde Product Backlog](docs/ORDER_INTAKE_USER_STORIES.md) — User stories & acceptance criteria
- [SOLUTION.md](SOLUTION.md) — Architecture & design decisions  
- [CONCORDE_BUILD_BLUEPRINT.md](CONCORDE_BUILD_BLUEPRINT.md) — Comprehensive engineering principles
- [Domain-Driven Design](https://martinfowler.com/bliki/DomainDrivenDesign.html)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
