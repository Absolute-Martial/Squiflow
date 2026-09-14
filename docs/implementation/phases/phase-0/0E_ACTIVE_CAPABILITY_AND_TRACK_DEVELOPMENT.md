# Phase 0E — Active Capability and Parallel-Track Development

**Purpose:** Make clear that Phase 0 is a maturity envelope, not a restriction to one kernel or one capability, while requiring every introduced responsibility to be production-honest for what it explicitly claims.

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## Production intent

After 0E passes, real product slices can advance in parallel without framework-first blocking, while every claimed path has an explicit owner/state/authority model and no introduced component is allowed to hide prototype debt behind narrow phase wording.

## Starting point after reset

No Customers, Orders, Workstation, Web, API, Guard, or observability/test project is currently implemented. Names in architecture/phase examples are illustrative product directions, not current repository inventory.

## Core rule

> Any real capability/component may advance when it obeys the architecture and engineering gates already reached and does not claim authority/durability/security/compatibility/recovery that its foundations do not provide.

A later phase headline unlocks deeper behavioral maturity; it is not necessarily the first date a capability name may exist.

## Real-slice rule

Start from actual product/domain work. Possible capability examples include Customers, Orders, Quotations, Inventory, Products/Pricing, Suppliers, Staff, Devices, Documents, and Payments—but do not create a folder per noun.

For each introduced responsibility, answer the applicable questions:

1. Who owns its business meaning?
2. Is its state authoritative, provisional, derived, cached, ephemeral, or presentation-only?
3. What invariants and edge cases already apply?
4. Does it introduce durability, trust, a process, provider, queue/retry/buffer, or versioned contract?
5. What failure/recovery/security/concurrency/compatibility obligations follow now?
6. What dependencies are allowed/forbidden?
7. What resource bounds apply?
8. What evidence/tests protect those claims?
9. What exact scope is being claimed and what is explicitly not yet in scope?

Do not force irrelevant infrastructure questions onto a pure value object.

## Production-honest current scope

Business breadth may remain intentionally small. What exists may not be fake.

Allowed examples:

- a value object production-honest for its declared validation/invariants;
- a semantic command/query contract needed by real work with explicit semantics;
- a UI adapter over a real application surface;
- an isolated provider/transport POC explicitly marked non-production and unable to satisfy the production gate.

Not allowed:

- in-memory state presented as final authoritative durability;
- Workstation provisional state presented as shared financial/stock/security authority;
- fire-and-forget work presented as durable;
- `TODO auth later` around an exposed privileged operation;
- empty future Infrastructure/Worker/Server projects for diagram symmetry;
- a partially introduced real path carried forward as later hardening.

Use only these gate states:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

`BLOCKED` must be resolved or un-introduced before the applicable gate passes.

## Pull-forward rule

If a real requirement needs a later foundation earlier, pull that owner/gate forward and satisfy its production-honesty bar rather than creating a temporary unsafe shortcut.

## Parallel work

Parallel changes are fine when they form coherent slices and do not create many unrelated half-finished architectural initiatives. A real slice may touch capability, host adapter, tests, observability, and deployment together if those changes are necessary for one production-honest declared scope.

## Exit gate

0E passes when real product slices prove that:

- capability development is not trapped behind a framework-first process;
- business meaning remains capability-owned across host adapters;
- another real capability can be introduced without changing fundamental dependency direction;
- later foundations can be pulled forward deliberately when truly required;
- `NOT_INTRODUCED`, `PRODUCTION_HONEST`, and `BLOCKED` are used explicitly rather than `complete enough` wording;
- `BLOCKED = none` for scope claimed by the gate;
- no component is called qualified merely because a phase checklist was minimally satisfied.
