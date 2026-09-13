# Workstation Encryption and Key Recovery

**Status:** Accepted architecture direction  
**Version:** v0.0.19

## 1. Decision

SquiFlow Workstation local business persistence must be encrypted at rest. SQLite/WAL remains the selected local database, but the exact SQLite encryption implementation remains a Phase-2 qualification choice rather than being prematurely hard-coded into domain/application architecture.

The requirement applies to the real durability surface, not only `workstation.db`.

Qualification must account for:

- main SQLite database;
- WAL/journal behavior;
- shared-memory/temporary artifacts where relevant;
- migration/backup copies;
- local outbox/provisional state;
- sensitive local payload/object staging;
- recovery checkpoints.

## 2. Device-specific data-encryption keys

Do not use one global SquiFlow master key directly as every Workstation SQLite key.

Each Workstation uses a device-specific Data Encryption Key (DEK):

```text
OpenBao/Vault KEK
       │
       │ wraps
       ▼
Device-specific DEK
       │
       ▼
encrypted SQLite/WAL
```

The device DEK is generated with cryptographically secure random material by the selected key-management flow.

## 3. Normal offline startup

SquiFlow is local-first. A Workstation must not require the Platform Admin service or OpenBao to be online every time the local database opens.

The local usable DEK is protected using the selected Windows/device protection mechanism, with DPAPI/TPM-backed protection as the preferred direction to qualify.

```text
Workstation process
      │
      ▼
Windows protected local key material
(DPAPI/TPM-backed where qualified)
      │
      ▼
Device DEK
      │
      ▼
encrypted SQLite
```

The key must not be stored as plaintext in `appsettings.json`, source, the installation directory, a normal environment variable, log output, or beside the database.

## 4. Central recovery without continuous central dependency

During provisioning/rekeying, maintain a recovery-wrapped representation of the device DEK under OpenBao/Vault key management.

Conceptually:

```text
Generate Device DEK
        │
        ├── local usable copy -> protected by Windows/device protection
        │
        └── recovery copy -> wrapped by OpenBao/Vault KEK
```

The central copy is wrapped/encrypted key material, not an ordinary plaintext database key exposed to Platform Admin.

If a local Windows binding is lost because of device repair/reprovisioning, an explicitly authorized recovery flow may unwrap/reprovision the DEK onto a replacement authorized device context.

## 5. Recovery is privileged

Device-key recovery requires:

- valid Platform Admin identity;
- approved/registered Admin device;
- operation-specific platform authorization;
- physical security/recovery factor where policy requires it;
- reason/approval where configured;
- authoritative audit.

The recovery path must not be callable by ordinary tenant Web/Workstation users or by Guard.

## 6. Guard does not own decryption keys

`SquiFlow.Guard` remains a small lifecycle/recovery coordinator. It does not permanently hold SQLite DEKs, OpenBao/Vault root credentials, KEKs, or central recovery credentials.

Guard may coordinate:

```text
checkpoint required
    │
    ▼
Maintenance/Backup capability
    │
    ▼
encrypted verified checkpoint
    │
    ▼
Guard permits migration/update
```

The process that legitimately performs DB backup/migration obtains only the minimum key access required for the operation and according to the chosen local security design.

## 7. Migration/update backups stay encrypted

An encrypted live database must not become plaintext during update safety processing.

Therefore:

- update checkpoints remain encrypted/protected;
- migration snapshots remain encrypted/protected;
- rollback copies remain encrypted/protected;
- unsynchronized payloads in a checkpoint retain equivalent protection;
- temporary decrypted migration output, if an implementation absolutely requires it, is minimized, access-restricted, short-lived, cleaned on failure/restart, and treated as a security-sensitive exception to prove during qualification.

`encrypted database -> plaintext backup` is not an acceptable default.

## 8. Recovery checkpoint metadata

Checkpoint manifests may contain identifiers/version/hash metadata needed for restore without containing raw encryption keys.

They should record encryption-relevant metadata such as:

```text
EncryptionProfileId
KeyReference / KeyVersion
CheckpointFormatVersion
DatabaseSchemaVersion
Integrity hashes
```

They must never include plaintext DEK/KEK/root/recovery material.

## 9. BitLocker/device-volume encryption is defense in depth

Windows BitLocker/device-volume encryption is strongly useful where available and should be part of deployment qualification, but SquiFlow must not assume every customer device is correctly BitLocker-managed.

Therefore:

```text
volume encryption
      +
application/database encryption
```

are complementary layers rather than interchangeable guarantees.

Volume encryption protects lost/offline storage. Database encryption additionally protects copied database artifacts according to the selected implementation. Neither protects plaintext after a fully compromised authorized running process has decrypted it.

## 10. Device revocation limitations

Server-side device revocation can stop future authentication/synchronization/recovery/key-rotation participation, but cannot magically erase plaintext already available to a stolen running/unlocked Workstation.

Revocation should therefore combine as applicable:

- device credential/session revocation;
- Sync/API denial;
- future key/recovery operations denied;
- future rotations exclude the device;
- operator/tenant-visible device state;
- incident/audit evidence.

Do not claim remote revocation equals secure remote erasure unless a separately implemented and proven mechanism exists.

## 11. Workstation objects and files outside SQLite

SQLite encryption does not automatically encrypt artwork, PDFs, exports, thumbnails, print files, upload staging, diagnostics, or other filesystem artifacts.

Classify local files:

- disposable/reconstructable cache: bounded lifetime and OS/device-volume protection may be sufficient;
- sensitive durable/pending business artifact: requires protection appropriate to its classification and lifecycle;
- diagnostic/support artifact: redact/package/protect according to diagnostics policy.

Deletion/cleanup behavior is explicit for success, failure, crash, restart, and update/recovery paths.

## 12. Local observability

Never log:

- local DB DEKs;
- Windows protected-key blobs as if they were harmless configuration;
- OpenBao/Vault tokens;
- recovery material;
- full sensitive DB rows merely to debug encryption failures.

Encryption failure events use stable failure codes and safe identifiers such as key reference/version, not secret bytes.

## 13. Qualification gate

Before paying-customer use, the Workstation encryption POC must prove on supported Windows/.NET packaging:

- selected SQLite encryption implementation works with the chosen WAL configuration;
- crash/restart and abrupt-shutdown recovery;
- WAL/journal/temp behavior does not expose plaintext unexpectedly;
- migration/update checkpoint remains encrypted and restorable;
- local protected DEK survives expected restart/update lifecycle;
- authorized recovery/reprovisioning works after simulated loss of local protected-key binding;
- unauthorized copy of DB/checkpoint cannot be opened without required key material;
- key rotation/rekey behavior is bounded and recoverable;
- disk-full/low-space behavior does not silently leave dangerous plaintext artifacts;
- backup/restore works with independently recovered key material;
- performance/resource cost is acceptable on the target Workstation class.

## 14. Architectural invariant

> The Workstation can decrypt its own local database while legitimately offline, but the database key is device-specific, locally protected, centrally recoverable only through an audited zero-trust recovery workflow, and never stored as ordinary plaintext configuration. Guard coordinates recovery; it is not a key vault.

Related owners:

- `docs/data/PERSISTENCE_SELECTION.md`
- `docs/workstation/UPDATE_MIGRATION_RECOVERY_OWNERSHIP.md`
- `docs/security/ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md`
