# Repository and Deployment Boundaries

**Version:** v0.0.20

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

## 2. Current implementation state

The `v0.0.20` 0A reset snapshot intentionally contained no application/service/foundation-library/capability/test projects. That clean snapshot is qualification evidence, not a permanent target.

The active 0B branch has now earned exactly two projects:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
```

`SquiFlow.Parties` currently owns only the accepted `PartyKind = Person | Organization` semantic. The test project verifies that semantic and the current no-outward-dependency boundary.

There is still no Foundation/ApplicationKernel project, host/service executable, persistence/provider project, or other capability project.

Earlier Phase-0 code remains in Git history and is not current implementation authority. New projects are introduced only with a real current responsibility, explicit dependency/authority boundary, material applicable behavior, and falsifiable verification.

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
|  |- core-api/                   # compact authoritative host if/when reintroduced
|  |- web-api/                    # interactive API host when workload split is real
|  |- sync-api/                   # Workstation sync host when workload split is real
|  |- admin-api/                  # private platform-control backend when needed
|  `- worker/                     # durable background execution host when needed
|- modules/
|  `- <capability>/
|     |- SquiFlow.<Capability>/
|     |- SquiFlow.<Capability>.Postgres/       # only with real provider code
|     `- SquiFlow.<Capability>.Workstation/    # only with real local adapter code
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
`- SquiFlow.Orders/
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

This is a sample shape, not a requirement to create every folder. Only folders with real responsibilities should exist. The current Parties slice therefore contains only the `Domain/` folder it actually needs.

A provider split is earned when provider code would contaminate host-neutral business code:

```text
modules/orders/
|- SquiFlow.Orders/
`- SquiFlow.Orders.Postgres/
```

A Workstation adapter is created when actual Workstation-specific presentation/local execution/platform code exists.

## 5. Earned Core/Server/Workstation split

A later compile-time split may be:

```text
modules/orders/
|- SquiFlow.Orders.Core/
|- SquiFlow.Orders.Server/
|- SquiFlow.Orders.Workstation/
`- SquiFlow.Orders.Postgres/
```

Only use it when at least one concrete pressure exists: genuine cross-host deterministic reuse, compiler-enforced provider/platform neutrality, package/reference isolation, module size/ownership clarity, selective packaging, or materially different application responsibilities.

Do not split merely to match a diagram.

## 6. Host/process rule

Runtime hosts/adapters invoke capability-owned application behavior; they do not own duplicate business implementations.

Conceptually:

```text
WebApi -----+
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

The current Parties project is the first compact capability boundary. The current test project is earned by the need for executable semantic/dependency evidence. No other project is implied by their existence.

## 10. KISS and file structure

The goal is low accidental complexity, not low capability.

KISS does **not** justify collapsing boundaries that protect authority/security/recovery, and it does not justify omitting edge/failure behavior. YAGNI prevents future-only scaffolding, not current correctness.

Detailed engineering rule: `docs/architecture/ENGINEERING_PRINCIPLES.md`.

## 11. Verification direction

Architecture verification proves boundaries that actually exist rather than requiring speculative ones.

The current Parties test project mechanically protects its no-outward-project/package dependency claim. Future provider/host leakage checks, executable-to-executable reference restrictions, Guard isolation, cross-capability ownership, and other dependency rules are added only when those concrete boundaries exist.
