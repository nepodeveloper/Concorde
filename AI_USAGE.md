# AI Usage Disclosure

Date: 2026-09-19

## Tools Used

- GitHub Copilot in Visual Studio Code.
- PowerShell and repository tooling were used to inspect, edit, build, test, and run the application.

## How AI Was Used

Copilot assisted with implementation and review of the Concorde order management MVP, including:

- Clean Architecture and vertical-slice structure across the Domain, Application, Infrastructure, API, and Angular layers.
- Order lifecycle rules, including status transitions and cancellation reasons.
- Order amendment support for Pending and Confirmed orders.
- Idempotent order creation and server-side total calculation.
- Multi-currency revenue grouping in the frontend.
- Responsive royal blue, white, and gold styling.
- Focused tests and troubleshooting of build and runtime issues.
- Git branch, commit, and push operations.

## Representative Prompts

The prompts were iterative and scoped to the repository, for example:

- "Build a complete order management system called Concorde with Clean Architecture, an Angular frontend, and an elegant royal blue, white, and gold design."
- "Add order editing for Pending and Confirmed orders, with server-side validation and no editing after Fulfilled or Cancelled."
- "Add cancel-with-reason support, requiring a reason when cancelling a Fulfilled order."
- "Track revenue per currency instead of combining values from different currencies."
- "Run the applications, run the full test suite, and create a branch, commit the changes, and push it."

This is a representative summary rather than a transcript of the full conversation.

## Engineering Decisions Made by the Developer

The developer reviewed and directed the implementation decisions, including:

- Keeping domain rules independent of ASP.NET Core, EF Core, and Angular.
- Using SQLite for the MVP while preserving an EF Core persistence boundary for a future Azure SQL deployment.
- Treating external references as idempotency keys and protecting uniqueness at the database boundary.
- Avoiding foreign-exchange conversion in the MVP and displaying totals separately by currency.
- Allowing cancellation from Fulfilled for returns or refunds, while requiring an explanatory reason.
- Limiting edits to Pending and Confirmed orders.
- Excluding generated local database artifacts from documentation-only commits.

## Validation

Generated and modified code was validated with:

- `dotnet test --nologo` from the repository root: 172 tests passed.
- Angular development build through `npx ng serve`: bundle generation completed successfully.
- Local application startup: API on `http://localhost:5000` and frontend on `http://localhost:4200`.
- Manual inspection of the resulting API, domain, persistence, tests, and frontend changes.
