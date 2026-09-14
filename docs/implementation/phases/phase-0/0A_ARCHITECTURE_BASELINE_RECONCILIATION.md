# Phase 0A — Architecture Baseline Reconciliation

**Detailed phase index:** `docs/implementation/phases/README.md`

**Purpose:** Ensure development starts from one coherent set of accepted architecture rules and from the repository that actually exists today.

## 1. Why 0A exists

SquiFlow has accumulated architecture decisions across focused owner documents, decision ledgers, implementation plans, historical reviews, and recent architecture MRs. Before expanding implementation, developers need one reliable precedence model so old wording does not silently reintroduce superseded architecture.

0A is therefore not documentation cleanup for its own sake. It prevents code from being built against the wrong authority, host, persistence, scheduler, security, or compatibility model.

The same reconciliation rule continues across all later detailed phase folders; a subphase file never overrides a newer focused canonical architecture owner merely because its phase number is earlier.

## 2. Mandatory foundation

Establish a documented precedence rule:

```text
current focused canonical owner
        ↓
current accepted decision record / current-decision summary
        ↓
implementation phase package
        ↓
historical review / source-study material
```

Where two focused owners conflict, the conflict must be resolved explicitly rather than resolved by whichever file a developer happens to read first.

## 3. Baseline that 0A must preserve

At minimum the implementation plan must respect these accepted architectural directions:

- .NET 10 LTS and C# long-term application implementation;
- Avalonia Workstation;
- Blazor tenant Web;
- ASP.NET Core server ingress;
- modular monolith first;
- Workstation local-first/offline for explicitly local-capable behavior;
- Web online-only for business operations in the current baseline;
- PostgreSQL central authoritative transactional state;
- SQLite/WAL Workstation local/provisional state;
- server authoritative for central/security-sensitive business decisions;
- ZITADEL for identity/authentication;
- OpenFGA for application relationship/permission decisions;
- SquiFlow remains authority for TenantContext, business/domain/workflow/concurrency/idempotency rules;
- Guard remains a small external Workstation supervision/recovery process;
- capability business meaning is shared; host/workload entry behavior may differ;
- API/Sync ingress must not duplicate the authoritative capability implementation;
- provider/framework types do not leak into host-neutral capability core;
- runtime/process/project splits are earned by real compiler/deployment/fault/security responsibilities;
- OpenBao is the current key-management direction and cryptographic root/key custody is not reimplemented by SquiFlow;
- Platform Admin is a separate private control-plane boundary when implemented;
- schema/contract compatibility and workload-driven strategy selection from open architecture refinement work must be preserved when those MRs are merged/reconciled.

## 4. Current repository reality audit

0A records what already exists so later phases do not rebuild or contradict it.

Required inventory:

```text
solution/projects
launchable executables
module projects
foundation projects
infrastructure/provider contracts
tests/specs
CI pipeline
version/deployment files
document owners
open architecture MRs
```

For each existing project classify:

```text
Seed | Active | Operational | Qualified | Expandable | ProductionQualified
```

The current code should at least recognize that ApplicationKernel, Workstation, Guard, Web, CoreApi, Customers, Observability and provider contracts already exist and are Phase-0 assets.

## 5. Active components may continue evolving during 0A

0A does **not** block ordinary development while documentation is reconciled.

Allowed work includes:

- fixing an architecture test;
- improving Workstation/Guard lifecycle code;
- adding safe host composition;
- adding a real capability concept or adapter;
- improving observability/failure vocabulary;
- adding deterministic build/deploy metadata;
- adding documentation required by current implementation.

The condition is that the work must not depend on an unresolved architecture contradiction.

## 6. Additional components may be seeded

A real new module may be created during 0A if its immediate ownership is clear.

For example:

```text
modules/orders/SquiFlow.Orders
```

may be introduced for Order identities/value semantics/contracts even though SQLite, Sync, Worker or payment behavior is not available yet.

The seed must not claim unsupported behavior and must preserve future dependency direction.

## 7. Decisions that stay open

0A does not force closure of every future implementation choice.

Examples that can remain open until their workload exists include:

- final Sync transport HTTP versus gRPC;
- exact REST version transport;
- exact SQLite encryption provider;
- exact PostgreSQL data-access implementation details;
- final Worker/scheduler implementation if current decision records are being superseded;
- future GraphQL/BFF/cache/broker/Kubernetes adoption;
- exact production capacity/SLO values.

An open choice must have an owner and a latest responsible phase/gate.

## 8. Contradiction register

Every material unresolved inconsistency should record:

```text
Topic
Conflicting documents/decisions
Current implementation impact
Temporary safe interpretation
Owner
Required resolution phase
```

A contradiction that can change code structure is not allowed to disappear into chat history.

## 9. Verification

0A is primarily documentary/governance work, but it still has executable checks where possible:

- current solution builds;
- architecture specs reflect the accepted dependency direction;
- project tree matches the documented runtime boundaries;
- open merge requests are not accidentally treated as merged `main` behavior;
- no implementation document claims a runtime/component already exists when it does not;
- no current code is deleted solely to make documentation look cleaner.

## 10. Deliverables

- current architecture-source precedence rule;
- repository/project status inventory;
- contradiction register;
- explicit list of open decisions and their owning future gate;
- confirmation that Phase-0 detailed files describe the current repository rather than a fresh fictional repository.

## 11. Exit gate

0A is complete when a developer can answer, without guessing:

1. Which document owns a material decision?
2. Which architecture decisions are accepted versus open?
3. Which components already exist?
4. Which open MRs contain not-yet-main refinements?
5. Which known contradictions could affect implementation?
6. Which later phase must close each still-open material implementation choice?

Completing 0A does not stop architecture evolution. It makes future evolution explicit and reviewable.