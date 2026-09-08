# Deployment Capacity, Physical Hardware, and Recovery

**Version:** v0.0.15

This document grounds SquiFlow in the current lower-spec owned rack and bootstrap external-storage reality. It does not pretend the environment is an elastic cloud.

## 1. Current environment

Current known constraints:
- lower-spec/desktop-class rack hardware;
- primary business-object storage on a **private Hugging Face Storage Bucket**;
- approximately **100 GB** current private Hugging Face storage envelope;
- temporary off-site backup carrier on a **private Kaggle Dataset** using encrypted opaque backup files;
- managed external observability;
- no assumption of automatic hardware replacement, autoscaling, or failover.

Exact rack machine count, CPU/RAM, disk/filesystem, network/uplink, power protection and spare inventory remain deployment facts to capture before production.

## 2. `Stateless` does not mean `high availability`

Web/Core API/future Worker process memory is not authoritative business state.

That does not mean:
- another node exists;
- failover is automatic;
- a disk/PSU/motherboard/network failure has zero downtime.

Until redundancy is actually implemented and tested, physical failure can require manual recovery.

## 3. Physical durability must be tested

The selected central/local database stack must be tested on the actual hardware class for:
- process/OS restart;
- abrupt power loss where practical/safe to reproduce;
- disk full/low-space behavior;
- database recovery/integrity checks;
- restore onto replacement hardware;
- measured restart/recovery time.

UPS, ECC, RAID/ZFS, enterprise SSDs, and similar hardware are not automatically required by architecture. Decide them from real RPO/RTO/risk/budget.

## 4. Capacity is bounded

Explicit budgets eventually cover:
- API concurrency;
- DB connections;
- future Worker concurrency;
- document/image work;
- memory/RSS;
- local staging/temp bytes;
- Hugging Face object bytes;
- backup bytes;
- provider requests/retries;
- network bandwidth.

Available CPU/RAM is headroom, not permission for unbounded cache/worker growth.

## 5. Hugging Face object capacity

Treat the current ~100 GB private Hugging Face capacity as finite.

Measure:
- total bytes;
- retained business objects;
- temporary/expiring objects;
- orphan candidates;
- bytes by tenant where useful;
- growth rate.

Do not silently delete retained business objects as the account approaches its limit. Reject/defer optional new large work before hard exhaustion when necessary.

The planned migration to a paid object-storage provider occurs when the first paying customer arrives, or earlier if Hugging Face capacity/rate/reliability/privacy/compliance requirements demand it.

## 6. Bandwidth is a real resource

Measure actual rack uplink/downlink.

Large uploads, restores, diagnostics and backups use bounded concurrent transfers and must not starve normal API/Workstation synchronization.

Do not add a complicated traffic scheduler before measurement shows one is needed; start with small concurrency limits and observe.

## 7. Kaggle backup bootstrap

The current off-site backup carrier is a private Kaggle Dataset.

Important constraints:
- upload only opaque **encrypted** backup artifacts;
- never upload raw customer DB/CSV/object files to Kaggle;
- keep the encryption key outside Kaggle and recoverable;
- verify remote download + checksum;
- perform actual restore drills;
- treat Kaggle private-storage limits/versioning as finite.

The backup pipeline can remain simple before paying customers, but the first paying customer is the planned trigger to move to a purpose-built paid backup arrangement. Migrate earlier if capacity/security/automation/restore requirements demand it.

## 8. Backup/restore contract

A useful backup identifies what is required to rebuild service:
- central DB;
- required object bytes/metadata;
- durable job/idempotency state where those exist;
- required configuration;
- secrets/key recovery through a separate safe process.

`Backup uploaded` is not enough. Restore must prove the application starts and tenant-isolated business state is usable.

Exact customer-facing RPO/RTO remain OPEN until a real production/commercial requirement exists.

## 9. Break-glass infrastructure recovery

Normal platform-control operations use future Platform Admin Web.

If the app/control plane itself is unavailable, private infrastructure recovery may be needed for:
- process restart/redeploy;
- node replacement;
- DB recovery;
- network/config repair required for the app to boot.

This is not a hidden Desktop/business API. Keep it private, least privilege, runbook-driven, and as small as the actual operating model allows.

## 10. Operator ownership

Before production answer:
- who receives alerts;
- who can physically access/recover the rack;
- who has private infrastructure credentials;
- what maintenance/support promise is realistic;
- what happens if that operator is unavailable.

Do not invent an enterprise on-call organization for a tiny team.

## 11. Production qualification

Before accepting paying-customer production traffic, prove at least:
- actual-node resource benchmark;
- DB/restart/disk-full recovery;
- Hugging Face capacity monitoring and migration readiness;
- encrypted Kaggle backup download/restore;
- single-point-of-failure inventory;
- private recovery runbook;
- provisional RPO/RTO;
- migration plan to purpose-built paid object/backup storage.
