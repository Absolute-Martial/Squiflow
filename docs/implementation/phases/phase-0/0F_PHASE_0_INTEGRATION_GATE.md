# Phase 0F — Integrated Phase-0 Production-Honesty Gate and Carry-Forward Ledger

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## Production intent

Passing Phase 0 means later development can safely depend on the development foundations that **actually exist at that time** without treating them as disposable prototype architecture. It does not require every architecture-reserved host/provider/process to exist and does not make current code permanently finished.

## Required scope contract

Before Phase-0 sign-off, record:

```text
PRODUCTION INTENT
- the real developer/operator/product scenario Phase 0 now makes dependable

WITHIN SCOPE — PRODUCTION_HONEST
- every introduced foundation/capability/host/process/verification responsibility
- exact claim + owner + evidence

NOT YET IN SCOPE — NOT_INTRODUCED
- future hosts/providers/processes/capabilities deliberately absent
- preservation constraint and trigger where material

BLOCKED
- must be empty
```

A narrow Phase-0 scope is acceptable. A shallow or knowingly disposable implementation inside the declared scope is not.

## Integrated proof categories

### Architecture ownership

Prove source precedence, explicit boundary ownership, one source of business meaning per capability, stable inward dependency direction, and in-process ordinary module communication for the boundaries actually introduced.

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

A green suite cannot prove a property it never exercises. Each material claim must identify the owner/invariant it is proving and evidence capable of falsifying it.

## Carry-forward record

Only `NOT_INTRODUCED` items may be carried forward.

Every material deferred item records:

```text
Item
State = NOT_INTRODUCED
Reason deferred
Owner
Current preservation constraint
Trigger
Latest closing phase/gate
Current check preventing accidental introduction/violation
```

## Phase-debt classification

Use exactly:

```text
NOT_INTRODUCED
```

or:

```text
PRODUCTION_HONEST
```

or:

```text
BLOCKED
```

Meaning:

- `NOT_INTRODUCED`: honest future scope; may be carried forward.
- `PRODUCTION_HONEST`: introduced and trustworthy for its declared scope; may gain new breadth later.
- `BLOCKED`: introduced but not trustworthy for its claimed scope; **cannot pass the gate and cannot be carried forward**.

Do not accept:

```text
introduced on a real path,
but correctness/security/recovery/compatibility will be fixed later
```

Later phases may add new responsibility. They cannot excuse defects in responsibility already claimed.

## Final exit questions

Phase 0 does not pass unless reviewers can answer `yes`, with evidence, to the applicable questions:

1. What real scenario does this Phase-0 scope now make dependable?
2. Is every introduced responsibility production-honest for its explicit claim?
3. Is `BLOCKED` empty?
4. Are failure/security/durability/recovery/resource/compatibility obligations covered where the introduced scope requires them?
5. Can another developer reproduce the important verification from repository instructions without tribal knowledge?
6. Can we state plainly what Phase 0 does **not** yet provide?
7. Is there any known introduced shortcut we already expect to rewrite merely to make the current claim trustworthy?

If question 7 is `yes`, the gate does not pass.

## Final exit statement

Phase 0 passes when SquiFlow has a trustworthy, reproducible, explicitly bounded, production-honest development foundation on which later identity, persistence, local-first, synchronization, Admin, Worker, file/recovery, protected financial/inventory, and production capabilities can be added without treating current code as disposable and without prebuilding future architecture for appearance.
