# Phase 0B — Application Kernel and Module Foundation

**Status:** RETIRED / HISTORICAL — the qualified implementation was purged on 2026-09-17

**Purpose:** Establish only the shared application/module primitives that real capability work actually needs, while preserving explicit business ownership and dependency direction.  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

> This file records the former 0B gate and what the deleted slice proved at qualification time. It does not describe current implementation, authorize recreation, or carry active evidence/regression claims. Current truth is in `README.IMPLEMENTATION.md` and `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md`.

## Production intent

After 0B passes, a developer can introduce and extend real capability code while relying on a production-honest ownership/dependency foundation: capability meaning stays capability-owned, host/provider leakage is mechanically prevented where a compile-time boundary exists, and shared Foundation is introduced only when real current product-wide reuse/change pressure actually earns it.

A valid 0B result does **not** require a shared Foundation project to exist. If real capability work demonstrates that no product-wide primitive is currently needed, keeping Foundation `NOT_INTRODUCED` is the correct production-honest result.

## Starting point after reset

The Phase-0A reset snapshot contained no `SquiFlow.ApplicationKernel` project and no capability project. The previous kernel implementation is historical evidence, not code to recreate mechanically.

0B therefore did not begin with “rebuild the old kernel.” It began with real product/capability work and asked which semantics, if any, genuinely needed a shared foundation.

The qualified 0B slice introduced:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
```

The accepted capability semantic is deliberately narrow:

```text
PartyKind
= Person | Organization
```

That real slice did not expose a justified product-wide primitive, so `SquiFlow.ApplicationKernel` and other shared-kernel machinery remain `NOT_INTRODUCED`.

## Foundation rule

A primitive belongs in Foundation only when its meaning is product-wide and real current consumers need it. Prefer capability-owned types until common semantics are demonstrated.

Potential shared concepts from accepted architecture include stable identifiers, tenant/security context, feature/permission/setting definitions, execution-authority semantics, and module dependency/composition primitives. Their exact shape is re-earned from consumers rather than copied from deleted implementation.

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

A host-neutral project must not depend outward on UI frameworks, ASP.NET host types, Windows APIs, DB/provider SDKs, identity/authorization/key-service SDKs, scheduler/actor/broker runtimes, or observability vendors unless the owning boundary explicitly permits them.

For the qualified 0B scope, `SquiFlow.Parties` has no outward project/package dependency. `PartiesDependencyBoundaryTests.Capability_has_no_outward_project_or_package_dependencies` mechanically protects that exact current claim.

## Explicit composition

Executable topology is not capability metadata. A future Workstation/CoreApi/SyncApi/Worker/Admin host explicitly composes the capability/adapters it references when that host actually exists.

Execution/authority vocabulary such as `DeviceLocal`, `LocalProvisional`, and `ServerAuthoritative` may be introduced when a real operation needs those semantics. Do not create it merely because an old kernel or roadmap mentioned it.

## Capability-first discovery rule

A real capability may be the first code introduced. Example shapes in repository docs are illustrative. Start with the folders/types the declared scope actually needs and keep the responsibility coherent.

If real capability work exposes duplicated stable product-wide semantics, that is evidence to extract/strengthen Foundation. Do not manufacture a second consumer merely to satisfy a reuse checklist.

Therefore 0B can qualify in either of two honest outcomes:

```text
real capability work
→ shared product-wide pressure exists
→ introduce and prove the smallest shared primitive
```

or:

```text
real capability work
→ no shared product-wide pressure exists
→ keep Foundation NOT_INTRODUCED
→ record that absence as the qualified discovery result
```

The second outcome is what the current 0B slice demonstrated.

## Smallest production-honest scope

0B is not satisfied by tiny abstractions that only make tests compile.

For every primitive or capability/composition boundary introduced, declare:

```text
SCOPE
- exact semantics it claims

PRODUCTION_HONESTY
- applicable invalid/failure/dependency cases
- dependency/ownership guarantee
- evidence that can falsify the guarantee

NON-SCOPE
- future semantics deliberately not introduced
```

A narrow primitive/capability semantic is fine. A shallow introduced responsibility whose known required behavior is deferred is `BLOCKED`.

Do not build future features merely to make the kernel broad. Breadth can remain small; the depth of the declared claim cannot be prototype-grade.

## Verification

As real boundaries appear, add tests for the claims they introduce. Do not pre-write architecture checks for assemblies/projects/providers that do not exist.

The qualified 0B slice uses:

- `PartyKindTests.Accepted_kinds_match_the_documented_party_semantics` for the accepted structural Party semantic;
- `PartiesDependencyBoundaryTests.Capability_has_no_outward_project_or_package_dependencies` for the current host/provider-neutral project boundary;
- repository-owned `restore → Release build → test` commands executed successfully by the GitHub Actions verification workflow.

Once a dependency/semantic claim qualifies, its mechanical rule remains a normal regression guard for later changes that can violate the same boundary.

A passing test is evidence only when it traces to an accepted invariant/contract; tests written around an implementation shortcut do not redefine that shortcut as correct.

## Scope contract at exit

At 0B qualification:

- the production intent above is achieved by the real Parties capability scenario;
- introduced Party semantic/dependency/verification responsibilities are `PRODUCTION_HONEST` with executable evidence;
- shared Foundation/ApplicationKernel remains honestly `NOT_INTRODUCED` because no current product-wide pressure earned it;
- future Party identity/lifecycle/persistence/runtime concerns remain `NOT_INTRODUCED`;
- `BLOCKED = none`.

Detailed evidence is recorded in `0B_STATUS.md`.

## Exit gate

0B passes when real capability work proves that:

- capability/business semantics have explicit ownership;
- any introduced shared primitive is justified by real current reuse/change pressure, **or** shared Foundation remains `NOT_INTRODUCED` when the real slice exposes no such pressure;
- no executable topology leaks into capability metadata;
- dependency direction is mechanically protected for the compile-time boundaries that actually exist;
- every introduced primitive/capability-boundary claim is production-honest for its declared semantics rather than a phase-only stub;
- no speculative common framework has been created;
- no speculative future evidence map has been created around not-yet-existing boundaries;
- there is no known introduced shortcut that must be rewritten merely to make the declared 0B guarantee trustworthy;
- `BLOCKED = none`.

The current Parties slice satisfies this gate without creating Foundation.
