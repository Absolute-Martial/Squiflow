# COM-028 — Cancellation before committed irreversible effects

Task ID: COM-028
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-014, COM-025
Conditional dependencies: OPS-003 when cancellation affects durable dispatched work; COM-017 when supplier work is involved; COM-018 when tracked stock is involved; COM-027 when credit exposure is involved
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Cancel only remaining reversible work with an attributable outcome that identifies already committed fulfillment and financial effects requiring separate correction.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [WORKFLOW_DESIGN.md](../../workflow/WORKFLOW_DESIGN.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: Orders/fulfillment-owned cancellation eligibility, remaining-work transition and history with focused tests. Preserve existing draft abandonment; exclude deleting issued invoices, undoing successful payments and pretending external supplier work was reversed.

## Decisions/prerequisites

Define irreversible boundaries and permission per actual supported stage. Inventory/outsourcing/credit integration is required only when COM-018/017/027 are enabled. Partial cancellation must name the portion still reversible.

## Acceptance and edge cases

- Draft abandonment remains its existing narrow one-way behavior.
- Cancellation reports exact pending quantity/work that stopped.
- Already fulfilled or invoiced portions survive and link to applicable compensation guidance.
- Concurrent handoff/issue/cancel has an explicit winner without false rollback.
- Retry returns the same cancellation result after authority recheck.
- External pending work is not declared cancelled before its actual outcome is known.

## Security/static review

Review cancellation scope, source-version checks and cross-capability atomic boundaries. Generic Cancel cannot grant credit/refund/stock adjustment rights.

## Dynamic verification and unavailable-environment handling

Prove before-effect and partial-effect cancellation through owned APIs with actual PostgreSQL races. If external dispatch exists, use qualified OPS-002/003 cancellation semantics and test late results without erasing business effects. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-028: Cancellation before committed irreversible effects only.
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
