# ADM-034 — Backend high-risk action and step-up contract

Task ID: ADM-034
Phase: 01-admin-platform
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: ADM-004, WEB-001, UIA-001, GATE-001
Release requirement: REQUIRED

## Outcome

Define and qualify the minimum backend freshness/guarded-transport contract needed by accepted Owner/device/operator recovery actions before either Web application is implemented.

## Current basis and canonical inputs

Read [identity/session](../../security/IDENTITY_AND_SESSIONS.md), [Admin surfaces](../../admin/ADMIN_SURFACES.md), [application security](../../security/APPLICATION_SECURITY_BASELINE.md), [current truth](../../../README.IMPLEMENTATION.md), [rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). WEB-001/UIA-001 provide topology decisions, not prerequisite browser runtimes.

## Scope and exclusions

Allowed areas: focused operation-risk/freshness contract, validated authentication-context admission adapter in each affected API, guarded Owner/device operation contract fixtures and bounded real-provider POC. Exclude UI, ordinary-operation approval requirements, parallel authentication mechanisms and universal security-workflow scaffolding.

## Decisions/prerequisites

Owner selects operation-specific recent/strong authentication requirements, configured ZITADEL acr/auth_time meaning and tolerances, trusted Web client/delegation proof, approval only where the focused owner requires it, and unavailable-factor recovery. Missing/unsupported evidence fails the affected operation safely.

## Acceptance and edge cases

- Client-supplied flags, arbitrary headers and tenant cookies cannot prove authentication freshness or Web-only provenance.
- Verified freshness is bound to exact authenticated actor and intended operation context; normal permission/device checks still run.
- Old, missing, wrong-issuer/audience and incompatible authentication context are rejected without authority fallback.
- API contract tests prove guarded denial/success before Web consumption; no GATE-002→Web→GATE-002 cycle is introduced.
- Independent API operation and recoverable Admin entry are preserved within the accepted risk policy.
- WEB-012 and UIA-008 later demonstrate real-browser use without redefining backend checks.

## Security/static review

Review claim provenance, timestamp tolerance, delegation boundary, approval/operation binding, replay and error redaction. Avoid blanket stronger-factor demands on low-risk reads/settings.

## Dynamic verification and unavailable-environment handling

Run `./eng/verify.sh`, real API authentication-context tests and an isolated configured-ZITADEL step-up proof. Static token fixtures verify rejection logic but cannot qualify actual provider acr mapping. Record exact pending provider checks and accepted non-claims.

## Handoff

Return source-only ZIP/hash, accepted per-operation requirements, real-provider POC, test commands and blockers. ADM-011/014/015/026 consume this contract; actual browser evidence belongs to later Web tasks.

## Assignable prompt

```text
Execute ADM-034 for the already accepted high-risk action set.
Read IdP and both Web topology decisions before designing admission.
Agree exact authentication recency/strength and trusted Web provenance.
Implement only the minimum backend validation and contract fixtures.
Keep permission, membership and device checks independent.
Reject forged, missing, stale and wrong-context evidence safely.
Prove real provider mapping where available without building Web UI.
Return source ZIP, hashes, owner requirements and exact pending checks.
Do not commit or add blanket approval/security-flow scaffolding.
```
