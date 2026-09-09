# Resource Consumption Accounting and Application Limits

**Version:** v0.0.15 baseline  
**Status:** Accepted architecture direction. Exact meter set, thresholds, commercial plan mapping, and billing behavior remain OPEN where stated.

This document owns SquiFlow's resource-consumption and limit-enforcement contract. It exists because usage may matter for safety, cost, capacity, support, future pricing, or contractual controls even when the data is not needed for analytics.

## 1. Core decision

SquiFlow separates three concerns:

```text
Consumption Accounting
= durable facts about what was consumed

Limit Policy / Enforcement
= whether new consumption is currently allowed

Analytics / Telemetry
= operational observation and product analysis
```

Consumption accounting is **not optional analytics**. Disabling analytics, changing telemetry providers, sampling traces, expiring metrics, or exceeding an observability quota must not erase authoritative usage needed for enforcement, reconciliation, support, provider-cost accounting, or future commercial decisions.

Likewise, OpenTelemetry metrics/logs are not the authoritative source for tenant usage or enforced limits.

## 2. No commercial plan is required to have limits

The application may enforce scoped limits before SquiFlow has any commercial `Free`, `Basic`, `Pro`, or `Enterprise` plans.

Examples:
- protect a provider account from a hard storage/request limit;
- keep one tenant/workload from exhausting shared Worker capacity;
- cap a dangerous or expensive feature for a specific tenant;
- enforce a contractual/manual tenant limit configured by Platform Admin;
- apply a rate/concurrency limit to protect the rack;
- warn before a soft capacity threshold.

A later commercial plan system may **map plan rules onto the same limit policies**, but pricing/plan names are a separate product decision.

The application-level limit mechanism is therefore baseline; exact plan/tier/pricing rules are not.

## 3. What should be metered

Do not create a generic event warehouse that records every click. Create meters only for resources that have a real enforcement, cost, capacity, abuse, recovery, contractual, or future billing reason.

Possible meter families include, only when implemented/needed:
- retained object-storage bytes;
- attachment upload/download bytes where provider/network cost matters;
- provider request attempts or paid-operation units;
- notification/SMS/email/provider delivery attempts;
- document/image/report generation jobs;
- concurrent expensive jobs;
- active device/workstation count if a real limit exists;
- retained records/documents if a real product rule later depends on them;
- API/integration calls where rate or provider budgets require accounting;
- backup/storage consumption where operations require it.

CPU time, memory, bandwidth, and other shared-compute estimates must be labelled as measured/estimated/allocated appropriately. Do not present approximate attribution as exact billing-grade usage.

## 4. Meter definition is explicit

Every authoritative meter must define:

```text
MeterId
Semantic meaning
Unit
Scope
When consumption occurs
Whether retries count
Whether failed attempts count
Whether the value is a delta, current gauge, rate-window event, or concurrent lease
Whether corrections/reversals are possible
Authoritative source / reconciliation source
Retention requirement
Enforcement consistency requirement
```

Examples:

```text
object.retained_bytes
unit = bytes
consume = verified retained object becomes referenced/retained
release = object becomes physically deletable and deletion is verified

provider.sms_attempt
unit = attempt
consume = request is actually sent to provider
failed attempt may still count because provider may charge it

business.document_generation
unit = semantic job
consume = one accepted semantic generation intent
request retry with same idempotency key does not count again
```

This prevents the incorrect assumption that one retry policy works for every kind of consumption.

## 5. Durable consumption record

Conceptually, authoritative consumption can retain fields such as:

```text
ConsumptionId
MeterId
TenantId when tenant-owned
Scope/resource reference when needed
Quantity
Unit
OccurredAt / AuthoritativeAt
SourceOperationId / semantic idempotency key
Attempt/provider reference where the meter counts attempts
Policy/version context where useful
CorrectionOf / adjustment reference where applicable
```

Do not store unrestricted payloads, message bodies, customer files, or unnecessary PII merely because a meter exists.

The exact persistence shape is selected with the first implemented meter. This requirement does not force a separate microservice or database.

## 6. Idempotency and retry semantics

Usage accounting follows the semantics of the meter, not the transport attempt by default.

Examples:

```text
same semantic PDF job retried
→ one semantic-job consumption

provider request sent three times and each attempt costs money
→ three provider-attempt consumption records

server commits order then response is lost
→ retry does not consume another order-count unit if that meter represents authoritative orders
```

The source operation/idempotency identity must prevent double counting where one semantic effect should count once.

When a provider's actual charge/usage is ambiguous, record an explicit reconciliation state rather than guessing.

## 7. Counter/snapshot versus ledger

Fast limit checks should not require scanning all historical usage.

A practical design can use:

```text
append/correct consumption facts
→ maintained current/window counters or snapshots
→ limit decision
```

Counters/snapshots are derived operational state and must be reconcilable/rebuildable from the authoritative source appropriate to the meter.

For resources such as object bytes, periodic reconciliation against authoritative DB/object/provider state detects drift.

For external paid providers, provider-side usage/reference data may be a reconciliation source when available.

## 8. Limit policy model

SquiFlow should support application-owned limit policies conceptually covering:

```text
PolicyId / Version
MeterId or protected resource
Scope
Threshold / capacity
Limit kind
Warning/grace behavior
Time window/reset rule where applicable
Enforcement mode
EffectiveFrom / optional expiry
Reason/source
Audit metadata
```

Possible scopes:
- whole platform/provider account;
- workload class;
- tenant;
- specific integration/provider/destination;
- resource family;
- another bounded scope only when a real requirement exists.

Possible limit kinds:
- hard capacity;
- soft warning threshold;
- period quota/count;
- rate limit;
- concurrency limit;
- retained-storage/gauge limit;
- provider/cost budget;
- admission/backlog limit.

Do not force all these kinds into one implementation before the first real meter/limit needs them.

## 9. Tenant-scoped limits are allowed without subscription tiers

A platform operator may need to set a tenant-specific limit for safety, contract, manual commercial handling, provider protection, abuse containment, or support reasons even when no formal pricing plans exist.

Therefore:
- tenant-scoped limit policies are an accepted application capability;
- exact default values and which resource families get tenant limits remain OPEN until implemented;
- tenant limits do not imply a subscription plan;
- a future plan may populate/update tenant limit policies through a separate product/commercial mapping layer.

Whether Tenant Owners may configure their own **lower self-imposed limits** is a separate product decision. Tenant Owners must never be able to raise themselves above non-overridable platform/provider/security caps.

## 10. Policy precedence and explanation

If several policies apply, the effective limit must be deterministic and explainable.

Conceptually:

```text
provider/platform hard safety cap
+ workload/resource cap
+ tenant-specific cap
+ temporary override/grace where authorized
→ effective decision
```

Do not silently choose whichever policy happened to be loaded last.

Every rejection/throttle/defer decision should be able to identify the effective policy/reason safely enough for support/admin UX.

## 11. Enforcement outcomes

Limit evaluation should use explicit results rather than generic errors, for example:

```text
Allowed
AllowedWithWarning
Deferred
Throttled
RejectedLimit
ProviderCapacityExceeded
LimitDecisionUnavailable
```

The exact public API vocabulary is defined with implementation, but these states must not be collapsed into `500` or misleading validation failures.

Where safe, response metadata can include retry/reset information without exposing sensitive platform capacity.

## 12. Strict versus advisory enforcement

Not every meter needs the same consistency.

### Strict/hard limits
For a limit whose violation would create unacceptable cost, provider failure, security risk, or resource exhaustion, the check must use sufficiently authoritative current state.

Where business mutation and consumption live in one store, prefer atomic check-and-consume/commit when practical.

Where the effect crosses systems, use an explicit reservation/claim/reconciliation pattern appropriate to the resource.

### Advisory/soft limits
Warnings or planning thresholds may tolerate eventual counters if the owning capability explicitly allows it.

Never use an eventually consistent approximate counter as if it were a hard contractual enforcement boundary without documenting the overshoot behavior.

## 13. Concurrent requests and reservations

Two requests can race near a limit.

A hard limit must define whether temporary in-flight capacity is reserved so both requests cannot independently see the same remaining allowance and overshoot it.

This may use a short-lived resource-consumption reservation/lease where needed. This is **not** the business inventory-reservation subsystem that SquiFlow intentionally does not baseline.

Reservations must have:
- ownership/identity;
- expiry/recovery;
- idempotent commit/release;
- crash handling;
- reconciliation of leaked/stale reservations.

## 14. Limit changes while work exists

Changing a limit does not rewrite historical consumption.

If a limit is lowered below current usage:
- do not delete retained customer objects or committed business state to become compliant;
- block/defer new optional consumption as defined by policy;
- allow an authorized recovery/migration/cleanup path;
- preserve already accepted in-flight work according to its reservation/commit semantics.

If a limit is raised, the new policy applies prospectively according to its effective version/time.

Policy changes are versioned/audited and must not silently reinterpret old usage facts.

## 15. Time windows and resets

Period quotas/rate limits must define authoritative window semantics.

Technical rate windows can normally use server/UTC time. A future commercial monthly/business-period quota may require the product's explicit tenant/business timezone policy.

Workstation device clocks never define authoritative reset windows.

Clock skew, DST, policy changes mid-window, and duplicated scheduled resets must not create double allowance or lost usage.

## 16. Workstation/offline behavior

A Workstation cannot guarantee a current central tenant/provider limit while offline unless the limit is explicitly designed for local authority.

Therefore:
- cached usage/limit snapshots may support warning/UX;
- central hard limits are re-evaluated at sync/server execution;
- offline local-capable user intent is preserved if the server later rejects/defer it because a limit changed or was exhausted;
- a limit whose violation is unacceptable and cannot be safely reconciled makes the operation server-required rather than pretending the cached allowance is authoritative.

Do not silently delete pending local work because a limit was exceeded while offline.

## 17. Already committed business truth versus later limited consequences

A later consequence hitting a limit does not roll back an already committed business fact.

Example:

```text
Order committed
→ SMS notification scheduled
→ SMS/provider budget exhausted
→ notification = deferred/rejected-limit
→ Order remains committed
```

If the limited external effect itself is the authoritative business operation, enforce/reserve before performing the irreversible effect and use `OutcomeUnknown` reconciliation for ambiguous responses.

## 18. Provider/account hard limits

Provider limits such as the current Hugging Face account capacity are real platform inputs.

The application should:
- measure/reconcile relevant consumption;
- warn before exhaustion;
- reserve headroom for recovery/critical operations where required;
- reject/defer optional new consumption before a provider hard failure when possible;
- distinguish provider/account exhaustion from tenant-specific policy limits.

The current ~100 GB bootstrap capacity is a provider/account fact. It is not automatically divided equally among tenants.

## 19. Usage is durable even if analytics is disabled

Authoritative usage required for limit decisions, cost/account reconciliation, or future contractual explanation must remain available independently of:
- OpenTelemetry sampling;
- log retention;
- metrics retention;
- analytics opt-out/configuration;
- third-party telemetry outage;
- dashboard deletion.

Analytics may consume a projection/export of usage, but analytics is not the source of truth.

## 20. Privacy and retention

Usage accounting should retain the minimum fields required for its purpose.

Tenant/resource identifiers may be necessary in authoritative application storage, but unrestricted user/customer content is not.

Retention may differ from telemetry retention. Exact retention is OPEN until enforcement, support, contractual, billing, privacy, and legal requirements are known.

Deletion/offboarding policy must deliberately address whether historical usage must remain for support/accounting/legal reasons instead of inheriting a generic telemetry deletion rule.

## 21. Backup and restore

If usage state affects current enforcement or future explainability, it belongs in the recoverable application state.

A restore must not accidentally:
- reset a consumed quota and allow duplicate usage;
- double-count restored consumption;
- forget active reservations;
- apply an old limit policy as current without version/effective-time checks.

Restore/reconciliation tests should cover counters, ledger/facts, policy versions and active reservations relevant to implemented meters.

## 22. Admin and support operability

When the first real limit is implemented, the appropriate admin surface should be able to explain:
- meter/resource;
- current authoritative or reconciled usage;
- effective limit;
- warning/remaining amount where safe;
- policy source/version;
- reset/window when applicable;
- recent adjustments/reconciliation;
- why an operation was allowed, warned, deferred or rejected.

Platform-critical limit changes belong to the Platform Admin control plane. Exact tenant-visible usage/limit UX and any tenant self-management remain product decisions per resource.

Avoid a generic `set any quota` or `edit raw counter` control. Counter correction/reconciliation is a guarded operation with evidence.

## 23. Security and abuse

Consumption and limits themselves are security-sensitive because an attacker could try to:
- forge lower consumption;
- double-consume another tenant's allowance;
- exhaust a victim tenant's limit;
- bypass a limit through retries or alternate endpoints;
- manipulate reset/window time;
- use Admin API to grant unlimited capacity without authorization;
- flood the metering path until enforcement fails.

All consumption writes/limit decisions derive authoritative TenantContext/resource ownership server-side and use the same authentication/authorization/isolation principles as other protected state.

## 24. Failure behavior of the accounting/limit subsystem

A meter/limit failure cannot default universally to `allow` or universally to `deny`.

Each enforced resource class must define its degraded behavior:
- strict paid/provider/hard-cap resource may fail closed/defer when current limit cannot be established;
- soft warning may proceed and reconcile later;
- already committed business state remains committed even if later usage projection/export fails.

The decision must be explicit and observable as `LimitDecisionUnavailable`/equivalent rather than hidden behind unrelated failures.

## 25. Relationship to observability

Operational telemetry should expose safe signals such as:
- limit evaluation latency/failure;
- near-capacity warnings;
- rejected/deferred/throttled operations;
- reconciliation drift;
- stale reservation count/age;
- accounting backlog/failure.

Do not place raw high-cardinality tenant usage into ordinary metrics without an intentional cardinality strategy. Tenant-scoped authoritative usage belongs in application storage; dashboards can query/project it through appropriate controlled paths.

## 26. Future commercial plans

If SquiFlow later introduces plans/subscriptions:

```text
Commercial Plan / Contract
→ produces entitlement + limit-policy configuration
→ existing consumption accounting measures usage
→ existing limit engine enforces effective policy
```

Do not make plan name itself the enforcement primitive scattered through business code.

Changing a customer's plan should change versioned effective policy, not rewrite historical usage.

A billing engine remains separate work. Having consumption accounting does not automatically mean SquiFlow invoices tenants based on it.

## 27. Implementation timing

Do not build a giant generic metering platform in Phase 0.

Implement the smallest shared consumption/limit primitives with the **first real resource that requires authoritative measurement/enforcement**. Candidates likely to force this include object-storage capacity, paid provider operations, expensive Worker jobs, or a manually configured tenant limit.

Before a paying-customer production profile, SquiFlow must at least know:
- which resources have hard platform/provider caps;
- which resources require tenant-scoped limits;
- which usage facts must be durable;
- which checks are strict versus advisory;
- how accounting is reconciled/restored;
- what happens when the limit subsystem is unavailable.

## 28. Acceptance/edge tests

For every implemented hard or customer-visible limit test at least:
- two concurrent requests at the final remaining unit;
- same semantic request retried after response loss;
- provider attempt retry where each attempt genuinely consumes cost;
- accounting write succeeds/business write fails and the reverse, according to transaction design;
- counter/snapshot drift then reconciliation;
- limit lowered below current usage;
- limit changed while work is in flight;
- reset/window boundary and clock skew;
- Workstation offline under an old limit snapshot;
- tenant A cannot consume/change tenant B's usage/limit;
- platform/provider cap and tenant cap apply simultaneously;
- limit store/provider unavailable;
- restore does not reset/double usage;
- stale reservation expires/reconciles;
- telemetry/analytics disabled while enforcement continues correctly.

## 29. Open decisions

The following are intentionally not invented here:
- exact first meter registry;
- exact default tenant limits;
- exact resource families that get tenant-scoped limits;
- exact threshold values and warning percentages;
- exact period/window/reset timezone for future commercial quotas;
- exact retention duration for usage facts;
- exact tenant-visible usage UI;
- whether Tenant Owners can set lower self-limits;
- exact temporary override/grace workflow;
- commercial plan names/prices/allowances;
- SaaS billing/invoicing from usage;
- whether any approximate shared-compute allocation becomes billing-grade.

The **existence of durable consumption accounting and application-level limit enforcement is accepted**; these concrete policies remain OPEN until the relevant product/workload requires them.
