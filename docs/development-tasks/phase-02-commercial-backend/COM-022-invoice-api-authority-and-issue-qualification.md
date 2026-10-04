# COM-022 — Invoice API authority and issue qualification

Task ID: COM-022
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-021, ADM-010, GATE-001
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Expose bounded invoice issuance and frozen-fact reads through CoreApi with separate current billing authority and complete real-host/provider qualification.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [TENANT_PERMISSIONS.md](../../security/TENANT_PERMISSIONS.md), [INVOICE_ISSUE_CONTRACT.md](../../implementation/INVOICE_ISSUE_CONTRACT.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: services/core-api/Application.CoreApi invoice ingress/composition, the dedicated billing relation in the reviewed tenant model, focused CoreApi/authorization tests and invoice owner updates. Reserve shared model rollout to the integrator.

CoreApi adapters, request/result contracts, OpenAPI, relation checks and focused qualification evidence. Business and SQL meaning stays in the owning capability; exclude tenant Web and automatic rendering/payment.

## Decisions/prerequisites

Cross-track prerequisites: qualified current identity/membership/tenant authority and billing relation rollout. Preserve accepted per-invoice bill-to choices; display format remains separately open unless explicitly selected. OPS-021 applies to independently supported API compatibility.

## Acceptance and edge cases

- Default organization, independent program and explicit individual issue are exercised through owned APIs.
- Billing selection is checked independently; create/edit/manual pricing grants do not imply it.
- Exact committed revision, NPR and accepted inactive-individual eligibility are validated before issue.
- Unauthorized, stale and provider-unavailable responses disclose no bill-to or invoice content.
- Both declared and actual request sizes are bounded and protected responses are no-store.
- Replay rechecks current authority and preserves original frozen facts after authorized recovery.

## Security/static review

Review authentication/authorization sequencing, mass assignment, body limits, error redaction and private Admin separation. Platform administration never implies tenant invoice access.

## Dynamic verification and unavailable-environment handling

Run real-host issue/read/denial/outage tests plus actual PostgreSQL and pinned OpenFGA cases. Inspect the normal gate and relation rollout smoke results separately; HTTP doubles do not qualify real authorization providers. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-022: Invoice API authority and issue qualification only.
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
