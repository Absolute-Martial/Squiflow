# Phase 1 — Identity, Tenant Authorization, and Session Foundation

**Parent owner:** `docs/implementation/PHASES_AND_GATES.md`

## Purpose

Phase 1 establishes the first production-shaped trust boundary for real users and tenants. It does not freeze business-module development: Customers, Orders, Workstation, Web, CoreApi, Guard, observability, deployment, and tests may all continue evolving while the Phase-1 security foundation is introduced.

A capability does not have to wait for Phase 1 to exist. Phase 1 is the point where externally reachable authenticated behavior can rely on the production-shaped identity/tenant-authorization foundation rather than test identities or UI-only checks.

## Subphases

```text
1A  ZITADEL identity and account binding
1B  TenantContext, OpenFGA authorization, roles and devices
1C  Web/Workstation session and surface security
1D  Authorization change reconciliation, failure and degraded modes
1E  Integrated Phase-1 gate
```

## Phase maturity added

After Phase 1, externally reachable business behavior may rely on a real authenticated identity and current application authorization model rather than test identities or UI-only role checks.

## Components that may continue/add

- any real capability module and its Web/Workstation/API adapter;
- tenant/account/device persistence needed by the trust model;
- authorization-sensitive application use cases;
- observability/audit evidence for identity/authorization paths;
- deployment/configuration required for ZITADEL/OpenFGA;
- tests against real isolated provider environments.

## Earned-only additions

Do not introduce Platform Admin, Worker, SyncApi, broker, service mesh, GraphQL, or new auth systems merely because identity/authorization now exists.

## Sustainability

Provider SDK types stay behind infrastructure/host adapters. `(issuer, subject)` and SquiFlow-owned permission/tenant semantics remain stable even if identity/authorization providers are replaced later.