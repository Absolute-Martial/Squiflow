# ADM-006 — Expose bounded platform registry reads

Task ID: ADM-006
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-002, GATE-001
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Provide authorized bounded tenant/account/membership detail and keyset browse needed by Admin Web, with current revision/lifecycle information for supported commands.

## Current basis and canonical inputs

AdminApi currently offers access and create/transition commands; Program has no platform tenant/account/membership browse/detail routes. CoreApi active membership listing is caller-specific and cannot serve as an administrative registry. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [Program.cs](../../../services/admin-api/Application.AdminApi/Program.cs); [TenantDirectory.cs](../../../modules/tenancy/Application.Tenancy/TenantDirectory.cs); [AccountDirectory.cs](../../../modules/identity-access/Application.IdentityAccess/AccountDirectory.cs).

## Scope and exclusions

Allowed areas: Tenancy and IdentityAccess read contracts/PostgreSQL adapters, explicit AdminApi read endpoints, scoped permissions and read tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Agree minimal fields, distinct read permissions, supported filters and cursor version. Split independent resource implementations into separate handoffs if their ownership requires different schema changes. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Unauthorized caller cannot discover account/tenant existence.
- Cross-resource cursor reuse and oversized limits fail safely.
- Browse order is deterministic without count/search claims.
- Membership detail reflects invited/suspended/removed and protected initial-Owner state.
- A read performs no tenant/account/membership mutation, command-receipt write or OpenFGA tuple write. Every registry access attempt must append the existing durable Platform Admin access-audit evidence; audit is the explicit security-side-effect exception to read-only business state.
- Concurrent changes produce a declared consistent detail snapshot and usable expected revision.

## Security/static review

Review PII field allow-list, SQL parameters, keyset indexes and platform permission separation. Do not expose raw persistence rows or identity provider administrative payloads. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run real PostgreSQL pagination/concurrent-read checks and AdminApi positive/negative authority tests; verify least-privilege reads and no-store with ./eng/verify.sh. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-006: Expose bounded platform registry reads.
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
