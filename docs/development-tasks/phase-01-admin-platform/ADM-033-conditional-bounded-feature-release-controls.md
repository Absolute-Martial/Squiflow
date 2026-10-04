# ADM-033 — Conditional bounded feature-release controls

Task ID: ADM-033
Phase: 01-admin-platform
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: ADM-021, ADM-018, OPS-021
Release requirement: CONDITIONAL

## Outcome

Control one real supported release channel/rollout or presentation variant when the release needs a platform ceiling beyond ordinary tenant profile publication.

## Current basis and canonical inputs

Read [release/experiments](../../architecture/FEATURE_RELEASE_AND_EXPERIMENTS.md), [profiles](../../architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md), [current truth](../../../README.IMPLEMENTATION.md), [rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md).

## Scope and exclusions

Allowed areas: one profile-owned versioned channel/rollout publication, bounded targeting/effective read, authorized AdminApi command and selected consumer tests. Reuse current profile activation. Exclude generic experimentation/analytics, arbitrary feature flags, unsupported host targeting and financial/security variant semantics.

## Decisions/prerequisites

Activate only for a named shipped capability/variant and supported consumers. Accept targeting identity, compatibility, tenant opt-in ceiling, deterministic assignment if used and safe disable behavior. Stable-only deployment is a valid unselected disposition.

## Acceptance and edge cases

- Feature availability, permission, domain validity and release channel remain separate.
- Publishing a ceiling neither grants roles nor deletes previously accepted facts/work.
- Version-incompatible clients are rejected or observe deliberate safe absence.
- Assignment is stable for its accepted subject type and changes only under approved publication semantics.
- Disable/rollback prevents new exposure while preserving prior facts and attributable revisions.
- Exposure diagnostics remain bounded and do not copy sensitive customer input.

## Security/static review

Review targeting provenance, cross-tenant cache keys, published revisions, compatibility and ineffective dormant grants. Security/money correctness cannot be experimental variants.

## Dynamic verification and unavailable-environment handling

Run `./eng/verify.sh`, actual host effective-profile/rollout/replay tests and later selected browser checks. Use concurrent publish/activate and compatibility fixtures. Missing consumer/provider evidence remains explicitly pending.

## Handoff

Return source ZIP/hash, selected control contract, compatibility/rollback evidence and blockers. UIA-007 and WEB-012 expose only the qualified controls.

## Assignable prompt

```text
Execute ADM-033 only for a selected shipped capability control.
Read profile and release owners plus accepted consumer contracts.
Agree one channel/rollout/variant and its compatible targeting.
Reuse immutable profile publication and activation semantics.
Keep availability, permissions and business validity separate.
Test stable assignment, concurrent publication and disable/rollback.
Review tenant cache scope, dormant grants and safe diagnostics.
Return source ZIP, hashes and exact consumer evidence.
Do not commit or build a generic experiment/feature-flag platform.
```
