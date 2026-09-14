# Phase 1B — Tenant Authorization, Roles, and Devices

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Production intent

After 1B passes, an authenticated subject can perform only the declared tenant/resource operations currently authorized for that SquiFlow account/device/context, and neither token claims, client-supplied tenant identifiers, stale snapshots, nor provider failure can silently widen authority.

## Required foundation

Establish authoritative tenant/application authorization:

```text
authenticated subject
→ SquiFlow account/membership lookup
→ authoritative TenantContext
→ tenant-scoped resource lookup
→ semantic authorization requirement
→ OpenFGA decision
→ SquiFlow domain/workflow validation
```

Implement the first OpenFGA store/model and pin the model ID used by SquiFlow.

## Initial concepts

Support enough real behavior to prove:

- Owner and Staff baseline relations;
- capability-owned stable permission definitions;
- tenant custom roles through relationship tuples rather than model redeploy for each role;
- multiple role assignments where accepted;
- device enrollment/visibility/revocation state needed by current Workstation security;
- tenant authorization revision/evidence where needed for snapshots and audit.

## Separation rules

OpenFGA does not own business arithmetic, stock/payment/workflow invariants, TenantContext derivation, feature enablement, or commercial limits. ZITADEL claims do not directly become SquiFlow permissions.

Authentication is necessary input, not authorization.

## Structured threat review

For every protected surface introduced in or before 1B, systematically consider at least:

- identity/tenant spoofing;
- cross-tenant object and relationship tampering;
- model/revision confusion;
- stale/replayed permission/device evidence;
- repudiation/missing audit for material role/device changes;
- information disclosure through authorization errors/list queries;
- denial/resource exhaustion of authorization dependencies;
- elevation from tenant authority to platform authority;
- provider outage/timeout/consistency behavior.

Findings affecting a reachable current surface are fixed or `BLOCKED`; they are not future hardening.

## Permanent hostile tests

The following are required passing regression evidence for the applicable implemented surfaces:

- spoofed TenantId;
- cross-tenant object ID/list/write attempt;
- incorrectly broad OpenFGA relation plus independent DB/resource tenant check;
- role revoked immediately before sensitive server admission;
- wrong/past model ID;
- OpenFGA unavailable/timeout;
- tenant user attempts platform authority;
- device revoked while session exists;
- stale effective-permission snapshot used only for UX/offline eligibility, never to override current server authority;
- authorization error responses do not reveal another tenant's resource existence/content.

Cheap/in-process/isolated authorization tests run `PER_MR`. Real OpenFGA model/store behavior and consistency paths are exercised on a recurring/pre-release integration cadence when they cannot run continuously.

## Other development allowed

Any active module may add permission checks and resource relationships, but every new protected operation inherits these regression rules. Web can add role/team UI. Workstation may consume effective permission snapshots for UX/offline eligibility, but cannot make role changes locally authoritative.

## Transitional restriction

If 1A is complete and 1B is not, a successfully authenticated user is **not** allowed to reach protected tenant mutation merely because identity exists. Restrict/disable such operations until their TenantContext/resource/OpenFGA/domain authority path is production-honest.

The restriction must be technical where material—not a comment saying `authorization later`.

## Exit gate

1B passes only when the declared authorization paths are demonstrated by the hostile/integration evidence above and protected by permanent regression checks. Tenant isolation and application permission remain independent checks; provider failure never turns into allow; device/role state is server-authoritative and explainable; `BLOCKED = none`.
