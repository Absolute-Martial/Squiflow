# Open Decisions — v0.1.0

These are decisions that can materially affect the current implementation baseline. Deferred ideas are not kept here merely because they may be useful someday.

## Phase-load-bearing technical decisions

- Exact Phase-0 application-kernel types/project packaging, module-discovery mechanism, first module descriptor, feature/settings/profile snapshot persistence, and generated-contract tool remain implementation details to prove. The SquiFlow-owned kernel, startup-loaded trusted modules, no automatic controller exposure and tenant-keyed Autofac runtime direction are selected. Open activation details are the first real implementation-variant capability, exact durable profile publication/administration path, measured cache/capacity values and the evidence threshold that escalates a workload from tenant-scoped Autofac composition to a separate process.

- Exact PostgreSQL version, deployment topology, measured Npgsql pool sizing, RLS policy implementation, migration tooling, WAL/checkpoint/autovacuum configuration, backup/restore procedure, and measured rack envelope after the Phase-3 qualification proof. PostgreSQL and a shared direct `NpgsqlDataSource` are selected for the current CoreApi runtime. External pooling remains open until aggregate-connection evidence triggers a PgBouncer-versus-PgDoorman compatibility/capacity proof; read routing, sharding and proxy-owned HA remain separate unselected topology decisions.
- Exact SQLite .NET driver, connection/transaction pattern, WAL/checkpoint configuration, migration tooling, encryption-at-rest mechanism, backup/integrity/recovery procedure, and Windows packaging details after the Phase-2 qualification proof. SQLite with WAL itself is selected.
- **ZITADEL Cloud is selected initially.** Open Phase-1 implementation choices are exact instance/project/application layout, service-account scopes, Web session pattern, tenant-organization mapping, region/contract constraints where applicable, and an export/reprovision/account-linking recovery plan. Self-hosting is not an open default; reconsider it only when scale/cost, residency/compliance, availability/control, or provider-dependency evidence justifies the operational burden.
- Exact Blazor Web App render-mode/session topology for tenant Web and future Admin Web, including whether Interactive Server is used on each surface and what that implies for circuit memory, reconnect, draining, session affinity, distributed circuit/session persistence, and multi-node behavior. This decision must not silently introduce Redis or claim transparent failover without evidence.
- Exact native Workstation callback mechanism after Windows packaging/security POC: app-claimed HTTPS if reliable, otherwise standards-compliant loopback IP callback.
- Exact session-revocation persistence/rotation strategy around ZITADEL and SquiFlow Web sessions.
- Whether/where ZITADEL Back-Channel Logout is enabled for SquiFlow Web sessions.
- Exact authentication-context (`acr`) mapping for high-risk step-up operations.
- **OpenFGA is selected and the first narrow model is implemented** for `tenant#can_view_workspace`: current SquiFlow membership is contextual input and persisted `workspace_viewer` is the independent permission relation. Open details are deployment topology, broader store layout, managed model-ID rollout automation, role/custom-role/resource models, tuple administration/reconciliation, authorization revision and consistency policy for later operation classes.
- **Proto.Actor is selected as the initial in-process `SquiFlow.Worker` execution/supervision runtime, and Quartz.NET 4.x with PostgreSQL persistent scheduling state is selected as the initial durable scheduler.** Open Phase-6 implementation details are the first real workload/actor topology, bounded mailbox/admission policy, any workload-specific dispatcher isolation, exact Quartz package patch version and schema/table-prefix/connection settings, the concrete SquiFlow schedule-definition representation, misfire/overlap policy mappings, trigger publication/reconciliation, graceful drain timings, and whether later multi-node evidence justifies Quartz clustering. Proto.Cluster/Remote/Persistence, Hangfire/TickerQ processing runtimes, MassTransit/RabbitMQ/Kafka, and a generic Worker Channel queue are not baseline. Detailed owner: `docs/server/WORKER_RUNTIME_AND_SCHEDULING.md`.
- Exact API/schema compatibility mechanism and supported overlap/retirement windows for the first old-Workstation/new-server and old/new-backend coexistence slice; the requirement for compatible evolution is accepted.
- **Final first Workstation synchronization transport is OPEN for Phase 3.** Ordinary HTTP is the simpler baseline; gRPC is the preferred candidate to compare when representative sync work demonstrates streaming, binary-efficiency, generated-contract, or sustained high-frequency RPC value. The sync correctness model must remain transport-independent.
- **Exact synchronous transport for any future independently deployed service boundary remains OPEN until that boundary exists.** gRPC is the preferred candidate to evaluate first when a real synchronous RPC need exists, but durable async work or ordinary HTTP may still be the correct answer. Admin API and Core API do not gain a normal runtime dependency merely because both processes exist.

Avalonia, Blazor Web App, ZITADEL, and OpenFGA are accepted product/technology decisions and are not provider-selection questions anymore.

## Non-functional target decisions

The semantic NFR model is accepted in `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md`; these numerical/operational values remain open until the relevant phase can measure or justify them:

- representative interactive Workstation/Web/API latency targets;
- DB pool wait/saturation thresholds;
- sync backlog-age/reconnect-drain targets and final supported incremental-history/long-offline window;
- Worker oldest-item/job-duration/no-progress thresholds when Worker exists;
- Workstation/Guard/server CPU/RAM/disk/network budgets from actual supported hardware;
- local diagnostics/update/temp disk-reserve thresholds;
- provider/network transfer concurrency and admission thresholds;
- final paying-customer RPO/RTO, backup frequency and retention;
- exact operational alert/escalation thresholds and support ownership.

These are measurement/operations decisions, not a license to invent commercial plan prices/allowances.

## Consumption accounting and limit-policy decisions

The **existence of durable/reconcilable consumption accounting and application-level scoped limit enforcement is accepted**. Detailed owner: `docs/requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md`.

The following concrete choices remain OPEN until the first real resource needs them:

- exact first meter registry and stable `MeterId` vocabulary;
- exact units and consumption trigger for each meter;
- whether a meter counts semantic effects, provider attempts, retained/current gauge, rate-window events, concurrent leases, or another explicit model;
- which failed/retried attempts genuinely consume units/cost;
- exact first tenant-scoped limits and default values;
- which resources have only platform/provider hard caps versus additional tenant limits;
- exact warning/soft-limit percentages and hard-limit thresholds;
- exact policy precedence where platform/provider/workload/tenant limits overlap;
- exact strict check-and-consume versus reservation/lease mechanism for concurrent hard enforcement;
- exact temporary override/grace workflow and who can authorize it;
- whether Tenant Owners may configure lower self-imposed limits for particular resources;
- exact tenant-visible usage/remaining-limit UX and support/admin explanation surface;
- exact usage-record/correction/adjustment retention;
- exact reconciliation frequency/source for storage/provider/cost counters;
- exact degraded behavior when the consumption/limit subsystem is unavailable per resource class;
- exact reset/window semantics for future period quotas, including business timezone only if a real product rule needs it;
- exact treatment of approximate/allocated shared CPU/RAM/network cost, and whether any of it ever becomes billing-grade;
- exact first Admin API control flow for platform/tenant limit changes when implemented.

Do not postpone the existence of metering/limit capability merely because commercial subscription plans are not yet defined.

## Authorization/product-control decisions

- Final built-in Staff permission defaults and role-template defaults.
- Exact first OpenFGA model for Owner/Staff, tenant-defined custom roles, branch/program scope, and the first resource-level relations.
- Exact mapping between SquiFlow Tenant membership and ZITADEL Organizations. Do not automatically make `ZITADEL OrganizationId == SquiFlow TenantId` until the multi-tenant identity POC proves that mapping fits cross-tenant users and future enterprise identity-provider needs.
- Exact durable/reconciliation protocol when SquiFlow role/grant metadata/audit and OpenFGA tuple writes span two systems; revocation/change success must not be reported before OpenFGA state is known.
- Which authorization checks require OpenFGA `HIGHER_CONSISTENCY` versus the lower-latency mode. Sensitive mutations/revocation-adjacent checks must fail safely; do not blindly force the highest consistency on every read without measurement.
- Whether explicit `Deny` semantics are ever necessary; baseline remains allow-oriented unless the OpenFGA model proves a real deny requirement.
- Exact persistence representation/type of `TenantAuthorizationRevision`; semantics are accepted.
- Exact resource families that support `Own/Assigned` scope and what ownership/assignment means for each.
- Which relationships belong in OpenFGA versus ordinary SquiFlow DB/domain facts. Domain state/workflow/financial invariants remain outside OpenFGA.

## Application-security implementation decisions

The layered requirements in `docs/security/APPLICATION_SECURITY_BASELINE.md` are accepted. Phase-specific implementation details remain open until the relevant surface exists:

- exact ASP.NET/Blazor CSRF/antiforgery mechanism after the Web session/render topology is selected;
- exact Content Security Policy and related response-header policy for tenant Web, custom domains, and future Platform Admin Web;
- whether any first-slice field permits sanitized rich content rather than plain encoded text;
- exact file scanning/quarantine/conversion mechanism for the first supported artwork/document types;
- exact outbound URL allow-list/DNS/redirect enforcement implementation for the first webhook/fetch capability;
- exact secret/configuration store and rotation procedure for the initial deployment profile;
- exact CI secret/dependency/image scanning tools and triage policy;
- exact conditional container hardening controls if the initial server packaging chooses containers.

## Workstation Guard decisions

`SquiFlow.Guard` is accepted. Remaining implementation choices:

- exact Windows IPC mechanism between Guard and Workstation;
- heartbeat/hang-detection intervals and restart budgets after measurement;
- update package signing/authenticity, handoff, staged rollout and rollback mechanism;
- crash-dump/evidence collection mechanism and retention/privacy policy;
- exact safe-mode UX and operator/support path;
- whether any future native/document/driver helper is isolated under Guard supervision.

Do not reopen the existence of Guard merely to reduce process count unless evidence shows the supervision/recovery requirement can be satisfied more reliably another way.

## Current hardware/operations decisions before production

- Authoritative inventory of actual rack nodes: machine count, CPU/RAM, disks/filesystem, network/uplink, OS, roles, and single points of failure.
- Whether UPS/power-loss protection is available/required for the initial production topology based on accepted recovery risk.
- Provisional/final RPO and RTO for the initial deployment.
- Private infrastructure break-glass mechanism and operator access policy.
- Who operates/responds to alerts and physical failures, and what maintenance/support promise is realistic.
- Actual rack uplink bandwidth and transfer-concurrency limits.
- Exact edge/reverse-proxy deployment and whether it is a single point of failure for tenant/admin access. The recovery plan must distinguish `edge unavailable` from `Core API/Admin API unavailable` and must not make the same public edge the only infrastructure-recovery path.
- DNS/TLS certificate renewal/expiry monitoring and recovery procedure for the initial deployment.
- Acceptable system clock-skew tolerance and alert/recovery policy for OIDC/TLS/leases/schedules/limit windows; clock time must not become a substitute for versions/fencing/idempotency.
- Backup scope for the initial managed ZITADEL Cloud deployment requires tested configuration inventory/export/reprovisioning and account-linking recovery evidence; if self-hosting is later selected, provider-supported database/config backup and restore becomes mandatory. OpenFGA backup scope follows its separately selected deployment topology.
- **Exact reproducible deployment/IaC mechanism for the paying-customer single-node profile:** e.g. direct host/service definitions, container-compose style packaging, Ansible/Terraform/other automation, or a combination. The requirement is versioned/rebuildable infrastructure; Kubernetes/Flux/Terraform are not preselected.
- **Exact initial server packaging boundary:** bare host processes versus containers for Core API/Admin API/Worker/edge/DB where applicable. Containerization is allowed when it improves repeatability/isolation, but is not a requirement by itself.
- **Exact first-production release strategy:** maintenance-window/in-place, spare-node/blue-green, canary, or another measured approach. The accepted requirements are immutable-artifact promotion, compatible/preflighted migration, health/smoke verification, explicit rollback/roll-forward, and no unsupported zero-downtime claim.
- Exact artifact provenance/checksum/signing mechanism and retention for the chosen build/deployment path.
- Exact database schema contraction/retirement evidence: how supported clients, old application instances, pending sync, queued work, and stored snapshots are inventoried/drained before incompatible removal.
- **Measured scaling thresholds and next-move table** for the first-order bottlenecks after actual load tests: DB connection/query/WAL pressure, CPU-heavy document work, Worker backlog, network/object-transfer bandwidth, external dependency latency, and node saturation.

## API/edge questions to close with implemented surfaces

- Cacheability classification for future API surfaces remains open. Current CoreApi protected operations are `no-store` across success and failure responses; public bootstrap has a separate bounded cache/ETag policy. Current-authority payment/stock/credit/authorization/hard-limit responses cannot become stale cache authority.
- Exact supported HTTP/proxy protocol configuration only after deployment measurements; HTTP/1.1, HTTP/2, or HTTP/3 transport negotiation must not change business semantics.
- Whether WebSocket/SignalR is needed for any implemented live-update UX. If used, it remains a signal/reconnect mechanism and not durable business or usage truth.
- Whether the simple edge/reverse proxy remains sufficient or a fuller API-management product is justified by real external-developer/version/transformation/policy requirements.
- For any boundary where gRPC is selected, exact HTTP/2/proxy/edge support, `.proto` compatibility rules, deadline/cancellation behavior, authentication/authorization, observability, and benchmark evidence versus the simpler HTTP path. Owner: `docs/api/TRANSPORT_SELECTION.md`.

## Bootstrap storage decisions already selected

The initial bootstrap providers and abstraction boundaries are not open:

- primary object storage: private Hugging Face Storage Bucket, approximately 100 GB current private-storage envelope;
- runtime provider contract: `IObjectStore`;
- bootstrap implementation: `HuggingFaceObjectStore`;
- off-site backup carrier: private Kaggle Dataset containing encrypted opaque backup archives;
- backup destination contract: infrastructure-level `IBackupTarget`;
- bootstrap implementation: `KaggleBackupTarget`.

The ~100 GB figure is a provider/account capacity fact, not a predetermined per-tenant allowance. Retained object bytes are a likely first consumption meter, but exact tenant storage limits remain OPEN.

Open follow-on decisions:

- exact post-paying-customer primary object-storage provider;
- exact post-paying-customer backup provider/topology;
- backup frequency/retention once real customer RPO/RTO exists;
- exact Hugging Face/Kaggle capacity warning/admission thresholds after measured data growth;
- encryption/key-recovery operational procedure for Kaggle backup archives;
- exact migration/cutover procedure from Hugging Face/Kaggle adapters to the paid production adapters;
- whether migration needs a temporary dual-read/dual-write mode or a simpler maintenance/copy/cutover procedure.

The planned provider-migration trigger is the **first paying customer**, but migration can happen earlier if service limits, privacy/compliance, reliability, capacity, or restore requirements make the bootstrap arrangement unsuitable.

## Device/desktop decisions

- Initial supported Windows/printer versions and first printer qualification matrix.
- Whether any scanner/barcode/cash-drawer/other peripheral becomes a real product requirement.
- Whether multiple SquiFlow users sharing one Windows OS profile is supported or prohibited.
- Exact Workstation local-data-at-rest and credential-storage mechanism for supported Windows versions.

## Domain/commercial decisions

- The identity and lifecycle of an individual billing account; who may assign a program's charges to it; when that assignment becomes effective; and how corrections, credit, consolidated statements and payment allocation work across organization, program and individual debtors. The default/allowed debtor choices are accepted in `docs/domain/BUSINESS_MODEL.md`, but no debtor or ledger contract is implemented yet.
- Jurisdiction-specific tax/invoice numbering/privacy/retention requirements.
- Exact money rounding/precision and tax-included/excluded rules before the first affected financial slice is production-qualified.
- Exact tenant/business timezone/effective-date semantics before quotation expiry, scheduled business-day work, invoice dates or timezone-sensitive reporting depend on them.
- **Commercial plan/tier names, prices, default allowances, feature packaging and subscription lifecycle are OPEN; none are accepted now.** Application-level limits may still exist independently.
- Whether v0.1.0 needs SaaS self-service billing/invoicing at all; manual commercial/account handling remains valid initially.
- If a later commercial plan model is introduced, it maps product/commercial rules onto versioned entitlement/limit policy; plan names must not become scattered enforcement conditions in business code.
- Whether authoritative consumption records later feed billing is a separate commercial/accounting decision; durable metering itself does not imply automatic tenant invoicing.
- First client-client portal implementation scope/timing and authentication/account model.
- Initial customer-data import/migration scope for onboarding existing businesses.
- Exact dynamic-form definition/version/migration contract before Phase 5 implements a configurable form.
- Initial notification channels actually required by a complete journey.
- Localization/multi-language scope if a real customer requirement introduces it.

## Currency decision boundary

Currency itself is not an open architecture subsystem: the baseline avoids hardcoding one currency and retains a currency code where monetary records need historical meaning.

Open only if a real requirement appears:
- multi-currency documents;
- exchange-rate sourcing;
- FX conversion;
- mixed-currency settlement/allocation;
- accounting gain/loss behavior.

## Deferred, not active v0.1.0 work

- formal accessibility/a11y conformance program or dedicated accessibility testing/documentation;
- browser partial-offline/offline business execution;
- Dynamic OpenID Connect Client Registration;
- OpenID Connect Native SSO for Mobile Apps as a Windows login mechanism;
- schema-per-tenant/database-per-tenant/deployment-per-tenant baseline;
- event sourcing as the authoritative persistence model;
- full SaaS billing/invoicing platform or commercial-plan engine;
- a generic analytics/data-warehouse metering platform that records every interaction without an enforcement/cost/capacity/support reason;
- advanced peripheral suite;
- specialized import/ETL platform;
- Kafka/event-log infrastructure, mandatory Redis, global CRDT model, sharding, or active-active multi-region without a measured requirement;
- Kubernetes before an actual cluster-orchestration problem exists;
- gRPC as a universal/default protocol, between ordinary modules, or as prebuilt infrastructure before the Phase-3/real-boundary transport POC; the candidate itself is active, but final adoption remains workload-driven;
- container sidecar/proxy/leader/scatter-gather patterns without a concrete deployment or workload problem.
