# ADM-024 — Publish bounded tenant branding overrides

Task ID: ADM-024
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-021, WEB-001
Conditional dependencies: OPS-008 when managed branding uploads are selected
Release requirement: REQUIRED
Cross-track prerequisites: WEB-001, OPS-008

## Outcome

Allow a bounded accepted subset of tenant name/theme/logo/support overrides in one immutable published profile, consumed only after current tenant authority is established.

## Current basis and canonical inputs

Deployment BrandProfile and public application bootstrap already validate white-label identity. The product identity owner keeps tenant-specific branding as future published profile authority; public candidate hostname/route values cannot select trusted tenant branding. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [PRODUCT_IDENTITY_AND_VERSIONING.md](../../product/PRODUCT_IDENTITY_AND_VERSIONING.md); [BrandProfile.cs](../../../modules/branding/Application.Branding/BrandProfile.cs); [TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md](../../architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md).

## Scope and exclusions

Allowed areas: Existing Branding validation, one profile-owned tenant override schema/publication input, verified tenant-scoped CoreApi branding read contract and focused branding tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Accept override field allow-list, deployment fallback semantics, asset URL policy and tenant edit/platform ceiling permissions. Use OPS object storage only if managed uploads are selected; do not invent an arbitrary theme/CSS system. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Missing tenant override has an explicit validated deployment fallback.
- Untrusted hostname or tenant candidate never selects another tenant’s branding.
- Script/unsafe URL/theme values fail at their owned boundary.
- Concurrent edits/publication retain expected revisions and immutable history.
- Branding changes cannot modify identity issuer, permissions or security policy.
- Published branding revision is consumed consistently without codename fallback.

## Security/static review

Review output encoding, external asset URL allow-list, CSP interaction, secret-free metadata and branding spoofing risk. Tenant theme cannot replace security indicators or trust boundaries. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run existing BrandProfileTests plus real PostgreSQL profile revision/isolation checks and protected CoreApi branding reads; verify tenant Web consumes the revision safely in its own integration task. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-024: Publish bounded tenant branding overrides.
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
