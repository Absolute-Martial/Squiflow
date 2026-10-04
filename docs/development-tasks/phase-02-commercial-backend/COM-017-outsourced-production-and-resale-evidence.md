# COM-017 — Outsourced production and resale evidence

Task ID: COM-017
Phase: 02-commercial-backend
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: COM-014, COM-016
Conditional dependencies: COM-015 when supplied artwork/input approval is required; OPS-008 when supplied input bytes are retained
Release requirement: CONDITIONAL

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Connect actual supplier-performed production to customer work and its cost/payable while preserving independent permitted resale pricing and customer settlement.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [PRICING_COMPONENT_BOUNDARY.md](../../implementation/PRICING_COMPONENT_BOUNDARY.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: bounded fulfillment-to-purchasing links, supplied input/evidence, partial outsourced completion and cost explanation. Reuse purchasing and pricing contracts; exclude invented supplier design activity and a manufacturing engine.

## Decisions/prerequisites

Activation needs the outsourced production case and its handoff/discrepancy rules. Retained artwork/input requires COM-015 and OPS-008 when applicable. Do not add a cost-plus pricing rule unless explicitly selected.

Omission from full completion requires an explicit owner disposition, safe absence behavior and an activation trigger.

## Acceptance and edge cases

- Supplier who only prints is recorded only for printing/production.
- Supplied design-ready input is distinguishable from actual performed design work.
- Partial outsourced completion links exact order quantities without duplicate progress.
- Actual cost/payable and authorized resale source are separately attributable.
- Customer settlement and supplier settlement show independent remaining balances.
- Supplier rejection or incomplete work creates an explicit continuation/discrepancy path.

## Security/static review

Review cross-capability tenant/identity links and disclosure of supplier costs to unauthorized customer-facing reads. No provider response alone proves a received business effect.

## Dynamic verification and unavailable-environment handling

Qualify an outsourced case through owned APIs with actual PostgreSQL partial receipt, replay and link races. Test lower authorized resale price without automatic markup and unpaid supplier after customer settlement. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-017: Outsourced production and resale evidence only.
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
