# ADM-023 — Provide authoritative administrative audit reads

Task ID: ADM-023
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-006, ADM-010, ADM-020, OPS-013
Release requirement: REQUIRED
Cross-track prerequisites: OPS-013

## Outcome

Expose authorized bounded audit reads linking proposal, externally observed effect and completion for implemented control operations without confusing access success with business completion.

## Current basis and canonical inputs

PlatformAdmin access audit and bootstrap audit already persist safe evidence; onboarding/membership/lifecycle commands retain actor/device receipts. Program exposes no audit/history browsing API. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [PlatformAdminAccessAudit.cs](../../../modules/platform-administration/Application.PlatformAdministration/PlatformAdminAccessAudit.cs); [ADMIN_SURFACES.md](../../admin/ADMIN_SURFACES.md); [APPLICATION_KERNEL_AND_MODULES.md](../../architecture/APPLICATION_KERNEL_AND_MODULES.md).

## Scope and exclusions

Allowed areas: Existing audit/receipt owners’ explicit read projections, bounded AdminApi audit endpoints, indexes and authorization tests; no centralized rewrite of every history table. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Accept minimum filters/retention visibility and distinct audit-view permission. Keep tenant audit and platform security scope separate; selective adapters may read existing receipts rather than duplicate them. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- An authorized access audit is not shown as a committed command effect.
- Pending/ambiguous/reconciled outcomes remain distinguishable.
- Foreign-tenant audit cannot leak through tenant-scoped view.
- Cursor pagination is deterministic and bounded.
- Read/export performs no mutation and respects cancellation.
- Stored secrets/customer bodies never appear in audit responses or errors.

## Security/static review

Inspect immutable evidence protection, PII fields, cardinality/index bounds, unsafe filter interpolation and stable actor identities after suspension. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run real PostgreSQL projection/pagination and retained-outcome correlation tests, denied audit-read routes and safe redaction checks; run ./eng/verify.sh. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-023: Provide authoritative administrative audit reads.
Read AGENTS.md and linked focused owners.
Use the declared model and shared rules.
Verify dependency handoffs against current evidence.
Preserve incoming files and restrict edits to the allowed areas.
Report unresolved policy; never invent defaults.
Reuse existing boundaries and framework mechanisms.
Implement or verify the exact outcome and acceptance cases above.
Perform static security review and the named real-boundary checks.
List exact unrun checks; do not claim success.
Return a source-only ZIP; no commit/push.
```
