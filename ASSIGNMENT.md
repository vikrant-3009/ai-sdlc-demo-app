# AI in SDLC Assignment

## 1. Assignment Overview

The objective of this assignment was to take a feature from a written
specification through implementation, testing, and review using an
AI-assisted software development workflow. GitHub Copilot was used as
the required AI coding assistant, while I remained responsible for
reviewing the requirements, checking generated changes, and validating
the results.

**Repository:** `ai-sdlc-demo-app`\
**Application:** .NET invoice approval service\
**Feature:** Approval limits by role, including dual approval for
high-value invoices

## 2. Feature Requirements

The invoice approval service was extended to support role-based approval
limits:

-   A **Manager** may approve an invoice up to and including
    **\$50,000**.
-   A **Director** may approve an invoice up to and including
    **\$500,000**.
-   An invoice above **\$500,000** requires two distinct approvers, and
    the second approver must be a Director.
-   The same user must never count as both approvers for one invoice.
-   A denied approval attempt must return a clear reason and leave the
    invoice unchanged.
-   A valid business rejection is different from a denied approval
    attempt. A valid rejection updates the invoice to `Rejected`.
-   A rejection may still be made while a high-value invoice is awaiting
    its second approval. The first approval history must be preserved.
-   Completed invoices must not be approved again.
-   Concurrent decisions must be handled safely so competing requests
    cannot both make decisions based on stale invoice state.

The most important risk areas were ensuring that the same person could
not approve twice and ensuring that a denied attempt did not mutate the
invoice or its approval history.

## 3. AI-Assisted Workflow

The work followed a specification-first workflow:

1.  Reviewed the assignment requirements and the existing application
    structure.
2.  Used a specification-focused review to identify ambiguities and gaps
    in the OpenSpec artifacts.
3.  Updated the proposal, design, tasks, and feature specification to
    clarify rejection after a first approval, concurrent decisions,
    supported first-approver roles, unsupported-role responses, and
    state-preservation checks.
4.  Implemented the first stage of the feature: role-based approval
    limits and associated tests.
5.  Reviewed the implementation and recorded review findings.
6.  Implemented the second stage: dual approval for high-value invoices,
    approval history, and related decision handling.
7.  Ran the available automated tests and manually checked the results.
8.  Ran Stryker.NET mutation testing and compared the baseline report
    with the report after Stage 2.

AI-generated changes were treated as proposals rather than accepted
automatically. Requirements, code changes, test results, and review
findings were checked as part of the workflow.

## 4. Implementation Summary

The implementation work covered the approval service and API behavior,
repository decision handling, tests, and end-to-end coverage.

The Stage 1 implementation included role-based approval caps, clear
denial handling, HTTP authorization behavior, and unit/end-to-end tests.

The Stage 2 implementation extended the flow to support a first approval
for invoices above \$500,000, followed by approval from a different
Director. It also addressed approval history and rejection while a
high-value invoice is awaiting the second approval. The intended
behavior is that invalid attempts are denied before a decision is
committed, and per-invoice decision handling prevents competing requests
from making conflicting decisions against stale state.

Relevant areas in the repository include:

-   `backend/src/InvoiceApp.Domain/ApprovalService.cs`
-   `backend/src/InvoiceApp.Domain/Invoice.cs`
-   `backend/src/InvoiceApp.Domain/IInvoiceRepository.cs`
-   `backend/src/InvoiceApp.Api/`
-   `backend/tests/InvoiceApp.Tests/`
-   `e2e/invoice-approval-limits.spec.ts`
-   `openspec/changes/add-invoice-approval-limits/`
-   `AGENTS.md`
-   `AI_INTERACTION_LOG.md`

## 5. Testing and Validation

The test strategy covers both ordinary approval behavior and the
high-risk edge cases in the specification:

-   Approval-limit boundary values.
-   Manager and Director approval permissions.
-   Valid and invalid first-approver/second-approver combinations.
-   Rejection of a second approval from the same user.
-   Rejection when the second approver is not a Director.
-   Clear denial responses for unsupported roles.
-   No invoice or approval-history mutation after a denied attempt.
-   Rejection after the first approval of a high-value invoice.
-   Attempts to approve an already completed invoice.
-   Competing decisions and concurrency behavior.
-   End-to-end verification of the API and resulting invoice state.

The implementation agent reported successful build and test runs for
Stage 1, including 15 passing xUnit tests and 2 passing Playwright
tests. The Stage 2 tests were also manually checked by me and were
passing at the time of verification.

## 6. Mutation Testing

Mutation testing was run before Stage 1 implementation and after the Stage 2 implementation.
The reports showed the following results:

| Metric | Baseline | After Stage 2 |
|---|---:|---:|
| Overall mutation score | 50.00% | 57.35% |
| Mutation score of covered code | 72.97% | 79.59% |
| Killed mutants | 27 | 39 |
| Survived mutants | 10 | 10 |
| Mutants with no coverage | 17 | 19 |
| Total mutants | 69 | 95 |

The reported overall mutation score increased by **7.35 percentage
points**, and the number of killed mutants increased from 27 to 39. The
covered-code mutation score increased by **6.62 percentage points**.

The Stage 2 report also showed 6 compile errors, compared with 2 in the
baseline report. The reports had different total mutant counts, so the
comparison should be treated as indicative rather than fully controlled
until the same Stryker.NET version, target project, and configuration
have been used for both runs.

**Version note:** Stryker.NET **5.0.0** was used for the assignment.

## 7. Review and Quality Considerations

The review process focused on whether the implementation matched the
specification, particularly the rules most likely to be implemented
incorrectly:

-   The same user cannot be counted as both approvers.
-   The second approver for an invoice above \$500,000 must be a
    Director.
-   An Admin does not substitute for the required Director as the second
    approver.
-   Denied attempts must not change invoice fields or approval history.
-   A valid rejection must remain distinct from a denied approval
    attempt.
-   Concurrent requests must be validated against the latest committed
    invoice state.

The specification review identified and addressed gaps around rejection
after the first approval, concurrency outcomes, unsupported-role
responses, allowed first-approver combinations, and full state
comparisons in end-to-end tests.

## 8. Lessons Learned

-   Clear, testable specifications improve the quality of AI-generated
    implementation work.
-   Boundary values and negative scenarios should be defined before
    coding.
-   State-preservation tests are important when a request must be denied
    without side effects.
-   Approval history and concurrent decisions require explicit design,
    not just role checks.
-   Mutation testing can reveal gaps that passing unit tests may not
    expose.
-   Mutation scores are only meaningfully comparable when the tool
    version, target project, and configuration are consistent.
-   Human review remains necessary: the developer must verify the
    specification, generated code, test output, and final repository
    diff.

## 9. Conclusion

This assignment applied an AI-assisted, specification-first workflow to
a role-based invoice approval feature. The work emphasized explicit
business rules, automated tests, review, and mutation testing rather
than relying on generated code alone. The reported mutation score
improved after Stage 2.
