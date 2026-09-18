# Order Intake & Tracking — Product Backlog

## Purpose

This document translates the technical assignment into a development-ready backlog that can be used from a **Business Analyst**, **Product Owner**, and **TDD** perspective.

The goal is to build a small internal application that allows sales users to:

- Submit customer purchase orders
- Prevent accidental duplicate submissions
- Validate order data
- Calculate order totals on the server
- List and retrieve submitted orders
- View order details
- Track order status
- Receive clear validation and domain error feedback

---

# Glossary & Cross-Cutting Decisions

These decisions apply to every story below and are referenced by ID.

| ID | Decision |
|---|---|
| CC-01 | **External Reference** is a client-supplied string, required, trimmed, 1–64 characters, matching `^[A-Za-z0-9._-]+$`. Comparison for uniqueness is **case-insensitive**. |
| CC-02 | **Customer** is captured as a required `customerName` (1–200 characters) and an optional `customerCode` (1–50 characters). No separate customer master exists in this MVP. |
| CC-03 | **Currency** is a required ISO 4217 alpha-3 code (e.g. `ZAR`, `USD`, `EUR`), stored upper-cased. Unknown codes are rejected with `INVALID_CURRENCY`. |
| CC-04 | **Notes** is optional, maximum 1000 characters. |
| CC-05 | **SKU** is required, trimmed, 1–50 characters. **Item name** is required, trimmed, 1–200 characters. |
| CC-06 | **Quantity** is a whole number in the range 1–1,000,000. |
| CC-07 | **Unit price** is a decimal ≥ 0 with at most **2 decimal places** and a maximum of 1,000,000,000.00. |
| CC-08 | All money is represented as `decimal` (never `float`/`double`). Line totals and order totals are rounded to 2 decimal places using **round-half-to-even** (banker's rounding). |
| CC-09 | **Subtotal** and **Total** are both persisted. In this MVP `Total = Subtotal` — no tax, discount, or shipping. The two fields are kept separate so future adjustments do not require a schema change. |
| CC-10 | All timestamps are stored and returned in **UTC** as ISO 8601 strings (e.g. `2026-09-18T10:05:00Z`). The UI formats them in the user's local time zone. |
| CC-11 | **Order ID** is a server-generated GUID. It is the only key used in API routes. |
| CC-12 | Orders are **never deleted**. `Cancelled` is the only way to retire an order. |
| CC-13 | The MVP has **no authentication or authorization**. Any user may perform any action. This is called out as a known trade-off in SOLUTION.md. |
| CC-14 | HTTP status mapping for errors: `400 VALIDATION_FAILED`, `404 ORDER_NOT_FOUND`, `409 INVALID_STATUS_TRANSITION`, `500 INTERNAL_ERROR`. See US-08. |

---

# Epic: Order Intake & Tracking

## Business Objective

Provide sales representatives with a reliable way to capture and track customer purchase orders while preventing accidental duplication and ensuring consistent order data.

---

# US-01 — Submit a Purchase Order

## User Story

**As a Sales Representative,**  
I want to submit a customer's purchase order  
so that the order can be recorded and tracked through the sales process.

## Functional Requirements

### FR-01.1
The system shall allow a user to create an order containing:

- External reference (CC-01)
- Customer name and optional customer code (CC-02)
- Currency (CC-03)
- One or more line items (maximum 100 per order)
- Optional notes (CC-04)

### FR-01.2
Each line item shall contain:

- SKU (CC-05)
- Product/item name (CC-05)
- Quantity (CC-06)
- Unit price (CC-07)

### FR-01.3
The system shall generate internal order metadata including:

- Order ID (CC-11)
- Created timestamp in UTC (CC-10)
- Last-updated timestamp in UTC (CC-10)
- Current status

### FR-01.4
Newly created orders shall initially be assigned the `Pending` status.

> **Product Decision:** The assignment provides example statuses but leaves the lifecycle design to the implementation. `Pending` is used as the initial status for this solution.

### FR-01.5
The API shall respond with `201 Created` and a `Location` header pointing to the new order.

## Acceptance Criteria

### AC-01.1 — Successful Order Submission

```gherkin
Given a valid customer name
And a unique external reference
And a valid ISO 4217 currency code
And at least one valid line item
When the user submits the order
Then the system creates the order
And assigns an internal order identifier
And records the creation timestamp in UTC
And sets the order status to Pending
And returns 201 Created with the created order
```

### AC-01.2 — Missing External Reference

```gherkin
Given an order without an external reference
When the order is submitted
Then the order must not be created
And the response is 400 with code VALIDATION_FAILED
And the details identify the field "externalReference"
```

### AC-01.3 — No Line Items

```gherkin
Given an order containing no line items
When the order is submitted
Then the order must not be created
And the response is 400 with code VALIDATION_FAILED
And the details identify the field "lines" with message "At least one line item is required."
```

### AC-01.4 — Missing Customer Name

```gherkin
Given an order with an empty customer name
When the order is submitted
Then the order must not be created
And the details identify the field "customerName"
```

## Suggested TDD Tests

```text
CreateOrder_WithValidRequest_CreatesOrder
CreateOrder_SetsStatusToPending
CreateOrder_GeneratesOrderId
CreateOrder_RecordsCreatedTimestampInUtc
CreateOrder_WithoutExternalReference_ReturnsValidationError
CreateOrder_WithoutLineItems_ReturnsValidationError
CreateOrder_WithoutCustomerName_ReturnsValidationError
CreateOrder_WithMoreThan100Lines_ReturnsValidationError
```

---

# US-02 — Prevent Duplicate Orders

## User Story

**As a Sales Representative,**  
I want repeated submissions of the same customer order to be handled safely  
so that accidentally submitting the same order again does not create duplicate orders.

## Functional Requirements

### FR-02.1
The client-provided external reference shall act as the business identifier used to detect duplicate order submissions. Matching is case-insensitive (CC-01).

### FR-02.2
The system shall check whether an order already exists for the supplied external reference before creating another order.

### FR-02.3
If an order already exists for the supplied external reference, a second order record shall not be created. The existing order is returned with `200 OK` (not `201 Created`).

### FR-02.4
Repeated submissions shall produce consistent behaviour from the user's perspective: the same order ID is returned every time.

### FR-02.5
Duplicate prevention shall be enforced on the server and shall not depend on the Angular application.

### FR-02.6
The persistence layer shall enforce a unique index on the normalised (upper-cased) external reference to protect against race conditions. If the unique-constraint violation is raised on insert, the application shall re-read and return the existing order rather than surface the error.

### FR-02.7
The duplicate check does **not** compare payload contents. A repeated external reference with different line items is still treated as a duplicate and the original order is returned unchanged.

> **Product Decision:** Payload-mismatch detection (returning `409` if the second submission differs) is deferred. In this MVP the external reference alone is authoritative.

## Acceptance Criteria

### AC-02.1 — Unique Reference Creates Order

```gherkin
Given no order exists with external reference "PO-10001"
When an order with external reference "PO-10001" is submitted
Then exactly one order is created
And the response is 201 Created
```

### AC-02.2 — Duplicate Reference Does Not Create Another Order

```gherkin
Given an order already exists with external reference "PO-10001"
When the same order is submitted again
Then another order must not be created
And the existing order is returned with 200 OK
And the returned order ID matches the original
```

### AC-02.3 — Case-Insensitive Match

```gherkin
Given an order already exists with external reference "PO-10001"
When an order with external reference "po-10001" is submitted
Then another order must not be created
And the existing order is returned
```

### AC-02.4 — Concurrent Repeated Submissions

```gherkin
Given the same request is submitted 10 times concurrently
When all submissions complete
Then only one order exists for that external reference
And every response returns the same order ID
```

## Suggested TDD Tests

```text
CreateOrder_WithUniqueReference_CreatesOrder
CreateOrder_WithExistingReference_DoesNotCreateDuplicate
CreateOrder_WithExistingReferenceDifferentCase_DoesNotCreateDuplicate
CreateOrder_RepeatedRequest_ReturnsExistingOrder
CreateOrder_RepeatedRequest_ReturnsSameOrderId
CreateOrder_ConcurrentRequests_ProducesSingleOrder
```

---

# US-03 — Validate Order Data

## User Story

**As a Sales Representative,**  
I want invalid purchase-order information to be rejected with clear feedback  
so that incorrect orders are not introduced into the system.

## Functional Requirements

### FR-03.1
Quantity must be a positive whole number greater than zero and no greater than 1,000,000 (CC-06).

### FR-03.2
Unit price must be greater than or equal to zero, have at most 2 decimal places, and not exceed 1,000,000,000.00 (CC-07).

### FR-03.3
Required fields — external reference, customer name, currency, SKU, item name — must be present and non-blank after trimming, and must respect the length limits in CC-01 to CC-05.

### FR-03.4
Currency must be a recognised ISO 4217 alpha-3 code (CC-03).

### FR-03.5
Invalid requests shall not create or modify an order.

### FR-03.6
Validation shall report **all** failures in one response, not just the first. Each failure identifies the field path (e.g. `lines[1].quantity`) and the rule violated.

## Acceptance Criteria

### AC-03.1 — Zero Quantity

```gherkin
Given a line item has quantity 0
When the order is submitted
Then the order is rejected with 400 VALIDATION_FAILED
And the details include field "lines[0].quantity" with message "Quantity must be greater than zero."
```

### AC-03.2 — Negative Quantity

```gherkin
Given a line item has quantity -1
When the order is submitted
Then the order is rejected with 400 VALIDATION_FAILED
```

### AC-03.3 — Fractional Quantity

```gherkin
Given a line item has quantity 1.5
When the order is submitted
Then the order is rejected with 400 VALIDATION_FAILED
And the details include field "lines[0].quantity" with message "Quantity must be a whole number."
```

### AC-03.4 — Negative Unit Price

```gherkin
Given a line item has a negative unit price
When the order is submitted
Then the order is rejected with 400 VALIDATION_FAILED
And no order is persisted
```

### AC-03.5 — Too Many Decimal Places

```gherkin
Given a line item has unit price 10.005
When the order is submitted
Then the order is rejected with 400 VALIDATION_FAILED
And the details include field "lines[0].unitPrice" with message "Unit price may have at most 2 decimal places."
```

### AC-03.6 — Invalid Currency

```gherkin
Given an order with currency "XXX1"
When the order is submitted
Then the order is rejected with 400 VALIDATION_FAILED
And the details include field "currency"
```

### AC-03.7 — Multiple Errors Reported Together

```gherkin
Given an order with an empty customer name
And a line item with quantity 0
When the order is submitted
Then the order is rejected with 400 VALIDATION_FAILED
And the details include both "customerName" and "lines[0].quantity"
```

## Suggested TDD Tests

```text
CreateOrder_WithZeroQuantity_Fails
CreateOrder_WithNegativeQuantity_Fails
CreateOrder_WithFractionalQuantity_Fails
CreateOrder_WithQuantityAboveMax_Fails
CreateOrder_WithNegativePrice_Fails
CreateOrder_WithPriceMoreThanTwoDecimals_Fails
CreateOrder_WithBlankSku_Fails
CreateOrder_WithInvalidCurrency_Fails
CreateOrder_WithMultipleErrors_ReportsAll
CreateOrder_WithValidLineItems_Succeeds
```

---

# US-04 — Calculate Order Totals

## User Story

**As a Sales Representative,**  
I want the system to calculate order totals automatically  
so that the financial value of the order is accurate and cannot be manipulated by the client.

## Functional Requirements

### FR-04.1

```text
Line Total = Round(Quantity × Unit Price, 2, HalfToEven)
```

### FR-04.2

```text
Subtotal = Sum(Line Totals)
Total    = Subtotal          (no tax/discount/shipping in MVP — see CC-09)
```

### FR-04.3
The server shall calculate all financial totals.

### FR-04.4
Any `lineTotal`, `subtotal`, or `total` fields present in the request body shall be ignored. They are not part of the request contract and the API shall not fail when they are present.

### FR-04.5
Monetary calculations shall use `decimal` (CC-08). Calculations shall not overflow within the bounds defined in CC-06 and CC-07 (max line total 10¹⁵, max order total 10¹⁷ — both within `decimal` range).

## Acceptance Criteria

### AC-04.1 — Correct Line and Order Totals

```gherkin
Given an order contains:
  SKU-A quantity 2 at 100.00
  SKU-B quantity 3 at 50.00
When the order is submitted
Then SKU-A line total is 200.00
And SKU-B line total is 150.00
And the subtotal is 350.00
And the total is 350.00
```

### AC-04.2 — Client Total Is Ignored

```gherkin
Given the client sends an incorrect total of 1.00
When the order is processed
Then the server ignores the supplied total
And calculates the total using the line items
```

### AC-04.3 — Zero Unit Price Is Valid

```gherkin
Given an order contains SKU-FREE quantity 5 at 0.00
When the order is submitted
Then the order is created
And the line total is 0.00
```

### AC-04.4 — Rounding

```gherkin
Given an order contains SKU-C quantity 3 at 0.10
When the order is submitted
Then the line total is exactly 0.30
And no floating-point drift is present
```

## Suggested TDD Tests

```text
CalculateLineTotal_ReturnsQuantityTimesUnitPrice
CalculateLineTotal_UsesDecimalWithoutDrift
CalculateOrderTotal_WithMultipleItems_ReturnsCorrectTotal
CalculateOrderTotal_WithZeroPrice_IsValid
CalculateOrderTotal_TotalEqualsSubtotal
CreateOrder_IgnoresClientProvidedTotal
```

---

# US-05 — View Orders

## User Story

**As a Sales Representative,**  
I want to see previously submitted orders  
so that I can verify whether an order was successfully captured and understand its current state.

## Functional Requirements

### FR-05.1
The system shall return a paginated collection of orders. Query parameters: `page` (default 1, min 1) and `pageSize` (default 20, min 1, max 100). The response includes `items`, `page`, `pageSize`, and `totalCount`.

### FR-05.2
Orders shall be sorted by creation timestamp descending. Ties are broken by order ID for deterministic paging.

### FR-05.3
Each list item shall contain:

- Order ID
- External Reference
- Customer name
- Created timestamp (UTC)
- Status
- Currency
- Total

### FR-05.4
The user shall be able to retrieve an individual order by its Order ID (GUID).

### FR-05.5
A nonexistent or malformed order ID shall return `404 ORDER_NOT_FOUND`.

### FR-05.6
The list may optionally be filtered by `status` (exact match against a defined status value). An unknown status value returns `400 VALIDATION_FAILED`.

## Acceptance Criteria

### AC-05.1 — Orders Are Sorted Newest First

```gherkin
Given orders were created at 10:00, 10:05 and 10:10
When the user requests the order list
Then the order created at 10:10 appears first
And the order created at 10:05 appears second
And the order created at 10:00 appears last
```

### AC-05.2 — Pagination

```gherkin
Given 25 orders exist
When the user requests page 2 with pageSize 10
Then 10 orders are returned
And totalCount is 25
And none of the returned orders appear on page 1
```

### AC-05.3 — Retrieve Existing Order

```gherkin
Given an order exists
When it is requested by its Order ID
Then the order details are returned
Including its customer
And line items with line totals
And subtotal and total
And status
And external reference
And created and last-updated timestamps
```

### AC-05.4 — Retrieve Unknown Order

```gherkin
Given no order exists with the requested identifier
When that identifier is requested
Then the system returns 404 with code ORDER_NOT_FOUND
```

## Suggested TDD Tests

```text
GetOrders_ReturnsNewestFirst
GetOrders_RespectsPageAndPageSize
GetOrders_ClampsPageSizeToMax
GetOrders_FiltersByStatus
GetOrder_WithExistingId_ReturnsOrder
GetOrder_WithUnknownId_ReturnsNotFound
GetOrder_WithMalformedId_ReturnsNotFound
```

---

# US-06 — View Order Details

## User Story

**As a Sales Representative,**  
I want to view the details of a submitted order  
so that I can verify exactly what was submitted, how much it is worth, and where it is in the sales process.

## Functional Requirements

### FR-06.1
The order detail view shall display:

- Order ID
- External Reference
- Customer name and code
- Created Date (formatted in local time, CC-10)
- Last Updated Date
- Currency
- Status
- Notes

### FR-06.2
The order detail view shall display each line item containing:

- SKU
- Name
- Quantity
- Unit Price
- Line Total

### FR-06.3
The order detail view shall display:

- Subtotal
- Total

### FR-06.4
Monetary values shall be formatted with the order's currency code and 2 decimal places.

### FR-06.5
If the order cannot be loaded (404 or network error), the view shall show a clear message and a link back to the order list.

## Acceptance Criteria

### AC-06.1 — Display Order Details

```gherkin
Given an order exists
When the user opens the order
Then the customer information is displayed
And the external reference is displayed
And all line items are displayed
And each calculated line total is displayed
And the subtotal and total are displayed with the currency code
And the current order status is displayed
```

### AC-06.2 — Unknown Order

```gherkin
Given the user navigates to a detail URL for an order that does not exist
When the page loads
Then a "not found" message is displayed
And a link back to the order list is provided
```

---

# US-07 — Change Order Status

## User Story

**As a Sales Representative,**  
I want to update an order's status  
so that the system accurately reflects its current stage in the sales process.

## Proposed Order Lifecycle

```text
Pending
├── Confirmed
│   ├── Fulfilled
│   └── Cancelled
└── Cancelled

Fulfilled = terminal state
Cancelled = terminal state
```

> **Product Decision:** The assignment requires sensible status transitions but does not prescribe the exact state machine. The workflow below is proposed for this implementation. Terminal states are final — there is no "un-cancel" or "reopen" in this MVP.

## Allowed Transitions

| Current Status | Target Status | Allowed |
|---|---|---|
| Pending | Confirmed | Yes |
| Pending | Cancelled | Yes |
| Confirmed | Fulfilled | Yes |
| Confirmed | Cancelled | Yes |
| Any | Same status | No (`409 INVALID_STATUS_TRANSITION`) |
| Fulfilled | Any other status | No |
| Cancelled | Any other status | No |

## Functional Requirements

### FR-07.1
Only defined status values (`Pending`, `Confirmed`, `Fulfilled`, `Cancelled`) shall be accepted. Unknown values return `400 VALIDATION_FAILED` with code detail `INVALID_STATUS`.

### FR-07.2
Status transitions shall follow the defined lifecycle.

### FR-07.3
Invalid transitions shall not modify the order and shall return `409 INVALID_STATUS_TRANSITION`.

### FR-07.4
The error message shall name both the current and the requested status, e.g. `"Order cannot transition from Fulfilled to Pending."`

### FR-07.5
A successful transition shall update the order's last-updated timestamp and append an entry to the order's **status history** (`fromStatus`, `toStatus`, `changedAtUtc`). The history is returned with the order detail.

### FR-07.6
Transitioning to the current status is a no-op rejection (`409`), not silently accepted, so accidental double-clicks are surfaced to the user.

### FR-07.7
The Angular UI shall only offer the transitions that are valid for the order's current status; the server remains the source of truth.

## Acceptance Criteria

### AC-07.1 — Pending to Confirmed

```gherkin
Given an order is Pending
When its status is changed to Confirmed
Then the order becomes Confirmed
And the status history contains an entry Pending → Confirmed
And the last-updated timestamp is advanced
```

### AC-07.2 — Confirmed to Fulfilled

```gherkin
Given an order is Confirmed
When its status is changed to Fulfilled
Then the order becomes Fulfilled
```

### AC-07.3 — Invalid Transition From Fulfilled

```gherkin
Given an order is Fulfilled
When the user attempts to change it to Pending
Then the request is rejected with 409 INVALID_STATUS_TRANSITION
And the order remains Fulfilled
And the message reads "Order cannot transition from Fulfilled to Pending."
```

### AC-07.4 — Invalid Transition From Cancelled

```gherkin
Given an order is Cancelled
When the user attempts to change it to Confirmed
Then the request is rejected with 409 INVALID_STATUS_TRANSITION
And the order remains Cancelled
```

### AC-07.5 — Unknown Status Value

```gherkin
Given an order is Pending
When the user attempts to change it to "Shipped"
Then the request is rejected with 400 VALIDATION_FAILED
And the order remains Pending
```

### AC-07.6 — Same-Status Transition

```gherkin
Given an order is Confirmed
When the user attempts to change it to Confirmed
Then the request is rejected with 409 INVALID_STATUS_TRANSITION
And no status-history entry is added
```

## Suggested TDD Tests

```text
ChangeStatus_PendingToConfirmed_Succeeds
ChangeStatus_PendingToCancelled_Succeeds
ChangeStatus_ConfirmedToFulfilled_Succeeds
ChangeStatus_ConfirmedToCancelled_Succeeds
ChangeStatus_PendingToFulfilled_Fails
ChangeStatus_FulfilledToPending_Fails
ChangeStatus_CancelledToConfirmed_Fails
ChangeStatus_SameStatus_Fails
ChangeStatus_InvalidStatus_Fails
ChangeStatus_Success_AppendsHistoryEntry
ChangeStatus_Success_UpdatesLastUpdatedTimestamp
ChangeStatus_OnUnknownOrder_ReturnsNotFound
```

---

# US-08 — Provide Consistent User Feedback

## User Story

**As a Sales Representative,**  
I want clear feedback when something goes wrong  
so that I understand what happened and know how to correct it.

## Functional Requirements

### FR-08.1
All non-2xx API responses shall use the following envelope:

```json
{
  "code": "VALIDATION_FAILED",
  "message": "One or more validation errors occurred.",
  "details": [
    { "field": "lines[0].quantity", "code": "INVALID_QUANTITY", "message": "Quantity must be greater than zero." }
  ],
  "traceId": "00-abc123..."
}
```

- `code` — top-level machine-readable error code (see FR-08.2)
- `message` — human-readable summary
- `details` — array, may be empty; each entry has `field` (nullable), `code`, `message`
- `traceId` — correlation identifier for log lookup

### FR-08.2
Error codes and HTTP status mapping:

| HTTP | Top-level `code` | Detail `code` values |
|---|---|---|
| 400 | `VALIDATION_FAILED` | `REQUIRED`, `TOO_LONG`, `INVALID_FORMAT`, `INVALID_QUANTITY`, `INVALID_PRICE`, `INVALID_CURRENCY`, `INVALID_STATUS` |
| 404 | `ORDER_NOT_FOUND` | — |
| 409 | `INVALID_STATUS_TRANSITION` | — |
| 500 | `INTERNAL_ERROR` | — |

### FR-08.3
The system shall not leave an order in a partially updated or inconsistent state after a failed operation. Each command runs in a single transaction.

### FR-08.4
`500 INTERNAL_ERROR` responses shall **not** expose stack traces or exception messages. The `traceId` is sufficient for diagnosis.

### FR-08.5
The Angular UI shall map `details[].field` to the corresponding form control and display the message inline; top-level `message` is shown as a banner.

## Acceptance Criteria

### AC-08.1 — Consistent Domain Error

```gherkin
Given the user performs an invalid operation
When the server rejects the operation
Then the response contains a machine-readable error code
And a human-readable explanation
And a traceId
And the system remains in a consistent state
```

### AC-08.2 — Field-Level Feedback in UI

```gherkin
Given the server returns VALIDATION_FAILED with a detail for "lines[0].quantity"
When the UI renders the response
Then the quantity input on the first line shows the error message inline
```

### AC-08.3 — No Leak on Unexpected Error

```gherkin
Given an unexpected exception occurs on the server
When the response is returned
Then the status is 500 with code INTERNAL_ERROR
And the response body contains no stack trace
```

## Suggested TDD Tests

```text
ErrorResponse_ValidationFailure_UsesEnvelope
ErrorResponse_NotFound_UsesEnvelope
ErrorResponse_InvalidTransition_Returns409
ErrorResponse_UnhandledException_Returns500WithoutStackTrace
```

---

# Non-Functional Requirements

| ID | Requirement |
|---|---|
| NFR-01 | Domain and application layers have no dependency on ASP.NET Core or EF Core. |
| NFR-02 | All domain rules are covered by unit tests; the API is covered by integration tests using `WebApplicationFactory`. |
| NFR-03 | The solution runs locally with a single documented command per tier (API and Angular). |
| NFR-04 | The API exposes OpenAPI/Swagger in Development. |
| NFR-05 | CORS is restricted to the Angular dev origin in Development. |

---

# MVP Delivery Priority

| Priority | Story | Reason |
|---|---|---|
| P0 | US-03 Validate Order Data | Establish domain invariants |
| P0 | US-04 Calculate Order Totals | Core business logic |
| P0 | US-01 Submit a Purchase Order | Primary capability |
| P0 | US-02 Prevent Duplicate Orders | Central business problem |
| P0 | US-05 View Orders | Enables order tracking |
| P0 | US-08 Provide Consistent User Feedback | Required by every other story's ACs |
| P1 | US-07 Change Order Status | Completes the order lifecycle |
| P1 | US-06 View Order Details | Enables user verification |

---

# Recommended TDD Implementation Sequence

```text
Domain Rules
    ↓
Unit Tests
    ↓
Order Entity / Value Objects
    ↓
Order Creation Use Case
    ↓
Duplicate Prevention / Idempotency
    ↓
Persistence
    ↓
Queries
    ↓
Status State Machine
    ↓
API + Error Envelope
    ↓
Angular UI
```

---

# Recommended Domain Structure

```text
Domain
├── Orders
│   ├── Order
│   ├── OrderLine
│   ├── OrderStatus
│   ├── OrderStatusHistoryEntry
│   ├── Money
│   ├── Currency
│   ├── OrderStatusTransitionPolicy
│   └── DomainException
│
Application
├── Orders
│   ├── CreateOrder
│   ├── GetOrder
│   ├── GetOrders
│   └── ChangeOrderStatus
├── Validation
│   └── ValidationError
│
Infrastructure
├── Persistence
│   └── OrderRepository
│
Api
├── Controllers
│   └── OrdersController
├── Errors
│   └── ErrorEnvelope / ExceptionMiddleware
│
Frontend
└── Angular
    ├── OrderCreate
    ├── OrderList
    └── OrderDetail
```

---

# Initial Test Suite

The first tests should focus on business behaviour rather than controllers or infrastructure.

## OrderLine Tests

```text
OrderLine_WithQuantityGreaterThanZero_IsValid
OrderLine_WithZeroQuantity_Fails
OrderLine_WithNegativeQuantity_Fails
OrderLine_WithNegativePrice_Fails
OrderLine_WithPriceMoreThanTwoDecimals_Fails
OrderLine_WithBlankSku_Fails
OrderLine_CalculatesLineTotal
```

## Order Tests

```text
Order_WithValidLineItems_CalculatesTotal
Order_WithNoLineItems_Fails
Order_WhenCreated_StartsPending
Order_ContainsCreationTimestamp
Order_WithInvalidCurrency_Fails
```

## Order Status Tests

```text
Order_PendingToConfirmed_Succeeds
Order_PendingToCancelled_Succeeds
Order_ConfirmedToFulfilled_Succeeds
Order_ConfirmedToCancelled_Succeeds
Order_PendingToFulfilled_Fails
Order_FulfilledToPending_Fails
Order_CancelledToConfirmed_Fails
Order_SameStatusTransition_Fails
Order_StatusChange_AppendsHistory
```

## Create Order Tests

```text
CreateOrder_WithUniqueReference_CreatesOrder
CreateOrder_WithExistingReference_DoesNotCreateDuplicate
CreateOrder_WithExistingReferenceDifferentCase_DoesNotCreateDuplicate
CreateOrder_RepeatedRequest_ReturnsExistingOrder
CreateOrder_ConcurrentRequests_ProducesSingleOrder
```

## Query Tests

```text
GetOrders_ReturnsNewestFirst
GetOrders_RespectsPagination
GetOrder_WithExistingId_ReturnsOrder
GetOrder_WithUnknownId_ReturnsNotFound
```

---

# Definition of Done

A user story is considered complete when:

- All acceptance criteria pass
- Domain rules are covered by automated tests
- API validation and errors use the US-08 envelope and status mapping
- Business rules are enforced server-side
- No duplicate order can be created for the same external reference (including under concurrency)
- Totals are calculated on the server using `decimal`
- Invalid status transitions are rejected
- Angular UI supports the required user flow and shows field-level errors
- Code is documented sufficiently for another developer to run locally
- README contains local setup instructions
- SOLUTION.md documents important design decisions and trade-offs (including CC-13 no-auth)

---

# Suggested Next Development Step

Start with the domain and tests before creating controllers or UI.

Recommended first implementation order:

1. `Money` / `Currency`
2. `OrderLine`
3. `Order`
4. `OrderStatus`
5. `OrderStatusTransitionPolicy`
6. `CreateOrder`
7. `IOrderRepository`
8. In-memory repository
9. `GetOrder`
10. `GetOrders`
11. `ChangeOrderStatus`
12. ASP.NET Core API + error envelope
13. Angular application
