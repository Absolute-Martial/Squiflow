# Explicit Boundaries and SOLID Design Rules

**Version:** v0.0.20  
**Status:** Accepted architecture owner  
**Scope:** Cross-cutting code/module/process boundary design and SOLID application

## 1. Decision

SquiFlow uses **explicit ownership and dependency boundaries first** and applies SOLID as a change-safety/design-review lens inside those boundaries.

Neither Clean Architecture nor SOLID is interpreted as a required number of projects, layers, interfaces, factories, base classes, handlers, repositories, or services.

The governing rule is:

> A boundary exists because it protects a real difference in business ownership, authority, dependency direction, platform/provider coupling, process/fault isolation, security, persistence, lifecycle, compatibility, or replaceability.

A boundary that cannot explain what change/failure/authority it protects is probably ceremony.

For implementation-quality rules, complete KISS, YAGNI, DRY, testing, resilience, security, data, API, CI/CD and observability guidance, also read `docs/architecture/ENGINEERING_PRINCIPLES.md`.

## 2. Explicit-boundary contract

Every material boundary should be able to answer these questions:

```text
Owner
    What responsibility/business meaning does this boundary own?

Inputs
    What intents/facts/contracts may enter?

Outputs/effects
    What decisions/state/effects may leave?

Authority
    Is it local, provisional, authoritative, derived, or presentation-only?

State
    What state may it own and what durability/transaction rules apply?

Dependencies
    What may it depend on and what is forbidden?

Failure
    What happens if this boundary or a dependency fails?

Compatibility
    Which contracts/versions must survive independent upgrade or offline duration?

Security
    What trust/tenant/permission/provider credentials may cross it?

Observability
    What evidence identifies success/failure without becoming business authority?
```

If these answers differ materially, combining responsibilities merely to reduce file/project count is not simplification.

If these answers are effectively identical and the split only forwards calls, keep the code together.

## 3. Dependency direction

The default source dependency direction is inward:

```text
Foundation
   ↑
Capability-owned business meaning / Capability Core
   ↑
Authoritative application and host/provider adapters
   ↑
Executable composition roots
```

Important consequences:

- Foundation does not depend on business capabilities, apps, services, host UI frameworks, provider SDKs, or deployment topology.
- A host-neutral Capability Core does not depend on Workstation/Web/API/Worker/Admin adapters or executable projects.
- Host/provider adapters may depend inward on their capability and Foundation.
- Executable composition roots may depend on the capability/adapters they actually run.
- Executables do not reference other executable projects to create hidden process coupling; cross-process communication requires an explicit protocol/contract boundary.
- Tests may cross these boundaries intentionally to verify them.

Physical `.csproj` boundaries are added when compiler/package/runtime isolation earns them. Conceptual ownership is mandatory even before a physical split is earned.

## 4. Capability boundary

A capability owns one source implementation of its business meaning.

For example, Orders owns Order identities/invariants/decisions/contracts and authoritative use cases. Workstation, Web/API, Sync and Worker may enter Orders through different adapters/use cases, but they do not create separate definitions of what an Order means.

Allowed:

```text
Workstation adapter -> Orders capability
CoreApi/WebApi       -> Orders authoritative application
Sync adapter         -> Orders admission use case
Worker               -> Orders background use case
```

Rejected:

```text
WorkstationOrderBusiness
WebOrderBusiness
SyncOrderBusiness
WorkerOrderBusiness
```

when each independently implements the same business meaning.

These names are architectural examples; the v0.0.20 reset currently contains no capability/host project.

## 5. Process/host boundary

A process boundary is different from a capability boundary.

Processes such as Workstation, Guard, CoreApi, future SyncApi, Worker or AdminApi exist for lifecycle, fault, workload, trust, deployment or operational isolation. Their names are **not application-kernel business vocabulary**.

Therefore:

- there is no global `HostKind` enum listing current/future executables in capability metadata;
- Guard is not representable as a business-capability host;
- adding a future Worker/SyncApi/AdminApi does not require changing capability-core enums merely to announce that the process exists;
- composition roots explicitly select the capability/adapters they run.

Host-specific availability belongs to an actual adapter/contribution when that adapter exists, not to speculative topology metadata in the Capability Core.

## 6. Authority and persistence boundaries

A persistence technology is not itself the business boundary.

The owning capability/application controls business mutation and transaction semantics. Provider-specific persistence stays behind that owner.

```text
authoritative command
    -> owning capability/application
    -> current facts + authorization + invariants
    -> explicit transaction/concurrency/idempotency
    -> PostgreSQL adapter
```

Workstation SQLite is local/provisional state. PostgreSQL is server authority. Object storage owns large object bytes. Cache/telemetry are never promoted to authority simply because they are convenient to access.

API/controllers/hosts do not bypass ownership with ad-hoc SQL.

## 7. SOLID in SquiFlow

### S — Single Responsibility Principle

A unit should have one **cohesive reason to change**, not one method or one tiny responsibility per class.

Good examples:
- Guard owns desktop supervision/recovery, not Customers/Orders logic.
- a capability owns its business semantics;
- a PostgreSQL adapter changes for persistence/provider reasons, not Avalonia UI reasons;
- a Workstation ViewModel changes for presentation interaction, not authoritative database rules.

SRP may justify a new class/project/process when reasons-to-change/fail are materially independent. It does not justify forwarding-only `Manager`, `Service`, `Helper`, or `Handler` chains.

### O — Open/Closed Principle

Stable business behavior should support expected extension without requiring unrelated central switch statements or topology enums.

Examples:
- adding a real capability should register/compose its descriptor and adapter rather than extend a global enum of every executable/capability combination;
- adding a provider behind an already-earned provider boundary should not leak provider SDK types into the business contract.

OCP does **not** mean source code may never be modified. Do not build speculative plugin systems/factories merely to claim extensibility.

### L — Liskov Substitution Principle

When SquiFlow defines a replaceable abstraction, implementations must preserve the semantic contract, including material non-functional behavior.

For a provider boundary this includes more than method signatures: failure classification, cancellation, idempotency assumptions, consistency/durability expectations, security, size limits and recovery semantics must remain compatible.

An implementation that technically implements an interface but changes these semantics is not substitutable.

### I — Interface Segregation Principle

Consumers should depend only on the capability they need.

Prefer narrow operation/provider contracts over giant `IServiceManager`, `IRepository<T>` or provider facades that expose unrelated operations.

Do not split interfaces mechanically. One cohesive concrete type is preferable to several one-method interfaces when there is no real consumer/replacement boundary.

### D — Dependency Inversion Principle

High-level business policy must not depend directly on volatile provider/platform details when a real inversion boundary is required.

Examples of valid seams include selected identity/authorization/key-management/object/backup/provider integrations and external fact/effect boundaries when concrete implementation begins.

DIP does **not** require:

```text
IThing for every Thing
IRepository<T>
IUnitOfWork
IManager
IService around every class
```

A concrete dependency is correct when it is stable, owned at the same level, and no meaningful inversion/replacement/failure boundary exists.

## 8. SOLID interaction with SquiFlow architecture

SOLID is subordinate to ownership/authority correctness.

A design is not accepted merely because it is "SOLID" if it:

- duplicates business rules across hosts;
- hides authority behind abstractions;
- creates a generic repository that weakens aggregate/query ownership;
- makes process boundaries invisible;
- introduces a distributed call between modules that should be in-process;
- turns provider failure semantics into a generic success/failure boolean;
- creates interfaces whose only job is forwarding;
- requires modifying Foundation whenever a new executable is added.

Likewise, using concrete classes is not a SOLID violation when responsibilities and dependency direction remain correct.

## 9. Review test for a proposed abstraction

Before introducing an interface, base class, adapter, project, process or generic framework, answer:

1. What concrete responsibility/replacement/failure/security boundary does it protect?
2. Who owns the contract semantics?
3. Which dependency is being inverted or isolated?
4. What is forbidden from crossing the boundary?
5. Is there a real current consumer/implementation pressure, or only anticipated symmetry?
6. What contract/failure behavior must every implementation preserve?
7. Would deleting the abstraction today lose a meaningful architectural guarantee?

If these cannot be answered, prefer the simpler cohesive concrete implementation.

## 10. Mechanical enforcement

Architecture tests should enforce what can be proven mechanically **when the corresponding projects exist**, including:

- Foundation and neutral capability projects cannot reference apps/services or host/provider adapters;
- neutral capability code cannot import forbidden UI/server/provider/runtime namespaces/packages;
- executable projects do not reference other executable projects;
- Guard cannot reference business capability modules;
- capability-core metadata does not reintroduce global executable-topology vocabulary such as `HostKind`/`SupportedHosts`;
- provider boundaries get contract tests when real replacement/substitution semantics exist.

Tests cannot prove SRP/OCP/LSP/ISP/DIP completely. Code review must still inspect cohesion, semantic substitutability, interface breadth and whether an abstraction has a real reason to exist.

## 11. Phase rule

Phase definitions are minimum maturity gates, not exceptions to these design rules.

When a component is introduced, it should be complete and coherent for the responsibility it currently owns, even if later capabilities remain intentionally deferred. "The next phase will clean it up" is not a justification for crossing an explicit boundary or introducing knowingly disposable architecture.

Related owners:
- `docs/architecture/ENGINEERING_PRINCIPLES.md`
- `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`
- `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`
- `docs/architecture/APPLICATION_KERNEL_AND_MODULES.md`
- `docs/architecture/REPOSITORY_STRUCTURE.md`
- `docs/decisions/CURRENT_DECISIONS.md`
