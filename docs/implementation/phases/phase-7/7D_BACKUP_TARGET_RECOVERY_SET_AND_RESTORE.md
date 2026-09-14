# Phase 7D — Backup Target, Recovery Set, and Restore

## Backup boundary

Complete `IBackupTarget` with the current encrypted bootstrap provider while preserving provider replacement.

Upload only opaque encrypted backup artifacts to the bootstrap off-site target; keep encryption/recovery material separately recoverable.

The bootstrap provider is not promoted into business/domain architecture. If reliability, privacy, compliance, capacity or the first-paying-customer trigger requires migration, replace the adapter and prove restore through the new provider without changing business semantics.

## Recovery set

Back up everything required to recreate usable service at this maturity level, including as applicable:

- PostgreSQL authoritative state;
- object bytes/metadata or a proven restorable object strategy;
- idempotency/outbox/jobs;
- rules/workflow/forms/configuration;
- usage/limit state that affects enforcement;
- deployment/configuration metadata;
- provider/identity/authorization identifiers required for reprovision/reconnect;
- key references/recovery procedure, never plaintext root/DEKs in the artifact.

## Restore is the gate

Actually discover/download, verify checksum/encryption, recover keys through the separate process, restore DB/state, reconcile objects/jobs/usage and start services.

## Failure tests

Corrupt/missing artifact, wrong key, interrupted restore, restored stale job/idempotency state, object mismatch, usage counter reset/double, provider unavailable.

## Exit gate

A backup is not considered successful until a real restore produces usable, tenant-isolated SquiFlow state.