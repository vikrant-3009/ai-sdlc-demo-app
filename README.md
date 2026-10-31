# Invoice Approval Demo App

A small invoice approval application used for AI-in-SDLC demonstrations. It consists of a .NET 10 API, a plain web UI, and an in-memory data store, so it can run locally without requiring a database.

The application demonstrates role-based invoice approval limits, multi-level approval for high-value invoices, and approval history.

## Features

- Create and submit invoices for approval.
- Approve or reject pending invoices.
- Enforce approval limits based on user roles.
- Support dual approval for invoices above $500,000.
- Maintain approval history for high-value invoices.
- Prevent the same user from acting as both approvers.
- Return clear reasons for denied approval attempts.
- Prevent denied attempts from modifying invoice state.
- Handle competing approval decisions safely.

## Approval Rules

| Role | Approval limit |
|---|---:|
| Manager | Up to $50,000 |
| Approver | Up to $50,000 |
| Director | Up to $500,000 |
| Admin | Up to $500,000 |

### High-value invoice approval

Invoices above $500,000 require two distinct approvers:

1. An eligible Manager, Director, or Admin provides the first approval.
2. A different Director must provide the second approval.
3. The invoice becomes Approved only after the second approval.
4. The same user cannot provide both approvals.
5. An Admin cannot substitute for a Director as the second approver.

A valid rejection is handled separately from a denied approval attempt. A pending invoice can be rejected even after its first approval, while preserving its existing approval history.

## Run the Application

Make sure the .NET 10 SDK is installed.

From the repository root, run:

```bash
dotnet run --project backend/src/InvoiceApp.Api
```

Open the application:

```text
http://localhost:5000
```

### Demo Accounts

The following original demo accounts are provided:

| User | Password | Role |
|---|---|---|
| `demo.approver` | `ChangeMe123!` | Approver |
| `demo.admin` | `ChangeMe123!` | Admin |

Additional role-specific demo accounts, if configured, can be checked in the application's authentication configuration.

These are demo accounts for a local, in-memory application. They are not real credentials and should not be used in production.

## Application Workflow

1. Sign in using a configured demo account.
2. Create an invoice.
3. Submit the invoice for approval.
4. An authorized user approves or rejects the invoice.
5. For invoices above $500,000, a second approval from a different Director is required.
6. View the invoice status and approval history.

Invoices move through the following states:

```text
Draft
  |
  v
Pending Approval
  |
  +------> Approved
  |
  +------> Rejected
```

High-value invoices remain in `Pending Approval` after the first approval until the second eligible approval is completed.

## Running Tests

### Backend Unit Tests

Run the .NET test suite from the repository root:

```bash
dotnet test backend/InvoiceApp.sln
```

### Playwright End-to-End Tests

Navigate to the E2E directory:

```bash
cd e2e
```

Install dependencies:

```bash
npm ci
```

Install the Chromium browser:

```bash
npx playwright install chromium
```

Start the application in a separate terminal:

```bash
dotnet run --project backend/src/InvoiceApp.Api
```

Run all Playwright tests:

```bash
npx playwright test
```

Run the invoice approval feature tests:

```bash
npx playwright test invoice-approval-limits.spec.ts
```

Run tests with a visible browser:

```bash
npx playwright test --headed
```

View the HTML test report:

```bash
npx playwright show-report
```

Check `playwright.config.ts` for the configured application URL and test execution settings.

## Mutation Testing

Mutation testing is used to evaluate the effectiveness of the test suite.

The project uses Stryker.NET 5.0.0 for mutation testing.

The assignment compares mutation-testing results before and after the Stage 2 implementation.

| Metric | Baseline | After Stage 2 |
|---|---:|---:|
| Overall mutation score | 50.00% | 57.35% |
| Covered-code mutation score | 72.97% | 79.59% |
| Killed mutants | 27 | 39 |
| Survived mutants | 10 | 10 |
| Mutants with no coverage | 17 | 19 |

The reported overall mutation score increased by 7.35 percentage points after Stage 2.

## AI-Assisted Development

This repository was used for an AI-in-SDLC assignment demonstrating how AI coding agents can support different stages of software development.

The workflow included:

- Specification-driven development using OpenSpec.
- Repository-level AI instructions using `AGENTS.md`.
- AI-assisted implementation using GitHub Copilot.
- Unit and end-to-end test generation and validation.
- AI-assisted code review.
- Mutation testing to evaluate test effectiveness.
- Documentation of AI interactions and development activities.

The developer reviewed AI-generated changes, validated business rules, and checked test results throughout the process.

## Project Structure

```text
ai-sdlc-demo-app/
|
|-- backend/
|   |-- src/
|   |   |-- InvoiceApp.Domain/       # Domain models and business rules
|   |   |-- InvoiceApp.Api/          # API, controllers, authentication and web UI
|   |
|   |-- tests/
|   |   |-- InvoiceApp.Tests/        # xUnit unit and integration tests
|   |
|   |-- InvoiceApp.sln
|
|-- e2e/                             # Playwright end-to-end tests
|
|-- openspec/
|   |-- changes/
|       |-- add-invoice-approval-limits/
|           |-- proposal.md
|           |-- design.md
|           |-- tasks.md
|           |-- specs/
|
|-- docs/
|   |-- use-cases/
|       |-- UC-1-bulk-approval.md
|       |-- UC-2-partial-approval.md
|
|-- AGENTS.md                        # Instructions for AI coding agents
|-- AI_INTERACTION_LOG.md            # AI interaction and activity log
|-- ASSIGNMENT.md                    # Assignment write-up
|-- README.md
```

## Use Cases

- [UC-1 Bulk invoice approval](docs/use-cases/UC-1-bulk-approval.md)
- [UC-2 Partial approval](docs/use-cases/UC-2-partial-approval.md)

## Technology Stack

- .NET 10
- C#
- ASP.NET Core
- xUnit
- Playwright
- OpenSpec
- GitHub Copilot
- Stryker.NET 5.0.0

## Notes

- The application uses an in-memory store. Application data is not persisted across restarts.
- The demo authentication accounts are intended for local demonstration only.
- This application is designed as a workshop/demo project, not as a production-ready invoice approval system.
