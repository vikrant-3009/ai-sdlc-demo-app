# Spec Delta

## Purpose

Define monetary approval authority and the additional independent approval required for high-value invoices, including clear rejection behavior and preservation of every approval decision.

## ADDED Requirements

### Requirement: Single-approver amount authority

The system MUST allow a Manager to approve a pending invoice alone when its amount is at most $50,000. The system MUST allow a Director or Admin to approve a pending invoice alone when its amount is at most $500,000. The existing `Approver` role MUST retain compatibility by having Manager authority. These limits are inclusive.

#### Scenario: Manager approves at the limit
- **WHEN** a Manager approves a pending invoice for $50,000
- **THEN** the invoice becomes Approved and the decision is recorded

#### Scenario: Manager exceeds the limit
- **WHEN** a Manager attempts to approve a pending invoice for $50,000.01 through $500,000
- **THEN** the attempt is rejected with a clear reason
- **AND** the invoice remains PendingApproval and its decision history is unchanged

#### Scenario: Director or Admin approves at the limit
- **WHEN** a Director or Admin approves a pending invoice for $500,000
- **THEN** the invoice becomes Approved and the decision is recorded

#### Scenario: Director or Admin exceeds the single-approver limit
- **WHEN** a Director or Admin attempts to complete a single approval for an invoice of $500,000.01 or more
- **THEN** the attempt is rejected with a clear reason that a second Director approval is required
- **AND** the invoice remains PendingApproval and its decision history is unchanged

### Requirement: Two distinct approvals for invoices above $500,000

The system MUST require two approvals for an invoice whose amount is greater than $500,000. A Manager, Director, or Admin may provide the first approval; this is the paired-approval exception to that user's single-approver limit. The second approval MUST be made by a Director whose authenticated identity differs from the first approver. Admin MUST NOT satisfy the Director requirement.

#### Scenario: First approval waits for a Director
- **WHEN** a Manager, Director, or Admin approves an invoice above $500,000 for the first time
- **THEN** the first approval is recorded
- **AND** the invoice remains PendingApproval until a different Director approves it

#### Scenario: Distinct Director completes approval
- **WHEN** a Director different from the first approver approves the invoice above $500,000
- **THEN** the second approval is recorded and the invoice becomes Approved
- **AND** both approvers and their decision times remain available in the invoice's approval history

#### Scenario: Same user attempts both approvals
- **WHEN** the first approver attempts the second approval, including through a repeated request
- **THEN** the attempt is rejected with a clear reason
- **AND** the invoice remains PendingApproval and the existing approval history is unchanged

#### Scenario: Non-Director attempts the second approval
- **WHEN** a Manager or Admin attempts the second approval for an invoice above $500,000
- **THEN** the attempt is rejected with a clear reason
- **AND** the invoice remains PendingApproval and the existing approval history is unchanged

### Requirement: Rejected approval attempts explain the denial

The system MUST return a non-success response with a clear reason when it denies an approval attempt. The existing `Approver` role is not unsupported; it maps to Manager authority.

#### Scenario: Unsupported role attempts approval
- **WHEN** an authenticated user with a role other than Manager, Director, Admin, or the compatible `Approver` role attempts approval
- **THEN** the request is denied with a clear reason
- **AND** the invoice and its approval history remain unchanged

#### Scenario: Approval exceeds the single-approver limit
- **WHEN** an approver attempts to decide an invoice above their single-approver limit without completing the required second approval
- **THEN** the request is denied with a clear reason that describes the required approval
- **AND** the invoice and its approval history remain unchanged

#### Scenario: Approval is repeated after completion
- **WHEN** any user attempts to approve an invoice that is already Approved or Rejected
- **THEN** the attempt is rejected with a clear reason
- **AND** the completed status and approval history remain unchanged

### Requirement: Rejected approval attempts preserve state

When the system rejects an approval attempt, it MUST leave the invoice amount, status, prior decision fields, and approval history unchanged.

#### Scenario: Invalid approval does not partially update the invoice
- **WHEN** an approval attempt is denied for an invalid role, amount, identity, sequence, or invoice status
- **THEN** the invoice amount, status, prior decision fields, and approval history are unchanged

#### Scenario: Business rejection remains a valid decision
- **WHEN** an authorized approver rejects a pending invoice with a reason
- **THEN** the invoice becomes Rejected and the rejection is recorded
- **AND** this valid business decision is distinct from a rejected authorization attempt

### Requirement: Approval transitions are concurrency-safe

The system MUST apply each approval decision atomically per invoice so concurrent requests cannot bypass limits, record one identity as two approvers, overwrite prior decisions, or complete the same approval transition more than once. Rejected concurrent attempts MUST leave the accepted decision and invoice state unchanged.

#### Scenario: Concurrent requests cannot duplicate an approval
- **WHEN** concurrent approval requests target the same invoice and would otherwise conflict in role, identity, sequence, or invoice status
- **THEN** the system accepts only a valid serialized transition
- **AND** any rejected request returns a clear reason without changing the accepted result or approval history