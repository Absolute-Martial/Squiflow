# ADM-021 — Activate and roll back a published profile

Task ID: ADM-021
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-020, ADM-011
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Atomically select an eligible immutable profile for new tenant operations and prove revision pinning; rollback selects a compatible retained revision.

## Current basis and canonical inputs

Current requests do not acquire durable profile authority. Accepted lifecycle requires atomic routing revision activation, compatibility checks and unchanged historical definitions. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md](../../architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md); [AUTOFAC_TENANT_PROFILE_RUNTIME_IMPLEMENTATION_PLAN.md](../../implementation/AUTOFAC_TENANT_PROFILE_RUNTIME_IMPLEMENTATION_PLAN.md); [ADMIN_SURFACES.md](../../admin/ADMIN_SURFACES.md).

## Scope and exclusions

Allowed areas: Profile authority activation pointer/receipt, verified tenant profile resolver, one real data-driven consumer and concurrent activation tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Define consumer compatibility, dormant-grant confirmation and what existing work retains. Data-only variation must stay data-only; ADM-022 is not an activation prerequisite without an implementation graph change. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Unpublished/incompatible revision cannot activate.
- Concurrent activation conflicts by expected authority revision.
- In-flight operation observes one pinned compatible profile.
- A new request observes current activation rather than stale client state.
- Rollback preserves history and domain facts already issued.
- Disable/reactivate permission diff obeys accepted confirmation and current authorization.

## Security/static review

Inspect pointer atomicity, current membership-before-profile resolution, caching freshness and permission independence. No cache signal becomes activation authority. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run real PostgreSQL two-process activation/rollback tests and a current protected business consumer journey; test restart rebuilding from authority and provider/cache outage behavior. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-021: Activate and roll back a published profile.
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
