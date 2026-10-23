# Design

## Context

See [proposal.md](proposal.md) for motivation and [specs/invoice-approval-limits/spec.md](specs/invoice-approval-limits/spec.md) for the behavior contract. Today `ApprovalService` owns invoice state transitions, but receives only an approver username; `UserSession` contains the authenticated username and role. `Invoice` stores one decision, and `InMemoryInvoiceRepository` locks individual methods rather than the service's complete read-check-update operation. The UI already displays API errors in its decision panel, while the role filter currently returns a body-less 403.

## Goals / Non-Goals

**Goals:**
- Keep amount and approval-sequence rules independent of ASP.NET Core and the UI.
- Preserve the existing single-approval API behavior for decisions within authority while exposing approval history for high-value invoices.
- Reject invalid attempts with a user-readable API reason and no partial mutation.
- Make the approval workflow safe when requests for one invoice overlap.

**Non-Goals:**
- Add a database or change the app's in-memory persistence approach.
- Implement bulk or partial approval from the existing use-case briefs.
- Add a general user-management or role-administration feature.

## Decisions

### Keep approval policy in the Domain service

Pass the authenticated username and role from the API into the domain decision operation. The domain service will normalize the legacy `Approver` role as Manager, then enforce the limits and approval sequence; the UI must not be trusted to enforce authority. The API remains responsible for authentication and returning HTTP responses. Keep endpoint role filtering aligned with Manager, Director, Admin, and the legacy role, and make denied role responses include an `error` reason instead of a bare 403. Service-level policy failures can continue through the existing controller error mapping.

**Alternative considered:** Put amount checks in `ApprovalsController` or JavaScript. Rejected because requests can bypass the UI and because it would move business rules out of the project's Domain boundary.

### Record every approval decision

For the two-approver stage, add an approval-history collection to the invoice's domain representation, with each entry retaining the authenticated username, role, decision time, and decision outcome. Keep the existing `DecidedBy`, `DecidedAt`, and `DecisionNote` fields for compatibility with current clients; on completed decisions they continue to describe the final invoice outcome, while the history preserves both approvals. A first approval above $500,000 is recorded in history while invoice status remains PendingApproval.

**Alternative considered:** Reuse only `DecidedBy` and `DecidedAt`. Rejected because those fields cannot distinguish two decisions or prove that two different people approved the invoice.

### Make each decision atomic per invoice

The repository boundary must perform the read, status/role/identity checks, history append, and resulting status update as one atomic operation for one invoice. The in-memory implementation can serialize this operation under a per-invoice lock or a repository lock; any later database implementation must use a transaction or concurrency token with equivalent behavior. Do not rely on the existing locks around `GetById` and `Update` separately, because two requests can otherwise both pass checks against stale state.

**Alternative considered:** Leave service-level read/check/update unchanged and test only sequential calls. Rejected because that does not protect the same-approver and repeated-transition rules under concurrent requests.

### Keep the policy tiers and role compatibility explicit

Manager (including the legacy `Approver` role) has a $50,000 solo cap; Director and Admin have a $500,000 solo cap. Above $500,000, Manager, Director, or Admin can record the first approval, but only a distinct Director can record the second and finalize. Admin is not treated as a Director. Configure test/demo users for the roles needed by service and UI tests without changing credentials into real secrets.

## Risks / Trade-offs

- [Role names remain string-based] → Centralize the legacy alias and supported-role mapping at the Domain/API boundary and test unknown role values explicitly.
- [Existing clients may assume one approver] → Keep the current invoice decision fields and add history without removing the existing response shape; cover the serialized API response in tests.
- [A lock around the in-memory repository may limit parallel throughput] → Scope serialization to one invoice where practical; correctness is more important than throughput for this demo, and the interface must allow a transactional implementation later.
- [A first approval leaves an invoice pending, unlike today's immediate final approval] → Return the updated invoice and render its approval progress in the UI; ensure refresh keeps it available for the second Director decision.
- [The Playwright project currently has no test cases and depends on a locally installed runner] → Add the focused test using the existing Playwright config and verify the runner is installed when executing the end-to-end suite.

## Migration Plan

1. Ship the single-approver limit stage with role mapping, reasoned failures, and unit tests. Existing `Approver` configuration continues to work as Manager.
2. Ship the history and atomic two-approver stage with its unit tests, then add the UI progress/error behavior and the Playwright rejection test.
3. Rollback can revert the code and configuration together. No database migration is required because the current repository is in-memory; if persistence is introduced before this change is deployed, the history representation will require a corresponding storage migration.