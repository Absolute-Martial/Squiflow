# Phase 0B — Application Kernel and Module Foundation

**Purpose:** Establish only the shared application/module primitives that real capability work actually needs, while preserving explicit business ownership and dependency direction.

## Starting point after reset

There is currently **no `SquiFlow.ApplicationKernel` project and no capability project**. The previous kernel implementation is historical evidence, not code to recreate mechanically.

0B therefore does not begin with “rebuild the old kernel.” It begins with real product/capability work and asks which semantics genuinely need a shared foundation.

## Foundation rule

A primitive belongs in Foundation only when its meaning is product-wide and at least one real current responsibility needs it. Prefer capability-owned types until common semantics are demonstrated.

Potential shared concepts from accepted architecture include stable identifiers, tenant/security context, feature/permission/setting definitions, execution-authority semantics, and module dependency/composition primitives. Their exact shape is re-earned from consumers rather than copied from the deleted implementation.

Do not prebuild:

```text
HostKind / process-name registries
generic IRepository<T>
generic IUnitOfWork
IServiceManager / ICommonService
one interface per class
universal handler/provider abstractions
future process/plugin registries
```

## Dependency direction

Preserve:

```text
Foundation
   ↑
Capability business meaning
   ↑
Application/host/provider adapters
   ↑
Executable composition roots
```

A host-neutral project must not depend outward on UI frameworks, ASP.NET host types, Windows APIs, DB/provider SDKs, identity/authorization/key-service SDKs, scheduler/actor/broker runtimes, or observability vendors.

## Explicit composition

Executable topology is not capability metadata. A future Workstation/CoreApi/SyncApi/Worker/Admin host explicitly composes the capability/adapters it references when that host actually exists.

Execution/authority vocabulary such as `DeviceLocal`, `LocalProvisional`, and `ServerAuthoritative` may be introduced when a real operation needs those semantics. Do not create it merely because the old kernel had an enum.

## Capability development during 0B

A real capability may be the first code introduced. Example shapes in repository docs are illustrative. Start with the folders/types the current responsibility actually needs and keep the responsibility coherent.

If a second real capability exposes duplicated stable product-wide semantics, that is evidence to extract/strengthen Foundation.

## Complete-KISS rule

0B is not satisfied by tiny abstractions that only make tests compile. Any introduced module/composition primitive must cover its applicable invalid/duplicate/cycle/version/failure cases and be named by stable meaning.

At the same time, do not build future features merely to make the kernel “complete.” Completeness applies to **current responsibility**, not speculative breadth.

## Verification

As boundaries appear, add tests for the claims they introduce, such as duplicate IDs, missing/cyclic dependencies, deterministic ordering, provider/host leakage, stable validation, and dependency-direction violations.

Architecture tests should be as small as possible while mechanically protecting real boundaries.

## Exit gate

0B is complete when real capability work proves that:

- shared primitives have clear product-wide ownership;
- at least two meaningful consumers can use the composition/foundation model without a competing framework, or an equivalent real reuse pressure is proven;
- no executable topology leaks into capability metadata;
- dependency direction is mechanically protected where a compile-time boundary exists;
- introduced primitives are complete for their current semantics, not phase-only stubs;
- no speculative common framework has been created.
