# COM-007 — Published price policy and explainable selection

Task ID: COM-007
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Runtime state: PRODUCTION_HONEST for the declared local scope
Model: GPT-6.1 Sol
Dependencies: COM-006, ADM-008
Release requirement: REQUIRED

`Status` above is the scheduling value defined by [`ORCHESTRATOR.md`](../ORCHESTRATOR.md): this task is no longer an undispatched planned assignment, because its implementation exists in the current receiving tree and awaits accountable owner review/acceptance. The runtime gate state is recorded separately above and is owned by [`PRICING_POLICY_AND_PUBLICATION.md`](../../implementation/PRICING_POLICY_AND_PUBLICATION.md). Exact local evidence is recorded in [`README.IMPLEMENTATION.md`](../../../README.IMPLEMENTATION.md) and [`TASK_STATUS.md`](../TASK_STATUS.md).

Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md) before any further work.

## Outcome

Implement the selected price sources and durable publication for the first actual organization/program variation, returning typed selected values and explanation to Orders and future consumers.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [PRICING_COMPONENT_BOUNDARY.md](../../implementation/PRICING_COMPONENT_BOUNDARY.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: pricing-owned bounded definitions, immutable publication revisions, scoped selection and provider persistence; protected publication/selection APIs and focused tests. Reuse admitted calculation rather than copying arithmetic into hosts; exclude supplier cost, tax and arbitrary expressions.

## Decisions/prerequisites

COM-006 precedence/validity/publication decisions were closed on 2026-10-06. Current source implements the accepted book scopes and independent publication authority; receiving evidence lives in [PRICING_POLICY_AND_PUBLICATION.md](../../implementation/PRICING_POLICY_AND_PUBLICATION.md) and [ORDER_CATALOG_PRICED_DRAFTS.md](../../implementation/ORDER_CATALOG_PRICED_DRAFTS.md). Cross-track prerequisites remain real qualification of grants/revision compatibility, not new product decisions. COM-009 owning quotation/agreement facts and OPS-021 independent-client compatibility are separate scopes.

## Acceptance and edge cases

- Effective resolution stays within the current tenant and applicable item/unit/currency/context.
- Concurrent publication yields an explicit active revision rather than mixed values.
- Missing, expired or conflicting sources produce typed non-success outcomes.
- Selection evidence identifies source/revision and cannot itself authorize order admission.
- Publication cannot silently reprice committed or issued documents.
- Unissued revalidation uses the selected rule and returns an explainable conflict when needed.

## Security/static review

Review publication versus selection permissions, stored definition bounds, policy leakage and immutable historical references. Neutral selection performs no external I/O or mutation.

## Dynamic verification and unavailable-environment handling

Test precedence and expiry deterministically; actual PostgreSQL proves publication/revision races, restart retention and tenant isolation. Real-host tests prove failures precede effects. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-007: Published price policy and explainable selection only.
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
