# COM-001 — Verify the commercial starting point

Task ID: COM-001
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Model: GPT-6 Luna (high)
Dependencies: BAS-001
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Produce a source-backed preservation map for the commercial journey before adding new business effects. Identify already implemented operations and their exact evidence rather than rebuilding them.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [ORDER_COMMITMENT_SLICE.md](../../implementation/ORDER_COMMITMENT_SLICE.md), [CUSTOMER_INDIVIDUAL_BILLING_RECORD_SLICE.md](../../implementation/CUSTOMER_INDIVIDUAL_BILLING_RECORD_SLICE.md), [PRICING_COMPONENT_BOUNDARY.md](../../implementation/PRICING_COMPONENT_BOUNDARY.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: read Orders, Customers, incoming Invoices, their adapters, CoreApi contracts and existing tests. Write only a bounded review/handoff artifact unless a separately assigned defect fix is authorized; preserve all incoming edits. Record invoice-domain work as present and awaiting qualification, not a missing module to recreate.

## Decisions/prerequisites

This read-only inventory follows BAS-001 and can precede GATE-001; it supplies the source facts needed to qualify incoming invoice work. New commercial runtime implementation waits GATE-001 where declared. Source-first truth outranks stale project counts or historical gate prose.

## Acceptance and edge cases

- Direct commitment freezes priced content and attribution without invoice, fulfillment or payment effects.
- Individuals retain name, optional email/phone and availability; they have no login or debtor-assignment authority.
- Create/preview require create plus manual pricing; full replacement requires edit plus pricing, including replay.
- Map existing receipt versions, tenant/RLS predicates, decimal arithmetic and historical read compatibility.
- Record current blockers and absent capabilities separately; do not convert an unrun test into a pass.

## Security/static review

Inspect current permission sequencing, receipt compatibility and provider isolation. Report concrete gaps with evidence; do not expand permissions or remove guards during review.

## Dynamic verification and unavailable-environment handling

Inspect named existing Orders/Customers/CoreApi regressions and run the normal repository gate when available; report exact command, result and test counts. A read-only map can be completed without qualifying runtime behavior. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-001: Verify the commercial starting point only.
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
