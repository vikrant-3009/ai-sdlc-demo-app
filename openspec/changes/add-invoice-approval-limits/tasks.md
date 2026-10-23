# Tasks

## 1. Stage One: Single-Approver Limits

- [ ] 1.1 Add Manager and Director role support while mapping the existing `Approver` role to Manager; configure non-secret demo/test identities needed for Manager, Director, and Admin checks, and verify the configured roles resolve as intended.
- [ ] 1.2 Enforce the inclusive $50,000 Manager cap and $500,000 Director/Admin cap in the Domain approval flow; return a clear reason for denied attempts and verify rejected calls do not alter invoice status or decision fields.
- [ ] 1.3 Add xUnit service tests for $50,000 and $500,000 accepted boundaries, $50,000.01 and $500,000.01 denied boundaries, Admin and Director authority, the legacy role mapping, unsupported roles, repeated decisions, and state preservation; run `dotnet test backend/tests/InvoiceApp.Tests/InvoiceApp.Tests.csproj`.
- [ ] 1.4 Add a Playwright test using the actual UI and accessible role/label locators: sign in as the legacy Manager account, create and submit a $50,000.01 invoice, attempt approval, and assert a clear denial is shown while the invoice remains PendingApproval; run the focused Playwright test.

## 2. Stage Two: Two-Approver Decisions

- [ ] 2.1 Add invoice approval history entries that preserve each approver identity, role, time, and outcome while retaining the existing final-decision fields; verify the serialized invoice still exposes those existing fields and the full history.
- [ ] 2.2 Extend the domain/repository decision boundary so checking invoice state, checking approver identity and role, appending history, and changing status are atomic per invoice; add concurrency tests proving overlapping requests cannot duplicate, overwrite, or bypass a valid transition.
- [ ] 2.3 Implement the above-$500,000 sequence: accept a first approval from Manager, Director, or Admin without finalizing; require a distinct Director as the second approver; reject Admin/Manager second approvals and same-user attempts; add xUnit tests for successful sequencing and all negative cases, including repeats before and after completion and unchanged state/history on rejection; run the focused .NET tests.
- [ ] 2.4 Update the UI to show that a high-value invoice is awaiting its second approval and keep the pending list usable after the first decision; verify a first approval remains pending and a distinct Director can complete it through the UI.
- [ ] 2.5 Run `dotnet test backend/InvoiceApp.sln` and the Playwright suite to verify both stages together; confirm the API/UI communicates rejection reasons and no rejected attempt changes invoice status or recorded approvals.