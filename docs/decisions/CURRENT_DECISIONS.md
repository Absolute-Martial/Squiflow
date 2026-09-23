# Current Decisions — v0.1.0

This file records accepted direction only. Detailed reasoning and supersession history live in focused owner documents, `docs/decisions/MATERIAL_DECISION_HISTORY.md`, and `docs/review/DECISION_AUDIT.md`. When a summary here is less detailed than a focused owner, the focused owner governs.

## Principles-first implementation reset

- The Phase-0A `v0.0.20` reset snapshot intentionally contains no production/test projects. That zero-project state is qualification-time reset evidence, not a permanent `v0.0.20` invariant. Later active work may add newly earned projects. Earlier Phase-0 implementation remains in Git history as evidence, not current implementation authority.
- The 2026-09-17 purge restored the tree to a zero-project reset point. The former 0B Parties implementation and its verification remain retired history; the later ApplicationProfiles, Branding, IdentityAccess, Tenancy, CoreApi and DbMigrator slices are independently earned and do not restore Parties scaffolding.
- Implementation is source-first. Before custom infrastructure, inspect applicable proven source and record whether to use a focused package, adapt bounded source when its license permits the intended use and distribution, reuse tests/algorithms, or retain it as reference with a concrete rejection reason. License does not exclude a source from internal research; product copying, dependency and distribution obligations are evaluated separately. `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` owns the current routing map.
- Development restarts from `docs/architecture/ENGINEERING_PRINCIPLES.md`, `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`, `docs/architecture/REPOSITORY_STRUCTURE.md`, and the focused owner for the responsibility being implemented.
- A phase is a minimum maturity/verification floor, not a maximum implementation-scope or quality ceiling; a phase label does not authorize its imagined project/runtime contents.
- KISS means the simplest design that completely covers the current responsibility, including material edge/failure/recovery/security/concurrency/compatibility/resource/observability/operability cases. KISS never means happy-path-only or fewest files/classes at any cost.
- YAGNI prevents speculative projects/providers/processes/interfaces; it does not permit omitting behavior required by a responsibility already introduced.
- SOLID is applied pragmatically to real ownership/change/replacement/fault/security boundaries. It does not imply one interface per class, generic repositories, forwarding layers, or speculative plugin systems.
- Architecture/file-structure samples are growth maps and ownership guidance, not mandatory scaffolding. A folder/project/process is created only when its responsibility and boundary are earned.
- Do not rebuild `SquiFlow.ApplicationKernel` merely because it existed before the reset. Start from real capability/application work and introduce shared Foundation/kernel primitives only when current consumers prove genuinely product-wide semantics.
- Repository CI/CD is allowed when real executable code earns a verification contract. Local verification and any thin provider wrappers must share repository-owned commands/scripts.
- Phase/gate governance follows the same earned-detail rule as implementation. Global production-honesty/evidence contracts are canonical, but future phase-specific evidence maps, cadences, transitional contracts and subphase decomposition are written only when real responsibilities/workloads earn them.
- Detailed Phase 0 and Phase 1 packages are retained. Phase 2–10 are direction-only `NOT_INTRODUCED` README stubs until activated by real work. Future planning ideas live in `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md` as non-authoritative anticipation, not implementation contracts.
- A future phase number does not force implementation to match an old anticipated decomposition. When a responsibility becomes real, derive its production intent, exact scope, evidence and regression guard from current requirements/workload/architecture and either use, change or reject the old anticipation.

## Product and runtime

- `SquiFlow` is the repository's internal development codename, not public product identity. Active solution/project names, project directories, namespaces, assemblies, tests and synthetic runtime resources use neutral `Application.*`/`application_*` identities; the repository hosting name, explanatory docs and historical evidence may retain the codename. Public UI/API messages, deployment branding, telemetry and operational configuration remain brand-neutral or derive from validated brand configuration. `docs/product/PRODUCT_IDENTITY_AND_VERSIONING.md` owns the detailed rule.
- Product version is exactly `v0.1.0` and remains locked until the complete production-capable product gate is explicitly qualified. Git revisions retain build traceability; API/schema/migration versions remain separate compatibility identifiers.
- C# / modern .NET is the application foundation; .NET 10 LTS is the current baseline.
- FullStackHero at pinned revision `3f2959e683e9f83f13e55e1678c9119f63c7e8e5` is the source-owned backend starting base. The repository keeps its complete source locally and selectively adapts it without the template generator, runtime dependency, FSH branding, reflective module loader, Mediator source generator, built-in identity/permission authority, per-tenant database model, Hangfire stack or bundled infrastructure. Standard .NET/ASP.NET Core remain the runtime primitives and this repository retains all product authority. Detailed evidence: `docs/review/APPLICATION_BASE_FRAMEWORK_ADMISSION_RESEARCH.md` and `docs/review/FULLSTACKHERO_BACKEND_ADOPTION_LEDGER.md`.
- ASP.NET Core is the server-host foundation when server hosts are implemented.
- Avalonia is the Windows Workstation UI framework when Workstation is implemented.
- Blazor Web App is the tenant Web presentation foundation and the future Platform Admin Web presentation foundation when those surfaces are implemented.
- The business core is a modular monolith. A module does not become a service merely because it has a name.
- `Foundation` is the narrow product-wide primitive/technical layer. Do not introduce a universal business `Shared`, `Common`, or `Utils` bucket.
- Business meaning is capability-owned: Orders, Customers, Inventory, Payments, Devices, etc. each have one source implementation of their business semantics.
- A Capability Core is the host-neutral/deterministic center of a capability where useful. The Authoritative Capability Application owns server current authority/facts/admission/commit. These are distinct concepts.
- CoreApi, future SyncApi, Worker, and AdminApi are runtime hosts/adapters into capability-owned application behavior; they do not own duplicate Orders/Customers/Inventory implementations. CoreApi owns the interactive Web/external tenant API role unless it is deliberately renamed/replaced by WebApi; both do not run as forwarding layers.
- Physical `.csproj` decomposition is earned. `Core`, `Server`, `Workstation`, `Postgres`, `Contracts`, etc. are responsibility categories first and become projects only when cross-host reuse, provider/platform isolation, packaging, dependency enforcement, or module complexity justifies them.
- Do not introduce a mandatory CoreApi network hop merely to avoid shipping the same module assembly in WebApi/SyncApi. In-process module execution remains the modular-monolith default.
- Future host/process names are not application-kernel business vocabulary. Capability metadata must not contain a global executable taxonomy such as `HostKind`/`SupportedHosts`.
- Tenant variation is represented by an immutable versioned Tenant Application Profile. Use the lowest sufficient layer: presentation metadata, feature/capability selection, settings/permissions/rules/workflows/forms, extensible typed information, then a trusted implementation variant only when the lower layers cannot safely express the supported difference. Arbitrary third-party tenant code/scripts/DDL remain outside the baseline.
- CoreApi uses Autofac `9.3.4` through `Autofac.Extensions.DependencyInjection` `11.0.2`; existing framework and capability registrations remain expressed through `IServiceCollection`, and capability projects remain Autofac-neutral. The internal bounded runtime registry is tenant-keyed: identical implementation fingerprints do not share a retained Autofac scope between tenants. It proves single-flight construction, operation-scoped `TenantContext`, idle retirement, lease-aware draining, shutdown and metrics. It is not reachable from production requests: durable profile authority and the first real implementation variant remain `NOT_INTRODUCED`. Containers are never stored on disk; future durable profiles reconstruct process-local acceleration state. DI scopes are not resource or process-fault isolation.
- A feature/module can be enabled or disabled at runtime for a tenant only through validated, versioned, audited publication. Disabling prevents new entry but never deletes authoritative data or silently loses accepted durable work.
- `apps/web`, `apps/desktop`, `services/core-api`, future SyncApi/Worker/Admin, and desktop helper paths are ownership locations, not automatic implementation claims.
- `services/core-api` is the current interactive tenant/business API seed. `services/sync-api` remains the accepted separate Workstation synchronization host when that workload is implemented. Do not add a second `services/web-api` pass-through host; rename/replace CoreApi only if the later Web product topology makes `WebApi` the clearer executable name.
- `services/admin-api` remains a separate Platform Admin backend executable/deployment/security boundary from tenant Web/Sync hosts when implemented.
- `services/worker` remains the durable background execution host when durable work is implemented. Worker invokes the same authoritative capability modules rather than owning Worker-specific business forks.
- Scheduler owns when a durable occurrence/job should exist; Worker owns durable execution mechanics; the owning capability owns the business mutation. Preferred chain: `Scheduler -> durable job -> Worker -> authoritative module -> persistence`.
- `SquiFlow.Guard` remains an accepted Workstation companion-process boundary when rebuilt. It owns desktop process supervision/recovery concerns and must not own business rules, authorization, sync semantics, central DB access, Worker scheduling, or cryptographic root/key custody.
- Printing is a Workstation device side effect. Other peripherals are requirement-driven.

## Completeness versus minimalism

- The architecture minimizes accidental complexity, not required behavior.
- A component must fully cover its accepted success, failure, recovery, security, compatibility, resource and operability responsibilities.
- Do not remove a real boundary or edge-case capability merely to reduce project/interface/process count.
- Do not create empty projects/directories to match an architecture diagram.
- Do not create generic helper/manager/service layers that only forward calls.
- Do not introduce an interface merely because an implementation class exists or because mocking it is possible.
- Generic `IRepository<T>`, `IUnitOfWork`, and one-interface-per-class conventions are not baseline. The unit-of-work concept remains an explicit transaction around one authoritative application command; EF Core or deliberate ADO.NET/Dapper code may implement it inside capability-owned persistence when persistence is introduced.
- An interface is justified for a real dependency-inversion/replacement boundary. The OpenBao/Vault key-management adapter is one example when that integration is implemented; it must remain narrow and must not reproduce the provider API.
- Clean-code/SOLID principles are design-review guidance tied to real boundaries, not reasons to create ceremonial layers.
- Secondary architecture articles/diagrams surface questions and trade-offs; high-impact security/database/protocol/provider decisions are closed against current primary specifications or authoritative evidence where practical.

## Non-functional, consumption, and operational requirements

- Cross-cutting NFRs are classified as HardInvariant, OperationalTarget, or DegradedMode requirements. Detailed owner: `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md`.
- A capability is not complete merely because the happy path works; material failure, recovery, concurrency, compatibility, consistency/freshness, capacity, security, observability/support and user-understandability behavior must also be defined/tested.
- Numerical latency/resource/recovery targets are measured from representative slices and actual deployment hardware before becoming release/customer promises.
- Consumption accounting for a defined metered resource is durable/reconcilable application state when needed for enforcement, provider/account capacity, cost, abuse control, support/contract explanation, or future billing. It is not optional analytics and OpenTelemetry is not its authority.
- Application-level scoped limit enforcement is accepted. Limits may be platform/provider, workload/resource, tenant, integration/destination, or another explicit bounded scope when the implemented resource requires it.
- A tenant-specific limit does not imply a subscription tier. Commercial plan names/prices/default allowances remain separate OPEN product decisions.
- Every authoritative meter defines what consumes a unit, whether retries/failed attempts count, unit/scope, correction/reconciliation behavior and consistency needed for hard enforcement.
- Hard limits define race/atomicity/reservation behavior so concurrent requests cannot both spend the same final allowance.
- Limit changes are versioned/audited. Lowering a limit below current consumption blocks/degrades new optional usage according to policy; it never silently deletes committed business state.
- Workstation offline usage/limit snapshots may support UX but do not override current server/provider hard limits. Pending local intent is preserved if server authority later rejects/defers it because a limit changed or was exhausted.

## Small-team tenant control

- `Owner` + `Staff` are the default small-team role templates.
- Tenant Owner controls ordinary staff permissions inside SquiFlow security/platform-capability constraints; no commercial plan is implied.
- Tenant administration is a server-authoritative capability. Web remains the primary/broader administration surface, but selected operations such as staff invitation/creation, role/permission assignment, device/workstation management, and other explicitly approved tenant-scoped actions may also be surfaced on an online authorized Workstation.
- An online Workstation tenant-admin surface is only a host adapter: it never grants/revokes authority locally or offline, and it never receives OpenFGA administrative credentials.
- Rules/workflow/form authoring/publication and broader administration may remain Web-only where no Workstation product requirement exists.
- Platform-critical controls are available only through the separate Platform Admin Web + Admin API during normal operation and are never exposed through Workstation/Sync/ordinary tenant API.

## Identity and authorization stack

- ZITADEL Cloud is the selected initial identity/authentication deployment for interactive authentication through standards-based OIDC/OAuth. Self-hosting is reconsidered only for explicit scale/cost, residency/compliance, control/availability, or provider-dependency needs and with proven operational capacity.
- Workstation login uses the system browser + Authorization Code + PKCE `S256`; no reusable native client secret or central DB credential is embedded in the Workstation.
- Stable external account identity remains `(issuer, subject)`, not email.
- OpenFGA is the current application-authorization engine choice for tenant roles, tenant-defined custom roles, role assignments, stable permissions/relations, and resource relationships where applicable.
- SquiFlow modules own stable permission definitions and metadata. Effective authorization also requires capability/feature availability, authoritative scope, OpenFGA decision at required freshness, delegation ceilings, and SquiFlow domain/concurrency/limit validation.
- Feature availability, settings, permissions, release channel, experiment assignment and domain validity are separate checks. Enabling a feature grants no role; hiding UI is not enforcement; ZITADEL role/token claims are not current business permission truth.
- ASP.NET Core policy/requirements/`IAuthorizationService` remain server integration points. Tenant hosts use tenant authorization; Admin API uses separate platform authorization.
- OpenFGA does not replace database tenant isolation, business validation, workflow guards, idempotency, concurrency checks, consumption accounting, or limits.
- OpenFGA production calls pin an explicit authorization model ID; model migrations are versioned/controlled.
- The implemented OpenFGA surface covers authenticated tenant-workspace read plus narrow Order draft create/read/browse/abandon operations. Finbuckle resolves the route tenant only as an untrusted candidate; current membership creates `TenantContext`; the pinned model then intersects contextual membership with the relevant persisted `workspace_viewer`, `order_creator`, `order_viewer`, or `order_abandoner` relation at `HIGHER_CONSISTENCY`. Abandonment requires its separate relation. Provider timeout/error fails closed. This does not introduce role or tuple-administration workflows.
- The first tenant-owned business persistence is an immutable priced Order draft with create/read/browse and one-way abandonment. It uses explicit tenant SQL predicates plus forced PostgreSQL RLS and transaction-local tenant context. Create commits a caller-scoped semantic-idempotency receipt atomically with the order; browse uses bounded keyset pagination over stable descending creation/identity order; abandonment uses an expected revision, preserves priced content and the original create receipt, and commits a separate caller-scoped receipt with the transition. This does not introduce text search or settle the broader Orders lifecycle or discovery-sensitive customer/work/job distinctions; `docs/implementation/ORDER_DRAFT_INTAKE_SLICE.md` owns the exact claim.
- Tenant-created custom roles/assignments are data/tuples, not a new authorization model deployment per edit.
- OpenFGA tuples use opaque SquiFlow IDs rather than unnecessary PII.
- Permission/relationship changes return success only after the authoritative external change is known/applied; ambiguous outcomes are reconciled.
- SquiFlow does not add a separate JWT/PASETO authentication subsystem.

## Zero-trust Platform Admin

- Platform Admin is a private security/control plane, not ordinary tenant administration.
- Current private ingress direction is Tailscale over the current Podman/rack profile, with deny-by-default Grants/ACL-equivalent policy and a trusted private ingress/Serve path to loopback/private-bound Admin services.
- Platform Admin authorization never relies on `RemoteIpAddress`, RFC1918/private CIDR, `100.x` address, tailnet membership, or proxy identity headers alone.
- Protected Platform Admin access requires an authenticated/authorized platform administrator on a registered, non-revoked Admin device. Private-network evidence is only one gate.
- Every Admin action is explicitly authenticated/authorized, contextually evaluated, narrowly scoped, time-bounded/JIT where appropriate, and authoritatively audited.
- There is no universal `SuperAdmin = everything` assumption.
- High-risk operations can require recent/step-up authentication, registered-device proof, a physical security/recovery factor, JIT/time-bounded elevation, and independent four-eyes approval according to risk.
- Break-glass is a narrow private emergency workflow, not a standing all-powerful account.
- Cross-tenant support access is separate from encryption administration and requires its own permission, reason/ticket context, tenant/resource scope, time limit/expiry and audit.
- Admin API outage does not by itself stop already-provisioned ordinary tenant business operation.

## Encryption and key management

- SquiFlow does not implement its own cryptographic vault/root-key lifecycle. OpenBao is the initial self-hosted key-management candidate; HashiCorp Vault remains a future provider alternative behind a narrow SquiFlow adapter.
- OpenBao/Vault owns cryptographic root/key storage and operations. SquiFlow owns policy, authorization, resource/key mapping, recovery workflow, provider integration semantics and authoritative audit.
- The current initial rack profile is OpenBao Transit + Integrated Storage/Raft + Shamir 3-of-5 unseal/recovery + PGP-encrypted shares + separate secure holders/locations + tested Raft snapshots + recovery drills.
- When a trustworthy external KMS/HSM exists, hardware-backed/external auto-unseal may supersede the initial manual Shamir profile after explicit migration/recovery proof.
- Static Unseal with a plaintext `master.key`/`unseal.key` stored beside OpenBao/Raft data is not the production target.
- One global symmetric master key does not directly encrypt every Workstation/server/backup object.
- Platform Admin governs a versioned EncryptionPolicy covering Workstation DB, server storage, sensitive-field classifications, object storage, backups, diagnostics/redaction, compatibility and key lifecycle.
- Raw KEKs, DEKs, root/seal material and recovery shares are not ordinary Admin UI/API output.
- Key rotation is staged and preserves old-data decryptability while rewrap/re-encryption progresses.
- Material cryptographic/admin actions create durable authoritative security audit state separate from lossy OTel/Serilog telemetry.

## Web and Workstation

- Web is online-only for business operations. No IndexedDB business replica, service-worker business sync, or browser offline mutation queue is baseline.
- Valuable online forms may use explicit server-side drafts/autosave when justified.
- Workstation is the local-first/offline client.
- When a useful Workstation slice is introduced, its preferred direct presentation base is Avalonia + CommunityToolkit.Mvvm + the minimum required Microsoft.Extensions host primitives. SquiFlow owns typed navigation/workspace/action/contribution contracts and stable IDs; this selection is architecture direction, not a claim that a Workstation project currently exists.
- Dock.Avalonia, native OIDC client, Windows protected-secret adapter, SQLite/encryption provider and updater are separately POC-gated focused mechanisms. Prism, Eclipse RCP, NetBeans Platform, XAF, Uno.Extensions, CSLA, Tryton and Odoo POS/client remain behavior/test donors rather than application runtimes. Dynamic runtime plugins are not baseline.
- Local Workstation success and server-authoritative acceptance are separate states (`LocalCommitted`, `PendingRemote`, `Authoritative`, `Conflict`, `Rejected`, `AuthorizationChanged`, `UpgradeRequired`) when the local-first slice is implemented.
- The accepted sync model is one semantic operation with staged authority: prepare intent + provisional projection + durable outbox locally -> authoritative server admission/commit once -> receipt-driven local reconciliation. The Workstation does not authoritatively execute a central transaction that the server then repeats.
- Each offline-capable operation owns its admission strategy: validate/current-fact computation, expected-revision conflict detection, proven convergent merge, or explicitly bounded delegated authority. Server-required operations remain online-authoritative. Revision/dependency evidence can avoid unrelated work, but current security/authorization and protected invariants are never skipped solely because a client token matches.
- Sync transports semantic intent and authoritative projections/receipts. Raw client database changesets are not a business-authority contract, and no general sync engine or CRDT framework is selected without a capability workload and invariant proof.
- SquiFlow adopts local-first interaction/durability, not a global CRDT or peer-authority model for payments, stock, credit, permissions, hard limits, or other shared invariants.
- Local-first synchronization is not described as generic eventual consistency: users can see whether work is local/pending or centrally authoritative.

## Multi-tenancy, persistence, and encryption

- Ordinary tenants use a pooled multi-tenant baseline with explicit tenant discriminators on tenant-owned authoritative data.
- Authentication, authorization, and tenant isolation are separate concerns.
- Schema-per-tenant, DB-per-tenant, queue-per-tenant, and deployment-per-tenant are not baseline.
- PostgreSQL is selected as the initial authoritative central transactional database.
- SQLite with WAL is selected as the Workstation-only embedded database for local provisional/pending/outbox state, not server authority.
- Workstation SQLite business persistence must be encrypted at rest; qualification includes database/WAL/journal/shared-memory/temp/migration/backup copies and local outbox/payload staging.
- Workstation encryption uses a device-specific DEK protected through the selected Windows/device mechanism with a centrally wrapped recovery copy managed through OpenBao/Vault. Normal offline DB startup must not depend on Admin/OpenBao connectivity.
- Guard is not a key vault.
- BitLocker/device-volume protection is defense in depth, not a replacement for application/database encryption.
- libSQL is deferred, not layered on top of SQLite.
- A dedicated NoSQL database is not baseline. Specialized stores require a named workload and explicit authority/consistency/rebuild/backup/tenancy/operations contract.
- Artwork, PDFs, images and other large unstructured objects belong behind a narrow object-store boundary when implemented; DB records retain ownership, integrity, lifecycle and hash metadata.
- Authoritative relational data is normalized around real business identities/relationships first. Denormalized/materialized read structures are derived optimizations with explicit source/freshness/rebuild/tenant-scope/failure contracts.
- Core invariants are not hidden in arbitrary JSON/EAV or tenant-specific DDL merely to avoid schema design.
- Indexes are workload-driven and their write/storage/WAL/migration/sync costs are measured.
- WebApi, SyncApi, AdminApi and Worker do not gain a local SQLite business DB.
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

- REST/task-oriented HTTP is the ordinary Web/external API baseline; SquiFlow does not require strict REST purity.
- GraphQL/Federation is deferred until a real query-composition requirement justifies its cost/authorization/schema complexity.
- CoreApi/Web API and SyncApi are separate workload hosts when synchronization is implemented, not separate business backends and not one-way ingress-only pipes.
- Reads remain module-owned first-class operations. Optimized read projections are allowed without creating a second authority.
- Retryable mutations use caller-provided semantic idempotency keys where duplicates can repeat effects; same key + changed intent is rejected.
- Where one store owns mutation + idempotency receipt + outbox, they commit atomically.
- At-least-once delivery/redelivery is assumed where durable async delivery exists; effects/usage are idempotent or explicitly reconcilable.
- Retry is finite, classified, budgeted, and uses backoff/jitter/`Retry-After` where appropriate.
- Long-running/resource-heavy work uses durable asynchronous status only when appropriate; ordinary short transactions remain synchronous.
- Conflict handling is aggregate-specific; no global last-write-wins policy.
- Large collection APIs are paginated/bounded. CoreApi currently shares one explicitly bounded, resetting Npgsql data-source pool across its capability DbContexts. External PostgreSQL middleware is not introduced; when aggregate connection evidence earns it, PgBouncer and PgDoorman are compared against the same Npgsql/EF/RLS/failure proof. Connection reuse must not leak tenant DB context.
- Caching, compression and async telemetry are selective performance techniques, not correctness mechanisms.
- Rate/admission limiting is multi-dimensional where needed and remains separate from authorization/durable quota accounting.
- Internal modular-monolith communication is in-process by default. Do not create HTTP/gRPC between ordinary modules to imitate microservices.
- gRPC is a preferred candidate, not a universal transport, for real process/service boundaries such as Workstation sync when POC evidence justifies it.
- Workstation sync semantics remain transport-independent.

## Network edge, deployment and protocol boundaries

- An edge reverse proxy/API-gateway capability may terminate TLS, route hostnames, enforce request-size/WAF/access policy, negotiate transport, and apply coarse rate limits where needed.
- Edge controls never replace backend authentication, authorization, TenantContext isolation, domain validation, idempotency/concurrency or resource-specific limits.
- Platform Admin uses a private network path; it is not exposed as a hidden route on the ordinary public tenant API.
- Current server/container direction uses Podman. Kubernetes is not baseline; revisit only after concrete multi-node orchestration/rollout/reconciliation/discovery/failover/scaling pain earns it.
- Current private Admin ingress preference is Tailscale with deny-by-default policy and trusted private ingress to private-bound Admin services.
- OpenBao is more restricted than Platform Admin and is not publicly exposed.
- Production external application traffic uses HTTPS/TLS; certificate validation is not disabled for recovery.
- HTTP transport version negotiation is infrastructure/runtime concern; application semantics do not depend on one HTTP version.
- WebSocket/SignalR, if used, is live signal/wakeup behavior only; durable truth remains in authoritative state.
- SSH/private network access is infrastructure recovery/operations only, never a normal tenant business channel.
- DNS/hostname information assists routing but is never tenant authority by itself.
- Paying-customer deployment must be reproducible from version-controlled definitions/runbooks and immutable verified artifacts when production topology is introduced.
- On a single active rack node, honest maintenance downtime can be safer than pretending zero downtime. Blue-green/canary/progressive delivery requires real topology/capacity/routing/compatibility/telemetry/recovery evidence.
- Scalability is finite and measured; add caching, replicas, sharding, extra nodes or service extraction only for identified bottlenecks.

## Rules/workflow

- SquiFlow owns the bounded native rule model; arbitrary tenant C#/JS/SQL is not allowed.
- Rules/workflows are edited/published through Web administration and distributed as immutable/versioned compatible snapshots.
- Workstation local rule evaluation cannot turn stale server-owned facts into authoritative financial/stock/security/hard-limit decisions.
- Workflow is continuation-first and versioned.
- Follow-on workflow actions use typed SquiFlow commands, durable waits/jobs or transactional outbox facts. Explicit capability-owned durable process state is the initial orchestration model; process-local events, actor messages and Quartz chaining are not continuation authority.
- Elsa and Temporal are later orchestration candidates with different fit. Elsa is closer to persisted/designed tenant definitions; Temporal is stronger for developer-authored replay-based durable execution but introduces a separate service/control plane. Neither is selected while bounded SquiFlow process state remains sufficient, and either adoption must replace overlapping scheduling/queue/execution authority for its admitted process class.

## Practical business scope

- Do not force a universal `ready-made` versus `custom-design`, separate `social`, or one quotation-type category model.
- Owner-authorized final pricing is supported inside permission/rule limits and historical issued values remain explainable rather than recomputed from later settings.
- Outsourced print/production records actual external work/cost/payable; it does not invent supplier design work.
- Inventory can use precise quantity, availability-only, non-stock/service, and damaged/unusable adjustment modes. Universal reservation, MRP/production planning, banner/roll wastage optimization, and universal lot/serial tracking are not baseline.

## Money/currency

- Currency is not hardcoded in application logic.
- A tenant has a configurable default currency code and monetary records requiring historical meaning retain the applicable currency code.
- Exact rounding/tax precision closes with the first affected production financial slice; it is not guessed globally.
- No baseline multi-currency ledger, FX provider, gain/loss accounting or currency-provider abstraction exists yet.

## Object storage and backups

- The bootstrap primary object store direction is a private Hugging Face Storage Bucket with provider/account capacity treated as a real finite limit.
- Because primary object storage is planned to change at the first paying customer, a narrow provider boundary is baseline when the first real object flow is implemented; provider SDK types do not leak into business/domain contracts.
- The bootstrap off-site backup target direction is a private Kaggle Dataset containing only encrypted opaque backup archives, never raw customer tables/files.
- Backup destination access uses a separate infrastructure-level boundary when implemented.
- Backup is infrastructure recovery, not merely an export. Recoverable state includes central DB, object data/metadata strategy, idempotency/outbox/jobs whose loss can change effects, rules/config, deployment metadata, identity/authorization configuration and independently recoverable key material according to topology.
- Encryption keys/recovery material remain separate from backup archives/destinations.
- Hugging Face/Kaggle are temporary; migrate to purpose-built paid providers at the first paying customer or earlier if requirements demand it.

## Operations and observability

- Current server hardware direction is lower-spec/desktop-class rack hardware; `stateless` does not imply automatic failover or zero downtime.
- Resource use is bounded; spare CPU/RAM is headroom rather than permission for unbounded caches/workers.
- OpenTelemetry/OTLP is the instrumentation boundary when observability is implemented. New Relic + Aiven OpenSearch are current managed targets and Backtrace remains crash-diagnostics direction.
- SquiFlow owns provider-neutral observability built on standard .NET/OpenTelemetry primitives; domain code does not depend on provider SDKs.
- `TraceId`, SquiFlow `CorrelationId`, and `CausationId` are distinct where introduced and propagated deliberately.
- Significant operational logs use stable `EventId`/`EventName`; abnormal/failure outcomes use stable `FailureCode` values where those vocabularies are implemented.
- Workstation observability is local-durable-first with bounded rotating evidence and selective central export; server observability is central-first with bounded buffering/export.
- High-cardinality identifiers belong primarily in controlled logs/traces, not ordinary unbounded metric dimensions.
- Telemetry-provider failure/quota exhaustion cannot block business correctness.
- Operational telemetry is separate from authoritative security/business audit state.

## Application security

- Application security is layered across identity, authorization, tenant/data isolation, domain/field rules, parameterized data access, browser output/forgery controls, bounded files/URLs, TLS/secrets/key management, dependency/build controls, private Admin ingress, and host defense in depth.
- Parameterized/ORM-bound data access is mandatory; untrusted values are never concatenated into SQL, and dynamic identifiers/operators use explicit allow-lists.
- Web output is encoded by default; permitted rich content is narrowly sanitized; cookie-backed mutations receive CSRF protection; exact CSP/security-header policy closes with selected Web topology.
- Server-side outbound URLs use explicit SSRF destination/scheme/redirect/size/timeout controls.
- Current containers use least privilege/minimal/pinned/secret-free/reproducible/resource-bounded images as practical when containers are introduced.
- Workstation contains no reusable central DB/platform-management credential and no plaintext local DB key in ordinary configuration/source/install paths.
- Logs/traces/crash dumps/support bundles never contain raw access tokens, cookies, provider secrets, OpenBao tokens, DEKs/KEKs/root/recovery shares, or unrestricted customer content.
- Security scanning is supporting evidence, not a substitute for tenant/resource/journey/provider/recovery tests.

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
