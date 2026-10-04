# COM-018 — Conditional item tracking and inventory movements

Task ID: COM-018
Phase: 02-commercial-backend
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: COM-005, COM-014
Conditional dependencies: COM-016 when stock is received through purchasing
Release requirement: CONDITIONAL

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Implement only the inventory modes needed by named supported items: precise quantity, availability-only or non-stock/service, with attributable adjustments and authoritative concurrency where stock is tracked.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: inventory-owned movements/current stock, damaged/unusable adjustment and fulfillment/purchase integration for precise items with focused tests. Availability-only records are not balances; exclude universal reservation, BOM/MRP, lots/serials and wastage optimization.

## Decisions/prerequisites

Activation requires real tracked items, movement/effective-time semantics, negative-stock policy, reversal/disposition rules and unit conversion only if necessary. COM-016 is a prerequisite only when tracked purchase receipts are selected.

Omission from full completion requires an explicit owner disposition, safe absence behavior and an activation trigger.

## Acceptance and edge cases

- Non-stock service creates no fictional stock movements.
- Availability-only mode never claims an exact available quantity.
- Precise movements retain quantity/unit, source, actor and immutable effect identity.
- Concurrent consumers enforce selected stock constraints using current server authority.
- Damaged adjustment is attributable and distinct from ordinary fulfillment.
- Tracking-mode change preserves historical interpretation and cannot erase prior movements.

## Security/static review

Review stock-adjustment grants, RLS, source forgery and numeric bounds. Current stock guidance is not stale cache authority.

## Dynamic verification and unavailable-environment handling

Test concurrent depletion, movement replay, damaged stock and mode transitions with actual PostgreSQL. Include all enabled tracking modes; document safe absence for unselected modes. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-018: Conditional item tracking and inventory movements only.
Read current owners and applicable instructions.
Inspect existing callers and tests; preserve incoming work.
Close listed decisions before dependent contracts.
Implement the smallest complete scope and focused tests.
Review security, authority, durability and concurrency.
Update focused behavior/decision documentation.
Run available checks; name exact unrun checks.
Deliver source-only ZIP and evidence handoff.
No commit/push or unrun qualification claims.
```
