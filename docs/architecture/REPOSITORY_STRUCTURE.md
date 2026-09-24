# Repository and Deployment Boundaries

**Version:** v0.1.0

## 1. Structural vocabulary

SquiFlow uses these categories consistently:

```text
Foundation
   ↓
Capability-owned business meaning
   ↓
Host / infrastructure adapters
   ↓
Executable composition roots
```

`Foundation` is narrow product-wide technical/domain infrastructure. It is not a universal `Shared`, `Common`, or `Utils` business bucket.

A capability owns one source implementation of its business meaning. `Core`, `Server`, `Workstation`, `Postgres`, `Contracts`, and similar labels describe responsibilities first; a separate project exists only when a real compile-time/provider/platform/packaging/lifecycle boundary earns it.

`LOGICAL_CAPABILITY_MAP.md` groups proposed responsibility names and routes them to focused owners. It is not a project inventory or a tenant feature catalog.

## 2. Current implementation state

The current tree contains five compact host-neutral capability projects (`Application.Profiles`, `Application.Branding`, `Application.IdentityAccess`, `Application.Tenancy`, and `Application.Orders`), three capability-owned PostgreSQL adapters, CoreApi and DbMigrator executables, ten test projects, and a repository build/test contract. ApplicationProfiles validates bounded feature graphs and compiles deterministic dependency-closed selections, but has no production catalog or durable profile authority. CoreApi has configured JWT validation, account and active-membership queries, Finbuckle route-candidate plumbing, and pinned-model OpenFGA tenant-workspace plus distinct Order `order_creator`, `order_viewer`, and `order_abandoner` permission checks. Tenancy resolves an immutable context from current membership. Orders owns immutable priced draft create/read/browse and a bounded abandon operation: `order_abandoner` may perform a one-way, expected-revision `draft` to `abandoned` transition, which records the abandoning account and timestamp while preserving priced content and the original create receipt. Its PostgreSQL adapter persists the lifecycle metadata under tenant isolation with explicit tenant predicates and forced RLS. This does not introduce draft editing, deletion, or a broader order lifecycle. The tree has no general Foundation/ApplicationKernel project or broader role/tuple administration. `README.IMPLEMENTATION.md` owns the precise current routes, evidence and non-claims.

The former 0B Parties slice was purged on 2026-09-17. Its phase record and earlier Phase-0 code remain historical evidence, not current implementation authority.

New projects are introduced only with a useful real responsibility, explicit dependency/authority boundary, material applicable behavior, falsifiable verification, and a source-admission decision under `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md`.

## 3. Accepted repository ownership map

The following is a **growth map**, not scaffolding instructions:

```text
SquiFlow/
|- apps/
|  |- web/                         # Blazor tenant Web / tenant administration UX
|  `- desktop/
|     |- workstation/             # Avalonia local-first Workstation
|     |- guard/                   # supervision/recovery companion
|     |- diagnostics/             # only if diagnostics earns process isolation
|     |- maintenance/             # only if maintenance/backup earns process isolation
|     |- sync/                    # only if desktop sync earns process isolation
|     `- document/                # only if heavy document work earns process isolation
|- services/
|  |- core-api/                   # current authoritative API host
|  |- db-migrator/                # current one-shot schema migration host
|  |- web-api/                    # interactive API host when workload split is real
|  |- sync-api/                   # Workstation sync host when workload split is real
|  |- admin-api/                  # private platform-control backend when needed
|  `- worker/                     # durable background execution host when needed
|- modules/
|  `- <capability>/
|     |- Application.<Capability>/
|     |- Application.<Capability>.Postgres/       # only with real provider code
|     `- Application.<Capability>.Workstation/    # only with real local adapter code
|- foundation/
|  |- application-kernel/         # only as current consumers earn shared primitives
|  |- observability/              # only with real instrumentation consumers
|  `- workstation-runtime/        # only with stable desktop IPC/runtime contracts
|- infrastructure/
|  |- storage/
|  |- backup/
|  |- identity/
|  `- authorization/
|- tests/
|- deploy/
`- docs/
```

A path in this map is an ownership reservation, not proof that the corresponding component exists or should be created now.

## 4. Compact capability shape

When a real capability is implemented, prefer a compact capability-owned project until pressure proves a split is useful:

```text
modules/orders/
`- Application.Orders/
   |- Domain/
   |- Application/
   |  |- Commands/
   |  |- Queries/
   |  `- Admission/
   |- Decisions/
   |- Rules/
   |- Contracts/
   `- Events/
```

This is a sample shape, not a requirement to create every folder. Only folders with real responsibilities should exist.

A provider split is earned when provider code would contaminate host-neutral business code:

```text
modules/orders/
|- Application.Orders/
`- Application.Orders.Postgres/
```

A Workstation adapter is created when actual Workstation-specific presentation/local execution/platform code exists.

## 5. Earned Core/Server/Workstation split

A later compile-time split may be:

```text
modules/orders/
|- Application.Orders.Core/
|- Application.Orders.Server/
|- Application.Orders.Workstation/
`- Application.Orders.Postgres/
```

Only use it when at least one concrete pressure exists: genuine cross-host deterministic reuse, compiler-enforced provider/platform neutrality, package/reference isolation, module size/ownership clarity, selective packaging, or materially different application responsibilities.

Do not split merely to match a diagram.

## 6. Host/process rule

Runtime hosts/adapters invoke capability-owned application behavior; they do not own duplicate business implementations.

Conceptually:

```text
CoreApi ----+
SyncApi ----+---> Orders / Customers / Inventory / ...
Worker -----+
AdminApi ---+
```

Different hosts may enter different use cases because trust/workload context differs, but business invariants remain capability-owned.

Inside one process, modules communicate in-process by default. HTTP/gRPC is not introduced between ordinary modules to imitate microservices.

## 7. Workstation/process map

The desktop product may eventually contain:

```text
SquiFlow Desktop Application
|- Workstation      UI/local application runtime
|- Guard            supervision/recovery companion
`- earned helpers   Diagnostics / Maintenance / Sync / Document only when isolation is justified
```

A separate executable is justified by process/fault/security/lifecycle/resource isolation, not aesthetics.

## 8. Persistence/state placement

Accepted directions remain:

- PostgreSQL: authoritative central transactional state;
- SQLite/WAL: Workstation local/provisional state;
- object storage: large object bytes with authoritative metadata elsewhere;
- durable processing state: explicit owner/recovery contract;
- cache/session/rate state: disposable unless explicitly defined otherwise.

These selections do not create projects until their owning implementation slice is developed.

## 9. Explicit project-creation rule

A project/executable earns existence for a real reason such as:

- independent lifecycle;
- process/fault/security isolation;
- materially different workload/backpressure/scaling;
- resource reclamation after heavy work;
- availability independence;
- compiler-enforced dependency direction;
- genuine cross-host shared code needing provider/platform neutrality;
- stable wire/IPC/plugin contract;
- active provider migration/multiple concrete implementations;
- selective packaging/reference requirements;
- benchmark/test executable needs.

If none applies, prefer a cohesive existing boundary.

The current Branding/CoreApi boundary is earned by the public white-label bootstrap slice and its host-neutral plus real-pipeline tests. Every additional project must likewise earn its existence through a useful vertical slice and evidence.

## 10. KISS and file structure

The goal is low accidental complexity, not low capability.

KISS does **not** justify collapsing boundaries that protect authority/security/recovery, and it does not justify omitting edge/failure behavior. YAGNI prevents future-only scaffolding, not current correctness.

Detailed engineering rule: `docs/architecture/ENGINEERING_PRINCIPLES.md`.

## 11. Verification direction

Architecture verification proves boundaries that actually exist rather than requiring speculative ones.

`Application.Architecture.Tests` now guards the current project-reference graph, host-neutral provider isolation, adapter ownership, executable classification, exact solution membership, canonical README implementation inventory and direct EF migration calls inside CoreApi. An intentionally invalid graph fixture proves that it detects capability-to-adapter or adapter-to-host references and an Npgsql package in a host-neutral capability. Source-level cross-module SQL ownership and future Guard/Worker boundaries are not claimed by this guard; add their own evidence when introduced.

The current enforceable repository conventions are deliberately short:

| Boundary | Current owner and regression guard |
|---|---|
| Host-neutral capabilities cannot depend on hosts or provider packages; PostgreSQL adapters depend toward their owning capability. | `tests/architecture/Application.Architecture.Tests` checks project metadata and solution membership. |
| Module SQL and migrations stay in their PostgreSQL adapter; the migrator selects known migration modules explicitly. | Adapter integration tests exercise schema and SQL; `MigrationRegistryTests` compares migration-owning projects with the ordered registry. |
| CoreApi serves requests; DatabaseMigrator runs once with a separate schema credential and advisory lock. | `Program.cs` leaves middleware ordering visible; migrator process tests cover lock contention and documented exit code. Deployment identity separation is described in the operations owner. |
| Runtime database rights are restricted to current queries and mutations. | `deploy/database/` owns the role-grant artifact; real PostgreSQL tests exercise allowed and forbidden statements. |

The first guard cannot detect arbitrary SQL against another module's tables or transitive assembly behavior. Review those changes at the owning adapter and extend real provider tests when they become a claimed path. Current focused owners and accepted decisions define intended behavior; source, migrations and tests show what is actually implemented. A mismatch is a drift defect to resolve, not permission to silently redefine the decision.
