# Conditional item inventory and movement screens

Task ID: WEB-009
Phase: 04-tenant-web
Status: CONDITIONAL
Model: GPT-6 Luna (high)
Dependencies: WEB-005, COM-018
Release requirement: CONDITIONAL

## Outcome

Expose precise item inventory only where the selected backend case enables tracking, with explainable movements, availability and authorized adjustments.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/BUSINESS_OPERATION_END_TO_END.md`; `docs/domain/BUSINESS_MODEL.md`; `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write inventory browse/detail/movement/adjustment components and browser checks under apps/web. Exclude business quantity arithmetic, universal warehouses, reservations or scanning/device features absent from the qualified contracts.

## Decisions/prerequisites

Backend prerequisites: accepted item/unit precision, tracking-versus-availability-only rules, movement ledger, negative-stock/concurrency policy and adjustment authority. Supported purchasing/fulfillment links qualify separately rather than becoming implicit stock mutations. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Non-stock/service items do not acquire fictional quantity balances.
- Server amounts/units and movement reasons are rendered without UI arithmetic.
- Availability-only items are labelled distinctly from precise tracking.
- Concurrent consumption/adjustment conflicts show current state and deliberate retry.
- Damage/return adjustments reference the actual supported corrective effect.
- View and adjust permissions remain independent after live revocation.
- Lists are bounded and unauthorized item/location IDs disclose no tenant facts.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Trace stock-sensitive freshness, adjustment field allow-lists and encoded reasons. Do not cache balances as authority or represent a client-calculated quantity as accepted availability.

## Dynamic verification and unavailable-environment handling

Use real .NET 10 APIs/Playwright with actual stock transactions for concurrent adjustments, stale versions, bounds and denial. Retain permanent browser cases plus backend-owned ledger/concurrency evidence. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Verify the supported inventory case and accepted units.
Use only qualified item and movement contracts.
Build clear tracking/availability views.
Submit explicit adjustment intent and expected version.
Treat displayed balance as observation, never authority.
Explain conflicts and unavailable fresh state.
Test independent rights and concurrent movement.
Keep services/non-stock items meaningful.
Avoid warehouse/device scaffolding.
Return source ZIP and stock case evidence.
```
