# COM-021 — Atomic PostgreSQL invoice issuance and numbering

Task ID: COM-021
Phase: 02-commercial-backend
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: COM-020, GATE-001
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Persist one authoritative invoice, selected debtor snapshot, numbering and retry receipt atomically under the accepted full-invoice or partial/multiple allocation rule.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [INVOICE_ISSUE_CONTRACT.md](../../implementation/INVOICE_ISSUE_CONTRACT.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: invoice PostgreSQL adapter and migrations, application issuance transaction, scoped runtime grants and actual provider regressions. Integrator reserves migrator ordering and solution/package registration. Exclude receivable posting, invoice HTTP, rendering and payment effects.

## Decisions/prerequisites

Use the four accepted COM-019 choices and qualified COM-020 contracts. Dedicated live billing ingress/model rollout belongs to COM-022; this provider task preserves the authority-port contract without adding an endpoint. Issue does not depend on rendering or notifications. The focused invoice owner defines business behavior and any required observation/serialization points.

## Acceptance and edge cases

- Tenant identity uniqueness and organization reference allocation survive concurrent issue.
- Invoice header/lines, frozen bill-to/prices, numbering and caller-scoped receipt commit together.
- Failed transaction leaves no orphan issue facts or consumed successful issue receipt.
- Same-key replay returns the original facts; changed payload conflicts.
- Arbitrate receipt identity before mapping an Order-allocation conflict. Recover a uniqueness failure with rollback/fresh transaction, an intentional savepoint or equivalent safe arbitration; never query an aborted transaction.
- Concurrent issue enforces the accepted one-full or partial/multiple allocation contract.
- RLS, explicit tenant predicates and least-privilege grants prevent runtime rewriting issued history.
- Validate quantity > 0 and prices/totals >= 0 under existing precision/range rules; PostgreSQL numeric coercion must not silently replace application ToEven validation.
- Known precommit failures roll back all local effects. Connection loss during COMMIT is OutcomeUnknown until durable same-key recovery, not proof of rollback.

## Security/static review

Review number locks/constraints, migration compatibility, parameterized embedded SQL, immutable grants, receipt version/source/caller linkage and bounded retries. A SECURITY DEFINER counter function is optional and earns its own locked search_path, schema-qualified SQL, PUBLIC EXECUTE revocation, tenant validation and non-bypassing execution-role tests; EXECUTE alone is not RLS proof. Do not assert gapless fiscal numbering or that RLS authenticates a caller-selected tenant context.

## Dynamic verification and unavailable-environment handling

Actual PostgreSQL must prove numbering races, rollback, ambiguous-response replay, cross-tenant isolation and runtime permission denial. Register recurring provider regressions in the normal repository gate; mocks cannot qualify atomicity. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-021's provider slice only after the four COM-019 choices are accepted.
Inspect the existing IInvoiceIssueStore and qualified COM-020 behavior.
Keep invoice business meaning out of SQL and HTTP.
Commit allocation, header, immutable lines and caller receipt atomically.
Resolve same-key races through safe transaction recovery and receipt-first reread.
Enforce the accepted Order allocation and organization-reference uniqueness.
Preserve forced RLS, embedded parameterized SQL and restricted runtime grants.
Test rollback, uncertain commit, restart replay, zero prices and permission denial.
Review optional counter-function privileges before adopting that mechanism.
Return source ZIP, hashes and actual PostgreSQL evidence; no endpoint or commit.
```
