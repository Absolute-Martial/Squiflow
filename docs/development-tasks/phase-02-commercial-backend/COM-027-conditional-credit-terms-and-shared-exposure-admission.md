# COM-027 — Conditional credit terms and shared exposure admission

Task ID: COM-027
Phase: 02-commercial-backend
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: COM-025, COM-013
Release requirement: CONDITIONAL

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Enforce selected credit terms, limits and overdue rules against current shared debtor exposure when credit sales are part of the supported promise.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [NATIVE_RULE_ENGINE.md](../../rules/NATIVE_RULE_ENGINE.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: credit-owned policy/exposure semantics, guarded admission/reservation only as earned, explicit approval exception and protected explanation with focused tests. Exclude credit scoring, lending, cached-authority decisions and a universal credit engine.

## Decisions/prerequisites

Choose exposure components and decision point: unbilled commitments, invoices, confirmed allocations and credits as applicable; decide organization/program/individual sharing explicitly. Credit exceptions depend on COM-012. Do not force credit checks on prepaid/non-credit cases.

Omission from full completion requires an explicit owner disposition, safe absence behavior and an activation trigger.

## Acceptance and edge cases

- Exposure belongs to the selected debtor/pool rather than mere customer attribution.
- Concurrent admissions cannot exceed the selected shared limit through stale reads.
- Unavailable current facts fail closed or wait, never authorize from a cached balance.
- Overdue state and exception approval have bounded attributable explanation.
- Settlement/credit/cancellation releases only the exposure actually removed.
- Policy change does not erase prior authoritative commitments or issued history.

## Security/static review

Review limit-edit/exception/admission privilege separation, pool cross-tenant links and exact concurrency mechanism. OpenFGA owns permission, not financial exposure.

## Dynamic verification and unavailable-environment handling

Actual PostgreSQL concurrent exposure tests are mandatory; include repayment/credit/admission races, revoked exception and provider outage. Report conditional safe absence and forbid claiming credit-supported release without these checks. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-027: Conditional credit terms and shared exposure admission only.
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
