# Phase 0F — Integrated Phase-0 Gate and Carry-Forward Ledger

**Full phase hierarchy:** `docs/implementation/phases/README.md`

## 1. Meaning of the Phase-0 gate

Passing Phase 0 means:

> Later development may safely depend on the current module/kernel, host/process, engineering-safety, and development-extension foundations.

It does **not** mean:

- Customers is finished;
- Workstation/Web/Guard/CoreApi are finished;
- persistence/security/sync/Worker/Admin are all implemented;
- no Phase-0 project will change again;
- the system is production-qualified.

All Phase-0 components remain expandable.

## 2. Integrated gate categories

### Architecture ownership

Prove:

- focused owners/decision precedence are understood;
- no known material contradiction is silently driving implementation;
- capability business meaning is not duplicated across hosts;
- host-neutral code remains provider/framework neutral;
- ordinary modules communicate in-process.

### Kernel/module model

Prove:

- module graph/dependency ordering works;
- host applicability works;
- capability-owned features/permissions/settings can be defined safely;
- duplicate/missing/cyclic definitions fail explicitly;
- adding another real module does not require a second framework.

### Host/process model

Prove:

- Workstation, Guard, Web and CoreApi build/start;
- composition roots own host concerns rather than business meaning;
- Guard can supervise Workstation externally with bounded restart behavior;
- current server host has safe startup/shutdown/health foundations;
- no empty Admin/Worker/Sync/helper process is present merely for architecture symmetry.

### Engineering safety

Prove:

- CI runs the current deterministic specs;
- architecture violations fail a check;
- current secret/configuration rules are enforced to a reasonable Phase-0 level;
- current runtime paths have safe structured lifecycle/failure evidence;
- deployment/startup can be reproduced from repository/runbook information.

### Development extensibility

Prove:

- Customers can continue evolving;
- another real capability can be seeded without violating dependency direction;
- Workstation/Web/API adapters may be added without moving business meaning into them;
- later persistence/sync/security/background foundations remain possible without rewriting host-neutral business meaning.

## 3. Required failure exercises

At least the current implementation should exercise:

- Workstation crash with Guard alive;
- Guard crash with Workstation alive;
- restart-budget behavior;
- invalid host configuration;
- module dependency cycle/missing dependency;
- wrong-host contribution;
- provider/framework leakage architecture check;
- telemetry/export failure does not become application authority failure;
- clean restore/build/start on another development environment or an equivalent reproducibility proof.

Do not claim tests for SQLite/PostgreSQL/ZITADEL/OpenFGA/OpenBao/Worker/Sync unless those implementations actually exist in the tested branch.

## 4. Carry-forward ledger

Every material item that remains intentionally deferred must use a record like:

```text
Item:
Reason deferred:
Owner:
Current preservation constraint:
Trigger:
Latest closing phase/gate:
Current preventing test/check:
```

### Example — Workstation Sync transport

```text
Item:
HTTP versus gRPC final Sync transport

Reason deferred:
No representative real sync workload has yet demonstrated the value of gRPC.

Owner:
docs/api/TRANSPORT_SELECTION.md

Current preservation constraint:
Sync/domain correctness must remain transport-independent;
no business core type may depend on ASP.NET/gRPC generated transport types.

Trigger:
Representative sync workload demonstrates streaming, binary-efficiency,
generated-contract or sustained high-frequency RPC value.

Latest closing phase/gate:
Phase 3C — first real Sync qualification.

Current preventing test/check:
Architecture dependency tests keep transport types outside host-neutral core.
```

### Example — Worker

```text
Item:
Worker executable/runtime

Reason deferred:
No real durable background workload yet.

Owner:
docs/server/CORE_API_AND_WORKER.md plus current Worker runtime owner.

Current preservation constraint:
Authoritative commits that later need consequences must be compatible with
transactional outbox/durable-work introduction; no fire-and-forget correctness dependency.

Trigger:
First real consequence that cannot safely remain in the bounded interactive request.

Latest closing phase/gate:
Phase 6C — durable Worker job lifecycle.

Current preventing test/check:
No in-memory generic queue is treated as accepted durable work.
```

### Example — PostgreSQL

```text
Item:
Full central PostgreSQL implementation

Reason deferred:
Phase 0 does not yet need real authoritative business persistence.

Owner:
docs/data/PERSISTENCE_SELECTION.md

Current preservation constraint:
Business/domain code does not depend on a temporary persistence provider or fake repository contract.

Trigger:
First real authoritative tenant/business state.

Latest closing phase/gate:
Phase 3A at the latest for the current canonical roadmap; if a real authoritative slice needs it earlier, pull that foundation forward explicitly.
```

## 5. Phase debt classification

Use only these two categories for material architecture:

```text
NOT INTRODUCED YET
```

or:

```text
INTRODUCED AND COMPLETE ENOUGH
FOR ITS CURRENT RESPONSIBILITY
```

Avoid:

```text
introduced in production path,
but correctness/security/recovery/compatibility will be added later
```

A responsibility may still gain new capabilities later; this rule is about correctness of what it already claims to do.

## 6. Transition to later phases

Phase 1 and later phases may begin while Phase-0 components keep changing.

The current detailed hierarchy continues through Phase 10 and adds maturity for:

```text
identity / tenant authorization
local Workstation durability
authoritative persistence + sync
long-offline conflict/recovery
rules/workflow/forms
Admin + Worker
files/documents/backup
cross-system hardening
payment/credit/inventory authority
paying-customer production qualification
```

Each later parent phase has its own directory and subphase files using the same cumulative model.

## 7. Final Phase-0 exit statement

Phase 0 is complete when SquiFlow has a trustworthy development foundation on which real business, persistence, identity, authorization, local-first, synchronization, Admin, Worker, recovery, and production capabilities can be layered **without treating today's Phase-0 code as disposable and without pretending today's Phase-0 components are permanently finished**.