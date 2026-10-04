# COM-009 — Optional quotations and immutable issued revisions

Task ID: COM-009
Phase: 02-commercial-backend
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: COM-007
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Provide optional quotation drafting and issuance with retained offer facts and later explicit issued revisions. Direct order entry remains a complete supported entry path.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [PRICING_COMPONENT_BOUNDARY.md](../../implementation/PRICING_COMPONENT_BOUNDARY.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: quotation-owned draft/issue/revision contracts, pricing integration, protected API and persistence with focused tests. Retain applied price/source/currency/customer context; exclude tender-specific bureaucracy, conversion and mandatory quotation requirements.

## Decisions/prerequisites

Decide first quotation fields, numbering, validity, business timezone, issuing authority and revision/supersession behavior. Document bytes/templates are OPS-009 only when required; issue facts must not depend on rendering success.

## Acceptance and edge cases

- Drafts use supported price selection and independent manual-entry/override permissions.
- Issuance freezes offer lines, context, validity and actor/time in an immutable revision.
- Later revision cannot overwrite a previously issued version or accepted evidence.
- Invalid validity intervals and unsupported units/currency fail explicitly.
- Issue retries retain one version; concurrent issue/revise commands have a clear winner.
- Direct order creation and commitment still work without quotation identity.

## Security/static review

Review offer disclosure, customer scope, issue rights and historical evidence integrity. No issued quotation is a receivable or payment fact.

## Dynamic verification and unavailable-environment handling

Test price changes after issue, concurrent revisions, numbering/retry behavior and actual PostgreSQL rollback/receipt atomicity. Test API denial/outage and historical reads. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-009: Optional quotations and immutable issued revisions only.
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
