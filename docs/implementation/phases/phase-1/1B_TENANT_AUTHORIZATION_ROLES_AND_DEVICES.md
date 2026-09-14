# Phase 1B — Tenant Authorization, Roles, and Devices

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

## Other development allowed

Any active module may add permission checks and resource relationships. Web can add role/team UI. Workstation may consume effective permission snapshots for UX/offline eligibility, but cannot make role changes locally authoritative.

## Hostile tests

- spoofed TenantId;
- cross-tenant object ID;
- incorrectly broad OpenFGA relation plus DB tenant check;
- role revoked immediately before sensitive action;
- wrong/past model ID;
- OpenFGA unavailable;
- tenant user attempts platform authority;
- device revoked while session exists.

## Exit gate

Tenant isolation and application permission remain independent checks; a provider failure never turns into allow; device/role state is server-authoritative and explainable.