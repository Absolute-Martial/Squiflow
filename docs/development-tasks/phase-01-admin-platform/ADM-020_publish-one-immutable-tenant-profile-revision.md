# ADM-020 — Publish one immutable tenant profile revision

Task ID: ADM-020
Phase: 01-admin-platform
Status: VERIFY_EXISTING
Model: GPT-6.1 Sol
Dependencies: ADM-019, ADM-009
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Publish one validated immutable tenant profile revision referencing exact catalog, settings and authorization evidence; leave activation to ADM-021.

## Current basis and canonical inputs

Application.Profiles retains compiled selections and the accepted first immutable profile persistence/publication contract. Accepted profile owner separates features, settings, permissions and their revisions. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md](../../architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md); [AUTOFAC_TENANT_PROFILE_RUNTIME_IMPLEMENTATION_PLAN.md](../../implementation/AUTOFAC_TENANT_PROFILE_RUNTIME_IMPLEMENTATION_PLAN.md); [CompiledFeatureSelection.cs](../../../modules/application-profiles/Application.Profiles/CompiledFeatureSelection.cs).

## Scope and exclusions

Allowed areas: Profile-owned draft/validate/publish contracts, minimal PostgreSQL authority adapter/migration, AdminApi publication endpoint and bounded profile tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Owner accepted the [COM-011/profile contract](../../review/COM_011_PROFILE_PREREQUISITE_DECISION_PROPOSAL.md) on 2026-10-08. The bounded source now has local qualification in the [implementation receipt](../../review/COM_011_IMPLEMENTATION_RECEIPT.md). The [focused consumer owner](../../implementation/ORDER_PROGRAM_REFERENCE_POLICY.md) defines exact scope, retained legacy policy, authority separation and regression obligations. Planning status `VERIFY_EXISTING` means review current source/evidence, not production acceptance.

Accept authority owner, initial schema/compatibility version, publication permission and edit conflict policy. Earn a new physical project only if a real provider boundary requires it. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Invalid dependency/ceiling prevents publication.
- Concurrent publication follows explicit expected revision.
- Retry returns the same immutable revision and receipt.
- Published data cannot be edited in place.
- Profile tenant cannot reference another tenant’s settings or grants.
- Unknown schema/catalog revision produces safe compatibility failure.

## Security/static review

Review snapshot consistency, schema size bounds, SQL tenant isolation, secret references and publisher actor/device evidence. Publication is not permission grant or runtime activation. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Use PostgreSQL atomic-publication/replay/concurrency/rollback tests plus AdminApi hostile authority checks; retain canonical fingerprint fixtures and inspect least-privilege migration grants. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-020: Publish one immutable tenant profile revision.
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
