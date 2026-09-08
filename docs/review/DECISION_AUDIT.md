# v0.0.15 Decision Audit

**Purpose:** Audit current architecture decisions for necessity, consistency, implementation timing, and accidental overengineering. This file records the audit result; accepted decisions live in `docs/decisions/CURRENT_DECISIONS.md` and unresolved ones in `docs/decisions/OPEN_DECISIONS.md`.

## Audit rules

Every architecture decision is classified as one of:

- **KEEP** — needed now or establishes a real correctness/runtime boundary.
- **SIMPLIFY** — the problem is real but the previous solution introduced unnecessary machinery.
- **DEFER** — valid possible future work, but no current vertical slice requires it.
- **REMOVE** — not a v0.0.15 requirement and should stop consuming implementation effort.
- **OPEN** — must be resolved just-in-time before a phase that actually needs it.

The default is the smallest design that preserves the required business/security/recovery invariant.

## 1. Runtime/project decisions

| Decision | Audit | Result |
|---|---|---|
| C#/.NET + ASP.NET Core | KEEP | Current application/server foundation. |
| Avalonia Workstation | KEEP | Current Windows desktop decision. |
| Blazor Web App | KEEP | Current tenant Web and future Admin Web presentation decision. |
| Modular monolith business core | KEEP | Fits current team/product scale and keeps network boundaries limited. |
| Separate Core API executable | KEEP | Real HTTP/security/composition boundary. |
| Separate Worker executable | KEEP, **create later** | Real deployment/failure boundary, but no project is needed until Phase 6 has durable background work. |
| Separate Platform Admin Web | KEEP, **create later** | Real privileged presentation boundary, but no project is needed until the platform-control slice exists. |
| `SquiFlow.Guard` always-running companion | REMOVE from baseline | It was a named component before a proven updater/crash/native isolation need. Start with one Workstation process. |
| On-demand helper processes | DEFER | Add only for a library/driver that demonstrably hangs/crashes/leaks or requires process isolation. |

## 2. Abstraction/interface audit

Previous planning risked creating directories such as `packages/`, `contracts/`, and `persistence/abstractions/` before any code proved they were needed.

### Current rule

Do not create:

```text
IRepository<T>
IUnitOfWork
IManager
IHelper
one-interface-per-class
provider wrapper around every SDK
```

merely for architectural symmetry or mocking.

Prefer:

```text
real vertical slice
→ concrete framework/provider integration contained in one area
→ extract a boundary only when replacement/inversion/process compatibility actually needs it
```

An interface earns its existence when at least one of these is true:

1. domain/application dependency direction cannot otherwise remain correct;
2. two production implementations genuinely coexist;
3. a stable wire/inter-process/plugin contract exists;
4. an imminent provider migration is being implemented and the seam materially reduces that migration;
5. a fault/process boundary requires a narrow contract.

"Maybe we will switch later" by itself is not enough.

Testing should prefer real adapters/integration tests where provider behavior matters instead of manufacturing interfaces solely to mock persistence/storage.

**Audit result: SIMPLIFY.** Provider details must remain localized, but speculative provider-neutral interface hierarchies are removed from the baseline.

## 3. Web/Desktop/offline decisions

| Decision | Audit | Result |
|---|---|---|
| Web online-only for business operations | KEEP | Avoids a second offline/sync client before Workstation sync is proven. |
| Server-side draft for selected valuable Web forms | KEEP selectively | Prevents data loss without IndexedDB/PWA sync. |
| Workstation local-first | KEEP | Core offline/business requirement. |
| Global CRDT/peer authority | REMOVE baseline | Wrong authority model for payments/stock/credit/permissions. |
| Full browser offline/PWA | DEFER | Revisit only after production demand. |

## 4. Identity/authorization decisions

Keep OIDC, system-browser Authorization Code + PKCE for Workstation, `(issuer, subject)` account identity, Web-only role/permission administration, ASP.NET Core authorization primitives, authoritative server reauthorization, and `TenantAuthorizationRevision` semantics.

Do **not** add a Zanzibar service, custom authentication protocol, dynamic client-registration system, or authorization microservice without a demonstrated requirement.

**Audit result: KEEP current simple model.**

## 5. Multi-tenancy decision

Keep pooled tenancy as the ordinary baseline:

```text
TenantContext
+ tenant discriminator
+ tenant-scoped data access
+ provider-specific defense in depth
```

Do not prebuild schema-per-tenant, database-per-tenant, queue-per-tenant, or stack-per-tenant routing.

PostgreSQL RLS remains a proof requirement only if PostgreSQL is the central candidate/selection; RLS is not allowed to silently select PostgreSQL.

**Audit result: KEEP.**

## 6. Currency audit

The previous cross-cutting document was drifting toward a future multi-currency/FX design that v0.0.15 does not need.

Current requirement is much smaller:

```text
Tenant.DefaultCurrencyCode
Money/financial record retains CurrencyCode where historical meaning requires it
```

Rules:
- never hardcode `NPR`, `USD`, or another currency throughout business logic;
- tenant setup provides the default currency;
- issued/posted monetary records retain the applicable currency code so a later tenant setting change does not reinterpret history;
- use decimal/fixed-precision money semantics appropriate to the selected DB/.NET implementation;
- do not implement exchange rates, FX conversion, multi-currency allocations, gain/loss accounting, or a currency provider/service until a real customer needs them.

No currency helper process, service, interface, rate feed, or conversion subsystem is baseline.

**Audit result: SIMPLIFY.**

## 7. Accessibility audit

A dedicated accessibility document, formal conformance gate, manual screen-reader plan, and release-level accessibility program were added before the product has an executable Phase 0.

That is not necessary for the current requested scope.

Normal UI engineering should still use framework-standard controls, clear labels/errors, usable focus/navigation, and readable state because those are ordinary quality concerns. But v0.0.15 does **not** create a separate accessibility workstream, conformance target, or dedicated test gate.

Revisit only if a customer contract, jurisdiction, public-sector requirement, or product goal makes it necessary.

**Audit result: REMOVE from baseline.**

## 8. Object-storage audit — Hugging Face bootstrap

The earlier docs incorrectly described the current ~100 GB resource generically as `S3/object storage`.

The actual bootstrap provider is **Hugging Face**.

Current Hugging Face documentation says:
- storage limits apply to repositories and Storage Buckets;
- free users/organizations currently have **100 GB private storage**;
- Storage Buckets are non-versioned/mutable S3-like object storage;
- private buckets are supported;
- an S3-compatible API is available.

Therefore the current bootstrap implementation can use a **private Hugging Face Storage Bucket** directly.

Do not create an `IObjectStorage` abstraction only because a future provider migration is planned. Keep Hugging Face SDK/API use localized inside infrastructure code and keep provider-specific types out of business/domain records. Business records store provider-neutral object metadata such as object key, tenant/resource owner, hash, size, lifecycle, and business reference.

Because Hugging Face buckets are mutable, SquiFlow itself uses immutable/versioned application object keys for issued/retained business objects rather than overwriting historical bytes.

### Migration trigger

Planned migration: **when the first paying customer arrives**.

Migrate earlier if any of these happen first:
- capacity approaches the current account limit;
- API/rate/latency behavior is unsuitable;
- contractual support/durability is insufficient;
- data residency/privacy/compliance requirement appears;
- operational recovery/restore requirements exceed the bootstrap arrangement.

The later paid provider is intentionally not selected now.

**Audit result: REPLACE generic initial-provider abstraction with a concrete Hugging Face bootstrap integration and a localized migration seam.**

## 9. Backup audit — Kaggle bootstrap

The current bootstrap off-site backup destination is **Kaggle private Datasets**.

Current Kaggle documentation supports private datasets, dataset versions, CLI/API upload/download, and currently documents a **200 GB per dataset** limit and **200 GB maximum private datasets**.

However Kaggle Datasets is a dataset platform, not a purpose-built backup service. Its documentation says uploaded archives can be unpacked and tabular data can be analyzed/typed. Therefore SquiFlow must **not upload raw DB dumps, CSV customer data, raw object directories, or ordinary ZIP archives containing customer data** as the backup representation.

### Bootstrap backup format

Use one opaque encrypted backup artifact per retained backup version, for example conceptually:

```text
DB dump + required metadata/config + selected object snapshot/manifest
→ package/compress locally
→ authenticated encryption locally
→ opaque `.sqfbak` blob
→ hash/checksum
→ private Kaggle Dataset version
→ download verification
→ periodic restore drill
```

The encryption key/recovery material is kept outside Kaggle and must itself be recoverable.

Kaggle privacy/versioning is not enough by itself: backup validity requires a successful restore test.

### Migration trigger

The planned move to purpose-built paid backup storage is the **first paying customer**, or earlier if private-capacity, security, terms, automation, retention, or restore requirements become inadequate.

**Audit result: KEEP as an encrypted bootstrap off-site carrier only, not as a permanent production backup architecture.**

## 10. Testing audit

Keep tests for real correctness risks: tenant isolation, idempotency, transaction atomicity, local DB crash/restart, sync conflict, permission revocation, backup restore, and resource bounds.

Remove a dedicated accessibility verification workstream from the current baseline.

Do not create mocks/interfaces simply to increase unit-test count. Provider correctness belongs in real adapter/integration tests.

**Audit result: KEEP but focus on correctness and real-provider evidence.**

## 11. Documentation audit

Detailed docs are useful only while they prevent mistakes. They should not generate new components automatically.

Current documentation rules:
- one topic owner document where depth is needed;
- review docs do not override current decisions;
- no generated CSV design authority;
- remove obsolete docs rather than keeping contradictory "historical current" guidance in the main navigation;
- architecture tree is a possible shape, not a scaffold command.

## 12. Resulting immediate implementation shape

Before any background Worker or platform-admin UI exists, the first executable implementation can be as small as:

```text
SquiFlow/
├── apps/
│   ├── web/          # Blazor tenant Web + tenant Settings
│   └── desktop/      # Avalonia Workstation
├── services/
│   └── core-api/     # ASP.NET Core
├── modules/          # only modules needed by the first slice
├── infrastructure/   # only concrete providers currently used
├── tests/
├── deploy/
└── docs/
```

Later, when the feature actually exists:

```text
apps/admin-web/       # when platform-control UI is implemented
services/worker/      # when durable background execution is implemented
```

Do not create `packages/`, `contracts/`, `persistence/abstractions/`, `helpers/`, or `Guard` projects until a real boundary earns them.

## 13. Audit conclusion

The architecture direction remains strong, but the previous review started turning good future-proofing into pre-implementation machinery.

The v0.0.15 correction is:

```text
preserve hard correctness boundaries
+ implement the current concrete provider/workflow
+ defer optional abstractions
+ create components only when their first real use exists
```

The repository should now move into Phase 0 rather than continuing to expand speculative architecture.
