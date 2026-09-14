# Phase 0E — Active Capability and Parallel-Track Development

**Full phase hierarchy:** `docs/implementation/phases/README.md`

**Purpose:** Make explicit that Phase 0 is a development envelope, not a restriction that only kernel/host foundation work may proceed. The same principle continues through every later phase package.

## 1. Core rule

> Any real capability or component may advance during Phase 0 when it obeys the architectural gates already reached and does not claim behavior whose required foundation has not yet been implemented.

Therefore:

```text
Phase 0 does not mean:
"only Customers exists"
"only kernel code may change"
"Orders must wait until an Orders phase"
"Web must remain empty"
"Workstation cannot gain more screens"
```

It means those components may grow **within the currently available authority/durability/security model**.

The same interpretation applies to later phases. For example, Phase 9 does not mean Inventory may only begin in Phase 9; it means protected stock concurrency/current-authority behavior must satisfy the Phase-9 maturity gate when that behavior is implemented.

## 2. Development tracks

### Capability track

Current Customers work may continue.

Additional real capability seeds may be added, for example:

```text
Orders
Quotes
Inventory
Products/Pricing
Suppliers
Documents
```

Only add capabilities supported by current product/domain work. Do not create a directory per possible ERP noun.

### Workstation track

May add:

- capability workspaces/views;
- navigation;
- presentation state;
- device/lifecycle UX;
- local/non-authoritative drafts where safe;
- UI for currently available feature/permission metadata.

Must not claim real local-first durable business execution until the SQLite/encryption/migration foundations are implemented and qualified.

### Web track

May add:

- capability pages;
- navigation/layout;
- safe forms that call real server behavior when it exists;
- tenant settings views backed by real authoritative operations when available.

Must not invent browser-authoritative offline business persistence.

### Server/API track

May add:

- reviewed endpoints for real capability queries/commands;
- explicit DTOs;
- host-level validation/Problem Details foundations;
- health/admission/security metadata;
- in-process composition over capability-owned application/query surfaces.

Must not put provider/database/business implementation directly into endpoints for convenience.

### Guard track

May improve supervision/restart/lifecycle evidence.

Must not become the database migration engine, business service, key vault, or server Worker.

### Observability/test/deployment tracks

Continue growing with every real path introduced.

## 3. Capability maturity is progressive

A capability can appear early and gain deeper execution modes later.

Example Customers progression:

```text
Phase 0
  identity/value meaning
  contracts
  feature/permission/setting metadata
  Workstation/API presentation adapters

later persistence phase
  authoritative PostgreSQL state

later local-store phase
  local SQLite representation when required

later sync phase
  versioned synchronization

later rules/forms phase
  configurable behavior

later restore/production phases
  full recovery/capacity qualification
```

Example Orders progression:

```text
Phase 0
  OrderId / line/value semantics / command contract seed

central persistence/security phases
  authenticated authoritative server operations

local-store phase
  local durable representation

sync phase
  OperationEnvelope/admission

local-provisional phase
  offline local execution + reconciliation

inventory/payment phases
  protected shared-fact/external-effect relationships
```

The later headline phase is when a new **behavioral maturity** becomes available, not necessarily when the capability's folder is first created.

## 4. Allowed early design work versus fake implementation

Allowed:

- define a value object needed by current UI/domain work;
- define a semantic command/query contract;
- create a capability module descriptor;
- create a Workstation/API adapter;
- write pure domain tests;
- create an isolated POC for SQLite/gRPC/provider behavior.

Not acceptable:

- use an in-memory dictionary as if it were the final server persistence for real authoritative customer data;
- let Workstation local state masquerade as authoritative stock/payment state;
- create a fake Sync service that drops operations on restart;
- introduce `TODO auth later` around externally reachable privileged operations;
- create empty Infrastructure/Server/Worker projects to make architecture diagrams look implemented.

## 5. Sustainability checklist for any component added in Phase 0

Before merging a new component/capability, ask only the applicable questions:

1. Who owns its business meaning?
2. Is the current state authoritative, provisional, derived, cached, or presentation-only?
3. Does it introduce a real durable state? If yes, is the correct persistence foundation available?
4. Does it introduce an external/user trust boundary? If yes, is the current security foundation sufficient?
5. Does it create a versioned/durable cross-process contract? If yes, is compatibility owned?
6. Does it introduce a process? If yes, are lifecycle/recovery/health obligations implemented now?
7. Does it create a provider boundary? If yes, is replacement/error/secret ownership explicit?
8. Does it create a queue/retry/buffer? If yes, is it bounded and non-authoritative unless intentionally durable?
9. Can it remain compatible with the accepted future architecture without embedding the wrong host/provider dependency?
10. What test protects those claims?

Do not force irrelevant questions onto a pure value object.

## 6. Parallel work and WIP

The high-level roadmap's WIP limit should be interpreted as limiting **uncontrolled unfinished architectural initiatives**, not prohibiting coherent parallel changes inside one active maturity envelope.

A Phase-0 vertical slice may legitimately touch:

```text
Orders core
Orders Workstation adapter
Orders API adapter
ApplicationKernel
architecture tests
observability
Web page
```

if those changes form one coherent responsibility and remain inside Phase-0 guarantees.

Avoid opening many unrelated half-finished platform initiatives simultaneously.

## 7. New architecture mechanisms remain workload-driven

A real capability requirement may also be evidence that a later boundary is needed earlier than originally scheduled.

If that happens, do not simply violate the phase plan. Instead:

```text
real requirement appears
→ identify required architecture responsibility
→ pull forward the owning foundation/gate
→ implement that foundation correctly
→ update roadmap/decision record
→ then use it
```

Example:

If a real Phase-0 capability unexpectedly requires durable long-running background work, either keep the work synchronous if it safely fits, or explicitly pull forward the durable Worker foundation. Do not invent a temporary fire-and-forget background task that will later be forgotten.

## 8. What can remain incomplete

A capability may remain incomplete in business breadth.

For example Customers does not need every future field, import flow, organization hierarchy, credit relationship, report, and portal API in Phase 0.

The requirement is that **what exists is internally honest**:

- no fake authority;
- no fake durability;
- no fake security;
- no fake compatibility claim;
- no fake recovery claim.

Breadth can expand later.

## 9. Exit gate

0E is complete when the team can demonstrate that:

- Phase 0 supports continued multi-track development rather than kernel-only work;
- at least one real capability can grow across core + host adapters without business duplication;
- adding another real capability does not require changing the fundamental dependency direction;
- later behavioral foundations can be pulled forward deliberately if a real requirement demands them;
- the roadmap distinguishes `not implemented yet` from `implemented incompletely/unsafely`;
- no component is declared finished merely because its Phase-0 gate passed.