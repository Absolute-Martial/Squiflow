# COM-002 — Customer contacts and representative relationships

Task ID: COM-002
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-001, GATE-001, ADM-008
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Allow authorized operators to maintain useful contacts and explicit representatives for an organization or program while keeping customer identity, operator identity and billing authority separate.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md](../../implementation/CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md), [CUSTOMER_INDIVIDUAL_BILLING_RECORD_SLICE.md](../../implementation/CUSTOMER_INDIVIDUAL_BILLING_RECORD_SLICE.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: Customers-owned contact editing and representative relationships in modules/customers, narrow PostgreSQL persistence, protected CoreApi operations and focused Customers/CoreApi tests. Reuse existing individual records where appropriate; exclude portal login, billing assignment and generic Parties restoration.

## Decisions/prerequisites

Confirm editable fields, relationship meaning, availability and independent permissions before exposing mutations. A contact person is not automatically an independent debtor or authorized approver.

## Acceptance and edge cases

- Revision-checked editing preserves historical transaction identity and records attributable command receipts.
- A representative may relate to an organization or its valid child program within one tenant.
- Same-name people and shared phone/email values remain legitimate.
- Inactive contacts cannot be silently used where an active relationship is required.
- Cross-tenant targets, stale revisions and replay after permission revocation fail safely.
- Reads and transition responses expose only independently authorized personal information.

## Security/static review

Review mass assignment, contact-data leakage, field bounds, SQL parameters and tenant/RLS protection. No business access follows from a representative label.

## Dynamic verification and unavailable-environment handling

Exercise edit/link/unlink races, same-key replay, outage and isolation in actual PostgreSQL plus real-host permission cases; preserve existing customer create/read behavior. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-002: Customer contacts and representative relationships only.
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
