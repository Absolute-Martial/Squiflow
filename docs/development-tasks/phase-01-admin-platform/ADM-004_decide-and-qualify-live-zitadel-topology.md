# ADM-004 — Decide and qualify live ZITADEL topology

Task ID: ADM-004
Phase: 01-admin-platform
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: ADM-002
Release requirement: REQUIRED
Qualification consumers: OPS-015 deploys the accepted provider configuration; OPS-018 proves its recovery. Neither is a prerequisite for this topology contract.

## Outcome

Produce a reviewed mapping and least-privilege live-provider qualification for tenant and platform identities, with explicit recovery and onboarding scope.

## Current basis and canonical inputs

Identity import verifies existing human subjects only. OPEN_DECISIONS leaves Cloud instance/project/application layout, service scopes, tenant organization mapping and recovery unresolved. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [IDENTITY_AND_SESSIONS.md](../../security/IDENTITY_AND_SESSIONS.md); [OPEN_DECISIONS.md](../../decisions/OPEN_DECISIONS.md); [ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md](../../implementation/ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md).

## Scope and exclusions

Allowed areas: Identity topology decision/POC evidence, isolated provider test tooling, focused owner and reviewed deployment examples; no production account creation yet. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Obtain authorized nonproduction ZITADEL Cloud access and known safe test identities. Do not equate provider OrganizationId and local TenantId or select tenant isolation by convenience. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- One human belongs to multiple local tenants without identity duplication.
- Owner and Staff authentication remain distinct from application grants.
- Platform administrator application/audience cannot become tenant authority.
- Machine identity cannot pass human onboarding.
- Enterprise SSO/custom-domain constraints are tested or explicitly excluded.
- Configuration export/reprovision preserves exact issuer/subject/account-link recovery.

## Security/static review

Inspect audience/issuer origin, redirect constraints, service scopes and secret handling. No blanket instance-owner token or credentials in test artifacts. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run a live sandbox matrix using the configured provider operations; retain redacted responses and scope evidence. A simulated V2 user response cannot qualify the Cloud instance. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-004: Decide and qualify live ZITADEL topology.
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
