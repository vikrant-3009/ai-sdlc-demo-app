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

#### Scenario: Every permitted first-approver role can start the sequence
- **WHEN** a Manager, Director, or Admin is the first approver for an invoice above $500,000
- **THEN** that approver is recorded as the first approval and the invoice remains PendingApproval
- **AND** a different Director can provide the second approval and complete the invoice

### Requirement: Rejected approval attempts explain the denial

The system MUST return a non-success response with a clear reason when it denies an approval attempt. The existing `Approver` role is not unsupported; it maps to Manager authority.

#### Scenario: Unsupported role attempts approval
- **WHEN** an authenticated user with a role other than Manager, Director, Admin, or the compatible `Approver` role sends an HTTP approval request
- **THEN** the API returns HTTP 403 with a non-empty `error` reason
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

#### Scenario: Authorized approver rejects after the first high-value approval
- **WHEN** an authorized role rejects an invoice above $500,000 that has one approval recorded and is still PendingApproval
- **THEN** the invoice becomes Rejected and the rejection is appended to approval history
- **AND** the first approval entry remains unchanged
- **AND** the final decision fields describe the rejection

### Requirement: Approval transitions are concurrency-safe

The system MUST apply each approval or rejection decision atomically per invoice and re-evaluate it against the latest committed state. The first committed operation determines serialization order. Concurrent requests MUST NOT bypass limits, record one identity as two approvers, overwrite prior decisions, or complete the same approval transition more than once. Rejected concurrent attempts MUST leave the state and history from the accepted operation unchanged.

#### Scenario: Simultaneous first approvals are re-evaluated in commit order
- **WHEN** a Manager, Director, or Admin first approval races with another approval for the same invoice above $500,000
- **THEN** only the first committed approval is recorded as the first approval
- **AND** the later request is evaluated as a second approval and succeeds only if it is from a different Director
- **AND** a rejected later request leaves the first approval and PendingApproval status unchanged

#### Scenario: Concurrent first approval and rejection follow commit order
- **WHEN** a first approval and an authorized rejection race for the same pending invoice above $500,000
- **THEN** the first committed operation is applied and the other request is re-evaluated against its resulting state
- **AND** if the first approval commits first, the authorized rejection may reject the still-pending invoice and must retain that approval entry
- **AND** if rejection commits first, the later approval is denied without changing the rejection or history

#### Scenario: Concurrent second approval and rejection follow commit order
- **WHEN** a second approval and an authorized rejection race for an invoice above $500,000 that has one approval recorded
- **THEN** the first committed operation is applied and the other request is re-evaluated against its resulting state
- **AND** if the distinct Director second approval commits first, the later rejection is denied without changing the Approved status or history
- **AND** if rejection commits first, the later approval is denied without changing the Rejected status or history

#### Scenario: Concurrent second approvals cannot finalize twice
- **WHEN** two requests attempt the second approval for the same invoice above $500,000
- **THEN** at most one valid distinct Director approval is recorded and the invoice becomes Approved once
- **AND** every later request is denied with a clear reason and adds no history entry

#### Scenario: Concurrent attempts by the first approver cannot count twice
- **WHEN** the first approver races another request to provide the second approval
- **THEN** the same authenticated identity is never recorded as both approvers
- **AND** any denied request leaves the first approval and invoice status unchanged