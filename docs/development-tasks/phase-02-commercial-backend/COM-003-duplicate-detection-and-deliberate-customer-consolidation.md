# COM-003 — Duplicate detection and deliberate customer consolidation

Task ID: COM-003
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Runtime state: PRODUCTION_HONEST for the declared local scope
Model: GPT-6.1 Sol
Dependencies: COM-002
Release requirement: REQUIRED

`Status` above is the scheduling value defined by [`ORCHESTRATOR.md`](../ORCHESTRATOR.md): this task is no longer an undispatched planned assignment, because its implementation exists in the current receiving tree and awaits accountable owner review/acceptance. The runtime gate state is recorded separately above and is owned by [`CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md`](../../implementation/CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md). Exact local evidence is recorded in [`README.IMPLEMENTATION.md`](../../../README.IMPLEMENTATION.md) and [`TASK_STATUS.md`](../TASK_STATUS.md).

Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md) before any further work.

## Outcome

Give operators a bounded way to identify likely duplicate customers and resolve the supported duplicate case without destroying issued history or enforcing fragile global uniqueness.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md](../../implementation/CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md), [CUSTOMER_INDIVIDUAL_BILLING_RECORD_SLICE.md](../../implementation/CUSTOMER_INDIVIDUAL_BILLING_RECORD_SLICE.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: Customers-owned duplicate suggestions and one explicitly selected consolidation or keep-separate operation, its receipts and protected reads. Exclude fuzzy matching engines, cross-tenant merging and rewriting frozen invoice debtor facts.

## Decisions/prerequisites

Owner decisions were closed on 2026-10-06: support both explicit keep-separate and manual canonicalization, nonunique normalized signals, independent resolve/consolidate authority and immutable historical facts. Current implementation/evidence and forward redirect semantics are owned by [CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md](../../implementation/CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md), not inferred from this scheduling status.

## Acceptance and edge cases

- Equal names, phones or emails are signals rather than unique business identities.
- Suggestions are tenant-scoped, bounded and explain which input caused the suggestion.
- A chosen survivor keeps traceable predecessor identity and an attributable resolution record.
- Existing committed/issued snapshots still explain the original customer identity.
- Concurrent consolidation and contact editing cannot create cycles or dangling references.
- A replay cannot consolidate twice; unavailable authority cannot approve resolution.

## Security/static review

Review contact-search enumeration, identity spoofing, mutation authorization and destructive cascades. Never use a matching score as merge authority.

## Dynamic verification and unavailable-environment handling

Test deliberate false positives, survivor races, stale versions and historical reads. Actual PostgreSQL must prove reference and receipt atomicity if consolidation is introduced. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-003: Duplicate detection and deliberate customer consolidation only.
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
