# Phase 0B — Application Kernel and Module Foundation

**Detailed phase index:** `docs/implementation/phases/README.md`

**Purpose:** Complete the smallest stable SquiFlow-owned module/composition foundation that multiple real capabilities can safely extend.

## 1. Current starting point

`SquiFlow.ApplicationKernel` already contains useful Phase-0 primitives:

```text
HostKind
ModuleId
FeatureId
PermissionId
SettingKey
TenantId
SubjectId
TenantContext
PermissionDefinition
EffectivePermissionSnapshot
FeatureDefinition
EffectiveFeatureSnapshot
SettingDefinition<T>
ModuleDescriptor
ModuleGraph
```

The goal is not to replace this with ABP/Orchard or create a larger framework. The goal is to make the existing kernel sufficient and stable enough for several real SquiFlow capabilities.

Passing 0B does not freeze the kernel. Later phases may add narrowly justified primitives when multiple real consumers need them; they should extend this foundation rather than create a second common framework.

## 2. Mandatory 0B foundation

The kernel must own only product-wide primitives that genuinely need common semantics.

It should support, to the depth required by current modules:

- stable module identity/version;
- explicit module dependencies;
- deterministic dependency ordering/cycle rejection;
- host applicability;
- feature definition/dependencies;
- permission definition ownership;
- typed setting definition/validation;
- effective revisioned feature/permission/settings state where required;
- capability/host contribution metadata where it prevents host-specific discovery duplication;
- stable tenant/security context primitives that do not embed provider SDK types.

Where accepted architecture needs an execution-mode concept, it may be represented in kernel/capability metadata when there is a real consumer, for example:

```text
DeviceLocal
LocalProvisional
ServerAuthoritative
```

Do not add the enum merely because documentation contains the words; add it when a capability or host composition path actually consumes it.

## 3. Dependency direction

Preserve:

```text
Foundation
   ↑
Capability core/domain/contracts
   ↑
Application use cases
   ↑
Host/provider adapters
   ↑
Executable composition roots
```

Host-neutral projects must not depend on:

- Avalonia;
- ASP.NET Core;
- Windows APIs;
- EF/Npgsql/SQLite provider APIs;
- ZITADEL/OpenFGA SDKs;
- OpenBao SDK/provider types;
- Worker/scheduler runtime types;
- message-broker/provider SDKs;
- observability vendor SDKs.

## 4. Kernel restraint

Do **not** add universal abstractions such as:

```text
IRepository<T>
IUnitOfWork
IServiceManager
ICommonService
IProvider<T>
IHandler<T> for every method
```

unless an actual replacement/inversion boundary exists.

Foundation is not a dumping ground for code used by two files.

A primitive moves into Foundation only when its semantics truly belong to the whole product.

## 5. Existing modules continue growing

Customers is not frozen while the kernel is being completed.

It may add real:

- identities/value objects;
- public contracts;
- permissions;
- settings;
- feature metadata;
- Workstation contribution;
- API adapter behavior;
- tests.

The same applies to any real new capability introduced during Phase 0.

## 6. New capabilities may be added in 0B

Examples include Orders, Quotes, Inventory, Products/Pricing, Suppliers, or another demonstrated capability.

The rule is:

> Add the smallest real capability shape justified by current development, while keeping its future host/persistence/sync boundaries possible.

Example early Orders shape:

```text
modules/orders/
└── SquiFlow.Orders/
    ├── Domain/
    ├── Contracts/
    └── OrdersModule.cs
```

Later project splits can be earned when compiler-enforced neutrality, true cross-host reuse, provider isolation, packaging, or complexity requires them.

Do not scaffold all future `.csproj` files on day one.

## 7. Capability-owned metadata

Permissions/features/settings belong to the capability that defines their meaning.

Examples:

```text
orders.view
orders.create
inventory.adjust
customers.search-result-limit
```

A central catalog/evaluator may compose definitions, but the central kernel does not become the owner of every business permission/setting.

## 8. Public versus internal contracts

Where a capability needs cross-module communication, distinguish:

- internal/domain events;
- public application/query surfaces;
- integration events that other modules/processes may depend on.

Do not expose persistence entities/tables/provider types as module contracts.

Inside one process, use in-process calls. Do not manufacture HTTP/gRPC because modules are conceptually separate.

## 9. Future sustainability checks

0B code should keep these later developments possible without implementing them now:

- server-authoritative application use cases;
- PostgreSQL persistence adapters;
- Workstation SQLite adapters;
- Sync admission paths;
- Web/API host adapters;
- Worker/background handlers;
- contract/schema evolution;
- capability-owned rules/workflow/forms;
- future provider replacements.

This is a dependency-direction requirement, not an instruction to prebuild every adapter.

## 10. Tests

Architecture/kernel tests should cover as applicable:

- duplicate module/feature/permission/setting IDs;
- missing dependency;
- dependency cycles;
- wrong-host contribution;
- feature dependency closure;
- platform ceiling violation;
- deterministic setting validation/precedence;
- provider/framework package leakage;
- provider/framework namespace leakage;
- accidental direct module-to-module network dependency where static checks can identify one;
- accidental business code in Guard/foundation where dependency checks can identify it.

## 11. Allowed additions during 0B

Developers may also work on:

- Web/Workstation presentation shells;
- Guard supervision;
- CoreApi transport composition;
- observability helpers;
- CI/deployment definitions;
- capability tests;
- documentation;
- isolated POCs for a future provider/transport.

0B is not a kernel-only sprint.

## 12. Exit gate

0B is complete when:

- at least one real capability uses the kernel without provider/host leakage;
- adding a second capability does not require inventing a competing module model;
- capability-owned features/permissions/settings have clear ownership;
- host applicability/dependency ordering are deterministic;
- architecture tests protect the important dependency direction;
- no large speculative framework has been created to model future work.

The kernel remains `Expandable` after this gate.