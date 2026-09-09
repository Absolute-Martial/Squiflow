# Open Decisions — v0.0.15

These are decisions that can materially affect the current implementation baseline. Deferred ideas are not kept here merely because they may be useful someday.

## Phase-load-bearing technical decisions

- Final central transactional database after the Phase-3 workload/isolation/transaction/performance proof.
- Final Workstation embedded database after the Phase-2 SQLite versus libSQL proof.
- **ZITADEL is selected**; open implementation choices are Cloud versus self-hosted deployment, exact instance/project/application layout, service-account scopes, Web session pattern, and tenant-organization mapping after Phase-1 proof.
- Exact Blazor Web App render-mode/session topology for tenant Web and future Admin Web, including whether Interactive Server is used on each surface and what that implies for circuit memory, reconnect, draining, session affinity, distributed circuit/session persistence, and multi-node behavior. This decision must not silently introduce Redis or claim transparent failover without evidence.
- Exact native Workstation callback mechanism after Windows packaging/security POC: app-claimed HTTPS if reliable, otherwise standards-compliant loopback IP callback.
- Exact session-revocation persistence/rotation strategy around ZITADEL and SquiFlow Web sessions.
- Whether/where ZITADEL Back-Channel Logout is enabled for SquiFlow Web sessions.
- Exact authentication-context (`acr`) mapping for high-risk step-up operations.
- **OpenFGA is selected**; open implementation details are deployment topology, store layout, first authorization model, authorization model ID rollout procedure, and consistency policy by operation class.
- Exact messaging/scheduling mechanism only when Phase 6 implements the first durable Worker path.

Avalonia, Blazor Web App, ZITADEL, and OpenFGA are accepted product/technology decisions and are not provider-selection questions anymore.

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

## Workstation Guard decisions

`SquiFlow.Guard` is accepted. Remaining implementation choices:

- exact Windows IPC mechanism between Guard and Workstation;
- heartbeat/hang-detection intervals and restart budgets after measurement;
- update package/handoff/rollback mechanism;
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
- Backup scope for ZITADEL/OpenFGA depends on Cloud versus self-hosted selection: exported configuration/reprovisioning evidence may be enough for managed services, while self-hosted state requires provider-supported database/config backup and restore.
- **Exact reproducible deployment/IaC mechanism for the paying-customer single-node profile:** e.g. direct host/service definitions, container-compose style packaging, Ansible/Terraform/other automation, or a combination. The requirement is versioned/rebuildable infrastructure; Kubernetes/Flux/Terraform are not preselected.
- **Exact initial server packaging boundary:** bare host processes versus containers for Core API/Admin API/Worker/edge/DB where applicable. Containerization is allowed when it improves repeatability/isolation, but is not a requirement by itself.
- **Measured scaling thresholds and next-move table** for the first-order bottlenecks after actual load tests: DB connection/query/WAL pressure, CPU-heavy document work, Worker backlog, network/object-transfer bandwidth, external dependency latency, and node saturation.
- Edge/gateway failure and SPOF handling, including a private recovery path that does not rely on the same public edge being healthy.
- Certificate renewal/expiry monitoring and operational response.
- Acceptable clock-skew threshold/monitoring for TLS/OIDC/leases/schedules on the initial topology.

## Bootstrap storage decisions already selected

The initial bootstrap providers and abstraction boundaries are not open:

- primary object storage: private Hugging Face Storage Bucket, approximately 100 GB current private-storage envelope;
- runtime provider contract: `IObjectStore`;
- bootstrap implementation: `HuggingFaceObjectStore`;
- off-site backup carrier: private Kaggle Dataset containing encrypted opaque backup archives;
- backup destination contract: infrastructure-level `IBackupTarget`;
- bootstrap implementation: `KaggleBackupTarget`.

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

- Jurisdiction-specific tax/invoice numbering/privacy requirements.
- Whether v0.0.15 needs tenant SaaS self-service billing/metering, or subscriptions/entitlements remain manually managed initially.
- First client-client portal implementation scope/timing and authentication/account model.
- Initial customer-data import/migration scope for onboarding existing businesses.
- Exact dynamic-form definition/version/migration contract before Phase 5 implements a configurable form.
- Initial notification channels actually required by a complete journey.

## Currency decision boundary

Currency itself is not an open architecture subsystem: the baseline avoids hardcoding one currency and retains a currency code where monetary records need historical meaning.

Open only if a real requirement appears:
- multi-currency documents;
- exchange-rate sourcing;
- FX conversion;
- mixed-currency settlement/allocation;
- accounting gain/loss behavior.

## Deferred, not active v0.0.15 work

- formal accessibility/a11y conformance program or dedicated accessibility testing/documentation;
- browser partial-offline/offline business execution;
- Dynamic OpenID Connect Client Registration;
- OpenID Connect Native SSO for Mobile Apps as a Windows login mechanism;
- schema-per-tenant/database-per-tenant/deployment-per-tenant baseline;
- event sourcing as the authoritative persistence model;
- full SaaS metering/billing engine;
- advanced peripheral suite;
- specialized import/ETL platform;
- Kafka/event-log infrastructure, mandatory Redis, global CRDT model, sharding, or active-active multi-region without a measured requirement;
- Kubernetes before an actual cluster-orchestration problem exists;
- gRPC before a concrete streaming/binary/generated-contract requirement justifies another transport;
- container sidecar/proxy/leader/scatter-gather patterns without a concrete deployment or workload problem.
