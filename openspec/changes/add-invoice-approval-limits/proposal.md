# Proposal

## Why

Invoice decisions currently have no monetary authorization limits and record only one approver. Adding amount-based authority and a distinct second Director approval for high-value invoices prevents approvals beyond an individual's authority and makes the required decision sequence auditable.

## What Changes

- Add single-approver limits: Managers may approve up to and including $50,000; Directors and Admins may approve up to and including $500,000.
- Require two distinct approvers for amounts above $500,000. The first approval may be by a Manager, Director, or Admin; the second must be by a different Director. This paired path is the explicit exception that allows a Manager or Admin to participate above their single-approver limit.
- Reject unsupported roles, over-limit single approvals, duplicate approvers, and repeat decisions with a clear API reason and no invoice or approval-history mutation.
- Preserve approval decisions as history and prevent concurrent requests from bypassing the amount, identity, or sequence rules.
- Deliver the implementation in two independently testable stages: single-approver limits, then two-approver decisions. Add a Playwright UI test for a rejected Manager over-limit approval.

## Capabilities

### New Capabilities

- `invoice-approval-limits`: Role-based monetary approval limits, high-value two-person approval, rejection behavior, and decision history.

### Modified Capabilities

None. The repository has no existing OpenSpec capability specs.

## Impact

Expected implementation impact: `ApprovalService`, `Invoice` and `IInvoiceRepository` in Domain; `ApprovalsController`, role checks, configured demo users, and `wwwroot` UI in Api; xUnit service tests and Playwright tests. The current API accepts role strings through `UserSession`, permits `Approver` and `Admin` on the approval controller, and maps service exceptions to HTTP errors; these boundaries will need to represent Manager/Director/Admin policy and expose rejection reasons consistently. The in-memory repository locks individual methods but not the service's full read-check-update sequence, so the two-approver workflow needs atomic per-invoice state changes.