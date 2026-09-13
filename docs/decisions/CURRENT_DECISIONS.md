# Current Decisions — v0.0.19

This file records accepted direction only. Detailed reasoning and supersession history live in focused owner documents, `docs/decisions/MATERIAL_DECISION_HISTORY.md`, and `docs/review/DECISION_AUDIT.md`. When a summary here is less detailed than a focused owner, the focused owner governs.

## Product and runtime

- C# / modern .NET is the application foundation; .NET 10 LTS is the current baseline.
- ASP.NET Core is the server-host foundation.
- Avalonia is the Windows Workstation UI framework.
- Blazor Web App is the tenant Web presentation foundation and the future Platform Admin Web presentation foundation.
- The business core is a modular monolith. A module does not become a service merely because it has a name.
- **SquiFlow owns a small application kernel on standard .NET/ASP.NET Core primitives.** ABP and Orchard Core are reference designs, not combined runtime foundations or application authorities.
- `Foundation` is the narrow product-wide primitive/technical layer. Do not introduce a universal business `Shared`, `Common`, or `Utils` bucket.
- Business meaning is capability-owned: Orders, Customers, Inventory, Payments, Devices, etc. each have one source implementation of their business semantics.
- A **Capability Core** is the host-neutral/deterministic center of a capability where useful. The **Authoritative Capability Application** owns server current authority/facts/admission/commit. These are distinct concepts.
- WebApi, SyncApi, Worker, and AdminApi are runtime hosts/adapters into capability-owned application behavior; they do not own duplicate Orders/Customers/Inventory implementations.
- Physical `.csproj` decomposition is earned. `Core`, `Server`, `Workstation`, `Postgres`, `Contracts`, etc. are responsibility categories first and become projects only when cross-host reuse, provider/platform isolation, packaging, dependency enforcement, or module complexity justifies them.
- Do not introduce a mandatory CoreApi network hop merely to avoid shipping the same module assembly in WebApi/SyncApi. In-process module execution remains the modular-monolith default.
- Trusted modules declare explicit dependencies, host contributions, features, permissions, settings, migrations/seeds and background handlers. The kernel validates dependencies and composes only the current host's contributions.
- Per-tenant feature activation is versioned data resolved through `TenantContext`; it does not create one DI container/application instance per tenant. Loaded assemblies are not hot-unloaded, and arbitrary third-party micro-plugins/scripts are not baseline.
- A feature/module can be enabled or disabled at runtime for a tenant only through validated, versioned, audited publication. Disabling prevents new entry but never deletes authoritative data or silently loses accepted durable work.
- `apps/web`, `apps/desktop`, and `services/core-api` are early executable boundaries.
- `services/web-api` and `services/sync-api` are accepted future workload-specific hosts when the split is implemented; the current compact CoreApi remains valid until then.
- `apps/admin-web` is a separate platform-control-plane UI executable and is created when the platform-admin slice needs it.
- **`services/admin-api` is a separate Platform Admin backend executable/deployment/security boundary from tenant Web/Sync hosts.** Platform operations do not depend on tenant Web/Sync API availability and are not hosted as hidden `/platform-admin/...` routes on the ordinary public business API.
- `services/worker` is the durable background execution host when durable work is implemented. Worker invokes the same authoritative capability modules rather than owning Worker-specific business forks.
- Scheduler owns when a durable occurrence/job should exist; Worker owns durable execution mechanics; the owning capability owns the business mutation. Preferred chain: `Scheduler -> durable job -> Worker -> authoritative module -> persistence`.
- **`SquiFlow.Guard` is a baseline Workstation companion process.** It owns desktop process supervision, bounded crash/hang recovery, update handoff/recovery, child/helper cleanup, and bounded diagnostic/resource evidence. It does not own business rules, authorization, sync semantics, central DB access, or cryptographic root/key custody.
- Printing is a Workstation device side effect. Other peripherals are requirement-driven.

## Completeness versus minimalism

- The architecture minimizes unnecessary layers/components, not required behavior.
- A component must fully cover its accepted success, failure, recovery, security, compatibility, resource and operability responsibilities.
- Do not remove a real boundary or edge-case capability merely to reduce project/interface/process count.
- Do not create empty projects/directories to match an architecture diagram.
- Do not create generic helper/manager/service layers that only forward calls.
- Do not introduce an interface merely because an implementation class exists or because mocking it is possible.
- Generic `IRepository<T>`, `IUnitOfWork`, and one-interface-per-class conventions are not baseline. The unit-of-work concept remains an explicit transaction around one authoritative application command; EF Core or deliberate ADO.NET/Dapper code may implement it inside capability-owned persistence.
- An interface is justified for a real dependency-inversion/replacement boundary. The OpenBao/Vault key-management adapter is one such boundary; it is intentionally narrow and does not reproduce the provider API.
- Clean-code/SOLID principles are design-review guidance, not reasons to create ceremonial layers.
- Secondary architecture articles/diagrams surface questions and trade-offs; high-impact security/database/protocol/provider decisions are closed against current primary specifications or authoritative evidence where practical.

## Non-functional, consumption, and operational requirements

- Cross-cutting NFRs are classified as **HardInvariant**, **OperationalTarget**, or **DegradedMode** requirements. Detailed owner: `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md`.
- A capability is not complete merely because the happy path works; material failure, recovery, concurrency, compatibility, consistency/freshness, capacity, security, observability/support and user-understandability behavior must also be defined/tested.
- Numerical latency/resource/recovery targets are measured from representative slices and actual deployment hardware before becoming release/customer promises.
- **Consumption accounting for a defined metered resource is durable/reconcilable application state** when needed for enforcement, provider/account capacity, cost, abuse control, support/contract explanation, or future billing. It is not optional analytics and OpenTelemetry is not its authority.
- **Application-level scoped limit enforcement is accepted.** Limits may be platform/provider, workload/resource, tenant, integration/destination, or another explicit bounded scope when the implemented resource requires it.
- A tenant-specific limit does not imply a subscription tier. Commercial plan names/prices/default allowances remain separate OPEN product decisions.
- Every authoritative meter defines what consumes a unit, whether retries/failed attempts count, unit/scope, correction/reconciliation behavior and consistency needed for hard enforcement.
- Hard limits define race/atomicity/reservation behavior so concurrent requests cannot both spend the same final allowance.
- Limit changes are versioned/audited. Lowering a limit below current consumption blocks/degrades new optional usage according to policy; it never silently deletes committed business state.
- Workstation offline usage/limit snapshots may support UX but do not override current server/provider hard limits. Pending local intent is preserved if server authority later rejects/defers it because a limit changed or was exhausted.
- Existing `entitlement` wording means a SquiFlow/platform capability/security ceiling or manually controlled product availability where required; it does not imply commercial subscription tiers.
- Detailed owner: `docs/requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md`.

## Small-team tenant control

- `Owner` + `Staff` are the default small-team role templates.
- Tenant Owner controls ordinary staff permissions inside SquiFlow security/platform-capability constraints; no commercial plan is implied.
- Tenant administration is a server-authoritative capability. **Web remains the primary/broader administration surface**, but selected operations such as staff invitation/creation, role/permission assignment, device/workstation management, and other explicitly approved tenant-scoped actions may also be surfaced on an **online authorized Workstation**.
- An online Workstation tenant-admin surface is only a host adapter: it never grants/revokes authority locally or offline, and it never receives OpenFGA administrative credentials.
- Rules/workflow/form authoring/publication and broader administration may remain Web-only where no Workstation product requirement exists.
- Platform-critical controls are available only through the separate Platform Admin Web + Admin API during normal operation and are never exposed through Workstation/Sync/ordinary tenant API.
- Desktop may initiate specifically approved tenant-admin commands while online; Desktop never makes platform-control-plane state locally authoritative.
- Tenant-visible usage/limit views may be added per implemented resource, but platform hard caps and authority to raise a tenant limit remain protected platform control. Whether a Tenant Owner may configure a lower self-limit is OPEN per resource.

## Identity and authorization stack

- **ZITADEL Cloud is the selected initial identity/authentication deployment** for interactive authentication, account/session/MFA/SSO capability through standards-based OIDC/OAuth. Self-hosting is reconsidered only for explicit scale/cost, residency/compliance, control/availability, or provider-dependency needs and with proven operational capacity.
- Workstation login uses the system browser + Authorization Code + PKCE `S256`; no reusable native client secret or central DB credential is embedded in the Workstation.
- Stable external account identity remains `(issuer, subject)`, not email.
- **OpenFGA is the current application-authorization engine choice** for tenant roles, tenant-defined custom roles, role assignments, stable permissions/relations, and resource relationships where applicable.
- SquiFlow modules own stable permission definitions and metadata. Effective authorization also requires capability/feature availability, authoritative scope, OpenFGA decision at required freshness, delegation ceilings, and SquiFlow domain/concurrency/limit validation.
- Feature availability, settings, permissions, release channel, experiment assignment and domain validity are separate checks. Enabling a feature grants no role; hiding UI is not enforcement; ZITADEL role/token claims are not current business permission truth.
- ASP.NET Core policy/requirements/`IAuthorizationService` remain server integration points. Tenant hosts use tenant authorization; Admin API uses separate platform authorization.
- OpenFGA does not replace database tenant isolation, business validation, workflow guards, idempotency, concurrency checks, consumption accounting, or limits.
- OpenFGA production calls pin an explicit authorization model ID; model migrations are versioned/controlled.
- Tenant-created custom roles/assignments are data/tuples, not a new authorization model deployment per edit.
- OpenFGA tuples use opaque SquiFlow IDs rather than unnecessary PII.
- Permission/relationship changes return success only after the authoritative external change is known/applied; ambiguous outcomes are reconciled.
- `TenantAuthorizationRevision` is SquiFlow evidence/versioning for effective authorization/config and Workstation snapshot freshness; it complements OpenFGA state.
- SquiFlow does not add a separate JWT/PASETO authentication subsystem.

## Zero-trust Platform Admin

- Platform Admin is a private security/control plane, not ordinary tenant administration.
- Current private ingress direction is **Tailscale over the current Podman/rack profile**, with deny-by-default Grants/ACL-equivalent policy and a trusted private ingress/Serve path to loopback/private-bound Admin services.
- Platform Admin authorization never relies on `RemoteIpAddress`, RFC1918/private CIDR, `100.x` address, tailnet membership, or proxy identity headers alone.
- Protected Platform Admin access requires an authenticated/authorized platform administrator on a **registered, non-revoked Admin device**. Private-network evidence is only one gate.
- Every Admin action is explicitly authenticated/authorized, contextually evaluated, narrowly scoped, time-bounded/JIT where appropriate, and authoritatively audited.
- There is no universal `SuperAdmin = everything` assumption. Admin-plane entry and capability/resource authority are separate decisions.
- High-risk operations can require recent/step-up authentication, registered-device proof, a physical security/recovery factor, JIT/time-bounded elevation, and independent four-eyes approval according to risk.
- Break-glass is a narrow private emergency workflow, not a standing all-powerful account; use is high-severity evidenced/audited and followed by credential/factor review/rotation.
- Cross-tenant support access is separate from encryption administration and requires its own permission, reason/ticket context, tenant/resource scope, time limit/expiry and audit; approval or tenant acknowledgement may be required by policy.
- Admin API outage does not by itself stop already-provisioned ordinary tenant business operation.
- Detailed owners: `docs/admin/ADMIN_SURFACES.md`, `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`, `docs/operations/PRIVATE_ADMIN_NETWORK_AND_PODMAN.md`.

## Encryption and key management

- SquiFlow **does not implement its own cryptographic vault/root-key lifecycle**. OpenBao is the initial self-hosted key-management candidate; HashiCorp Vault remains a future provider alternative behind a narrow SquiFlow adapter.
- OpenBao/Vault owns cryptographic root/key storage and operations. SquiFlow owns policy, authorization, resource/key mapping, recovery workflow, provider integration semantics and authoritative audit.
- The current initial rack profile is **OpenBao Transit + Integrated Storage/Raft + Shamir 3-of-5 unseal/recovery + PGP-encrypted shares + separate secure holders/locations + regular tested Raft snapshots + periodic unseal/recovery drills**.
- When a trustworthy external KMS/HSM exists, hardware-backed/external auto-unseal may supersede the initial manual Shamir profile after an explicit migration/recovery proof.
- Static Unseal with a plaintext `master.key`/`unseal.key` stored beside OpenBao/Raft data is not the production target.
- An ordinary removable drive containing a raw plaintext master key is not the production root-of-trust target. Prefer non-exportable hardware-backed/HSM protection when practical. Transitional recovery material must itself be protected and normally offline.
- One lost/broken physical token must not permanently destroy customer data. Recovery shares/factors are independently protected and physically separated.
- Use envelope-style key management. One global symmetric master key does not directly encrypt every Workstation/server/backup object.
- Platform Admin governs a versioned `EncryptionPolicy` covering Workstation DB, server storage, sensitive-field classifications, object storage, backups, diagnostics/redaction, compatibility and key lifecycle. Publication is audited, integrity-protected where supported, and rollback/roll-forward aware.
- Raw KEKs, DEKs, root/seal material and recovery shares are not ordinary Admin UI/API output; generic raw-key export is not an ordinary feature.
- Key rotation is staged and preserves old-data decryptability while rewrap/re-encryption progresses. Conceptual states include `Pending`, `Active`, `DecryptOnly`, `Retiring`, `Revoked`, `Destroyed`; revocation is distinct from irreversible destruction.
- Material cryptographic/admin actions create durable authoritative security audit state separate from lossy OTel/Serilog telemetry. Safe conceptual fields include `ActorId`, `Action`, `Target`, optional `TenantId`/`DeviceId`, `Reason`, `ApprovalId`, `PreviousVersion`, `NewVersion`, `CorrelationId`, `Timestamp`, and `Outcome`; secret key bytes/tokens/shares are never audit fields.
- Detailed owners: `docs/security/ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md`, `docs/security/ENCRYPTION_POLICY_KEY_LIFECYCLE_AND_PRIVILEGED_ACCESS.md`, `docs/security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md`.

## Web and Workstation

- Web is online-only for business operations. No IndexedDB business replica, service-worker business sync, or browser offline mutation queue is baseline.
- Valuable online forms may use explicit server-side drafts/autosave when justified.
- Workstation is the local-first/offline client.
- Local Workstation success and server-authoritative acceptance are separate states (`LocalCommitted`, `PendingRemote`, `Authoritative`, `Conflict`, `Rejected`, `AuthorizationChanged`, `UpgradeRequired`).
- The accepted sync model is **provisional local execution -> semantic OperationEnvelope/revision evidence -> authoritative server admission/commit**, not blind execution/storage of the full transaction twice.
- Revision/version evidence permits a fast path/selective re-evaluation where authoritative dependencies are unchanged; current security/authorization and protected invariants are never skipped solely because a revision token matches.
- SquiFlow adopts local-first interaction/durability, not a global CRDT or peer-authority model for payments, stock, credit, permissions, hard limits, or other shared invariants.
- Local-first synchronization is not described as generic eventual consistency: users can see whether work is local/pending or centrally authoritative.

## Multi-tenancy, persistence, and encryption

- Ordinary tenants use a pooled multi-tenant baseline with explicit tenant discriminators on tenant-owned authoritative data.
- Authentication, authorization, and tenant isolation are separate concerns.
- Schema-per-tenant, DB-per-tenant, queue-per-tenant, and deployment-per-tenant are not baseline.
- **PostgreSQL is selected as the initial authoritative central transactional database.** Qualification covers ACID behavior, normalized constraints, tenant isolation/RLS defense in depth, workload/query performance, migration, backup/restore, recovery, and bounded operation on the actual rack.
- **SQLite with WAL is selected as the Workstation-only embedded database.** It stores local provisional/pending/outbox state, not server authority.
- Workstation SQLite business persistence must be encrypted at rest. Qualification covers the actual database, WAL/journal/shared-memory/temp behavior, migration/backup copies, local outbox/payload staging and recovery checkpoints, not merely the main `.db` file.
- Workstation encryption uses a device-specific DEK. The local usable copy is protected through the selected Windows/device mechanism (DPAPI/TPM-backed direction to qualify), while a centrally wrapped recovery copy is managed through OpenBao/Vault. Normal offline DB startup does not depend on Admin/OpenBao connectivity.
- Guard is not a key vault. Encrypted live DB state must not silently become plaintext migration/update/rollback checkpoints; manifests may carry key references/versions but never raw key bytes.
- BitLocker/device-volume protection is defense in depth, not a replacement for application/database encryption.
- Server protection is layered: encrypted block/volume storage according to deployment capability, TLS at relevant provider/network boundaries, PostgreSQL least privilege/RLS, selective application/key-service encryption for classified values where justified, and encrypted backup/object handling.
- **libSQL is deferred, not layered on top of SQLite.** Reopen only for a concrete libSQL-specific need with supported .NET/Windows integration and proven value.
- **A dedicated NoSQL database is not baseline.** Bounded semi-structured configuration may use PostgreSQL JSONB or explicit SQLite representation; specialized stores require a named workload and explicit authority/consistency/rebuild/backup/tenancy/operations contract.
- Artwork, PDFs, images and other large unstructured objects belong behind `IObjectStore`; DB records retain ownership, integrity, lifecycle and hash metadata.
- Authoritative relational data is normalized around real business identities/relationships first. Denormalized/materialized read structures are derived optimizations with explicit source/freshness/rebuild/tenant-scope/failure contracts.
- Core invariants are not hidden in arbitrary JSON/EAV or tenant-specific DDL merely to avoid schema design.
- Indexes are workload-driven and their write/storage/WAL/migration/sync costs are measured.
- PostgreSQL operational qualification includes connection/backend-process cost, WAL growth, checkpoints, autovacuum, temp/sort spill, archive/log growth, restart/crash recovery, disk-full behavior and restore on the actual rack.
- WebApi, SyncApi, AdminApi and Worker do not gain a local SQLite business DB. Durable processing state such as inbox/jobs/idempotency/outbox may initially live in PostgreSQL under explicit ownership; ephemeral caches remain disposable.
- Module-owned query paths may use optimized projections for reads; protected mutations go through owning authoritative application paths. Controllers/hosts do not perform ad-hoc business SQL merely because the DB is reachable.
- Database schema changes account for supported old/new backend processes, skipped Workstations, pending sync, durable work and stored snapshots. Prefer expand-migrate-switch-contract evolution.
- Expected-version/optimistic concurrency is the ordinary edit contract; database constraints protect uniqueness; stronger isolation/locks are selected narrowly for actual invariants.

## Consistency model

- SquiFlow does not choose one consistency model for the whole product.
- Current/strong authority is required where temporary disagreement can create unsafe effects, including sensitive authorization, tenant isolation, shared stock/credit, payment/refund authority, unique issued-document truth, expected-version transitions, and strict hard-limit decisions.
- Eventual/derived consistency is acceptable for consequences such as notifications, telemetry, non-authoritative caches, and read/search/report projections when freshness/rebuild behavior is explicit.
- An eventually updated projection declares its authoritative source, freshness/version evidence where material, duplicate/out-of-order handling, and reconciliation/rebuild path.
- Stale derived data must not silently become authority for permissions, payment, stock, credit, hard limits, or another protected invariant.

## API, sync, and Worker correctness

- REST/task-oriented HTTP is the ordinary Web/external API baseline; SquiFlow does not require strict REST purity. Semantic command/action subresources are valid for material domain transitions.
- GraphQL/Federation is deferred until a real query-composition requirement justifies its cost/authorization/schema complexity.
- **WebApi and SyncApi are separate workload hosts, not separate business backends and not one-way ingress-only pipes.** WebApi supports interactive commands/queries; SyncApi supports upload/admission and pull/download with device/batch/cursor/backpressure concerns. Both converge on the same authoritative capability modules.
- Reads remain module-owned first-class operations. Optimized read projections are allowed without creating a second authority.
- Retryable mutations use caller-provided semantic idempotency keys; same key + changed intent is rejected.
- Where one store owns mutation + idempotency receipt + outbox, they commit atomically.
- At-least-once delivery/redelivery is assumed; effects/usage are idempotent or explicitly reconcilable.
- Retry is finite, classified, budgeted, and uses backoff/jitter/`Retry-After` where appropriate.
- Long-running/resource-heavy work uses durable asynchronous status only when appropriate; ordinary short transactions remain synchronous.
- Conflict handling is aggregate-specific; no global last-write-wins policy.
- Large collection APIs are paginated/bounded. Connection pools are bounded/measured and must not leak tenant DB context.
- Caching, compression and async telemetry are selective performance techniques, not correctness mechanisms. Security/business audit and authoritative usage do not exist only in lossy telemetry buffers.
- Rate/admission limiting is multi-dimensional where needed and remains separate from authorization/durable quota accounting.
- Internal modular-monolith communication is in-process by default. Do not create HTTP/gRPC between ordinary modules to imitate microservices.
- **gRPC is a preferred candidate, not a universal transport**, for real process/service boundaries such as Workstation sync when POC evidence justifies streaming/binary/generated-contract value.
- Workstation sync semantics remain transport-independent.

## Network edge, deployment and protocol boundaries

- An edge reverse proxy/API-gateway capability may terminate TLS, route hostnames, enforce request-size/WAF/access policy, negotiate transport, and apply coarse rate limits where needed.
- Edge controls never replace backend authentication, authorization, TenantContext isolation, domain validation, idempotency/concurrency or resource-specific limits.
- Platform Admin uses a private network path; it is not exposed as a hidden route on the ordinary public tenant API.
- Current server/container profile uses **Podman**. Kubernetes is not baseline; revisit only after concrete multi-node orchestration/rollout/reconciliation/discovery/failover/scaling pain earns it.
- Current private Admin ingress preference is **Tailscale**, using deny-by-default policy and trusted private ingress to loopback/private-bound Admin services. Another VPN/private-network provider may replace it only with equivalent or stronger properties.
- OpenBao is more restricted than Platform Admin and is not publicly exposed. Normal Admin operations reach it through the narrow Admin API key-management adapter. Direct operator access is only for documented private bootstrap/unseal/recovery.
- Production external application traffic uses HTTPS/TLS; certificate validation is not disabled for recovery.
- HTTP transport version negotiation is infrastructure/runtime concern; application semantics do not depend on one HTTP version.
- WebSocket/SignalR, if used, is live signal/wakeup behavior only; durable truth remains in DB/outbox/state records.
- SSH/private network access is infrastructure recovery/operations only, never a normal tenant business channel.
- DNS/hostname information assists routing but is never tenant authority by itself.
- The paying-customer deployment is reproducible from version-controlled deployment definitions/runbooks and immutable verified artifacts. Business/platform settings remain first-class Web/Admin API concerns rather than YAML-only administration.
- On a single active rack node, honest maintenance downtime can be safer than pretending zero downtime. Blue-green/canary/progressive delivery requires real topology/capacity/routing/compatibility/telemetry/recovery evidence.
- Scalability is finite and measured; add caching, replicas, sharding, extra nodes or service extraction only for identified bottlenecks.

## Rules/workflow

- SquiFlow owns the bounded native rule model; arbitrary tenant C#/JS/SQL is not allowed.
- Rules/workflows are edited/published through Web administration and distributed as immutable/versioned compatible snapshots.
- Workstation local rule evaluation cannot turn stale server-owned facts into authoritative financial/stock/security/hard-limit decisions.
- Workflow is continuation-first and versioned.

## Practical business scope

- Do not force a universal `ready-made` versus `custom-design`, separate `social`, or one quotation-type category model. Tenant-visible categories exist only for real business distinctions while shared quotation revision/issued-history rules remain stable.
- Owner-authorized final pricing is supported inside permission/rule limits and historical issued values remain explainable rather than recomputed from later settings.
- Outsourced print/production records actual external work/cost/payable; it does not invent supplier design work. Informal/phone supplier ordering and partial payment/outstanding payable are valid.
- Inventory can use precise quantity, availability-only, non-stock/service, and damaged/unusable adjustment modes. Universal reservation, MRP/production planning, banner/roll wastage optimization, and universal lot/serial tracking are not baseline.

## Money/currency

- Currency is not hardcoded in application logic.
- A tenant has a configurable default currency code and monetary records requiring historical meaning retain the applicable currency code.
- Exact rounding/tax precision closes with the first affected production financial slice; it is not guessed globally.
- No baseline multi-currency ledger, FX provider, gain/loss accounting or currency-provider abstraction exists yet.

## Object storage and backups

- The bootstrap primary object store is a **private Hugging Face Storage Bucket**, with its finite storage envelope treated as a real provider/account limit rather than a predetermined tenant allowance.
- Retained object bytes are a likely first authoritative consumption meter because provider/account capacity is real; exact tenant limits remain OPEN until configured/required.
- Because primary object storage is planned to change at the first paying customer, a narrow **`IObjectStore`** provider boundary is baseline; provider SDK types do not leak into business/domain contracts.
- The bootstrap off-site backup target is a **private Kaggle Dataset** containing only encrypted opaque backup archives, never raw customer tables/files.
- Backup destination access uses a separate infrastructure-level **`IBackupTarget`** boundary.
- Backup is infrastructure recovery, not merely an export. Recoverable state includes central DB, object data/metadata strategy, idempotency/outbox/jobs whose loss can change effects, rules/config, deployment metadata, identity/authorization configuration and independently recoverable key material according to topology.
- Encryption keys/recovery material remain separate from backup archives/destinations. Restore drills prove download + decrypt + integrity + reconstruction.
- Hugging Face/Kaggle are temporary; migrate to purpose-built paid providers at the first paying customer or earlier if requirements demand it.

## Operations and observability

- Current server hardware is lower-spec/desktop-class rack hardware; `stateless` does not imply automatic failover or zero downtime.
- Resource use is bounded; spare CPU/RAM is headroom rather than permission for unbounded caches/workers.
- Web/Sync/CoreApi and Admin API have independent process/deployment health. An ordinary business-host outage must not automatically remove the Platform Admin application control surface; Admin API outage must not block ordinary already-provisioned tenant business work.
- OpenBao availability is a runtime dependency only for operations that actually require an online key-service operation. Workstation local DB startup remains independent after legitimate provisioning.
- OpenTelemetry/OTLP is the instrumentation boundary. New Relic + Aiven OpenSearch are current managed targets and Backtrace remains crash-diagnostics direction.
- SquiFlow owns provider-neutral observability built on standard .NET/OpenTelemetry primitives; domain code does not depend on provider SDKs.
- `TraceId`, SquiFlow `CorrelationId`, and `CausationId` are distinct and propagated deliberately.
- Significant operational logs use stable `EventId`/`EventName`; abnormal/failure outcomes use stable `FailureCode` values.
- Sync/outbox/worker/Guard and similar state machines emit meaningful transition events.
- Workstation observability is local-durable-first with bounded rotating evidence and selective central export; server observability is central-first with bounded buffering/export.
- Workstation diagnostics protect a disk reserve and shed low-value telemetry before threatening SQLite/OS/update recovery.
- Guard contributes bounded lifecycle/crash/resource evidence without becoming business/key authority.
- High-cardinality identifiers belong primarily in controlled logs/traces, not ordinary unbounded metric dimensions.
- Telemetry wall-clock timestamps are UTC and elapsed durations use monotonic timing; Workstation wall clock is not distributed ordering/idempotency authority.
- Telemetry-provider failure/quota exhaustion cannot block business correctness.
- Operational telemetry is separate from authoritative security/business audit state.

Detailed observability owners remain under `docs/observability/`.

## Application security

- Application security is layered across ZITADEL identity, OpenFGA/ASP.NET authorization, TenantContext/data isolation, domain/field rules, parameterized data access, browser output/forgery controls, bounded files/URLs, TLS/secrets/key management, dependency/build controls, private Admin ingress, and host defense in depth.
- Parameterized/ORM-bound data access is mandatory; untrusted values are never concatenated into SQL, and dynamic identifiers/operators use explicit allow-lists.
- Web output is encoded by default; permitted rich content is narrowly sanitized; cookie-backed mutations receive CSRF protection; exact CSP/security-header policy closes with the selected Web topology.
- Server-side outbound URLs use explicit SSRF destination/scheme/redirect/size/timeout controls. File names, templates, provider responses and uploaded documents are untrusted.
- Current containers use least privilege/minimal/pinned/secret-free/reproducible/resource-bounded images as practical; production secrets/key material are never baked into source or image layers.
- Workstation contains no reusable central DB/platform-management credential and no plaintext local DB key in ordinary configuration/source/install paths.
- Logs/traces/crash dumps/support bundles never contain raw access tokens, cookies, provider secrets, OpenBao tokens, DEKs/KEKs/root/recovery shares, or unrestricted customer content.
- Security scanning is supporting evidence, not a substitute for tenant/resource/journey/provider/recovery tests.
- Detailed ownership is in `docs/security/APPLICATION_SECURITY_BASELINE.md` and the v0.0.19 encryption/zero-trust security documents.

## Explicitly not baseline

- full browser offline business sync;
- generic repository/unit-of-work/one-interface-per-class abstractions;
- Kafka, mandatory Redis, event-sourced/full-CQRS/Saga core architecture;
- GraphQL/Federation without a concrete query need;
- service mesh without concrete east-west complexity;
- Kubernetes without concrete cluster-orchestration need;
- heavyweight API-management platform selected before a concrete need;
- HTTP/gRPC between ordinary modules;
- gRPC as universal/default transport;
- database-per-service rules applied to the modular monolith;
- eventual-consistency-everywhere;
- denormalized authoritative core schema;
- global CRDTs;
- microservice-per-module design;
- per-tenant infrastructure by default;
- invented commercial plan names/prices/default allowances or SaaS billing engine without current product/commercial requirement;
- giant generic metering/data-warehouse platform without enforcement/cost/capacity/support reason;
- advanced peripheral suite, MRP/wastage, specialized ETL/search without a current need;
- mandatory cache product, data lake, custom authentication/token system, or pattern-driven security microservice;
- custom SquiFlow cryptographic vault/root-key implementation;
- plaintext raw master key stored beside OpenBao or ordinary application data;
- public Platform Admin/OpenBao exposure or `private IP = admin` authorization shortcuts.
