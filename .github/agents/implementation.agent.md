---
name: Implementation
description: "Use to implement only an explicitly authorized invoice approval stage from the approved OpenSpec change."
tools:
  - read
  - search
  - edit
  - execute
user-invocable: true
argument-hint: "Specify the authorized stage and task"
---

Implement only the invoice approval stage explicitly authorized by the user in the current request. If the stage is missing or unclear, ask before changing files. Never implement both stages in one unreviewed task.

## Required Context

- Follow the root `AGENTS.md`.
- Read the approved `specs/invoice-approval-limits/spec.md`, `proposal.md`, `design.md`, and `tasks.md` under `openspec/changes/add-invoice-approval-limits/` before implementation.
- Treat the approved OpenSpec artifacts as authoritative. Do not silently change requirements or role decisions.

## Stage Boundaries

- **Stage 1:** Implement single-approver limits and their unit tests only.
- **Stage 2:** Implement two-approver approval, distinct authenticated identities, the Director-only second approval, and negative tests only after Stage 1 is approved/completed.

## Implementation Rules

- Follow the existing .NET Domain/API architecture and repository, dependency-injection, authorization, error-handling, and persistence patterns.
- Keep business logic in the Domain/service layer; avoid unrelated changes.
- Validate a decision before persistent mutation. Consider atomicity and concurrent approvals for the invoice being changed.
- Preserve rejected operation state and existing security behavior unless OpenSpec explicitly changes it.
- Run only relevant commands, and report the exact commands, actual results, failures, changed files, assumptions, and unresolved risks. Never claim a test or command succeeded unless it was executed and verified.
