# COM-005 — Tenant product and service catalog with explicit units

Task ID: COM-005
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Runtime state: PRODUCTION_HONEST for the declared local scope
Model: GPT-6.1 Sol
Dependencies: COM-001, GATE-001
Release requirement: REQUIRED

`Status` above is the scheduling value defined by [`ORCHESTRATOR.md`](../ORCHESTRATOR.md): this task is no longer an undispatched planned assignment, because its implementation exists in the current receiving tree and awaits accountable owner review/acceptance. The runtime gate state is recorded separately above and is owned by [`CATALOG_AND_UNIT_BOUNDARY.md`](../../implementation/CATALOG_AND_UNIT_BOUNDARY.md). Exact local evidence is recorded in [`README.IMPLEMENTATION.md`](../../../README.IMPLEMENTATION.md) and [`TASK_STATUS.md`](../TASK_STATUS.md).

Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md) before any further work.

## Outcome

Introduce stable tenant item/service identity and useful availability and unit context for price selection, without forcing every business into a ready-made/custom taxonomy.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [PRICING_COMPONENT_BOUNDARY.md](../../implementation/PRICING_COMPONENT_BOUNDARY.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: catalog-owned identity, display fields, only earned tenant categories, availability, quantity/unit rules and protected bounded reads/mutations with focused tests. Identify the owning capability and exact approved paths before adding a project. Existing free-description order entry remains valid if selected; exclude generic units conversion, MRP and stock movements.

## Decisions/prerequisites

Owner decisions were closed on 2026-10-06. Stable product/service and unit identity, immutable/versioned direct conversions, retirement history and independent precise/availability-only/non-stock modes are accepted. Actual numerical inventory remains COM-018. Current implementation/evidence: [CATALOG_AND_UNIT_BOUNDARY.md](../../implementation/CATALOG_AND_UNIT_BOUNDARY.md).

## Acceptance and edge cases

- Product and service identities are tenant-owned and remain explainable after rename or retirement.
- Only supported quantity/unit combinations are accepted; fractional quantities remain possible.
- Unavailable items have explicit new-selection behavior without deleting existing order facts.
- No fixed ready-made, custom-design or social category is mandatory.
- Catalog identity does not itself grant price, stock or invoice authority.
- Revision conflicts and same-key retries retain one attributable configuration change.

## Security/static review

Review category/text bounds, enumeration, independent publication/edit rights and provider-free neutral contracts. New projects require a real boundary, not a planned folder.

## Dynamic verification and unavailable-environment handling

Test tenant isolation, unit mismatch, retirement versus historical reads and edit races with actual PostgreSQL where durable configuration is introduced. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-005: Tenant product and service catalog with explicit units only.
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
