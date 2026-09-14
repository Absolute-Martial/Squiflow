# Phase 0B — Application Kernel and Module Foundation

**Purpose:** Establish only the shared application/module primitives that real capability work actually needs, while preserving explicit business ownership and dependency direction.

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## Production intent

After 0B passes, a developer can introduce and extend real capability code while relying on a production-honest dependency/ownership foundation: shared primitives have stable meaning, host/provider leakage is mechanically prevented where a compile-time boundary exists, and no disposable framework/kernel shortcut is being presented as architectural foundation.

## Starting point after reset

There is currently **no `SquiFlow.ApplicationKernel` project and no capability project**. The previous kernel implementation is historical evidence, not code to recreate mechanically.

0B therefore does not begin with “rebuild the old kernel.” It begins with real product/capability work and asks which semantics genuinely need a shared foundation.

## Foundation rule

A primitive belongs in Foundation only when its meaning is product-wide and at least one real current consumer needs it. Prefer capability-owned types until common semantics are demonstrated.

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

A real capability may be the first code introduced. Example shapes in repository docs are illustrative. Start with the folders/types the declared scope actually needs and keep the responsibility coherent.

If a second real capability exposes duplicated stable product-wide semantics, that is evidence to extract/strengthen Foundation.

## Smallest production-honest scope

0B is not satisfied by tiny abstractions that only make tests compile.

For every primitive/composition boundary introduced, declare:

```text
SCOPE
- exact semantics it claims

PRODUCTION_HONESTY
- invalid/duplicate/cycle/version/failure cases that materially apply
- dependency/ownership guarantee
- evidence that can falsify the guarantee

NON-SCOPE
- future semantics deliberately not introduced
```

A narrow primitive is fine. A shallow primitive whose known failure/validation/dependency behavior is deferred is `BLOCKED`.

Do not build future features merely to make the kernel broad. Breadth can remain small; the depth of the declared claim cannot be prototype-grade.

## Verification

As boundaries appear, add tests for the claims they introduce, such as duplicate IDs, missing/cyclic dependencies, deterministic ordering, provider/host leakage, stable validation, and dependency-direction violations.

Architecture tests should be as small as possible while mechanically protecting real boundaries.

A passing test is evidence only when it traces to an accepted invariant/contract; tests written around an implementation shortcut do not redefine that shortcut as correct.

## Scope contract before exit

Before 0B closes, record:

- the production intent above as achieved by a real consumer scenario;
- every introduced primitive/boundary as `PRODUCTION_HONEST` with evidence;
- genuinely future items as `NOT_INTRODUCED`;
- `BLOCKED = none`.

## Exit gate

0B passes when real capability work proves that:

- shared primitives have clear product-wide ownership;
- at least two meaningful consumers can use the composition/foundation model without a competing framework, or an equivalent real reuse pressure is proven;
- no executable topology leaks into capability metadata;
- dependency direction is mechanically protected where a compile-time boundary exists;
- every introduced primitive is production-honest for its declared semantics rather than a phase-only stub;
- no speculative common framework has been created;
- there is no known introduced shortcut that must be rewritten merely to make the declared 0B guarantee trustworthy.
