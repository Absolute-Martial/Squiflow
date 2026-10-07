# COM-008 — Controlled manual final-price overrides

Task ID: COM-008
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Model: GPT-6.1 Sol
Dependencies: COM-007
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Review existing reasoned overrides, policy envelopes and separate beyond-policy
authority, closing only demonstrated gaps. Preserve independent manual-entry
authority for initial entry and full later replacement.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [PRICING_COMPONENT_BOUNDARY.md](../../implementation/PRICING_COMPONENT_BOUNDARY.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: pricing-owned override validation and evidence, Orders integration and protected guidance. Add only selected reason/approval fields and compatibility changes; exclude automatic markup, cost-based resale restrictions and blanket Owner bypass.

## Decisions/prerequisites

COM-006 selected the reason plus bounded absolute/percentage envelope and
additional beyond-policy capability branch on 2026-10-06. These controls already
exist; do not rebuild them or add an unselected approval requirement. Current
owners are [Pricing](../../implementation/PRICING_POLICY_AND_PUBLICATION.md)
and [catalog-priced Orders](../../implementation/ORDER_CATALOG_PRICED_DRAFTS.md).
Approval integration depends on COM-012 only when a later selected rule requires
it; ordinary permitted manual entry remains usable.

## Acceptance and edge cases

- Create/preview keep create plus manual pricing; full replacement keeps edit plus manual pricing.
- Numerically unchanged full replacement still checks current pricing authority.
- Required reason is bounded and retained with actor, context and applicable policy version.
- Unauthorized or out-of-scope prices, and outside-envelope prices without the additional current beyond-policy capability, are denied without storing an effect.
- Retained exception evidence binds the exact priced revision and cannot authorize a different total. Approval-specific claims apply only if that separate branch is selected.
- Revoked permissions affect replay without erasing previously committed historical values.

## Security/static review

Review reason content redaction, privilege escalation and comparisons at exact decimal boundaries. Guidance and UI fields never grant an exception.

## Dynamic verification and unavailable-environment handling

Test initial/later entry, unchanged replacement, exact envelope boundaries and
revocation/outage on replay; prove retained evidence and atomic receipts with
actual PostgreSQL. Test stale approval only if the separate approval branch is
selected and implemented. Run `./eng/verify.sh` when available. Without
SDK/Docker/credentials, implement tests, finish static review and hand off exact
unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Review COM-008: existing controlled manual final-price overrides only; fix demonstrated gaps without reimplementation.
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
