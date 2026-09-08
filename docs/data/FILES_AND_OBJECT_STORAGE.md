# Files, Object Storage, and Backup Bootstrap

**Version:** v0.0.15

## 1. Current bootstrap providers

The current bootstrap storage arrangement is concrete rather than hypothetical:

- **Primary business object storage:** private Hugging Face Storage Bucket.
- **Current private-storage envelope:** approximately **100 GB**.
- **Off-site backup carrier:** private Kaggle Dataset containing only encrypted opaque backup artifacts.

This is intended for the pre-paying-customer bootstrap stage. The planned migration trigger is the first paying customer, or earlier if capacity, privacy/compliance, reliability, rate limits, contractual support, or restore requirements make the bootstrap providers unsuitable.

Do not describe the current Hugging Face allocation as generic AWS `S3`. Hugging Face Storage Buckets expose S3-like/S3-compatible access, but Hugging Face is the current provider.

## 2. Storage classes

### Workstation/local filesystem
Use for:
- local database;
- unsynced/pending attachments;
- bounded temporary processing/cache;
- import/export staging explicitly controlled by the user/application.

### Server node filesystem
Use only for bounded disposable temp/processing/staging.

### Hugging Face private Storage Bucket
Use for retained business binary objects such as:
- customer artwork;
- generated/issued documents;
- retained attachments;
- product/profile images.

Temporary exports/diagnostic bundles may use it only with explicit short retention when needed.

### Business database
Stores metadata/reference such as:
- object key;
- tenant/resource ownership;
- content type;
- size/hash;
- version/lifecycle state;
- business relationship.

The database, not the bucket pathname alone, decides which tenant/business record owns an object.

## 3. No speculative storage interface hierarchy

Do **not** create `IObjectStorage`, `IStorageProvider`, one-interface-per-provider, or a provider-neutral storage project solely because migration is expected later.

For the bootstrap implementation:
- keep Hugging Face SDK/S3-compatible calls localized in infrastructure code;
- keep Hugging Face-specific types out of domain/business records;
- expose application operations in business language (attach file, fetch authorized object, retire object) rather than leaking provider SDK calls through modules;
- when the paid-provider migration actually begins, extract/replace the narrow seam needed by the migration.

Provider portability is achieved first through containment, not speculative interfaces.

## 4. Immutable business-object behavior over a mutable bucket

Hugging Face Storage Buckets are mutable/non-versioned storage, so SquiFlow must protect historical business meaning itself.

For issued/retained/versioned objects:
- use immutable/versioned application keys;
- do not silently overwrite bytes behind an issued document reference;
- store size/hash/version metadata;
- replacing an asset creates a new object reference when historical identity matters.

A mutable bucket capability is not permission to mutate issued business history.

## 5. Upload lifecycle

```text
authorize
→ validate size/type/path
→ stage/stream
→ upload
→ verify size/hash
→ commit metadata/reference
→ publish availability
```

Handle both asymmetric failure windows:
- object upload succeeds, DB metadata fails → orphan reconciliation;
- DB reference exists, object missing/corrupt → explicit unavailable/repair state.

Do not load large customer artwork wholly into RAM merely for convenience.

## 6. Capacity is real

Treat the current ~100 GB Hugging Face private-storage envelope as finite.

Track at least:
- total stored bytes;
- retained business bytes;
- temporary/expiring bytes;
- orphan/GC-candidate bytes;
- bytes by tenant when useful;
- recent growth rate.

Do not invent thresholds in architecture before measuring real file sizes. But the application/operations path must be able to warn and eventually reject optional new large work before the account limit is hit.

Never silently delete retained customer objects to make room.

## 7. File/content safety

Client filename/content type is not proof of actual content.

Where applicable:
- bound upload and expanded/decompressed size;
- sanitize paths/names;
- do not execute uploaded customer content as code;
- isolate/limit risky parsers if one is eventually required;
- unsupported/suspicious input fails validation rather than receiving unlimited native processing.

Do not create a helper process until a real parser/driver proves process isolation is needed.

## 8. Workstation attachment sync

Large local attachments stay as files plus durable metadata rather than being duplicated inside the local DB.

Track enough to detect:
- local file missing/changed;
- upload already completed;
- server metadata accepted;
- retry/resume status.

A large file transfer must not starve small semantic synchronization operations.

## 9. Printing

Printing is a Workstation side effect, not business truth.

```text
committed invoice/order
→ print request
→ Windows printer/spooler path
→ success/failure/unknown physical output
```

Printer failure does not roll back the committed business transaction. Retry or alternate-printer action is separate.

Start in-process with the normal Windows printing path. A native/helper process is added only if actual driver/library behavior proves isolation is necessary.

## 10. Kaggle backup bootstrap

Kaggle is used as a temporary off-site backup carrier, not as the live object store and not as a permanent production backup architecture.

### Never upload raw customer data to Kaggle as normal dataset files

Kaggle Datasets can process uploaded files, including unpacking recognized archives and analyzing tabular data. Therefore do not upload:
- raw DB dumps;
- CSV exports containing customer/business data;
- unencrypted object directories;
- ordinary ZIP/TAR archives containing readable customer data.

### Backup representation

Create the backup locally and encrypt it before Kaggle sees it:

```text
central DB dump
+ required configuration/metadata
+ selected object snapshot/manifest
→ package/compress locally
→ authenticated encryption locally
→ opaque backup file (for example `.sqfbak`)
→ checksum/hash
→ upload as a private Kaggle Dataset version
→ verify by downloading/checking hash
→ periodically restore-test
```

Keep backup encryption/recovery key material outside Kaggle and make sure it is itself recoverable.

The exact backup set can begin small while there is no paying customer, but it must expand to include every piece of state required for a real restore before production use.

## 11. Kaggle capacity/retention reality

Current Kaggle documentation describes:
- private datasets;
- dataset versioning/API/CLI upload/download;
- a 200 GB per-dataset limit;
- a 200 GB maximum private-dataset allocation.

Treat this as another finite bootstrap constraint, not an unlimited backup service.

Retain only the backup versions that fit the chosen bootstrap retention policy and keep at least one known-restorable off-site version. Move to purpose-built paid backup storage at the first paying customer or earlier if limits/requirements demand it.

## 12. Backup is valid only after restore

`upload succeeded` is not a backup proof.

Test:
- remote artifact can be downloaded;
- checksum matches;
- encryption key/recovery material is available;
- DB can restore;
- object metadata/bytes can reconcile;
- tenant isolation remains intact;
- idempotency/job state does not recreate completed effects unexpectedly.

## 13. Deletion/retention

Logical business deletion and physical object deletion are separate.

Physical deletion respects:
- active references;
- issued/immutable document history;
- applicable retention;
- supported long-offline Workstation behavior;
- backup/recovery needs.

Do not build a generic lifecycle framework before the first real retention rules exist.

## 14. Post-paying-customer migration

The migration target/provider is deliberately not selected now.

When migration is triggered:
1. choose paid primary object and backup providers from real workload/compliance requirements;
2. copy objects while preserving keys/hashes/tenant metadata;
3. verify counts/hashes and authorized reads;
4. switch the contained infrastructure integration;
5. keep rollback/read-only access long enough to verify;
6. perform a full restore drill on the new backup path.

The business/domain model should not need rewriting merely because the storage provider changes.
