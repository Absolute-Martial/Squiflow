# v0.0.15 Decision Audit

**Purpose:** Audit architecture decisions for necessity, consistency, implementation timing, and both forms of architectural failure:

1. **overengineering** — unnecessary layers/components that increase cost without protecting a requirement;
2. **over-minimalism** — removing a real boundary or edge-case capability until the system can no longer perform its accepted job safely.

Accepted decisions live in `docs/decisions/CURRENT_DECISIONS.md`; unresolved details live in `docs/decisions/OPEN_DECISIONS.md`.

## Audit rules

Every decision is classified as:
- **KEEP** — needed now or protects a real correctness/runtime boundary;
- **SIMPLIFY** — the responsibility is real but the prior shape had unnecessary machinery;
- **DEFER** — valid future work with no current slice;
- **REMOVE** — no current requirement;
- **OPEN** — resolve just-in-time before the phase that needs it;
- **RESTORE** — a previous simplification removed capability that is actually required.

The target is:

```text
smallest structure that fully owns the required behavior
```

not:

```text
fewest files/processes/interfaces at any cost
```

A component is not allowed to become functionally weak merely to satisfy a minimalist architecture aesthetic.

## 1. Runtime/project audit

| Decision | Audit | Result |
|---|---|---|
| C#/.NET + ASP.NET Core | KEEP | Current foundation. |
| Avalonia Workstation | KEEP | Current Windows desktop decision. |
| Blazor Web App | KEEP | Tenant Web and future Platform Admin presentation decision. |
| Modular monolith business core | KEEP | Fits team/product scale and keeps network boundaries limited. |
| Core API executable | KEEP | Real tenant/business HTTP/security/composition boundary. |
| **Admin API executable** | **RESTORE / KEEP** | Super-admin/control-plane backend must be independent of Core API availability/security surface. Hosting it as Core API routes would couple privileged control to the tenant data plane. |
| Worker executable | KEEP, create when needed | Real deployment/failure boundary once durable background work exists. |
| Platform Admin Web | KEEP, create when needed | Real privileged presentation/control boundary. |
| `SquiFlow.Guard` | **RESTORE / KEEP** | Process supervision, crash/hang recovery, update handoff and evidence require an external companion boundary. Removing it would weaken the Workstation. |
| Arbitrary helper processes | DEFER | Only a specific native/heavy/driver problem earns another process. |

### Platform Admin backend correction

The earlier baseline had a separate Admin Web but still treated `/platform-admin/...` as part of Core API. That does not satisfy the intended control-plane independence.

Current accepted runtime is:

```text
Tenant Web / Workstation
→ Core API

Platform Admin Web
→ Admin API
```

not:

```text
Platform Admin Web
→ Core API /platform-admin/...
```

and not:

```text
Platform Admin Web
→ Admin API
→ Core API
```

for ordinary platform-control work.

The reason is concrete, not stylistic:
- Core API can be unhealthy/overloaded while operators still need the application control plane;
- privileged platform endpoints should not enlarge the tenant/business API attack surface;
- Admin API needs separate platform authentication/authorization/service credentials/health/deployment lifecycle;
- tenant authority must never become platform authority by route confusion;
- restarting/deploying Admin API should not require restarting Core API, and vice versa.

Shared libraries and underlying infrastructure are still allowed. Independence means no runtime dependency on the Core API process for normal platform administration, not impossible independence from the same central DB/provider when an operation genuinely requires it.

**Audit result: RESTORE / KEEP separate `services/admin-api`.** Create it with Admin Web in Phase 6, not as an empty Phase-0 project.

### Guard correction

The prior audit removed Guard because it looked like pre-implementation machinery. That was too aggressive.

Guard has a concrete responsibility independent of future native helpers:
- supervise Workstation lifecycle;
- detect/recover bounded crash/hang cases;
- coordinate updater handoff/recovery;
- preserve/process diagnostic evidence outside a failed Workstation;
- monitor bounded process/resource/lifecycle health;
- provide safe-mode/restart-budget behavior.

This is exactly the kind of boundary that cannot be reliably reduced into code inside the process it must supervise.

**Audit result: RESTORE.** Keep the Guard capable enough to do its job; optimize its resources after measurement, not by deleting responsibilities.

## 2. Interface/abstraction audit

The anti-interface rule remains valid for speculative abstractions such as:

```text
IRepository<T>
IUnitOfWork
IManager
IHelper
IService for every concrete Service
```

But the previous audit incorrectly treated every planned provider switch as speculative.

### Current rule

An interface earns its existence when at least one is true:
1. dependency direction materially requires it;
2. multiple live implementations coexist;
3. stable wire/process/plugin contract exists;
4. a provider migration is **already committed/near-term** and the seam prevents application code from depending on the bootstrap provider;
5. a fault/process boundary needs a narrow contract.

### Current justified provider interfaces

The first paying customer is already the planned migration trigger for object and backup storage. Therefore these are not hypothetical:

```text
IObjectStore
└── HuggingFaceObjectStore

IBackupTarget
└── KaggleBackupTarget
```

They must stay narrow and SquiFlow-owned. They do not mirror all provider SDK features and do not imply a generic provider framework.

**Audit result: RESTORE narrowly scoped interfaces.**

## 3. Web/Desktop/offline audit

| Decision | Audit | Result |
|---|---|---|
| Web online-only for business operations | KEEP | Avoids a second offline/sync client before Workstation sync is proven. |
| Server-side draft for selected valuable Web forms | KEEP selectively | Prevents loss without IndexedDB/PWA sync. |
| Workstation local-first | KEEP | Core offline/business requirement. |
| Global CRDT/peer authority | REMOVE baseline | Wrong for payments/stock/credit/permissions. |
| Full browser offline/PWA | DEFER | Revisit after real demand. |

Minimalism must not reduce the Workstation from a durable local-first client into a thin online shell. Offline durability, pending work, long-offline recovery and conflict handling remain core capabilities.

## 4. Identity and authorization audit

### ZITADEL

The prior docs left the OIDC provider open. Current correction: **ZITADEL is selected**.

Use it for standards-based identity/authentication, MFA/SSO/session/account capability. Workstation continues system-browser Authorization Code + PKCE.

Open details are deployment/layout choices, not product selection.

### OpenFGA

The prior audit explicitly rejected a Zanzibar-style authorization service because the initial Owner/Staff model looked simple. That conclusion was too broad once OpenFGA became the chosen authorization component and tenant-created custom roles/resource relationships are required.

**OpenFGA is selected** for application authorization.

This does not mean copying Zanzibar machinery or putting every domain rule in OpenFGA. Keep responsibilities separated:

```text
ZITADEL      identity/authentication
OpenFGA      relationships/roles/permissions
ASP.NET Core authorization integration
SquiFlow     workflow/domain/business invariants
DB           tenant data isolation
```

Core API uses tenant/business authorization scope. Admin API uses separate platform/super-admin scope. Reusing the same OpenFGA technology does not mean reusing tenant roles as platform authority.

OpenFGA's custom-role model is a good fit because tenant-created roles are represented as tuples/data rather than requiring authorization-model redeployment for each role instance.

**Audit result: RESTORE/KEEP OpenFGA as the authorization engine, while rejecting unnecessary custom authorization infrastructure around it.**

## 5. Multi-tenancy audit

Keep pooled tenancy:

```text
TenantContext
+ tenant discriminator
+ tenant-scoped data access
+ provider-specific defense in depth
```

OpenFGA relationship checks do not replace pooled DB isolation.

Do not prebuild schema-per-tenant, DB-per-tenant, queue-per-tenant or stack-per-tenant routing.

**Audit result: KEEP.**

## 6. Currency audit

Current requirement remains intentionally small:

```text
Tenant.DefaultCurrencyCode
monetary records retain CurrencyCode where historical meaning requires it
```

Do not hardcode one currency. Do not add FX/rate/ledger machinery without a real multi-currency requirement.

**Audit result: SIMPLIFY without removing currency identity.**

## 7. Accessibility audit

Formal accessibility/a11y work remains outside the current requested baseline. Ordinary UI quality still uses normal controls/labels/errors/navigation.

**Audit result: REMOVE dedicated workstream from baseline.**

## 8. Object storage audit — Hugging Face + `IObjectStore`

Current bootstrap provider is the private Hugging Face Storage Bucket with the current ~100 GB private-storage envelope.

Because replacement at the first paying customer is already planned, direct Hugging Face dependencies throughout runtime code would create guaranteed rewrite work.

Therefore:

```text
business/application
→ IObjectStore
→ HuggingFaceObjectStore
```

is justified now.

The interface remains intentionally narrow; provider-specific migration tooling can still use provider APIs inside infrastructure when necessary.

**Audit result: KEEP Hugging Face bootstrap; RESTORE provider interface.**

## 9. Backup audit — infrastructure-level `IBackupTarget`

Current bootstrap off-site carrier is private Kaggle with locally encrypted opaque artifacts only.

Backup is not merely an application feature. A useful restore may require:
- DB state;
- object data/metadata;
- idempotency/job state;
- rules/config;
- deploy/recovery configuration;
- ZITADEL/OpenFGA restore/reprovision evidence depending on managed vs self-hosted topology;
- independently recoverable keys/secrets through the appropriate secure recovery path.

The provider destination is abstracted at infrastructure level:

```text
backup orchestration
→ IBackupTarget
→ KaggleBackupTarget
```

This boundary is justified because backup-provider migration at the first paying customer is already planned.

**Audit result: KEEP encrypted Kaggle bootstrap + RESTORE infrastructure provider interface.**

## 10. Testing audit

Do not reduce testing to unit tests simply because the design is lean.

Keep tests for:
- tenant isolation;
- OpenFGA permission/custom-role/revocation/model-version/reconciliation behavior;
- ZITADEL login/session/PKCE flows;
- transaction/idempotency correctness;
- local DB crash/restart/long-offline;
- Guard crash/hang/update recovery;
- Admin API platform authorization and Core-API-outage independence;
- Core API tenant operation while Admin API is unavailable;
- shared invariant correctness where Admin API and Core API legitimately touch common state;
- `IObjectStore` adapter contract and migration proof;
- `IBackupTarget` encrypted artifact download/restore;
- resource bounds and actual deployment behavior.

Testing should attack required edge cases even when the implementation uses few components.

## 11. Documentation audit

One focused document owns each detailed topic. Review docs never override current decisions.

Current owners include:
- identity/session → `docs/security/IDENTITY_AND_SESSIONS.md`;
- authorization → `docs/security/TENANT_PERMISSIONS.md`;
- platform admin/control backend → `docs/admin/ADMIN_SURFACES.md`;
- control-plane/data-plane split → `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`;
- object/backup provider boundaries → `docs/data/FILES_AND_OBJECT_STORAGE.md`;
- Guard → `docs/workstation/GUARD_AND_RECOVERY.md`;
- local-first Workstation → `docs/workstation/LOCAL_FIRST_DESKTOP.md`.

## 12. Corrected implementation shape

The early implementation is still compact, but includes only boundaries already justified by required behavior or committed migration:

```text
SquiFlow/
├── apps/
│   ├── web/
│   └── desktop/
│       ├── workstation/
│       └── guard/
├── services/
│   └── core-api/
├── modules/
├── infrastructure/
│   ├── storage/        # IObjectStore + HuggingFaceObjectStore
│   ├── backup/         # IBackupTarget + KaggleBackupTarget
│   ├── identity/       # ZITADEL
│   └── authorization/  # OpenFGA
├── tests/
├── deploy/
└── docs/
```

When Phase 6 begins, add together because their first real use exists:

```text
apps/admin-web/
services/admin-api/
services/worker/
```

Admin API is not a placeholder microservice; it is the independent platform-control backend required by the super-admin boundary.

## 13. Audit conclusion

The architecture should use **disciplined completeness**:

```text
remove unnecessary ceremony
without removing required responsibility
```

Specifically, v0.0.15 treats Guard, separate Admin API, `IObjectStore`, `IBackupTarget`, ZITADEL, and OpenFGA as justified boundaries. Generic repositories, forwarding services, arbitrary helper processes, and unrelated provider interfaces remain rejected.

The implementation should begin, but every phase must prove the edge/failure behavior that makes SquiFlow's core abilities real rather than merely produce the smallest possible happy-path codebase.
