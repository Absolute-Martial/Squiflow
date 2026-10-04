# Priced draft entry, preview, revision and abandonment

Task ID: WEB-004
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: WEB-003, COM-008, COM-011
Release requirement: REQUIRED

## Outcome

Provide operator order-entry and draft maintenance over qualified pricing/Orders contracts, preserving authoritative valuation, revision history and distinct action permissions.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/ORDER_DRAFT_INTAKE_SLICE.md`; `docs/implementation/PRICING_COMPONENT_BOUNDARY.md`; `docs/implementation/ORDER_DRAFT_HISTORY.md`; `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write only apps/web order-entry/draft presentation and its tests. Business price selection, arithmetic, idempotency, persistence and new policy publication remain backend-owned; do not create browser autosave authority.

## Decisions/prerequisites

Backend prerequisites: qualified draft/preview/history contracts, chosen pricing-source/override policy, effective customer/program information and bounded validation. Valuable draft recovery uses an explicit backend resource; transient component/circuit state is not a saved draft. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Price preview uses the same server valuation and never reports persisted creation.
- Manual pricing requires its independent grant with create/edit; view/abandon remain separate.
- Revisions submit full replacement with expected revision and preserve prior issued facts.
- Abandon is one-way and renders only permitted outcome metadata.
- Quantity, unit, currency and line bounds match backend contracts without local business arithmetic.
- Response loss retains the same semantic key; changed intent uses a deliberate new operation.
- Expired session, stale revision and reconnect preserve safe form work but reauthorize before submit.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review amount/precision rendering, excessive fields, protected prices and persisted browser storage. Separate pending, receipt-confirmed and unknown outcomes; encoded descriptions and labelled numeric inputs are required.

## Dynamic verification and unavailable-environment handling

Run .NET 10/Playwright against actual pricing APIs with real permission/transaction checks; inject response loss and a concurrent revision deterministically. Retain preview parity and denial/retry regressions. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Trace create, preview, revise, history and abandon contracts.
Implement the smallest coherent draft editor.
Display server totals and price-source evidence.
Do not calculate authoritative prices in UI or JavaScript.
Keep semantic keys and expected revisions explicit.
Distinguish in-memory edits from confirmed server drafts.
Cover response loss and concurrent editors.
Preserve independent view/edit/price/abandon grants.
Report missing provider/browser checks honestly.
Return source ZIP and permanent evidence.
```
