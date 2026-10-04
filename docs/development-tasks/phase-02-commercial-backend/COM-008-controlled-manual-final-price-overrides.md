# COM-008 — Controlled manual final-price overrides

Task ID: COM-008
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-007
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Apply the accepted reason, ceiling and approval rules to permitted manual final prices while preserving independent manual-entry authority for initial entry and full later replacement.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [PRICING_COMPONENT_BOUNDARY.md](../../implementation/PRICING_COMPONENT_BOUNDARY.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: pricing-owned override validation and evidence, Orders integration and protected guidance. Add only selected reason/approval fields and compatibility changes; exclude automatic markup, cost-based resale restrictions and blanket Owner bypass.

## Decisions/prerequisites

Follow COM-006's accepted reason and threshold choices. Approval integration depends on COM-012 only when a selected price rule requires approval; ordinary permitted manual entry must remain usable.

## Acceptance and edge cases

- Create/preview keep create plus manual pricing; full replacement keeps edit plus manual pricing.
- Numerically unchanged full replacement still checks current pricing authority.
- Required reason is bounded and retained with actor, context and applicable policy version.
- Unauthorized, out-of-scope or over-ceiling final price is denied without storing an effect.
- Approved exceptions bind the exact priced revision and cannot authorize a different total.
- Revoked permissions affect replay without erasing previously committed historical values.

## Security/static review

Review reason content redaction, privilege escalation and comparisons at exact decimal boundaries. Guidance and UI fields never grant an exception.

## Dynamic verification and unavailable-environment handling

Test initial/later entry, unchanged replacement, floor/ceiling boundaries, stale approval and revocation replay; prove retained evidence and atomic receipts with actual PostgreSQL. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-008: Controlled manual final-price overrides only.
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
