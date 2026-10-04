# ADM-008 — Decide Owner Staff and delegation contract

Task ID: ADM-008
Phase: 01-admin-platform
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: ADM-001, ADM-004
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Produce accepted Owner/Staff permissions, ManageRoles/delegation ceilings, authority decision points and safe Owner handoff rules before role APIs exist.

## Current basis and canonical inputs

The initial Owner flag is a protected immutable bootstrap marker. It grants no general Owner permissions; Staff defaults, custom-role scope and delegation ceilings remain explicitly open. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [OPEN_DECISIONS.md](../../decisions/OPEN_DECISIONS.md); [ADMIN_API_MEMBERSHIP_LIFECYCLE.md](../../implementation/ADMIN_API_MEMBERSHIP_LIFECYCLE.md); [1B_TENANT_AUTHORIZATION_ROLES_AND_DEVICES.md](../../implementation/phases/phase-1/1B_TENANT_AUTHORIZATION_ROLES_AND_DEVICES.md).

## Scope and exclusions

Allowed areas: Focused authorization owner/accepted decisions, capability permission inventory and bounded model experiments for current operations only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Use current capability operations and product-owner choices. Do not invent universal Owner = everything, Staff defaults, explicit deny or branch scopes without an accepted need. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Initial Owner conversion preserves recoverable tenant administration.
- Tenant roles never imply platform authority.
- A delegator cannot grant a permission above its ceiling.
- Disabled-feature grants remain dormant under accepted reactivation confirmation.
- Multiple role assignments have defined union/ceiling semantics.
- Sensitive operation admission uses current permission despite stale UI/token evidence.

## Security/static review

Review self-escalation, orphaned Owner, custom-role model boundaries and independent resource tenant checks. Do not move domain arithmetic/lifecycle into OpenFGA. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Use isolated pinned OpenFGA model examples and hostile permission matrices; record which live checks ran. Accepted policy is a prerequisite, not evidence of an implemented role-administration surface. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-008: Decide Owner Staff and delegation contract.
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
