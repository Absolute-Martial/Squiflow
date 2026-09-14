# Future Phase Carry-Forward and Anticipated Direction

**Status:** non-authoritative anticipation ledger  
**Applies to:** responsibilities that are not yet introduced  
**Canonical gate owners:** `PHASE_GATE_PRODUCTION_HONESTY.md` and `PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

This document preserves useful architectural thinking about likely future work **without turning that thinking into a specification before the real responsibility exists**.

The rule is:

```text
real responsibility arrives
        ↓
understand its actual claim
        ↓
declare production intent and scope
        ↓
write evidence capable of falsifying that claim
        ↓
assign permanent/recurring regression protection
        ↓
qualify it
```

Do not reverse that dependency by pre-writing detailed future evidence maps, cadences, transition contracts, or subphase implementations and then forcing later work to fit them.

Everything below is `NOT_INTRODUCED` unless a current owner explicitly promotes it into active implementation scope.

## How to use this ledger

For each anticipated area, the entries below preserve:

- likely responsibility class;
- why it is deferred;
- current preservation constraints;
- safe behavior while absent;
- activation trigger;
- questions worth revisiting when the responsibility becomes real.

The listed questions are **prompts, not requirements**. When work begins, the developer/reviewer must derive the actual gate from the real implementation, workload, architecture owners, and then-current requirements. The eventual subphase decomposition may differ completely from the anticipation here.

No future phase has a preassigned evidence class, cadence, hostile-test list, transition contract, or exact closing gate merely because this ledger mentions it.

---

## Anticipated Phase 2 direction — local-first Workstation durability and recovery

**State:** `NOT_INTRODUCED` as a qualified runtime responsibility.

**Why deferred:** the current repository has not yet introduced a real local durable business slice whose correctness depends on SQLite/WAL/encryption/outbox/recovery semantics.

**Current preservation constraints:**

- do not make transient UI/process memory look durable;
- preserve the architectural distinction between local/provisional and server-authoritative state;
- do not create ad-hoc local stores that would bypass the accepted local-first direction;
- Guard remains a supervision/recovery boundary rather than business authority.

**Behavior while absent:** no product path may claim crash-safe local business durability or offline authoritative completion merely because Workstation UI exists.

**Trigger:** the first real business operation that must be durably accepted locally before server confirmation.

**Anticipated questions when activated:**

- what exact local operation is being guaranteed;
- what local state is authoritative, provisional, derived, or pending;
- what storage/encryption implementation actually owns durability;
- what happens on process death, power loss, disk exhaustion, migration interruption, or restart;
- whether an outbox/operation record is part of the same atomic boundary;
- what Guard must coordinate versus what the owning capability/storage component must perform.

These questions do not preselect an evidence cadence or exact subphase structure.

---

## Anticipated Phase 3 direction — central authority and synchronization

**State:** `NOT_INTRODUCED` as a qualified cross-device/server foundation.

**Why deferred:** real authoritative PostgreSQL admission and real Workstation synchronization should be designed from an actual capability/workload, not synthetic CRUD.

**Current preservation constraints:**

- capability business meaning must remain host-neutral;
- local execution must not be described as centrally authoritative;
- operation identity/version/concurrency concepts must remain transport-independent where introduced;
- no HTTP/gRPC network hop is created between ordinary modules in the same process.

**Behavior while absent:** there is no claim that local intent is synchronized, deduplicated, centrally admitted, or compatible across independent versions.

**Trigger:** a real local-capable operation needs authoritative server admission or another real centrally persisted capability requires the same authority boundary.

**Anticipated questions when activated:**

- central tenant isolation and transaction ownership;
- retry/idempotency/concurrency semantics;
- upload/download/change-cursor behavior;
- backpressure and bounded batches;
- version overlap, migrations, and unsupported-version behavior;
- whether HTTP remains sufficient or representative measurements justify another sync transport.

---

## Anticipated Phase 4 direction — long-offline conflict and recovery

**State:** `NOT_INTRODUCED` as a qualified long-offline model.

**Why deferred:** meaningful conflict/rebase/resnapshot semantics require real locally pending intent and real server authority first.

**Current preservation constraints:**

- do not design local state so pending semantic intent can only be recovered by deleting it;
- keep expected-version/revision concepts capability-owned when introduced;
- do not make stale local security/payment/stock/credit/limit data authoritative.

**Behavior while absent:** only the currently qualified connectivity/offline duration is supported; long-offline return must not be claimed implicitly.

**Trigger:** supported operation must survive offline duration/version drift beyond the simple synchronization overlap already qualified.

**Anticipated questions when activated:**

- compatibility assessment after long absence;
- capability-specific conflict semantics;
- resnapshot versus incremental recovery;
- preserving and rebasing pending local intent;
- refresh of permissions, configuration, rules, limits, and other current-authority data.

---

## Anticipated Phase 5 direction — versioned configurable behavior

**State:** `NOT_INTRODUCED` unless a current product requirement explicitly pulls a subset forward.

**Why deferred:** rule/workflow/form engines are accidental complexity unless real tenant variability requires them.

**Current preservation constraints:**

- ordinary business behavior stays strongly typed by default;
- no arbitrary tenant C#/JavaScript/SQL execution;
- historical/issued truth must not depend on silently mutable configuration.

**Behavior while absent:** variability that cannot be expressed safely by current typed settings/business code is not advertised as supported.

**Trigger:** a real capability requires tenant-configurable decision/workflow/form behavior that cannot be represented honestly by simpler typed configuration.

**Anticipated questions when activated:**

- fact authority and deterministic evaluation;
- immutable publication/version identity;
- workflow continuation ownership;
- safe bounded dynamic forms;
- Workstation snapshot/cross-version behavior;
- decision trace and historical reproducibility.

---

## Anticipated Phase 6 direction — independent platform control/background execution

**State:** `NOT_INTRODUCED` for Admin/Worker runtime boundaries.

**Why deferred:** a process boundary is earned by real lifecycle/security/fault/resource needs, not architecture symmetry.

**Current preservation constraints:**

- authoritative commits may preserve transactional consequence/outbox semantics when real work needs them;
- platform permissions/audit concepts may exist without creating Admin executables;
- accepted durable work must never be process-memory-only once such work exists.

**Behavior while absent:** there is no independent Admin or Worker operational promise. Infrastructure/bootstrap administration remains through the explicitly accepted non-product/operator mechanisms.

**Trigger:** first real control-plane operation requiring independent security/availability, or first real durable background workload requiring an independent execution runtime.

**Anticipated questions when activated:**

- private Admin exposure, device/JIT/approval/audit requirements;
- durable job state/claim/lease/retry/quarantine semantics;
- failure independence from interactive API paths;
- scheduling occurrence identity and durable handoff;
- whether an actor/scheduler/runtime library is still the best fit under the actual workload.

---

## Anticipated Phase 7 direction — files, documents, printing, backup/restore

**State:** `NOT_INTRODUCED` as a qualified provider-bound lifecycle.

**Why deferred:** exact object/document/backup behavior must follow real capability ownership and provider workload.

**Current preservation constraints:**

- relational business authority must not be silently moved into opaque file providers;
- provider contracts stay behind adapters;
- large attachment transfer remains conceptually separate from semantic synchronization;
- a backup claim is never stronger than its restore evidence.

**Behavior while absent:** no capability may claim durable provider-backed object/document/restore guarantees that have not been implemented.

**Trigger:** first real attachment/object/document/print/recovery-set requirement.

**Anticipated questions when activated:**

- object ownership, hash/integrity, lifecycle and orphan reconciliation;
- bounded transfer/staging/capacity/accounting;
- document/printing process and physical-effect ambiguity;
- encrypted recovery set and actual restore proof.

---

## Anticipated Phase 8 direction — cross-system qualification

**State:** `NOT_INTRODUCED` as a standalone qualification package; the underlying controls must be introduced with their owning responsibilities earlier.

**Why deferred:** realistic cross-system attack/failure/performance qualification depends on the topology that actually exists at that time.

**Current preservation constraints:**

- security, observability, failure handling, and resource bounds are never postponed merely because a later qualification phase may exist;
- every new boundary already follows the global gate contracts.

**Behavior while absent:** each current responsibility is only qualified to its current evidence; no broader system-wide load/security/runtime claim is inferred.

**Trigger:** enough real integrated topology exists that combined failure/attack/load effects matter beyond individual component qualification.

**Anticipated questions when activated:**

- end-to-end trace/diagnostic usefulness;
- attack paths across API/browser/provider boundaries;
- dependency/latency/backpressure/cache budgets;
- release/container/runtime behavior under combined failure;
- which SLOs are actually supported by measured workloads.

---

## Anticipated Phase 9 direction — protected financial/shared authority

**State:** `NOT_INTRODUCED` as a qualified high-risk authority package unless a product requirement pulls a specific capability forward.

**Why deferred:** payment/inventory/credit/pricing semantics must follow the actual business model and providers rather than a generic financial architecture.

**Current preservation constraints:**

- external effects require semantic identity and outcome ambiguity handling when introduced;
- shared stock/credit/payment authority is server/current-authority work where required;
- historical financial truth is corrected/reversed rather than silently rewritten.

**Behavior while absent:** unsupported financial/shared-authority operations are unavailable rather than simulated with unsafe local authority.

**Trigger:** real payment, shared stock, credit exposure, pricing-history, refund/reversal or equivalent protected requirement.

**Anticipated questions when activated:**

- provider idempotency and `OutcomeUnknown`;
- reconciliation/refund/reversal semantics;
- inventory concurrent mutation/correction;
- credit/current-exposure authority;
- price source, applied value, currency, and historical truth.

---

## Anticipated Phase 10 direction — paying-customer production qualification

**State:** `NOT_INTRODUCED` as a final production qualification envelope.

**Why deferred:** qualification must be derived from the actual production promise, deployment topology, providers, supported clients, and workloads that exist then.

**Current preservation constraints:**

- every earlier responsibility remains governed by its own production-honesty and regression evidence;
- architecture must retain recoverability/replaceability rather than depending on undocumented machine state;
- provider abstraction must remain real enough to migrate when the product profile requires it.

**Behavior while absent:** no paying-customer readiness, rack-capacity, replacement-restore, rollback/roll-forward, or provider-migration claim is inferred merely because lower phases pass.

**Trigger:** an explicit first-paying-customer scope and target deployment/support profile are known well enough to qualify.

**Anticipated questions when activated:**

- actual rack/resource envelope and SPOFs;
- replacement-environment restore and key/security recovery;
- immutable release/migration/rollback-or-roll-forward drill;
- provider migration and capacity limits;
- operator ownership, RPO/RTO, supported client/version windows, and production non-claims.

---

## Promotion rule

When an anticipated item becomes real:

```text
ledger item = NOT_INTRODUCED
        ↓ real requirement/workload
promote into active phase/subphase scope
        ↓
rewrite from current facts
        ↓
declare exact production intent
        ↓
derive evidence + regression guard
        ↓
qualify
```

The active document may use, modify, or reject the anticipation here. **Conformance to this ledger is not itself a success criterion.** The real owner/invariant/workload decides.