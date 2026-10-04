# COM-015 — Artwork approval and accountable handoff

Task ID: COM-015
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-014
Conditional dependencies: OPS-008 when artwork bytes are retained; OPS-009 when handoff includes a generated document
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Bind approval to the exact artwork or supplied production input and record the supported pickup/delivery handoff facts without inventing design work or equating handoff with payment.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [WORKFLOW_DESIGN.md](../../workflow/WORKFLOW_DESIGN.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: fulfillment-owned artwork revision references, approval evidence and partial handoff/discrepancy receipts. OPS-008 is a prerequisite when artwork bytes are retained; exclude a client portal and local printer implementation.

## Decisions/prerequisites

Choose actual artwork types, approving actor/evidence and handoff fields. Owner may select a no-artwork service case; do not demand an upload for every order. OPS-009 applies only to a generated document handoff requirement.

## Acceptance and edge cases

- Approval references one immutable usable artwork/input revision.
- Changed artwork invalidates or requires explicit renewed approval as selected.
- Rejected/quarantined/missing bytes cannot become approved printable content.
- Partial handoff names fulfilled lines/quantities and records discrepancy or recipient evidence.
- Provider/file failure leaves durable business work discoverable rather than falsely delivered.
- Physical print failure cannot reverse a previously committed order or invoice.

## Security/static review

Review object authorization, untrusted filenames/types, recipient privacy and evidence spoofing. File access rights and operator/customer approval meaning remain separate.

## Dynamic verification and unavailable-environment handling

Test old/new artwork races, unauthorized object access, quarantine, partial pickup and duplicate handoff. Actual PostgreSQL plus the qualified file adapter proves reference durability; report unrun storage/print checks separately. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-015: Artwork approval and accountable handoff only.
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
