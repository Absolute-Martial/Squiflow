# Files, Object Storage, and Local Staging

**Version:** v0.0.15

## 1. Storage classes

### Workstation/local filesystem
Use for:
- temporary processing;
- cache;
- local diagnostics staging;
- unsynced/pending attachments;
- import/export staging.

### Server node filesystem
Use only for disposable temp/cache/processing/diagnostic staging.

### Primary durable object storage
Use for retained business binary objects such as:
- customer artwork;
- generated/issued documents;
- retained attachments;
- product/profile images.

Temporary exports and diagnostic archives can also use object storage **only under explicit retention/capacity policy**.

### Backup storage
Backup copies are a separate durability class. Do not assume the primary business-object bucket is also its own only backup merely because it is remote/object storage.

The final independent backup target is an OPEN deployment decision.

### Business database
Stores metadata/reference:
- object ID/key;
- tenant/resource ownership;
- content type;
- size/hash;
- version;
- lifecycle state;
- retention/storage class;
- business relationship.

Do not mount object storage as if it were the application's authoritative ordinary filesystem.

## 2. Current capacity reality

The currently available object-storage envelope is approximately **100 GB**.

Treat that as a planning constraint, not unlimited cloud capacity.

Before production, track at least:
- total bytes/objects;
- bytes by tenant where applicable;
- bytes by object class;
- growth rate;
- orphan/GC-candidate bytes;
- expiring temporary export/diagnostic bytes.

Define configurable warning/critical/hard-admission thresholds after measuring realistic customer artwork/document sizes. Do not invent arbitrary percentages in the architecture.

When capacity is constrained, protect retained business objects first. Expire only data whose retention policy already permits deletion; never silently delete customer files or unsynced Workstation attachments to recover capacity.

See `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

## 3. Upload lifecycle

```text
authorize
→ validate type/size/content
→ stage/stream
→ upload object
→ verify size/hash
→ commit metadata/reference
→ publish availability
```

Handle both failure windows:
- object succeeded, metadata failed → orphan reconciliation/garbage collection;
- metadata exists, object missing → unavailable/corrupt state and repair workflow.

Large transfers should stream rather than load whole files into RAM. Resumable/multipart upload is used when provider/file-size/network evidence justifies it.

## 4. File/content security

Do not trust filename extension or client-provided content type as proof of file content.

Depending on the file class and supported processing path, enforce:
- allowlisted types where appropriate;
- maximum compressed and expanded size;
- decompression-bomb/resource limits;
- safe filename/path handling;
- metadata stripping/normalization where required;
- quarantine/scanning before risky server-side processing when the threat model justifies it;
- no execution of uploaded customer files as application code.

A suspicious/unsupported file is a validation/quarantine result, not a reason to let a native helper parse arbitrary bytes without limits.

## 5. Immutable/versioned keys

Prefer immutable/versioned/content-hash-style object identities for assets/documents where appropriate.

Updating a profile/product image should create a new object reference rather than silently replacing bytes behind a long-lived cache key.

## 6. CDN/cache

CDN/node cache is derived/rebuildable and never the master copy.

Use it to reduce repeated object-store reads/egress for hot assets.

Authenticated/private tenant objects require an explicit authorization/cache policy; do not accidentally make private files publicly cacheable.

## 7. Provider neutrality

SquiFlow code depends on a storage abstraction/policy rather than scattered B2/MEGA/S3/provider calls.

Provider choice/tiering remains a current evaluation decision and should consider:
- access frequency;
- retention;
- egress economics;
- latency/region;
- durability/security;
- available capacity/quota;
- backup/restore needs;
- current contractual/compliance requirements.

The current ~100 GB allocation is a deployment envelope, not a permanent provider architecture decision.

## 8. Workstation attachment sync

Large local attachments can be referenced from the local outbox without duplicating the entire binary into the local database.

Required local metadata includes enough to detect:
- file missing;
- file changed since queueing;
- upload already completed;
- server metadata accepted;
- retry/resume status.

Do not mark the business attachment authoritative until the required durable object + metadata lifecycle completes.

Attachment transfer is scheduled separately enough that one giant file cannot starve small semantic sync operations.

## 9. Local/server staging limits

Both Workstation and server staging areas are bounded.

Track:
- bytes in use;
- age;
- owning operation/tenant where applicable;
- whether data is safe to discard;
- whether data is unsynced business evidence.

A cleanup process can remove expired disposable temp files, but must never classify an unsynced attachment as disposable merely because it is old.

Low-space behavior should stop optional new heavy work before the disk becomes completely full and explain the problem to the user/operator.

## 10. Printing

Printing is a side effect, not business truth.

If an invoice/order commits but the printer fails/out-of-paper/driver crashes, the transaction remains committed and the print attempt remains separately retryable.

The device/process boundary is defined in `docs/workstation/GUARD_AND_DEVICE_INTEGRATION.md`.

## 11. Deletion/retention

Logical business deletion/reference removal and physical object deletion are separate.

Physical garbage collection must respect:
- active references;
- retention/legal policy;
- backup/restore strategy;
- immutable issued documents;
- sync/offline references where applicable;
- supported long-offline window/tombstone policy.

A Workstation returning after a supported offline interval must not find a required referenced object physically purged solely because central GC ignored offline retention semantics.

## 12. Restore/reconciliation

Restore planning must account for object/database version skew.

Test:
- DB restored to a point before/after an object upload;
- object store restored independently from DB;
- idempotent re-upload/relink where safe;
- missing immutable issued document;
- orphan objects after restore;
- object version/hash verification.

Primary object storage, metadata DB and backup copies form one recovery story even though they are separate systems.