# Deployment Capacity, Physical Hardware, and Recovery

**Version:** v0.0.15

This document grounds SquiFlow's server assumptions in the current deployment reality instead of treating owned desktop-class hardware as if it were an elastic cloud.

## 1. Current environment constraint

The current available environment includes:

- a rack of lower-spec/desktop-class server hardware;
- approximately 100 GB of currently available S3/object-storage capacity;
- managed external observability targets;
- no assumption of automatic cloud autoscaling or automatic hardware replacement.

Exact machine count, CPU/RAM, disk models, filesystem, network uplink, UPS/power protection and spare-hardware inventory must be captured before production qualification. Do not invent those values in architecture docs.

## 2. What `stateless/disposable server node` means

`Stateless` means Web/Core API/Worker process memory is not authoritative business state and a process/node can be restarted or replaced without intentionally losing committed business truth.

It does **not** mean:

- another healthy node automatically exists;
- failover is automatic;
- a failed motherboard/SSD/PSU causes zero downtime;
- the physical machine is disposable in operational practice.

Until redundant capacity and routing are actually proven, a node failure can be a real outage requiring manual recovery.

## 3. Physical durability must be qualified

Database-level ACID semantics are necessary but not sufficient to claim a physical durability objective.

The selected central/local storage stack must be tested on the hardware actually used, including:

- abrupt power loss;
- OS/process crash;
- disk-full and low-free-space behavior;
- filesystem/storage errors where reproducible;
- restart/recovery time;
- database integrity/check/repair path;
- backup restore onto replacement hardware.

UPS/power-loss protection is a deployment decision, not an implied property. If no UPS exists, the accepted durability/recovery risk must be explicit rather than hidden behind `COMMIT` semantics.

ECC memory, enterprise SSD power-loss protection, RAID/ZFS or other hardware features are **not automatically required** by this document. Their need is decided from the target RPO/RTO, measured failure risk and actual hardware budget.

## 4. Hardware inventory and node roles

Before a production topology is declared, maintain a small authoritative deployment inventory containing at least:

- node identifier;
- CPU / RAM;
- disk type/capacity/health source;
- OS/version;
- network interfaces/uplink assumptions;
- intended runtime roles;
- whether the node is a single point of failure;
- replacement/spare procedure;
- last qualification date.

Do not spread this information across architecture Markdown. It belongs in deployment configuration/runbooks and can be generated for diagnostics.

## 5. Capacity is budgeted, not inferred from free resources

Server capacity planning uses explicit budgets for:

- API concurrent work;
- DB connections;
- Worker concurrency by work class;
- report/document/image processing;
- memory/RSS;
- local temp/staging bytes;
- object storage bytes;
- outbound provider calls;
- retry volume;
- network upload/download bandwidth.

Available RAM/CPU is headroom, not permission for caches/workers to grow without bounds.

## 6. Current 100 GB object-storage ceiling

Treat the current ~100 GB object-storage capacity as a hard planning constraint, not an effectively unlimited cloud bucket.

Track usage by purpose and tenant where applicable:

- retained customer artwork/attachments;
- immutable issued/generated documents;
- product/profile assets;
- temporary exports;
- diagnostic archives;
- orphaned/unreferenced objects awaiting GC.

Backups are a separate durability concern and must **not** be assumed to fit indefinitely inside the same 100 GB primary-object budget or to make the same bucket its own only backup copy.

Before production, define configurable:

- soft warning threshold;
- critical threshold;
- hard admission behavior;
- per-tenant/default quota policy where useful;
- retention/expiry by object class;
- orphan GC policy;
- usage trend/forecast alert.

Exact percentages are deployment policy and should not be invented before measuring normal file sizes and growth.

## 7. Capacity exhaustion behavior

When capacity approaches a hard limit:

1. preserve authoritative business state;
2. do not silently delete retained customer/business objects;
3. reject/defer new optional large work with a stable `StorageCapacityExceeded`/equivalent result;
4. preferentially expire disposable caches/temp/expired diagnostics according to policy;
5. surface the reason and remediation in Platform Admin;
6. keep enough reserve for metadata/audit/recovery operations.

A full object store or local staging disk must not cascade into corrupt transaction state.

## 8. Bandwidth is also a resource

Object-store capacity is only one constraint. Measure the actual rack uplink/downlink and normal business traffic.

Large attachments, restore downloads, diagnostics and backups use:

- bounded concurrent transfers;
- resumable/multipart transfer where provider/size justifies it;
- retry/backoff without saturating the link;
- work-class priority so backups/diagnostics do not starve interactive API/sync traffic;
- observable transfer rate and backlog age.

## 9. Backup and restore are separate from primary object storage

A valid backup plan states:

- what is backed up: central DB, object metadata/bytes, durable job/idempotency state where required, configuration/secrets according to policy;
- where independent backup copies live;
- encryption/key ownership;
- backup frequency;
- retention;
- restore order;
- expected RPO/RTO;
- how a restore is verified;
- how DB/object versions are reconciled after asymmetric recovery.

`Backup succeeded` is not enough. A restore drill must prove recovery to usable business state.

The final backup target and RPO/RTO remain OPEN until deployment requirements are chosen.

## 10. Application control plane versus break-glass infrastructure recovery

Normal platform-critical operations use Platform Admin Web and audited `/platform-admin/...` commands.

However, if Admin Web/Core API itself is unavailable, recovery cannot depend on the unavailable application control plane.

A separate private infrastructure break-glass path is therefore required for tasks such as:

- restarting/redeploying a failed application process;
- replacing a failed node;
- recovering the database when the app cannot start;
- restoring configuration needed for the app to boot;
- diagnosing network/storage failure.

This path is not an alternative business/admin API. It uses least-privilege private infrastructure access (for example the selected Tailscale/Twingate/SSH/container mechanism), has a documented runbook, and records operator/recovery evidence where feasible.

## 11. Operations ownership

Before production, answer explicitly:

- who receives an outage/capacity alert;
- who can access the rack physically;
- who can perform private infrastructure recovery;
- expected support/maintenance hours;
- how customers are informed of significant outages/maintenance;
- what happens when the only qualified operator is unavailable.

Do not design an enterprise on-call organization if the actual team is tiny. Define a realistic operating model and scope the SLA accordingly.

## 12. Production qualification gates

A release/deployment is not production-qualified until it has evidence for:

- actual-node resource benchmark;
- storage free-space/capacity alerts;
- DB connection/resource bounds;
- abrupt-power/restart recovery appropriate to hardware;
- object-store exhaustion behavior;
- bandwidth/backlog behavior;
- backup + restore drill;
- single-point-of-failure inventory;
- break-glass runbook;
- telemetry-provider quota/degradation behavior;
- documented provisional/final RPO/RTO.
