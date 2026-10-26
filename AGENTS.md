# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core Web API targeting .NET 10, with a static HTML/CSS/JavaScript UI.

## Layout

- `backend/src/InvoiceApp.Domain`: business rules and repository interface. `ApprovalService` owns invoice transitions; `Invoice` and `InvoiceStatus` are the main invoice models.
- `backend/src/InvoiceApp.Api`: API entry point is `Program.cs`; controllers, sign-in (`Auth/`), `InMemoryInvoiceRepository`, and the static UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit unit tests. Current pattern: `<ClassUnderTest>Tests`, `[Fact]`, and `FakeInvoiceRepository`.
- `e2e`: Playwright configuration and test directories (`tests/`, `pages/`). Add tests here; the directories currently have no test cases or page objects.
- `docs/use-cases`: short briefs for features we may build.

## Conventions

- Keep business rules in Domain, not controllers or JavaScript. Keep repository interfaces in Domain and implementations in Api.
- Register services and middleware in `Program.cs`.
- Resource controllers use `api/[controller]`; `AuthController` uses `api/auth`. `ApiControllerBase.Handle(...)` maps service exceptions where used; not every controller inherits that base.
- API requests require a bearer session except `POST /api/auth/login`. Use `[RequireRole("...")]` for role-restricted actions. Current approval roles are `Approver` and `Admin`; invoice deletion is Admin-only.
- Add xUnit tests for new rules and endpoints. Follow `ApprovalServiceTests` and use `FakeInvoiceRepository` for Domain service tests.
- When adding Playwright tests, prefer accessible role/label locators. Use page objects where they help avoid repeated workflow code.

## Commands

- Run the app: `dotnet run --project backend/src/InvoiceApp.Api` then open http://localhost:5000
- Build the solution: `dotnet build backend/InvoiceApp.sln`
- Run unit tests: `dotnet test backend/InvoiceApp.sln`
- Run Playwright tests from PowerShell: `Push-Location e2e`; `npm ci`; `npx playwright install chromium`; `npm test`; `Pop-Location`

## Demo accounts (not real credentials)

- Demo accounts and their local-only passwords are configured in `backend/src/InvoiceApp.Api/appsettings.json`. Do not reuse them outside this demo.

## Never

- Commit real credentials, tokens or customer data.
- Add business logic to `Program.cs` or controllers.
