# NFR Decision Challenge — Common, Edge, Failure, and Recovery Scenarios

**Version:** v0.0.15 baseline  
**Status:** Review record. It challenges current accepted decisions and identifies OPEN gaps; it does not invent commercial plans or numeric SLOs.

This review intentionally questions the current architecture instead of assuming that an accepted decision is correct merely because it already exists.

## 1. Challenge method

For each material decision ask:

```text
Common case: does the simplest expected use work?
Edge case: what happens at a boundary/extreme/version change?
Failure case: what happens when the process/network/provider/storage/operator fails?
Recovery case: can the system return to a safe explainable state?
Abuse case: can untrusted input/another tenant/operator misuse the boundary?
Resource case: can the mechanism exhaust CPU/RAM/disk/connections/provider capacity?
Compatibility case: what happens across schema/API/protocol/config version skew?
Small-team case: can Owner + Staff actually recover/use it?
```

A decision survives this review only if its boundary is still useful under those cases or if the unresolved part is explicitly left OPEN.

## 2. Decision challenges

### 2.1 Workstation is local-first
**Common:** shop staff can capture local-capable Customer/Order work without waiting for ordinary network round trips.  
**Edge:** months offline, old schema/rules/permissions, huge pending queue.  
**Failure:** Workstation/Guard/network failure after local commit.  
**Challenge:** local-first could become dangerous if local success were presented as server authority or if recovery simply deletes the local DB.  
**Conclusion:** KEEP. Preserve explicit local/remote states and durable pending intent; server-required facts remain server authority.

### 2.2 Web remains online-only
**Common:** browser operations use current server authority without a second sync engine.  
**Edge:** long valuable form interrupted.  
**Failure:** network/circuit/process loss during edit.  
**Challenge:** online-only could create user-data loss if every form exists only in memory.  
**Conclusion:** KEEP, with explicit server-side drafts only where value/recovery justifies them. No hidden IndexedDB business replica.

### 2.3 Server revalidates Workstation commands
**Common:** pending offline work syncs successfully.  
**Edge:** Owner revoked permission, rule or limit changed while offline.  
**Failure:** authorization/limit dependency unavailable.  
**Challenge:** accepting stale local authority would violate current server/provider truth; rejecting by deleting work would violate user-intent durability.  
**Conclusion:** KEEP current review/rejection approach: preserve local intent but revalidate current authority, rules and hard limits.

### 2.4 No global CRDT/multi-master authority
**Common:** Customer/draft data can still use merge/version behavior where safe.  
**Edge:** payments, inventory, credit, permissions and strict usage limits conflict.  
**Challenge:** a global CRDT would make protected invariants much harder without a demonstrated need.  
**Conclusion:** KEEP aggregate/resource-specific authority rules.

### 2.5 Semantic idempotency is end-to-end
**Common:** user retries after timeout.  
**Edge:** same intent arrives through another transport attempt; response lost after commit.  
**Failure:** consumer crashes after external effect but before acknowledgement.  
**Challenge:** one HTTP or broker dedupe mechanism is not enough, and usage accounting can also double count if it follows transport attempts blindly.  
**Conclusion:** KEEP semantic idempotency + receipts/reconciliation; usage meters define separately when attempts genuinely count.

### 2.6 `OutcomeUnknown` exists for ambiguous effects
**Common:** provider replies success/failure normally.  
**Edge:** provider performs effect but response disappears.  
**Challenge:** mapping every timeout to failure encourages duplicate money/external effects and possibly duplicate provider-cost accounting; mapping it to success invents truth.  
**Conclusion:** KEEP `OutcomeUnknown` and reconciliation.

### 2.7 Posted/issued truth uses correction, not destructive edit
**Common:** Owner fixes a typo before issuance.  
**Edge:** price/tax/config changes after issue.  
**Challenge:** allowing arbitrary edit after financial/legal effect destroys explainability; forbidding all correction makes the product unusable.  
**Conclusion:** KEEP explicit revision/refund/reversal/adjustment according to aggregate.

### 2.8 Owner-authorized manual/final pricing remains possible
**Common:** small shop negotiates a real price.  
**Edge:** Staff attempts unauthorized margin-changing override; later default price changes.  
**Challenge:** removing manual pricing is unrealistic; unrestricted overrides harm audit/security.  
**Conclusion:** KEEP with permission/rule/audit and retained applied historical values. Exact rounding/tax rules remain OPEN.

### 2.9 Pooled multi-tenancy is baseline
**Common:** many small tenants share one app/data topology economically.  
**Edge:** one large tenant, compliance/residency need, connection context leakage.  
**Challenge:** pool reduces operations but increases blast radius if TenantContext is easy to bypass or if one tenant can consume all shared capacity.  
**Conclusion:** KEEP pooled baseline with hard isolation tests plus fair/limited resource policies; dedicated placement remains a future evidence-triggered profile.

### 2.10 No commercial tenant plan/tier is accepted
**Common:** Owner/Staff use the implemented product without hidden `Free/Pro/Enterprise` assumptions.  
**Edge:** a tenant needs a manual contractual/safety limit before formal plans exist.  
**Challenge:** refusing all tenant limits because pricing is undecided would leave the platform unable to enforce real contractual/provider/safety requirements. Conversely, inventing plan names/prices would create product behavior the Owner has not decided.  
**Conclusion:** KEEP **no commercial plan model**, but ACCEPT tenant-scoped application limits independently from plans. Plan names/prices/default allowances remain OPEN.

### 2.11 Shared-resource and tenant-scoped limits exist without pricing tiers
**Common:** ordinary workloads share capacity and an operator can apply an explicit tenant/resource limit where needed.  
**Edge:** one workload floods document generation/sync/provider calls; platform/provider and tenant caps overlap.  
**Challenge:** unbounded shared work is unsafe, while a scattered collection of hardcoded tenant exceptions is unmaintainable and not explainable.  
**Conclusion:** KEEP global/work-class/fairness controls and ACCEPT versioned scoped limit policies, including tenant scope. Exact values, product visibility, and commercial mapping remain OPEN.

### 2.12 ZITADEL owns authentication techniques
**Common:** standards-based login/MFA/SSO.  
**Edge:** provider outage, account recovery, user has multiple tenants.  
**Challenge:** implementing a second credential/MFA system inside SquiFlow duplicates security risk; blindly trusting ZITADEL organization/role claims would collapse tenancy/authorization.  
**Conclusion:** KEEP ZITADEL authentication with SquiFlow membership/TenantContext and OpenFGA authorization kept separate. Deployment topology remains OPEN.

### 2.13 OpenFGA owns relationship/permission decisions, not business state
**Common:** Owner/Staff/custom roles.  
**Edge:** role change write succeeds remotely but local completion write fails; high-consistency check needed after revocation.  
**Challenge:** putting payment/stock/workflow/usage arithmetic into OpenFGA would make authorization a domain database; ignoring cross-system partial failure would report false permission state.  
**Conclusion:** KEEP split and reconciliation. Consumption/limit state remains SquiFlow application/platform state.

### 2.14 Workstation permission snapshot is UX evidence, not capability token
**Common:** offline menus/actions can be shown appropriately.  
**Edge:** permission or central limit revoked/lowered while offline.  
**Conclusion:** KEEP; current server authority wins while pending local intent is preserved.

### 2.15 Guard is a separate process
**Common:** Workstation crash can be observed/restarted.  
**Edge:** Guard itself crashes; update partially replaces binaries; false hang during sleep.  
**Challenge:** Guard adds process complexity but in-process supervision cannot reliably recover/replace the process that failed.  
**Conclusion:** KEEP Guard, but keep IPC narrow and restart/hang policies bounded/measurement-driven.

### 2.16 Platform Admin uses independent Admin API
**Common:** super-admin action uses Admin Web→Admin API.  
**Edge:** Core API is down; Admin API is down; shared DB is down.  
**Challenge:** routing platform control through Core API destroys failure independence; pretending Admin API survives every shared-infrastructure failure overclaims isolation.  
**Conclusion:** KEEP process/security/deployment independence while documenting shared-dependency limits honestly. Platform-critical limit-policy changes belong here when implemented.

### 2.17 Worker is created only with first real durable workload
**Common:** short business commands remain synchronous; real long work is durable async.  
**Edge:** schedule fires twice, stale lease, poison work, process crash after external effect.  
**Challenge:** creating Worker/broker infrastructure too early adds failure modes; waiting too long can trap durable work in HTTP process memory.  
**Conclusion:** KEEP phase-triggered Worker with explicit durable lifecycle. Meter semantic jobs versus provider attempts separately where needed.

### 2.18 Command/job and event remain distinct
**Common:** `GeneratePdf` has an execution owner; `InvoiceIssued` is a committed fact.  
**Edge:** several independent consequences react to one event.  
**Challenge:** hidden event choreography can obscure ownership of money/stock/security transitions and usage charging points.  
**Conclusion:** KEEP semantic distinction; each meter defines which committed fact/attempt actually consumes a unit.

### 2.19 Kafka/event-stream infrastructure is not baseline
**Common:** DB-backed outbox/job system handles current workload.  
**Edge:** future replay/independent offsets/high throughput becomes real.  
**Challenge:** early Kafka adds operating cost without proven requirement; permanently banning streams would block future evidence.  
**Conclusion:** KEEP NotBaseline-now, evidence-trigger later. A consumption ledger does not require Kafka.

### 2.20 `IObjectStore` and `IBackupTarget` are accepted provider seams
**Common:** bootstrap provider works behind narrow SquiFlow contract.  
**Edge:** first paying customer migration; provider-specific failure needs to surface.  
**Challenge:** no abstraction would leak temporary provider APIs into business code; giant generic abstraction would hide useful provider semantics.  
**Conclusion:** KEEP narrow seams because replacement is already planned.

### 2.21 Hugging Face/Kaggle are bootstrap, not timeless architecture
**Common:** cheap/private bootstrap storage and encrypted off-site artifact carrier.  
**Edge:** capacity/privacy/reliability/rate/contract constraints appear before paying customer.  
**Challenge:** treating them as permanent is unsafe; forcing migration before evidence wastes effort.  
**Conclusion:** KEEP temporary-provider status and earlier migration trigger if constraints require it. Retained bytes are a likely first real consumption meter.

### 2.22 Backup validity requires restore
**Common:** encrypted artifact uploads successfully.  
**Edge:** wrong key, missing object, lost idempotency/job/usage state, replacement environment.  
**Challenge:** upload-only backup gives false confidence; restoring consumed usage as zero could permit duplicate/over-limit effects.  
**Conclusion:** KEEP restore qualification including implemented usage counters/ledger/policy/reservations. Numeric RPO/RTO/frequency remain OPEN until production profile.

### 2.23 Workflow/rule/form definitions are versioned
**Common:** Owner publishes a new stage/rule/form.  
**Edge:** active old instances; removed stage; Workstation offline with old snapshot.  
**Challenge:** silently applying latest definitions destroys historical explainability and may invalidate in-progress work.  
**Conclusion:** KEEP immutable/versioned publication and explicit migration.

### 2.24 No arbitrary tenant code in rules/templates
**Common:** bounded configuration covers required business variation.  
**Edge:** malicious/expensive expressions, nondeterministic external I/O, script injection.  
**Challenge:** arbitrary code creates tenant isolation/security/resource problems disproportionate to current need.  
**Conclusion:** KEEP bounded structured model.

### 2.25 Custom domains require verification/lifecycle
**Common:** Owner maps a verified domain.  
**Edge:** DNS expires/reassigns, certificate fails, stale OIDC callback remains.  
**Challenge:** Host header or previously owned domain cannot remain tenant authority forever.  
**Conclusion:** KEEP verification, TLS lifecycle, callback retirement and safe fallback.

### 2.26 Printing is a physical side effect
**Common:** print succeeds.  
**Edge:** spooler accepts then printer jams, duplicate reprint.  
**Challenge:** application cannot always prove physical output.  
**Conclusion:** KEEP business commit independent from print result and expose unknown/retry state honestly. If printing is ever metered, define whether print request, spool acceptance, or another event consumes the unit rather than guessing physical success.

### 2.27 Notifications/external delivery are consequences unless explicitly transactional
**Common:** committed order schedules notification.  
**Edge:** provider accepts message but ACK is lost; destination repeatedly fails; provider charges attempts.  
**Challenge:** rolling back business truth because email failed is wrong; counting one semantic notification while ignoring provider-attempt cost can also be wrong.  
**Conclusion:** KEEP explicit delivery state/idempotency/`OutcomeUnknown`; define semantic-delivery and paid-attempt meters separately where needed.

### 2.28 Observability provider is not business/usage authority
**Common:** logs/metrics/traces aid support.  
**Edge:** telemetry quota/provider is down; analytics disabled; retention expires; sensitive payload leaks.  
**Challenge:** telemetry failure must not break transactions, and sampled/expired metrics cannot be the only source for limit enforcement or historical consumption.  
**Conclusion:** KEEP provider-neutral/bounded telemetry and separate durable authoritative audit/usage state.

### 2.29 Owned rack availability claims remain conservative
**Common:** services restart on current hardware.  
**Edge:** PSU/disk/network/machine failure.  
**Challenge:** `stateless` or containerization does not create another node.  
**Conclusion:** KEEP honest availability/recovery language; RPO/RTO/SPOF inventory and hard resource limits must be measured before production promises.

### 2.30 Formal accessibility program is deferred
**Common:** ordinary Owner/Staff UI should still be understandable.  
**Edge:** later customer/legal requirement needs formal conformance.  
**Challenge:** treating deferral as `accessibility does not matter` would be a product-quality mistake; starting a full compliance program now is outside current scope.  
**Conclusion:** KEEP dedicated program deferred while retaining ordinary human-operability expectations. Reopen on real requirement.

### 2.31 Consumption accounting exists even without analytics
**Common:** a metered resource is consumed and the current usage/remaining allowance is updated.  
**Edge:** analytics is disabled, telemetry is sampled, dashboard data expires, provider logs are unavailable, or the same semantic request is retried.  
**Failure:** accounting write and business/provider effect are split by a crash; counter drifts from provider/object truth.  
**Recovery:** rebuild/reconcile derived counters from authoritative meter facts/provider/resource state; append correction rather than editing history blindly.  
**Abuse:** actor tries to bypass limit through alternate endpoints/retries or exhaust another tenant.  
**Challenge:** treating logs/metrics as authoritative usage makes enforcement dependent on sampling/retention and cannot reliably distinguish semantic effects from charged attempts. A giant data warehouse would be overkill.  
**Conclusion:** ACCEPT a minimal durable consumption-accounting boundary for each real metered resource. Implement only meters with an enforcement/cost/capacity/support reason; do not record every click.

### 2.32 Hard-limit concurrency must be explicit
**Common:** tenant has remaining allowance and one request consumes it.  
**Edge:** two requests arrive at the final unit; limit is lowered while work is in flight; stale reservation remains after crash.  
**Challenge:** `read usage → compare → write later` can overshoot under concurrency. Globally serializing every operation would harm throughput unnecessarily.  
**Conclusion:** strict resources use an appropriate atomic check-and-consume or bounded reservation/lease/reconciliation mechanism scoped to that resource. Advisory limits may tolerate eventual counters only when overshoot is explicitly acceptable.

### 2.33 Limit changes cannot rewrite or delete committed state
**Common:** operator raises a tenant/provider resource limit.  
**Edge:** operator lowers the limit below current retained usage.  
**Challenge:** deleting old customer objects/orders to force current usage under the new limit would violate historical/business integrity; ignoring the new limit forever would make enforcement meaningless.  
**Conclusion:** policy changes are versioned/prospective. Existing committed state stays; new optional consumption is warned/deferred/rejected as policy defines, with explicit cleanup/migration/support paths.

### 2.34 Consistency is selected per invariant
**Common:** current payment/stock/authorization/limit decisions use authoritative state while reports/notifications may lag.  
**Edge:** a stale projection looks newer to a UI or arrives out of order.  
**Challenge:** saying the whole system is eventually consistent can normalize unsafe stale decisions; saying everything is strongly consistent makes async/read projections unnecessarily expensive.  
**Conclusion:** KEEP per-invariant consistency. Derived structures declare source/freshness/rebuild and cannot become authority for protected invariants.

### 2.35 REST/API gateway/protocol boundaries do not become business authority
**Common:** gateway routes HTTPS traffic and applies coarse transport protections; REST/task endpoints express application intent.  
**Edge:** Admin/Core routing, old Workstation protocol, live WebSocket signal loss, gateway outage.  
**Challenge:** a gateway can accidentally become a second authorization/tenant/limit policy source, or hide backend ownership; transient live channels can be mistaken for durable truth.  
**Conclusion:** KEEP backend authority and explicit protocol compatibility. Gateway/routing/transport is infrastructure; Admin API remains independent; durable state recovers after live-signal loss; unsupported versions fail explicitly.

### 2.36 Normalized authoritative data with derived read structures
**Common:** transactional data keeps clear relational identities/invariants; query projections optimize real reads.  
**Edge:** projection drift, rebuild, large cardinality, index/write amplification.  
**Challenge:** denormalizing the authoritative core for convenience makes correction/consistency harder; never allowing projections harms real query performance.  
**Conclusion:** KEEP normalized authority + explicitly derived/rebuildable optimizations, with measured indexes and tenant scope.

## 3. Requirement components that were underrepresented before this review

The architecture had many of these behaviors scattered across owner documents, but they were not centralized as NFR families. The NFR owners now explicitly surface:

- business historical-truth/correction integrity;
- money rounding/precision as a production gate rather than an indefinite TODO;
- business timezone versus event/device time;
- human work discoverability and two-person workflow deadlock;
- external-effect ambiguity/reconciliation;
- data/object integrity and untrusted-content safety;
- custom-domain anti-takeover/callback lifecycle;
- operator safety and break-glass separation;
- update/version-skew/release compatibility;
- data lifecycle/privacy across DB, files, Workstation, usage records, telemetry, diagnostics and backups;
- supportability/explainability as a product quality;
- performance/resource targets as measurement contracts, not guessed numbers;
- provider/vendor degradation behavior;
- durable consumption accounting independent from analytics/telemetry;
- application-level global/workload/tenant/provider limit policies independent from commercial plans;
- hard-limit concurrency/reservation/reconciliation;
- policy-change and offline-limit behavior;
- consistency/freshness per invariant and derived-state rebuildability;
- gateway/protocol/live-channel non-authority;
- normalized authoritative state plus measured derived projections/indexes;
- small-business human operability.

## 4. Still-open gaps that should not be invented

- commercial tenant plan/tier names, prices, default allowances and subscription lifecycle;
- exact first meter registry and tenant/resource limit values;
- exact warning percentages, reset windows, policy precedence and temporary override/grace workflow;
- exact usage retention, tenant-visible usage UI and whether Tenant Owners may set lower self-limits;
- SaaS billing/invoicing from usage;
- jurisdiction-specific tax/invoice/privacy/retention;
- exact money rounding/tax precision;
- exact tenant/business timezone semantics;
- supported long-offline history/compatibility duration;
- production RPO/RTO, backup frequency and retention;
- numeric API/Worker/sync/Workstation/rack SLOs;
- installer/update signing and rollback mechanism;
- exact Windows local data-at-rest protection;
- Blazor session/circuit topology;
- localization/multi-language scope;
- formal accessibility conformance target;
- first client-portal account/scope;
- exact notification channels.

These are recorded as OPEN/deferred instead of being filled from generic SaaS assumptions. The existence of consumption accounting and scoped application limits is no longer an open question.
