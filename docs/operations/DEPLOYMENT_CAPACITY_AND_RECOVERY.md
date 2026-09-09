# Deployment Capacity, Physical Hardware, and Recovery

**Version:** v0.0.15

This document grounds SquiFlow in the current lower-spec owned rack and bootstrap external-service reality. It does not pretend the environment is an elastic cloud.

## 1. Current environment

Current known constraints:
- lower-spec/desktop-class rack hardware;
- primary business-object storage on a private Hugging Face Storage Bucket behind `IObjectStore`;
- approximately 100 GB current private Hugging Face storage envelope;
- temporary off-site backup carrier on a private Kaggle Dataset behind infrastructure-level `IBackupTarget`, using encrypted opaque backup files;
- ZITADEL selected for identity/authentication;
- OpenFGA selected for application authorization;
- managed external observability;
- no assumption of automatic hardware replacement, autoscaling, or failover.

Exact rack machine count, CPU/RAM, disk/filesystem, network/uplink, power protection and spare inventory remain deployment facts to capture before production.

## 2. `Stateless` does not mean `high availability`

Web/Core API/future Worker process memory is not authoritative business state.

That does not mean:
- another node exists;
- failover is automatic;
- a disk/PSU/motherboard/network failure has zero downtime.

Until redundancy is implemented and tested, physical failure can require manual recovery.

## 3. Reproducible deployment is required

The paying-customer deployment must not exist only as undocumented shell commands or one administrator's memory.

Keep version-controlled deployment/infrastructure definitions and runbooks sufficient to recreate the intended environment, including as applicable:
- Core API/Admin API/Worker service definitions;
- edge/reverse-proxy routing and TLS configuration;
- environment/bootstrap configuration placement;
- database provisioning/migration procedure;
- backup scheduling and `IBackupTarget` configuration;
- observability exporters/agents;
- process restart/shutdown/resource limits;
- private recovery access prerequisites.

This is an infrastructure/deployment concern. It does **not** mean tenant or platform operators should normally edit YAML/Terraform files for business/application settings; those remain first-class Web/Admin API concerns where implemented.

The exact IaC/automation mechanism is OPEN. A simple version-controlled host/container/service setup is valid if it is reproducible and testable. Do not introduce Kubernetes, Flux, Terraform or another platform solely to claim IaC/GitOps.

Production qualification includes rebuilding SquiFlow on a clean/replacement environment using these definitions/runbooks rather than relying on the original machine state.

### 3.1 Release, migration, and recovery contract

The first production profile needs an explicit release procedure appropriate to its real node count and spare capacity.

Baseline requirements:

- CI produces one immutable versioned artifact (and container image if containers are selected) with checksum/provenance evidence;
- promote the same verified bytes between environments rather than rebuilding environment-specific binaries;
- keep environment configuration/secrets outside the artifact;
- run configuration, capacity/free-space, dependency, and database-migration preflight checks;
- ensure API/schema/durable-work compatibility for supported old/new processes and skipped Workstations;
- drain or bound in-flight requests/jobs where the change requires it;
- deploy, then evaluate process health **and** a small authorized smoke journey rather than trusting process existence alone;
- state whether failure uses binary rollback, database roll-forward, maintenance restore, or another tested recovery path;
- retain the previous known-good artifact and the evidence/runbook needed to operate it for the supported rollback window;
- record who approves, performs, observes, and can stop/recover the release without inventing a large-team ceremony.

On a single active rack node, a maintenance window with honest downtime can be safer than pretending to provide zero downtime. Blue-green, canary, rolling, or feature-flagged exposure is adopted only when the deployment has the spare capacity/routing, compatible data contracts, observability, and rollback controls to make that strategy real.

Database rollback is not assumed. Many schema/data migrations are safer through compatible expand-migrate-switch-contract and roll-forward. See `docs/data/PERSISTENCE_SELECTION.md`.

## 4. Containerization versus orchestration

Containerization is allowed when it improves packaging, dependency isolation, reproducibility or deployment consistency.

Kubernetes is **not** baseline. It becomes a candidate only when concrete multi-node orchestration problems repeatedly appear, such as:
- manual placement/reconciliation of replicas;
- rollout/rollback coordination across many instances;
- service discovery becoming operationally fragile;
- automated failover/replacement requirements;
- scaling/placement policy becoming difficult to operate with the simpler deployment.

Having more than one container or more than one server is not by itself sufficient evidence for Kubernetes.

### Conditional container hardening

If the selected profile uses containers:

- use trusted minimal base images pinned to reviewed versions/digests;
- build reproducibly and scan the final image/dependencies;
- do not bake production secrets into image layers;
- run as non-root/least privilege where practical;
- expose only required ports/capabilities and define writable-storage needs;
- set health, shutdown/drain, CPU/memory, temp/disk, and log bounds;
- promote the same verified image rather than rebuilding per environment.

These are conditional safety requirements, not a decision to use Docker or Kubernetes.

## 5. Physical durability must be tested

The selected central/local database stack must be tested on the actual hardware class for:
- process/OS restart;
- abrupt power loss where practical/safe to reproduce;
- disk full/low-space behavior;
- DB recovery/integrity checks;
- restore onto replacement hardware;
- measured restart/recovery time.

If PostgreSQL is selected, also observe connection/backend-process resource cost, WAL growth, checkpoints, autovacuum, temp spill and archive/log growth under the actual SquiFlow burst workload. Logical SQL correctness alone is not sufficient qualification on a small rack.

UPS, ECC, RAID/ZFS, enterprise SSDs and similar hardware are not automatically required. Decide them from actual RPO/RTO/risk/budget.

## 6. Workstation/Guard deployment reality

The desktop baseline is:

```text
SquiFlow.Guard
└── SquiFlow.Workstation
```

Resource qualification measures them together and separately.

Guard must remain low-resource and bounded, but operational qualification is based on whether it reliably performs launch/supervision/hang/crash/update recovery—not on an arbitrary tiny memory target.

A Guard crash must not destroy Workstation business state; a Workstation crash must be observable/recoverable by Guard. See `docs/workstation/GUARD_AND_RECOVERY.md`.

## 7. Capacity is bounded, and scalability is profile-specific

SquiFlow does not claim infinite scalability.

Each deployment profile needs:
- a measured workload/capacity envelope;
- the first-order bottlenecks;
- warning/critical evidence;
- the next scaling/recovery move for each bottleneck.

Candidate first-order bottlenecks include:
- database connection/query/lock/WAL pressure;
- CPU-heavy document/image/report processing;
- Worker backlog/queue age;
- object-transfer/network bandwidth;
- ZITADEL/OpenFGA/provider latency or limits;
- disk capacity/I/O;
- one physical node becoming saturated or unavailable.

Do **not** choose the remedy before proving the bottleneck. Examples:
- DB query/index problem → fix query/schema/index before adding replicas;
- expensive document work → isolate/bound Worker concurrency before sharding data;
- uplink saturation → schedule/bound transfers before adding application nodes;
- one-node capacity/recovery limit → add a node/load-balancing/failover only when the measured requirement justifies it.

Caching, read replicas, sharding, distributed databases, service extraction or Kubernetes are scaling techniques, not baseline requirements.

Explicit budgets eventually cover:
- API concurrency;
- DB connections;
- future Worker concurrency;
- ZITADEL/OpenFGA dependency requests and timeout/retry behavior;
- document/image work;
- Workstation + Guard memory/process-tree behavior;
- local staging/temp bytes;
- Hugging Face object bytes;
- backup bytes;
- provider requests/retries;
- network bandwidth.

Available CPU/RAM is headroom, not permission for unbounded cache/worker/resource growth.

## 8. Hugging Face object capacity and provider migration

Treat current ~100 GB private Hugging Face capacity as finite.

Measure:
- total bytes;
- retained business objects;
- temporary/expiring objects;
- orphan candidates;
- bytes by tenant where useful;
- growth rate.

Do not silently delete retained business objects as the account approaches its limit. Reject/defer optional new large work before hard exhaustion when necessary.

Runtime code uses `IObjectStore`, implemented initially by `HuggingFaceObjectStore`, so the first-paying-customer migration does not require rewriting business/application code.

Migration to a paid object-storage provider occurs at the first paying customer, or earlier if capacity/rate/reliability/privacy/compliance requirements demand it.

## 9. Bandwidth is a real resource

Measure actual rack uplink/downlink.

Large uploads, restores, diagnostics and backups use bounded concurrent transfers and must not starve normal API/Workstation synchronization.

Start with explicit small limits; add a more elaborate scheduler only if measurement proves it necessary.

## 10. Backup is infrastructure recovery

Backup is not only an application-level export.

The recovery set must include all state needed to rebuild usable SquiFlow service, according to actual deployment topology, including as applicable:
- central DB;
- object bytes/metadata or a proven restorable object snapshot strategy;
- idempotency/outbox/job state whose loss could recreate/lose effects;
- rules/workflows/configuration;
- deployment/configuration metadata required to recreate services;
- provider/integration configuration needed to reconnect ZITADEL/OpenFGA/storage;
- independently recoverable secrets/key material through a separate secure process.

Managed services and self-hosted services require different recovery plans. If ZITADEL/OpenFGA are managed, backup may emphasize reproducible configuration/export/reprovision evidence. If self-hosted, their supported DB/config backup and restore becomes part of infrastructure recovery.

## 11. Kaggle backup bootstrap through `IBackupTarget`

Current off-site backup carrier is private Kaggle.

The backup destination is accessed through:

```text
IBackupTarget
└── KaggleBackupTarget
```

Rules:
- upload only opaque encrypted backup artifacts;
- never upload raw customer DB/CSV/object files directly;
- keep encryption key material outside Kaggle and recoverable;
- verify remote list/download/checksum;
- perform actual restore drills;
- treat Kaggle storage/versioning limits as finite.

The first paying customer is the planned trigger to implement a purpose-built paid `IBackupTarget` adapter and migrate. Migrate earlier if requirements already demand it.

## 12. Backup/restore contract

`Backup uploaded` is not enough.

Restore must prove:
- required artifacts can be discovered/downloaded;
- checksums and encryption verify;
- DB restores;
- object references/bytes reconcile;
- idempotency/job state is safe;
- ZITADEL/OpenFGA can be restored/reprovisioned/reconnected according to topology;
- Core API starts with correct tenant isolation/authorization behavior;
- a Workstation/Guard can reconnect without losing pending local work;
- operator has the required key/recovery material.

Exact customer-facing RPO/RTO remain OPEN until production/commercial requirements are chosen.

## 13. Identity/authorization dependency recovery

ZITADEL/OpenFGA are security-critical dependencies.

Recovery design must answer:
- managed vs self-hosted ownership;
- service credentials/secrets and rotation recovery;
- configuration/model/store identifiers needed after redeploy;
- OpenFGA authorization model ID and tuple-state recovery/reconciliation;
- what operations fail closed if either dependency is unavailable;
- how operators distinguish provider outage from application/configuration defect.

Do not create a local bypass that grants authorization merely because OpenFGA/ZITADEL is down.

## 14. Edge/DNS/TLS/time failure is part of production recovery

The edge, DNS, certificate chain and clock synchronization can make a healthy application unreachable or unable to authenticate.

Before production, prove/document:
- what happens if the public edge/reverse proxy is down while the application hosts are healthy;
- private recovery access that does not depend on the same broken public edge;
- certificate issuance/renewal/expiry monitoring and response;
- DNS failure/misconfiguration recovery;
- acceptable/monitored clock skew for OIDC/TLS/leases/schedules;
- no insecure HTTP/TLS-validation bypass is used as a recovery shortcut.

## 15. Break-glass infrastructure recovery

Normal platform controls use future Platform Admin Web.

If the application/control plane itself is unavailable, private infrastructure recovery may be needed for:
- process restart/redeploy;
- node replacement;
- DB recovery;
- network/config repair;
- ZITADEL/OpenFGA/storage connectivity/configuration recovery required for the app to boot.

This is not a hidden Desktop/business API. Keep it private, least privilege, runbook-driven and auditable/evidenced where feasible.

## 16. Operator ownership

Before production answer:
- who receives alerts;
- who can physically access/recover the rack;
- who has private infrastructure credentials;
- who can manage ZITADEL/OpenFGA/storage provider configuration;
- what maintenance/support promise is realistic;
- what happens if the qualified operator is unavailable.

Do not invent an enterprise on-call organization for a tiny team.

## 17. Production qualification

Before accepting paying-customer production traffic, prove at least:
- actual-node resource benchmark;
- Workstation/Guard recovery/resource benchmark;
- DB/restart/disk-full recovery;
- explicit database workload profile and burst test;
- Hugging Face capacity monitoring and `IObjectStore` migration readiness;
- encrypted Kaggle `IBackupTarget` download/restore;
- ZITADEL/OpenFGA recovery/reprovision procedure appropriate to selected deployment mode;
- single-point-of-failure inventory;
- public-edge/DNS/TLS/time failure and private recovery path;
- clean/replacement-environment rebuild from version-controlled deployment definitions/runbook;
- immutable-artifact promotion plus preflight/migration/health/smoke evidence;
- failed-release drill proving the documented rollback/roll-forward/maintenance recovery path without corrupting authoritative or pending local work;
- supported cross-version/schema/durable-work compatibility and obsolete-reader/writer drain evidence before destructive contraction;
- measured capacity envelope plus next scaling move for first-order bottlenecks;
- private recovery runbook;
- provisional RPO/RTO;
- migration plan to paid object/backup providers.
