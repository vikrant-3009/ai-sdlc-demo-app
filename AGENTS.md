# Agent Guide

Invoice approval demo app: ASP.NET Core Web API targeting .NET 10, with a static HTML/CSS/JavaScript UI.

## Project Layout

- `backend/InvoiceApp.sln`: solution containing the Domain, API, and unit-test projects.
- `backend/src/InvoiceApp.Domain`: business rules, `ApprovalService`, invoice models (`Invoice`, `InvoiceStatus`), and `IInvoiceRepository`.
- `backend/src/InvoiceApp.Api`: API entry point `Program.cs`, controllers, authentication under `Auth/`, `InMemoryInvoiceRepository`, and static UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests, including `ApprovalServiceTests` and `FakeInvoiceRepository`.
- `e2e`: Playwright configuration and `tests/` and `pages/` directories. Playwright is configured, but those directories currently have no test cases or page objects.
- `docs/use-cases`: feature briefs.
- `openspec/changes/add-invoice-approval-limits/`: reviewed proposal, design, tasks, and authoritative approval-limit spec. Follow these artifacts; report implementation conflicts rather than changing approved requirements.

## Existing Conventions

- Keep business rules in Domain, not in controllers or JavaScript. Controllers handle HTTP input/output and call Domain services.
- Keep repository interfaces in Domain and implementations in Api. Register services and middleware in `Program.cs`.
- Resource controllers use `api/[controller]`; `AuthController` uses `api/auth`. Request records are declared inside controllers.
- `ApiControllerBase.Handle(...)` maps service exceptions to HTTP errors where used; `AuthController` does not inherit this base.
- API requests require a bearer session except `POST /api/auth/login`. Use `[RequireRole("...")]` for role-restricted actions. Current configured roles are `Approver` and `Admin`; approvals currently permit those roles and invoice deletion is Admin-only.
- Demo users and local-only passwords are in `backend/src/InvoiceApp.Api/appsettings.json`. Never reuse them outside this demo or commit real credentials, tokens, or customer data.
- Add xUnit tests for new rules and endpoints. Follow `ApprovalServiceTests` naming and `[Fact]` conventions; use `FakeInvoiceRepository` for Domain service tests.
- Use accessible Playwright locators such as `getByRole` and `getByLabel`. Add page objects when they reduce repeated workflow code; do not rely on brittle CSS or XPath selectors when an accessible locator is available.
- Avoid unrelated refactoring, unnecessary dependencies, and breaking API changes. Preserve existing authorization, error-handling, and security behavior unless the approved spec explicitly changes it.

## Invoice Approval Limits

The feature's business requirements and acceptance scenarios are authoritative in `openspec/changes/add-invoice-approval-limits/specs/invoice-approval-limits/spec.md`; read it with the change's `proposal.md`, `design.md`, and `tasks.md` before implementation. In brief: Manager's inclusive single-approver limit is 50,000; Director's and Admin's are 500,000; the existing `Approver` role is Manager-compatible. Above 500,000, the spec permits a Manager, Director, or Admin first approval and requires a distinct Director for the second. Rejections must give a clear reason without changing invoice or approval-history state. The spec also governs valid business rejection, repeated attempts, history, and concurrency.

Inspect the implementation before coding. Keep policy in the Domain/service layer, validate before mutation, follow existing repository, dependency-injection, error-handling, authorization, and persistence patterns, and consider transaction boundaries and concurrent attempts. If code conflicts with the approved spec, report the conflict; do not silently change the spec or role decisions.

## Staged Workflow

Use the reviewed OpenSpec proposal, capability spec, design, and task breakdown as the task source. Implement one approved stage at a time; do not combine both stages into one unreviewed change.

### Stage 1: Single-Approver Limits

- Implement the approved Manager and Director limits only.
- Add and review the relevant unit tests, including boundary, just-above-boundary, invalid-role, clear-reason, and unchanged-state cases.
- Run the focused and solution unit tests; review and commit this stage independently.

### Stage 2: Two Approvers Above 500,000

- Implement the two-approver flow, distinct authenticated identities, and Director requirement for the second approver.
- Add negative tests for duplicate users, a non-Director second approver, repeated attempts, and state/history preservation, including rejection after a first approval has been recorded.
- Consider concurrency and ensure rejected attempts do not partially mutate invoice or history state.
- Run the relevant unit and Playwright tests; review and commit this stage independently.

## Test Expectations

Use xUnit and `FakeInvoiceRepository` for Domain service tests. Derive coverage from the OpenSpec scenarios, including boundaries and just-above values, valid and invalid second approvals, duplicate identities, repeats, rejection after a first approval, concurrency, clear reasons, and unchanged invoice/history state. An error-message assertion alone is insufficient when state preservation is required.

For each AI-generated test, state which defect it detects in the test name, review notes, or PR summary. Review every assertion and have a human verify generated tests and code. Add the required Playwright test using the real UI/API flow to verify an over-limit denial, its displayed reason, and unchanged invoice state. Use `e2e/playwright.config.ts` and accessible locators where practical.

## Build, Test, and Mutation Testing

Run the relevant tests after each stage. From the repository root:

```powershell
dotnet build backend/InvoiceApp.sln
dotnet test backend/InvoiceApp.sln
```

For focused Domain unit tests:

```powershell
dotnet test backend/tests/InvoiceApp.Tests/InvoiceApp.Tests.csproj
```

Run Playwright from PowerShell; its config starts the API at `http://localhost:5000`:

```powershell
Push-Location e2e
npm ci
npx playwright install chromium
npm test
Pop-Location
```

Use Stryker.NET (`dotnet-stryker` 5.0.0) from the unit-test project directory to assess how well unit tests detect code changes. Stryker generates code mutants; it does not write xUnit tests. No Stryker configuration file is present; do not invent one or add one without a reason. The verified invocation is:

```powershell
Push-Location backend/tests/InvoiceApp.Tests
dotnet stryker
Pop-Location
```

Capture actual mutation results before test improvements and again afterward. Report the score, killed and surviving mutants, and important survivors. Never invent results or claim a command succeeded unless it was executed and verified.

## Agent Responsibilities

Where the installed tooling supports separate agents, assign these distinct responsibilities; otherwise perform them as separate review passes. Do not claim an agent, sub-agent, hook, or automation ran unless it was actually invoked.

- Specification: check requirements, role decisions, boundaries, edge cases, acceptance criteria, and task breakdown against the approved OpenSpec artifacts.
- Implementation: implement only the currently approved stage and avoid unrelated changes.
- Test: independently inspect test quality, duplicate-user protection, rejection reasons, and state preservation.
- Review: inspect the actual diff for defects, authorization/security problems, regressions, and unnecessary changes.

AI output is assistance, not approval. A human must verify implementation, tests, and review decisions.

## Pull Request Review

Review the actual pull-request diff and investigate every finding. Give each finding exactly one verdict:

- **Fixed:** describe the correction and how it was verified.
- **Dismissed:** explain why it is not a valid defect.
- **Deferred:** explain why it remains open and why deferral is acceptable.

Prioritize monetary boundary errors, authorization bypasses, duplicate approver identities, partial state updates, concurrency issues, and missing negative tests. Do not dismiss a finding merely because current tests pass.

## AI Interaction Log and Assignment Deliverables

Maintain a root `AI_INTERACTION_LOG.md` with meaningful AI interactions: date/task, agent or tool, prompt or faithful summary, files changed, commands and actual results, human review decisions, and follow-up actions. Do not log credentials, tokens, or customer data. Use existing hooks or automation only when configured and verified; otherwise record entries manually. Check that automated records are real and accurate.

Maintain root `ASSIGNMENT.md` with the implementation approach, specification, actual test results, mutation scores before and after improvements, pull-request findings and verdicts, review outcomes, and demo video link.

Final assignment deliverables: pull request on the fork with at least three meaningful commits; updated root `AGENTS.md`; committed OpenSpec artifacts; unit tests; Playwright test; mutation results; review verdicts; `AI_INTERACTION_LOG.md`; and `ASSIGNMENT.md`. Keep Stage 1 and Stage 2 in separate commits; any additional commit must be independently meaningful.

## Never

- Commit real credentials, tokens, or customer data.
- Add business logic to `Program.cs` or controllers.
- Claim tests, mutation runs, reviews, agents, hooks, or commands succeeded unless they were actually run and verified.
