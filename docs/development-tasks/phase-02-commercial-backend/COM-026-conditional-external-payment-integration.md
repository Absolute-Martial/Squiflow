# COM-026 — Conditional external payment integration

Task ID: COM-026
Phase: 02-commercial-backend
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: COM-025, OPS-003
Conditional dependencies: OPS-005 when provider reconciliation uses scheduled occurrences
Release requirement: CONDITIONAL

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Integrate one explicitly selected payment provider with durable intent, outcome reconciliation and idempotent financial application when the product promise actually requires it.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [INTEGRATION_RESPONSIBILITY_AND_AUTHORITY.md](../../integrations/INTEGRATION_RESPONSIBILITY_AND_AUTHORITY.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: provider-neutral payment intent/outcome ownership and one isolated provider adapter, callbacks/status reconciliation and support reads with focused tests. Exclude default vendor choice, multiple-provider platform, FX and subscription billing.

## Decisions/prerequisites

Activation requires chosen provider/channel, credential/callback protocol, uncertain outcome rules, test environment and ownership. OPS-001/002/003 are needed for introduced durable external execution/recovery; OPS-005 only for scheduled reconciliation.

Omission from full completion requires an explicit owner disposition, safe absence behavior and an activation trigger.

## Acceptance and edge cases

- Stable payment identity and provider key survive transport timeout and retry.
- Pending, succeeded, failed and OutcomeUnknown are distinct durable outcomes.
- Signed callback authentication plus replay protection binds exact tenant/payment/provider.
- Callback and polling races apply one confirmed financial effect.
- An unknown response never permits blind resubmission or false settlement.
- Provider refund/reversal evidence is linked to the owning financial correction.

## Security/static review

Review secrets, callback forgery, SSRF/redirect boundaries and provider SDK isolation. External success is reconciled evidence, not authority to bypass allocation permissions.

## Dynamic verification and unavailable-environment handling

Use selected-provider sandbox or authorized test account plus fault/duplicate tests and actual PostgreSQL. If credentials/environment are unavailable, implement bounded tests and list exact unrun sandbox/recovery checks; keep the integration unqualified. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-026: Conditional external payment integration only.
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
