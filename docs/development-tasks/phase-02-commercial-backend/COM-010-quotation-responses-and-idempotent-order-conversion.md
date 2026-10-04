# COM-010 — Quotation responses and idempotent order conversion

Task ID: COM-010
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-009
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Record acceptance, rejection and expiry of a specific issued offer and convert a valid accepted version to the supported order without duplicate creation on retry.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [ORDER_COMMITMENT_SLICE.md](../../implementation/ORDER_COMMITMENT_SLICE.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: quotation response/conversion ownership, Orders entry integration, protected commands and retained links with focused tests. Keep conversion distinct from order commitment; exclude portal authentication and automatic invoice or fulfillment effects.

## Decisions/prerequisites

Select permitted accepting actor/evidence, expiry decision point, supersession rules and whether acceptance requires fresh price revalidation. No external customer identity is inferred from an operator login.

## Acceptance and edge cases

- Acceptance names one exact issued version and rejects an expired or superseded offer as defined.
- Rejection and expiry retain reason/time without deleting issued facts.
- Duplicate conversion returns the linked order rather than creating a second order.
- Two accepted-version conversion attempts cannot race into inconsistent order links.
- Converted priced facts retain quotation origin and any explicitly accepted revalidation evidence.
- Direct orders remain valid and conversion never fabricates fulfillment or invoice state.

## Security/static review

Review acceptance evidence spoofing, cross-tenant links, permission checks on replay and atomic transaction ownership across the real boundary.

## Dynamic verification and unavailable-environment handling

Prove conversion/link/receipt recovery with actual PostgreSQL crash and concurrency tests. Exercise exact expiry boundaries and protected API failures; mocks cannot qualify duplicate-conversion durability. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-010: Quotation responses and idempotent order conversion only.
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
