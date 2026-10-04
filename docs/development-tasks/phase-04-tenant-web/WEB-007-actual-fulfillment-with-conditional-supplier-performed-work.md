# Actual fulfillment with conditional supplier-performed work

Task ID: WEB-007
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6 Luna (high)
Dependencies: WEB-005, COM-015
Conditional dependencies: COM-017 when supplier-performed work is selected; OPS-008 when artwork uploads are selected
Release requirement: REQUIRED

## Outcome

Present required actual production/fulfillment and partial handoff for the complete operator journey. Linked outsourced work is a conditional branch when the qualified backend supports it.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/BUSINESS_OPERATION_END_TO_END.md`; `docs/domain/BUSINESS_MODEL.md`; `docs/workflow/WORKFLOW_DESIGN.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write selected fulfillment routes/components and browser checks under apps/web. Exclude equipment automation, Workstation printing, supplier accounting mutations, stock arithmetic and an invented universal production workflow.

## Decisions/prerequisites

Backend prerequisites: selected required work stages, quantity semantics, handoff/pickup/delivery facts, discrepancies, cancellation rights and qualified outsourcing linkage. Supplier cost/payable and customer resale authority are independently qualified before display. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Actual work and remaining quantity are shown separately from order commitment.
- Partial completion and repeated handoff follow current revision/receipt contracts.
- Required artwork/customer approval is visible only if its backend exists.
- Outsourcing records only supplier-performed work, not invented design steps.
- Supplier cost/payable and customer selling price remain distinct and field-authorized.
- Correction/cancellation explains already committed effects and remaining work.
- Disconnected or uncertain submission cannot report a completed handoff.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review customer artwork/contact access, cost/margin visibility and cross-tenant work IDs. Accessible status text cannot rely solely on colour; confirmation must describe the real effect.

## Dynamic verification and unavailable-environment handling

Run Playwright against real selected fulfillment/outsourcing APIs for partial work, stale revisions, denied cost fields and retry outcomes. Retain permanent case regressions without mocking transactional claims. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Select the required qualified fulfillment case.
Read its quantity/stage and conditional outsourcing contracts.
Build clear current/remaining-work views.
Display only authorized costs and customer facts.
Submit explicit server-owned stage commands.
Explain partial effects and discrepancies.
Do not equate handoff with payment or settlement.
Test concurrency, response loss and forbidden work IDs.
Leave physical printing and offline work deferred.
Deliver source ZIP and case-specific evidence.
```
