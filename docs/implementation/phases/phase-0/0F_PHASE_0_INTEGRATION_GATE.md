# Phase 0F — Integrated Phase-0 Gate and Carry-Forward Ledger

## Meaning

Passing Phase 0 means later development may safely depend on the development foundations that **actually exist at that time**. It does not require every architecture-reserved host/provider/process to exist and does not make current code permanently finished.

## Integrated proof categories

### Architecture ownership

Prove source precedence, explicit boundary ownership, one source of business meaning per capability, stable inward dependency direction, and in-process ordinary module communication.

### Shared foundation/module model

Prove only the primitives/modules that were actually introduced. If a kernel exists, prove its real duplicate/dependency/validation/composition semantics and that it does not encode executable topology. Do not require a kernel project merely for symmetry if real code has not earned one.

### Host/process model

For every executable that exists, prove composition, startup/shutdown/cancellation, safe configuration, bounded behavior, failure semantics, and that the process boundary is justified. If Guard/Workstation/Web/CoreApi have not all been reintroduced, do not invent them solely to satisfy this gate.

### Engineering safety

Prove repository-owned build/test/architecture checks, safe secret/configuration handling, useful structured diagnostics, bounded resources/retries/buffers, and reproducible run/deployment instructions for actual components.

### Development extensibility

Prove a real capability can grow without business duplication or provider/host leakage and that another real responsibility can be added without inventing a competing architecture.

## Failure exercises

Exercise the failures implied by actual implementation, not a fixed historical project list. Examples may include invalid configuration, dependency-cycle/missing-dependency checks, process crashes/restart budgets, cancellation, provider outage, telemetry export failure, version mismatch, duplicate command/idempotency, or restore/migration failure—but only where those responsibilities exist.

Never claim tests for PostgreSQL/SQLite/ZITADEL/OpenFGA/OpenBao/Sync/Worker/etc. unless the tested branch actually contains those implementations.

## Carry-forward record

Every material deferred item records:

```text
Item
Reason deferred
Owner
Current preservation constraint
Trigger
Latest closing phase/gate
Current check preventing accidental violation
```

## Phase-debt classification

Use:

```text
NOT INTRODUCED YET
```

or:

```text
INTRODUCED AND COMPLETE ENOUGH
FOR ITS CURRENT RESPONSIBILITY
```

Do not accept:

```text
introduced on a real path,
but correctness/security/recovery/compatibility will be fixed later
```

Later phases may add new responsibility. They cannot excuse defects in responsibility already claimed.

## Final exit statement

Phase 0 is complete when SquiFlow has a trustworthy, reproducible, explicitly bounded development foundation on which later identity, persistence, local-first, synchronization, Admin, Worker, file/recovery, protected financial/inventory, and production capabilities can be added without treating current code as disposable and without prebuilding future architecture for appearance.
