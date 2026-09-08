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

## 3. Physical durability must be tested

The selected central/local database stack must be tested on the actual hardware class for:
- process/OS restart;
- abrupt power loss where practical/safe to reproduce;
- disk full/low-space behavior;
- DB recovery/integrity checks;
- restore onto replacement hardware;
- measured restart/recovery time.

UPS, ECC, RAID/ZFS, enterprise SSDs and similar hardware are not automatically required. Decide them from actual RPO/RTO/risk/budget.

## 4. Workstation/Guard deployment reality

The desktop baseline is:

```text
SquiFlow.Guard
└── SquiFlow.Workstation
```

Resource qualification measures them together and separately.

Guard must remain low-resource and bounded, but operational qualification is based on whether it reliably performs launch/supervision/hang/crash/update recovery—not on an arbitrary tiny memory target.

A Guard crash must not destroy Workstation business state; a Workstation crash must be observable/recoverable by Guard. See `docs/workstation/GUARD_AND_RECOVERY.md`.

## 5. Capacity is bounded

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

## 6. Hugging Face object capacity and provider migration

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

## 7. Bandwidth is a real resource

Measure actual rack uplink/downlink.

Large uploads, restores, diagnostics and backups use bounded concurrent transfers and must not starve normal API/Workstation synchronization.

Start with explicit small limits; add a more elaborate scheduler only if measurement proves it necessary.

## 8. Backup is infrastructure recovery

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

## 9. Kaggle backup bootstrap through `IBackupTarget`

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

## 10. Backup/restore contract

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

## 11. Identity/authorization dependency recovery

ZITADEL/OpenFGA are security-critical dependencies.

Recovery design must answer:
- managed vs self-hosted ownership;
- service credentials/secrets and rotation recovery;
- configuration/model/store identifiers needed after redeploy;
- OpenFGA authorization model ID and tuple-state recovery/reconciliation;
- what operations fail closed if either dependency is unavailable;
- how operators distinguish provider outage from application/configuration defect.

Do not create a local bypass that grants authorization merely because OpenFGA/ZITADEL is down.

## 12. Break-glass infrastructure recovery

Normal platform controls use future Platform Admin Web.

If the application/control plane itself is unavailable, private infrastructure recovery may be needed for:
- process restart/redeploy;
- node replacement;
- DB recovery;
- network/config repair;
- ZITADEL/OpenFGA/storage connectivity/configuration recovery required for the app to boot.

This is not a hidden Desktop/business API. Keep it private, least privilege, runbook-driven and auditable/evidenced where feasible.

## 13. Operator ownership

Before production answer:
- who receives alerts;
- who can physically access/recover the rack;
- who has private infrastructure credentials;
- who can manage ZITADEL/OpenFGA/storage provider configuration;
- what maintenance/support promise is realistic;
- what happens if the qualified operator is unavailable.

Do not invent an enterprise on-call organization for a tiny team.

## 14. Production qualification

Before accepting paying-customer production traffic, prove at least:
- actual-node resource benchmark;
- Workstation/Guard recovery/resource benchmark;
- DB/restart/disk-full recovery;
- Hugging Face capacity monitoring and `IObjectStore` migration readiness;
- encrypted Kaggle `IBackupTarget` download/restore;
- ZITADEL/OpenFGA recovery/reprovision procedure appropriate to selected deployment mode;
- single-point-of-failure inventory;
- private recovery runbook;
- provisional RPO/RTO;
- migration plan to paid object/backup providers.
