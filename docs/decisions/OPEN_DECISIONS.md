# Open Decisions — v0.0.15

These are decisions that can materially affect the current implementation baseline. Deferred ideas are not kept here merely because they may be useful someday.

## Phase-load-bearing technical decisions

- Final central transactional database after the Phase-3 workload/isolation/transaction proof.
- Final Workstation embedded database after the Phase-2 SQLite versus libSQL proof.
- Exact OpenID Connect provider/implementation and any future federation strategy.
- Exact native Workstation callback mechanism after Windows packaging/security POC: app-claimed HTTPS if reliable, otherwise standards-compliant loopback IP callback.
- Final browser cookie/server-session implementation per Web host.
- Exact session-revocation persistence/rotation strategy after the identity/session implementation is selected.
- Whether the chosen identity provider supports/should enable Back-Channel Logout for SquiFlow Web sessions.
- Exact authentication-context (`acr`) vocabulary/provider mapping for high-risk step-up operations.
- Exact messaging/scheduling mechanism only when Phase 6 implements the first durable Worker path.

Avalonia and Blazor Web App are accepted presentation decisions and are not open.

## Authorization/product-control decisions

- Final built-in Staff permission defaults and role-template defaults.
- Whether explicit `Deny` semantics are ever necessary; baseline remains allow-oriented.
- Exact persistence representation/type of `TenantAuthorizationRevision`; semantics are accepted.
- Exact resource families that support `Own/Assigned` scope and what ownership/assignment means for each.
- Whether a future real sharing requirement justifies per-resource relationship authorization beyond tenant/role/scope. No Zanzibar-style service is planned without evidence.

## Current hardware/operations decisions before production

- Authoritative inventory of actual rack nodes: machine count, CPU/RAM, disks/filesystem, network/uplink, OS, roles, and single points of failure.
- Whether UPS/power-loss protection is available/required for the initial production topology based on the accepted recovery risk.
- Provisional/final RPO and RTO for the initial deployment.
- Private infrastructure break-glass mechanism and operator access policy.
- Who operates/responds to alerts and physical failures, and what maintenance/support promise is realistic.
- Actual rack uplink bandwidth and transfer-concurrency limits.

## Bootstrap storage decisions already selected

The initial bootstrap providers are **not open**:

- primary object storage: private Hugging Face Storage Bucket, approximately 100 GB current private-storage envelope;
- off-site backup carrier: private Kaggle Dataset containing encrypted opaque backup archives.

Open follow-on decisions:

- exact post-paying-customer primary object-storage provider;
- exact post-paying-customer backup provider/topology;
- backup frequency/retention once real customer RPO/RTO exists;
- exact Hugging Face/Kaggle capacity warning/admission thresholds after measured data growth;
- encryption/key-recovery operational procedure for Kaggle backup archives;
- exact migration/cutover procedure from Hugging Face/Kaggle bootstrap storage to the paid production providers.

The planned provider-migration trigger is the **first paying customer**, but migration can happen earlier if service limits, privacy/compliance, reliability, capacity, or restore requirements make the bootstrap arrangement unsuitable.

## Device/desktop decisions

- Initial supported Windows/printer versions and first printer qualification matrix.
- Whether any scanner/barcode/cash-drawer/other peripheral becomes a real product requirement; none is baseline without a customer journey.
- Whether multiple SquiFlow users sharing one Windows OS profile is supported or prohibited.
- Exact Workstation local-data-at-rest and credential-storage mechanism for supported Windows versions.
- Whether a separate Workstation helper/supervisor process is ever required. Baseline is one Workstation process; process isolation is added only after a concrete updater/native-library/crash-isolation case proves it.

## Domain/commercial decisions

- Jurisdiction-specific tax/invoice numbering/privacy requirements.
- Whether v0.0.15 needs tenant SaaS self-service billing/metering, or subscriptions/entitlements remain manually managed initially.
- First client-client portal implementation scope/timing and authentication/account model.
- Initial customer-data import/migration scope for onboarding existing businesses.
- Exact dynamic-form definition/version/migration contract before Phase 5 implements a configurable form.
- Initial notification channels actually required by a complete journey; do not build channels without one.

## Currency decision boundary

Currency itself is not an open architecture subsystem: the baseline simply avoids hardcoding one currency and retains a currency code where monetary records need historical meaning.

Open only if a real requirement appears:
- multi-currency documents;
- exchange-rate sourcing;
- FX conversion;
- mixed-currency settlement/allocation;
- accounting gain/loss behavior.

Do not design those features before a customer requires them.

## Deferred, not active v0.0.15 work

- formal accessibility/a11y conformance program or dedicated accessibility testing/documentation;
- browser partial-offline/offline business execution;
- Dynamic OpenID Connect Client Registration;
- OpenID Connect Native SSO for Mobile Apps as a Windows login mechanism;
- Zanzibar-style authorization microservice/tuple store;
- schema-per-tenant/database-per-tenant/deployment-per-tenant baseline;
- full SaaS metering/billing engine;
- advanced peripheral suite;
- specialized import/ETL platform;
- Kafka/event-log infrastructure, mandatory Redis, global CRDT model, sharding, or active-active multi-region without a measured requirement.
