# SquiFlow Non-Functional and Operational Requirements

**Version:** v0.1.0 baseline
**Status:** Accepted cross-cutting requirements model. Exact numerical targets remain measurement-driven or OPEN where stated.

This document owns the cross-cutting non-functional/operational requirement model for SquiFlow. Focused architecture/domain documents still own detailed semantics. This document does not replace them; it makes the required quality, failure, degradation, recovery, compatibility and operability properties explicit across components.

## 1. What an NFR means in SquiFlow

A useful SquiFlow non-functional requirement is not merely `fast`, `secure`, `reliable`, or `scalable`.

For every capability that materially needs it, the requirement must answer:

```text
normal/common behavior
+ failure/degraded behavior
+ edge/adversarial behavior
+ authoritative source of truth
+ consistency/freshness behavior
+ resource/capacity/limit behavior
+ recovery behavior
+ compatibility/upgrade behavior
+ observable evidence
+ acceptance test
```

A capability is not complete because its happy path works.

## 2. Requirement classes

### HardInvariant
A property that must not be violated merely to stay available or fast.

Examples:
- no cross-tenant data access;
- a retry must not create a second semantic payment effect;
- a permission-provider outage must not become accidental authorization success;
- recovery must not silently delete durable unsynchronized user intent.

### OperationalTarget
A measurable quality target such as latency, backlog age, memory, recovery time, disk usage, or reconciliation drift.

These targets are not invented before the relevant vertical slice/hardware measurement exists. The metric and measurement method can be required before the final number is known.

### DegradedMode
The defined behavior when a dependency/resource/capability is unavailable or impaired.

Examples:
- Workstation local-capable work continues during network loss;
- Web does not show false success during network loss;
- telemetry-provider failure does not roll back business transactions;
- provider delivery outage creates backlog/degraded state rather than corrupting the originating business fact.

## 3. Decision/target status

Every NFR value or policy should be recognizable as one of:

```text
Accepted             semantic requirement is decided
MeasuredProvisional  numeric target exists from current evidence but can be revised
OpenBeforeProduction must be closed before the relevant production promise
Deferred             intentionally not a current baseline capability
NotBaseline          explicitly rejected unless a new requirement reopens it
```

Do not turn an OPEN target into an accidental customer promise.

## 4. Limits exist independently from commercial plans

SquiFlow currently has **no accepted commercial tenant subscription plan, tier, priced allowance, or billing model** such as `Free`, `Basic`, `Pro`, or `Enterprise`.

However, the application-level capability to **measure resource consumption and enforce scoped limits is accepted**.

The distinction is:

```text
Consumption Accounting
= durable usage facts needed for enforcement/cost/reconciliation/support

Limit Policy / Enforcement
= what is currently allowed for a platform, workload, tenant, provider or resource

Commercial Plan / Pricing
= future product rules that may later populate those policies

Analytics / Telemetry
= observation; never the authoritative usage source
```

Therefore SquiFlow may have tenant-scoped limits, provider/account limits, workload limits, concurrency limits, rate limits, storage limits, or manually configured contractual limits **without first having subscription tiers**.

Do not invent plan names, prices, default allowances, or feature packaging from generic SaaS assumptions. Exact first limits and values remain OPEN until their workload/product requirement exists.

Current references to `entitlement` mean the SquiFlow/platform capability/security ceiling or manually controlled product availability where required; they do not imply that a commercial pricing plan already exists.

Detailed owner: `docs/requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md`.

### Technical safety and tenant limits are related but not identical

The platform requires bounded resources so one workload cannot exhaust RAM, disk, DB connections, Worker capacity, provider accounts, network bandwidth, or external APIs.

Safety mechanisms can include:
- global concurrency limits;
- workload-class limits;
- tenant-scoped limits where required;
- bounded queues/batches/uploads;
- backpressure/admission control;
- fair scheduling/aging;
- dynamic noisy-neighbor protection;
- hard external-provider/account capacity handling.

A tenant-scoped limit is an application policy. It becomes a **commercial plan allowance** only if a later product decision maps it to a subscription/contract/price.

Usage needed for enforcement must remain durable even if analytics/telemetry is disabled or unavailable.

## 5. Business integrity requirements

### NFR-BIZ-001 — Historical truth and corrections
**Class:** HardInvariant  
Issued/posted facts with financial, stock, contractual or legal meaning are not silently rewritten. Corrections use revision, refund, reversal, adjustment, corrective document or another aggregate-appropriate action.

Common case: an issued quotation or recorded payment remains explainable after later configuration changes.  
Edge case: an operator edits an old form after a tax/price/workflow setting changes. Historical truth must not silently recompute.

Owners: `docs/domain/BUSINESS_MODEL.md`, `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`.

### NFR-BIZ-002 — Monetary precision/rounding
**Class:** OpenBeforeProduction for the first financial slice requiring the rule  
Currency cannot be hardcoded. Decimal/fixed precision is required, but exact rounding mode, precision, line-vs-document rounding and tax-included/excluded rules must be explicitly closed before affected invoices/payments/quotations are production-qualified.

Do not invent a multi-currency/FX subsystem from this requirement.

### NFR-BIZ-003 — Quantity/unit integrity
**Class:** Accepted semantics; exact units/conversions are feature-driven  
Quantities are not assumed to be integers. The implemented item/business flow defines its quantity/unit semantics. Do not add a generic unit-conversion/MRP/wastage engine without a real requirement.

### NFR-BIZ-004 — Business time versus event time
**Class:** Accepted distinction; business-timezone policy OpenBeforeProduction where needed  
Authoritative event timestamps, tenant/business date/timezone meaning, and device-reported offline time are distinct. Workstation wall clock is not server/distributed ordering authority.

Exact tenant/business timezone behavior must be closed before features such as quotation expiry, scheduled business-day work, invoice-date rules or timezone-sensitive reporting depend on it.

### NFR-BIZ-005 — Pricing explainability
**Class:** HardInvariant for issued/posted pricing  
When a material price becomes historical business truth, SquiFlow retains enough applied value/context to explain the result without relying on current settings.

Possible price sources remain business-defined: default, organization/program, wholesale, negotiated quotation, Owner-authorized final price, outsourced-production resale price. This does not require a generic pricing engine.

### NFR-BIZ-006 — Order/customer intent durability
**Class:** HardInvariant for local-capable actions  
A successful durable Workstation local commit survives normal process/Guard restart according to the selected local-store durability contract. Local success remains distinct from server-authoritative acceptance.

Edge case: resnapshot/protocol recovery cannot silently discard pending local intent.

### NFR-BIZ-007 — Payment/refund/external financial effect safety
**Class:** HardInvariant  
Repeated delivery/retry of the same semantic financial intent must not create an additional effect. Ambiguous provider outcomes use `OutcomeUnknown` plus reconciliation rather than guessed success/failure or blind duplicate execution.

### NFR-BIZ-008 — Inventory concurrency
**Class:** HardInvariant where authoritative stock is implemented  
Concurrent authoritative stock operations must not be resolved by generic last-write-wins. The aggregate/store transaction/concurrency rule decides the valid effect and preserves correction/audit semantics.

### NFR-BIZ-009 — Supplier/purchasing traceability without bureaucracy
**Class:** Accepted  
Informal supplier communication such as phone ordering remains valid. If SquiFlow records receipt/cost/partial payment/payable/customer-job relationship, the resulting state must still be durable and explainable. A formal PO workflow is not required for every purchase.

### NFR-BIZ-010 — Printing/physical-output failure isolation
**Class:** HardInvariant  
Printer/spooler/physical-output failure does not roll back committed business truth. Physical output can be `success`, `failure`, or `unknown`; software must not claim certainty it cannot prove.

### NFR-BIZ-011 — Workflow continuation/version integrity
**Class:** HardInvariant  
Every material non-terminal workflow state needs a continuation owner/discovery/action/recovery story. Active instances remain explainable against the version that created them unless an explicit migration exists. Two-person tenants must not be accidentally stranded by an approval design without a defined recovery path.

### NFR-BIZ-012 — Rule determinism and fact authority
**Class:** HardInvariant  
Tenant rules are bounded structured definitions, deterministic for the same supported input/version, and do not execute arbitrary tenant code. A locally cached rule never converts stale `ServerRequired` facts into authoritative financial/stock/security/limit truth.

## 6. Platform/runtime requirements

### NFR-PLAT-001 — Tenant isolation
**Class:** HardInvariant  
Authentication, application authorization and tenant data isolation remain separate boundaries. A guessed ID, host header, request TenantId, stale local value, or even an incorrectly broad authorization relation must not make cross-tenant data access acceptable.

### NFR-PLAT-002 — Authentication/authorization failure safety
**Class:** HardInvariant + DegradedMode  
Explicit deny, provider timeout/unavailability, model/configuration error and stale-consistency concern are distinguishable. Failure to obtain required current authorization does not become `allow=true`.

### NFR-PLAT-003 — Web online-only honesty
**Class:** DegradedMode  
Web business mutation is online-only in the current baseline. Network loss must show clear unsaved/unknown state and never false success. Valuable forms may use explicit server-side drafts; browser storage is not a hidden business replica.

### NFR-PLAT-004 — Workstation offline survivability
**Class:** HardInvariant + DegradedMode  
Explicitly local-capable work does not require network round trips to become durably local. Network loss, sign-out, Guard restart, Workstation restart, old credentials or provider outage must not by themselves delete durable local business state.

### NFR-PLAT-005 — Sync/reconciliation quality
**Class:** HardInvariant + OperationalTarget  
Sync uses durable local outbox state, bounded batches, current server authority, semantic idempotency, aggregate-specific conflict rules and durable acknowledgement. Reconnect storms use backoff/admission/fairness so recovery does not amplify an outage.

Important measures include oldest pending semantic operation, pending bytes/count, last successful semantic sync, conflict/reconciliation age and `OutcomeUnknown` age—not only connectivity.

### NFR-PLAT-006 — Guard fault containment
**Class:** HardInvariant + DegradedMode  
Guard can recover/observe Workstation lifecycle failure without becoming business authority. Guard failure does not corrupt Workstation business state; Workstation failure does not erase committed local state. Restart/hang recovery is bounded rather than infinite.

### NFR-PLAT-007 — Core API / Admin API fault containment
**Class:** HardInvariant for the implemented control-plane boundary  
Core API and Admin API are independent process/deployment/security surfaces. Core API outage does not automatically remove Admin API application-control capability, and Admin API outage does not block ordinary tenant business API work, subject to each operation's actual shared dependencies.

### NFR-PLAT-008 — Durable Worker semantics
**Class:** HardInvariant + OperationalTarget  
Durable work uses explicit states, bounded concurrency, finite classified retry, lease/fencing where required, no-progress handling, quarantine and graceful drain. A process may run indefinitely; a failed work loop may not spin indefinitely.

Priority must not create accidental permanent starvation unless that starvation is an explicit policy.

### NFR-PLAT-009 — External delivery/integration failure isolation
**Class:** HardInvariant + DegradedMode  
Notifications/webhooks/provider consequences normally occur after authoritative business commit. Provider outage or exhausted provider capacity creates visible delivery degradation/backlog without rewriting the originating business transaction. `OutcomeUnknown` is used where acknowledgement is ambiguous.

### NFR-PLAT-010 — Object/file integrity and content safety
**Class:** HardInvariant  
Retained object identity is application-owned and remains meaningful across provider migration. Issued/versioned bytes are not silently overwritten. Upload/reference asymmetry is reconcilable; size/hash metadata and tenant ownership are verified. Untrusted uploads are bounded and not executed as code.

### NFR-PLAT-011 — Backup/restore recoverability
**Class:** HardInvariant + OperationalTarget  
`backup uploaded` is not success. A valid recovery path must discover/download, verify checksum/encryption, restore required state, reconcile objects, preserve idempotency/job/usage safety, reconnect/reprovision identity/authorization dependencies according to topology, start services and prove tenant/business correctness.

RPO/RTO are OpenBeforeProduction for the paying-customer production profile.

### NFR-PLAT-012 — Custom-domain lifecycle safety
**Class:** HardInvariant + DegradedMode  
A hostname is not tenant authority merely because it appears in a request or Owner form. Ownership verification, unique mapping, TLS lifecycle, safe fallback, callback retirement on removal/reassignment and routing/audit evidence are required.

### NFR-PLAT-013 — Upgrade/version compatibility
**Class:** HardInvariant  
Central schema, Workstation schema, API/sync protocol, Guard↔Workstation IPC, authorization model, durable messages, rule/workflow/form snapshots and provider migration must fail explicitly when incompatible. Old clients receive upgrade/recovery semantics rather than silent partial corruption.

Exact installer signing/update trust mechanism remains an implementation decision and must be qualified before customer rollout.

### NFR-PLAT-014 — Capacity/backpressure/resource safety
**Class:** HardInvariant + OperationalTarget  
RAM, disk, DB connections, request/upload/batch size, Worker concurrency, provider calls, network transfer, telemetry buffers and local temp/diagnostics are bounded.

When pressure grows, low-value/optional work is deferred or rejected before authoritative business state is corrupted or silently deleted.

Scoped application limit policies and consumption accounting are part of this protection where the resource needs durable enforcement. Exact commercial packaging is separate.

### NFR-PLAT-015 — Performance targets are measured, not guessed
**Class:** OperationalTarget  
Before a performance promise becomes a release gate, measure the representative slice on the actual deployment class.

Target families include as applicable:
- interactive Workstation action latency;
- Web/API latency distribution;
- DB pool wait/saturation;
- sync backlog age/reconnect drain time;
- Worker oldest-item age/job duration;
- identity/authorization dependency latency;
- document/image peak resource use;
- object transfer throughput;
- startup/restart/update recovery time;
- backup/restore time;
- Workstation/Guard/server CPU/RAM/disk/network overhead.

Do not manufacture arbitrary p95/p99 values in architecture documents before measurement.

### NFR-PLAT-016 — Observability, auditability and supportability
**Class:** HardInvariant + OperationalTarget  
Operational telemetry must help distinguish symptom/failure class/recovery without becoming the only copy of authoritative business/security audit history. Telemetry provider failure cannot block committed business correctness.

Support evidence must distinguish materially different failures such as authentication failure vs identity-provider outage, explicit authorization deny vs OpenFGA failure, business conflict vs DB failure, server-applied sync with lost ACK vs genuine rejection, and provider-capacity exhaustion vs tenant-policy limit rejection.

Usage/consumption required for limit enforcement is application state, not an observability metric. Analytics or telemetry retention cannot be the only copy.

### NFR-PLAT-017 — Privacy/data lifecycle
**Class:** HardInvariant where policy is known; OpenBeforeProduction where jurisdiction defines retention  
Profile/contact data, financial/business history, security audit, operational telemetry, diagnostic bundles, usage/consumption records, object files, backups and Workstation-local copies may have different lifecycle rules. Do not promise one universal delete/anonymize behavior before legal/product requirements are known.

### NFR-PLAT-018 — Operator safety and break-glass separation
**Class:** HardInvariant  
Normal platform controls use Admin Web→Admin API with exact state/diff, authorization, verification and audit appropriate to risk. Avoid generic `run SQL`, `force success`, `mark payment complete`, `set raw usage counter`, or arbitrary-state controls.

If the application control plane is unavailable, private infrastructure recovery is separate from tenant/business APIs and follows least-privilege runbooks/evidence.

### NFR-PLAT-019 — Maintainability/dependency boundaries
**Class:** HardInvariant architecture rule  
Domain/application code does not depend on provider SDKs merely for convenience. Interfaces/processes are added where they protect a real replacement/fault/security/recovery boundary, not one-interface-per-class ceremony.

Provider replacement already planned for object/backup storage justifies `IObjectStore`/`IBackupTarget`; generic repository/unit-of-work and service-per-module patterns remain non-baseline. Ordinary modules remain in-process unless a real process boundary is justified.

### NFR-PLAT-020 — Human operability
**Class:** Accepted  
Ordinary Owner/Staff operation should expose business/recovery concepts rather than infrastructure jargon. Examples: `Saved locally`, `Waiting to sync`, `Needs review`, `Permission changed`, `Payment status unknown—checking`, `Printer failed—order remains saved`, `Storage pressure`, `Usage limit reached`.

A small business should not need to understand OpenFGA, OTLP, job leases, DB locks, gateway routing, or protocol negotiation to use normal flows.

Formal accessibility/a11y conformance remains deferred as a dedicated program in v0.1.0; that deferral must not be reinterpreted as a requirement to create inaccessible UX.

### NFR-PLAT-021 — Durable consumption accounting and limit enforcement
**Class:** HardInvariant + DegradedMode + OperationalTarget where a resource is metered/enforced  
For every implemented resource whose usage matters for limit enforcement, provider/account capacity, cost, abuse control, contractual explanation, or future billing, SquiFlow records authoritative/reconcilable consumption independently of analytics/telemetry.

Each meter defines what consumes a unit, whether retries/failed attempts count, scope, unit, correction/reconciliation behavior and enforcement consistency.

Limits may be platform-wide, workload/resource scoped, tenant-scoped, provider-scoped, integration-scoped, or another explicit bounded scope. A tenant-specific limit does not require a commercial subscription tier.

Hard limits define concurrency/atomicity behavior so two requests cannot both spend the same final allowance. Limit-policy changes are versioned; lowering a limit never silently deletes already committed business state. Workstation offline snapshots can warn but do not override current central hard limits.

If the accounting/limit decision path is unavailable, each resource class defines fail-closed/defer versus advisory/reconcile behavior explicitly rather than defaulting universally to allow or deny.

Owner: `docs/requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md`.

### NFR-PLAT-022 — Consistency/freshness is chosen per invariant
**Class:** HardInvariant architecture rule  
Current/strong authority is required where stale data can create unsafe business effects. Eventual/derived consistency is permitted only where the owning capability defines source, freshness/version evidence, duplicate/out-of-order handling and rebuild/reconciliation behavior.

Caches, reports, telemetry and projections must never silently become current authority for tenant isolation, sensitive authorization, payment/refund, stock, credit, hard limits, or another protected invariant.

### NFR-PLAT-023 — Network edge and protocol boundaries remain non-authoritative
**Class:** HardInvariant + DegradedMode  
Reverse proxy/API-gateway, TLS/HTTP transport negotiation, WebSocket/SignalR, DNS/hostname routing and other network mechanisms do not replace backend authentication, OpenFGA authorization, TenantContext isolation, domain validation, idempotency, consistency or limit enforcement.

Gateway outage/routing failure is distinguishable from backend business rejection. Live-signal loss must recover from durable state rather than becoming lost business truth. Unsupported client/API/sync protocol versions fail explicitly rather than being silently interpreted as current semantics.

## 7. Component coverage matrix

| Component/capability | Required NFR families |
| --- | --- |
| Customer/Order | durability, historical truth, concurrency, offline state, usability |
| Quote/Pricing | revision integrity, price explainability, money/time precision, workflow |
| Payments/Credit | idempotency, `OutcomeUnknown`, reconciliation, authority freshness, correction |
| Inventory | transactional concurrency, correction, server fact authority |
| Purchasing/Suppliers | durable cost/payable state without mandatory enterprise workflow |
| Documents/Printing | immutable references, file integrity, physical-effect uncertainty |
| Workstation | offline durability, local capacity, Guard recovery, update/protocol compatibility, limit snapshot semantics |
| Sync | current authority, idempotency, conflict, long-offline recovery, backpressure, central limit revalidation |
| Web | online-only honesty, server draft recovery, session/circuit non-authority |
| Identity | protocol security, account stability, session/recovery, dependency degradation |
| Authorization | tenant/platform separation, freshness, reconciliation, fail closed |
| Rules/Workflow/Forms | bounded execution, versioning, continuation, fact authority |
| Core API | REST/task contracts, admission, security, tenant isolation, idempotency, dependency budgets, limit decisions |
| Admin API | platform authority, Core API independence, operator safety, limit-policy audit |
| Worker | durable lifecycle, lease/retry/quarantine, fairness, external effects, metered attempts/jobs where needed |
| Object storage | integrity, tenant ownership, provider replacement, finite capacity, retained-byte accounting |
| Backup | encrypted recovery set, key recovery, restore qualification, usage/limit state recovery where applicable |
| Custom domains/edge | ownership/TLS/callback lifecycle, routing non-authority, anti-takeover, fallback |
| Integrations | idempotency, SSRF/input bounds, `OutcomeUnknown`, provider degradation, paid/provider attempt accounting where needed |
| Consumption/Limits | meter semantics, durable usage, policy versioning, concurrency, reconciliation, explanation |
| Observability | correlation, privacy, bounded telemetry, authoritative-audit/usage separation |
| Physical rack | honest availability, capacity, restart/recovery, SPOF inventory |

## 8. Requirement areas that remain genuinely OPEN or deferred

The following must not be silently invented by implementation:
- commercial tenant plan names/tiers/prices/allowance mapping;
- exact initial meter registry and default tenant/resource limit values;
- exact tenant-visible usage/limit UX and whether Tenant Owners may set lower self-limits;
- usage-accounting retention duration and exact correction/override workflow;
- SaaS self-service billing/invoicing model;
- jurisdiction-specific tax/invoice numbering/privacy/retention;
- exact money rounding/tax precision rules before the first affected production slice;
- exact tenant/business timezone policy before timezone-dependent business behavior;
- final supported long-offline/incremental-history window;
- production RPO/RTO and backup frequency/retention;
- actual hardware capacity/SLO thresholds;
- exact Workstation update signing/handoff/rollback mechanism;
- exact Workstation data-at-rest/credential protection;
- final Blazor session/circuit topology;
- first client portal account/scope;
- exact notification channels;
- formal accessibility/a11y conformance program;
- localization/multi-language scope unless a real customer requirement introduces it.

## 9. Capability completeness gate

For each implemented capability, reviewers must be able to answer the following only where materially applicable:

1. What is the authoritative state/source of truth?
2. What is the normal/common path?
3. What happens on duplicate/retry/response loss?
4. What happens when the network/dependency is unavailable?
5. What happens on process crash/restart?
6. What happens under concurrency/race?
7. What happens when local/server/derived state disagrees?
8. What happens when permissions/config/rules/limit policies change?
9. What happens across API/schema/protocol/version skew?
10. What happens under low disk/RAM/DB/provider capacity?
11. If the capability consumes a metered resource, what exactly is counted, when, in what unit, and how is it made idempotent/reconcilable?
12. What consistency/freshness model applies, and can a derived/cache value accidentally become authority?
13. What data may be logged/exported/backed up and what must be protected?
14. How does an Owner/Staff/operator understand the state and recover safely?
15. Which measurements are required and which numeric targets/limits are still OPEN?
16. Which hostile/edge test proves the requirement?

Do not force every question onto a trivial pure calculation, but do not waive a material failure mode merely to make a feature appear complete.
