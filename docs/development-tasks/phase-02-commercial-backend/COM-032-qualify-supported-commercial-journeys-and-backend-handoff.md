# COM-032 — Qualify supported commercial journeys and backend handoff

Task ID: COM-032
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: GATE-001, COM-003, COM-010, COM-013, COM-015, COM-022, COM-025, COM-028, COM-029, COM-030, COM-031, OPS-009, OPS-013
Conditional dependencies: COM-004 when customer import is selected; COM-016 when purchasing is selected; COM-017 when outsourcing is selected; COM-018 when tracked stock is selected; COM-026 when provider payments are selected; COM-027 when credit is selected
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Prove the connected supported commercial backend through owned APIs and deliver a precise frontend-readiness package, with each enabled conditional case qualified separately.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: focused journey regression tests and integration/requalification handoff only; fix newly exposed defects within separately bounded ownership. Exclude frontend implementation, product-version bump and declaring the whole production product qualified.

## Decisions/prerequisites

Cross-track prerequisites: GATE-001 plus relevant identity/authority and operational qualifications; GATE-002 owns final backend readiness. COM-004/016/017/018/026/027 are conditional dependencies only when their workloads are included. Required quotations are optional in transaction use, not a mandatory order-entry step.

## Acceptance and edge cases

- Organization-default direct order completes partial work, invoice, manual payment/allocation and corrections.
- Independent program and explicitly selected individual remain distinct frozen debtors.
- Optional quote issues/revises, accepts and converts once while direct orders remain usable.
- Selected customer variation requires correct information/approval and recovers an unavailable actor.
- Partial cancellation/return/credit/refund explains every remaining business effect.
- Enabled outsourcing/stock/credit/provider cases add their exact financial and recovery evidence.
- Forbidden authority, cross-tenant links, stale revisions and uncertain-response retries cannot duplicate effects.

## Security/static review

Review complete authority flow, secret/error redaction and public non-claims. No mock may replace the transactional/provider property this journey claims.

## Dynamic verification and unavailable-environment handling

Run named actual PostgreSQL/OpenFGA/real-host journeys and inspect normal ./eng/verify.sh. Report each unrun external/deployment check separately. Frontend readiness requires relevant BLOCKED items closed by inspected evidence, not a finished task checklist. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-032: Qualify supported commercial journeys and backend handoff only.
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
