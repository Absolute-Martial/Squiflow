# ADM-009 — Persist one tenant grant proposal and status

Task ID: ADM-009
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-008, ADM-002, GATE-001
Conditional dependencies: OPS-003 when automatic background grant reconciliation is selected
Release requirement: REQUIRED
Cross-track prerequisites: OPS-003

## Outcome

Persist one bounded grant proposal with semantic idempotency, expected authorization revision and safe status; return Pending rather than Applied before provider verification.

## Current basis and canonical inputs

Current tenants have direct capability relations checked in OpenFGA, but application-owned grant metadata, authorization revision and durable reconciliation intents are absent. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [1D_AUTHORIZATION_CHANGE_RECONCILIATION_AND_FAILURE.md](../../implementation/phases/phase-1/1D_AUTHORIZATION_CHANGE_RECONCILIATION_AND_FAILURE.md); [ADMIN_SURFACES.md](../../admin/ADMIN_SURFACES.md); [APPLICATION_KERNEL_AND_MODULES.md](../../architecture/APPLICATION_KERNEL_AND_MODULES.md).

## Scope and exclusions

Allowed areas: Accepted tenant authorization capability/core and PostgreSQL adapter, one proposal/status contract and focused grants/migrations; AdminApi admission adapter only if platform policy authorizes it. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

This capability owns its durable grant proposal even with synchronous bounded reconciliation. OPS-002's first commercial consequence is not a generic prerequisite; use OPS-003 only for the explicitly selected automatic execution path.

Select exact capability owner in ADM-008, do not create a generic repository or copy grant meaning into each host. Map execution to OPS infrastructure when autonomous retry is needed. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Same caller/key returns the same proposal; altered content conflicts.
- Invalid permission or target tenant creates no durable proposal.
- Metadata and requested audit commit atomically.
- Concurrent expected-revision proposals follow one declared arbitration rule.
- Status distinguishes pending, failed and uncertain without guessed authority.
- Process restart preserves enough identity for reconciliation.

## Security/static review

Inspect tenant predicates/RLS, bounded payloads, least-privilege intent/status writes and immutable audit evidence; no provider credentials stored in proposals. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run PostgreSQL migration/rollback, concurrency, crash-before-commit and idempotency tests; exercise protected status reads against real AdminApi/OpenFGA authority. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-009: Persist one tenant grant proposal and status.
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
