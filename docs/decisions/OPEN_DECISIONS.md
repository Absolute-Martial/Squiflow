# Open Decisions — v0.0.15

These are decisions that can materially affect the current implementation baseline. Deferred ideas are not kept here merely because they may be useful someday.

## Phase-load-bearing technical decisions

- Final central transactional database after workload/benchmark proof.
- Final Workstation embedded database after SQLite versus libSQL proof.
- Exact OpenID Connect provider/implementation and any future federation strategy.
- Exact native Workstation callback mechanism after Windows packaging/security POC: app-claimed HTTPS if reliable, otherwise standards-compliant loopback IP callback.
- Final browser cookie/BFF/server-session implementation per Web host.
- Exact session-revocation persistence/rotation strategy after identity-provider and multi-node session design is selected.
- Whether the chosen identity provider supports/should enable Back-Channel Logout for SquiFlow Web sessions.
- Exact authentication-context (`acr`) vocabulary/provider mapping for high-risk step-up operations.
- Final Web/UI framework commitment where implementation evidence is still required.
- Exact messaging/scheduling libraries after POC.

## Authorization/product-control decisions

- Final set of built-in Staff permission defaults and role-template defaults.
- Whether explicit `Deny` permission semantics are ever necessary; baseline starts allow-oriented.
- Exact persistence representation/type of `TenantAuthorizationRevision`; semantics are accepted, storage detail remains implementation-level.
- Whether any future business requirement actually justifies explicit per-resource relationship sharing beyond tenant/role/scope authorization. No Zanzibar-style relationship service is planned without such evidence.
- Exact semantics for multiple role assignments (`allow` union is the current simple direction unless a real conflict requires more).
- Exact resource families that support `Own/Assigned` scope and what assignment ownership means for each.

## Current hardware/operations decisions that must be closed before production

- Authoritative inventory of the actual rack nodes: machine count, CPU/RAM, disks/filesystem, network/uplink, OS, intended roles and single points of failure.
- Whether UPS/power-loss protection is available/required for the initial production topology, based on accepted durability/recovery risk.
- Final backup target independent enough from primary business storage, backup frequency/retention and restore procedure.
- Provisional and final RPO/RTO for the initial deployment; do not claim HA/zero-downtime without them.
- Private infrastructure break-glass mechanism and access policy (Tailscale vs Twingate/other private access, SSH/container/admin boundary).
- Who actually operates/responds to alerts/physical failures and what support/maintenance promise is realistic for the current team.
- Exact warning/critical/hard thresholds for the current approximately 100 GB object-storage envelope after realistic file-growth measurement.
- Whether any additional object-storage capacity/tier is required for independent backups versus primary retained business objects.
- Actual rack uplink bandwidth and transfer-concurrency limits for attachments/backups/restores.

## UX/device/accessibility decisions

- Exact formal accessibility conformance/legal target for Web/public/client-portal surfaces. The implementation already treats keyboard/focus/labels/non-color/scalable-text as release requirements.
- Initial supported Windows/printer versions and the first printer integration/qualification matrix.
- Whether any scanner/barcode/cash-drawer/other peripheral becomes a real product requirement; none is baseline without a customer journey.
- Whether multiple SquiFlow users sharing one Windows OS profile is supported or prohibited; separate Windows accounts/profiles are simpler if acceptable.
- Exact Workstation local-data-at-rest mechanism/credential storage policy for supported Windows versions.

## Domain/commercial decisions

- Jurisdiction-specific money/tax/invoice numbering/privacy requirements.
- Whether v0.0.15 needs tenant SaaS self-service billing/metering, or whether subscriptions/entitlements are initially managed manually by SquiFlow operators.
- First client-client portal implementation scope/timing and authentication/account model for tenant customers.
- Initial customer-data import/migration scope (CSV/manual/import tooling) for onboarding from existing systems.
- Exact dynamic-form definition/version/migration contract before Phase 5 implements tenant-configurable forms.
- Initial notification channels actually required by a complete journey; do not build email/SMS/webhook channels without one.

## Deferred, not an active v0.0.15 decision

- Browser partial-offline/offline business execution is deliberately deferred. Do not spend implementation time selecting IndexedDB synchronization, service-worker business queues, browser conflict resolution or PWA offline mutation architecture during the current baseline.
- Dynamic OpenID Connect Client Registration is not needed for the controlled SquiFlow client set now.
- OpenID Connect Native SSO for Mobile Apps is not the Windows Workstation login architecture.
- A dedicated Zanzibar-style authorization microservice/tuple store/specialized set index is not justified by the current Owner/Staff + scoped-role product model.
- Schema-per-tenant/database-per-tenant/deployment-per-tenant is not baseline until an actual residency/compliance/SLA/isolation trigger appears.
- Full SaaS metering/billing engine, advanced peripheral suite, specialized import ETL platform and formal multi-region orchestration are not work items merely because they are listed as possible future needs.
