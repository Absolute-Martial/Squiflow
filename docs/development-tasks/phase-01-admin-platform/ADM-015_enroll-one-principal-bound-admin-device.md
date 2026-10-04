# ADM-015 — Enroll one principal-bound Admin device

Task ID: ADM-015
Phase: 01-admin-platform
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: ADM-014, OPS-015, ADM-034
Release requirement: REQUIRED
Cross-track prerequisites: OPS-015, ADM-034

## Outcome

Add a bounded authorized enrollment ceremony binding one device certificate to one approved principal without handling its private key.

## Current basis and canonical inputs

Bootstrap stores the first SHA-256 certificate fingerprint. Runtime authorizer requires an active matching fingerprint joined to the exact principal. Normal certificate enrollment and proof-of-possession ceremony are absent. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [ADMIN_SURFACES.md](../../admin/ADMIN_SURFACES.md); [AdminClientCertificateProvider.cs](../../../services/admin-api/Application.AdminApi/AdminClientCertificateProvider.cs); [PlatformAdministrationDbContext.cs](../../../modules/platform-administration/Application.PlatformAdministration.Postgres/Persistence/PlatformAdministrationDbContext.cs).

## Scope and exclusions

Allowed areas: PlatformAdministration device enrollment core/PostgreSQL path, one AdminApi enrollment endpoint, certificate proof tests and focused enrollment owner only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Require accepted enrollment approver/fresh-auth policy, certificate acceptance rules and private ingress proof boundary. Infrastructure owns certificate issuance/trust distribution, not this task. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Certificate possession is proved before active enrollment.
- Fingerprint cannot bind to two principals or impersonate another device.
- Expired/unaccepted certificate follows defined policy.
- Changed idempotent enrollment content conflicts.
- Concurrent duplicate enrollment preserves one device/receipt.
- Unapproved ingress or identity cannot enroll using only a supplied fingerprint.

## Security/static review

Review forwarded-certificate trust, TLS termination, names/lengths, certificate logs and least-privilege inserts. Never transmit/store/export a device private key. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run real TLS/certificate request tests plus PostgreSQL uniqueness/idempotency and principal-binding denial; qualify supported direct/proxy ingress mode separately. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-015: Enroll one principal-bound Admin device.
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
