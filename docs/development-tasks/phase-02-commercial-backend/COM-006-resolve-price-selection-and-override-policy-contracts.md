# COM-006 — Resolve price selection and override policy contracts

Task ID: COM-006
Phase: 02-commercial-backend
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: COM-005
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Close only the concrete pricing choices needed for supported customers and produce examples that can be implemented without guessing source precedence or override authority.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [PRICING_COMPONENT_BOUNDARY.md](../../implementation/PRICING_COMPONENT_BOUNDARY.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: focused pricing owner and decision records with executable decision examples; preserve current calculator, manual_pricer model and initial/later manual entry. No speculative price engine or new persistence belongs in this decision assignment.

## Decisions/prerequisites

Choose priced identity, quantity/unit/currency/validity context; precedence, fallback and conflict among default, organization/program, wholesale, negotiated quote and permitted final/resale prices. Decide reason evidence, floors/ceilings, approval and draft revalidation.

## Acceptance and edge cases

- Operator entry stays a valid initial source and requires its separate current permission.
- Each supported context has a missing-price and conflicting-price outcome.
- Selection explains source and effective revision without leaking other customers' prices.
- Initial and later manual changes retain explicit reason/approval requirements when selected.
- Quantity breaks or discounts are included only for a demonstrated case.
- Issued facts never recompute after a policy change; unissued work has an explicit revalidation rule.

## Security/static review

Review privilege separation among publishers, manual pricers and approvers; customer scope cannot grant permission. Verify policy language cannot bypass protected financial facts.

## Dynamic verification and unavailable-environment handling

Provide table-driven examples for precedence, absent/expired price and override ceilings. Decision approval and documented examples qualify the contract only; no runtime pass is claimed. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-006: Resolve price selection and override policy contracts only.
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
