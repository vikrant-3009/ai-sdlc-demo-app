# Agent Guide

Invoice approval demo app: ASP.NET Core Web API targeting .NET 10, with a static HTML/CSS/JavaScript UI.

## Project Layout

- `backend/InvoiceApp.sln`: solution containing the Domain, API, and unit-test projects.
- `backend/src/InvoiceApp.Domain`: business rules, `ApprovalService`, invoice models (`Invoice`, `InvoiceStatus`), and `IInvoiceRepository`.
- `backend/src/InvoiceApp.Api`: API entry point `Program.cs`, controllers, authentication under `Auth/`, `InMemoryInvoiceRepository`, and static UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests, including `ApprovalServiceTests` and `FakeInvoiceRepository`.
- `e2e`: Playwright configuration and `tests/` and `pages/` directories. Playwright is configured, but those directories currently have no test cases or page objects.
- `docs/use-cases`: feature briefs.
- `openspec/changes/add-invoice-approval-limits/`: reviewed proposal, spec, design, and task breakdown for invoice approval limits. Use these as the implementation scope; resolve any mismatch with the actual role model before coding.

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

Before implementation, inspect the current role configuration and authentication flow, `[RequireRole]` usage, `Invoice` and `InvoiceStatus`, `ApprovalService`, repository behavior, and any existing approval-history representation. The current role values are strings; do not assume `Approver` means Manager or `Admin` means Director, or infer Admin's approval authority. Reconcile role mapping and authority with the reviewed OpenSpec change and get the decision approved before coding. The current proposal/tasks include role-mapping assumptions that must be verified against the application and product decision.

The approved feature requirements are:

- A Manager may approve an invoice up to and including 50,000.
- A Director may approve an invoice up to and including 500,000.
- Monetary limits are inclusive. Tests must cover each boundary and amounts immediately above it.
- An invoice above 500,000 requires two distinct approvers. The second approver must be a Director.
- The same authenticated user must never count as both approvers for one invoice.
- A rejected approval attempt must return a clear reason and leave invoice data and approval history unchanged.

Keep approval rules in the existing Domain/service layer. Follow existing repository interfaces, dependency injection, error handling, and persistence conventions. Validate the full decision before mutating persistent state. Consider atomicity and concurrent attempts on the same invoice; do not allow parallel requests to bypass a limit, duplicate an approver, or partially record a rejected operation.

## Staged Workflow

Use the reviewed `proposal.md`, capability spec, `design.md`, and `tasks.md` under `openspec/changes/add-invoice-approval-limits/` as the task source. Do not ask an agent to implement both stages as one unreviewed change. Resolve the role-mapping issue above before starting the affected implementation.

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

Use xUnit and `FakeInvoiceRepository` for Domain service tests. At minimum, cover:

- Manager approval at 50,000 and rejection immediately above 50,000.
- Director approval at 500,000 and rejection immediately above 500,000 when acting alone.
- Successful two-person approval above 500,000.
- Rejection when the same user attempts to satisfy both approvals, or when the second approver is not a Director.
- Clear reasons for rejected attempts and unchanged invoice status, amount, decision fields, approval records, and history.
- Rejection after an earlier approval has been recorded, where applicable; verify the first approval and pending status remain unchanged.
- Relevant repeated and concurrent attempts so decisions cannot be duplicated or overwritten.

For every AI-generated test, explain in its test name, review notes, or PR summary which defect it detects. Review each assertion: an error-message assertion alone is insufficient when the requirement also demands unchanged state. A human must verify AI-generated code and tests.

Add one Playwright end-to-end test using the actual UI and API flow: create/submit an invoice above the acting user's single-approver limit, attempt approval in the UI, verify the displayed rejection reason, and verify the invoice remains unchanged. Use `e2e/playwright.config.ts` and accessible locators where practical.

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

Use the installed `dotnet-stryker` tool from the unit-test project directory to assess how well the unit tests detect code changes. Stryker generates code mutants; it does not write xUnit tests. No Stryker configuration file is currently present; do not invent one or add one without a reason. The CLI usage supports this invocation:

```powershell
Push-Location backend/tests/InvoiceApp.Tests
dotnet stryker
Pop-Location
```

Capture actual mutation results before test improvements and again afterward. Report the mutation score, killed and surviving mutants, and explain important survivors. Never invent results or claim a command succeeded unless it was executed and verified.

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

Maintain root `ASSIGNMENT.md` with the implementation approach, specification, test results, mutation scores before and after improvements, pull-request findings and verdicts, and review outcomes.

Final assignment deliverables: pull request on the fork with at least three meaningful commits; updated root `AGENTS.md`; committed OpenSpec artifacts; unit tests; Playwright test; mutation results; review verdicts; `AI_INTERACTION_LOG.md`; and `ASSIGNMENT.md`. Keep Stage 1 and Stage 2 in separate commits, and keep any additional commit independently meaningful.

## Never

- Commit real credentials, tokens, or customer data.
- Add business logic to `Program.cs` or controllers.
- Claim tests, mutation runs, reviews, agents, hooks, or commands succeeded unless they were actually run and verified.
