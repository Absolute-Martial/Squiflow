# Files, Object Storage, and Backup Bootstrap

**Version:** v0.1.0

## 1. Current bootstrap providers

The current bootstrap arrangement is concrete:

- **Primary business object storage:** private Hugging Face Storage Bucket.
- **Current private-storage envelope:** approximately **100 GB**.
- **Off-site backup carrier:** private Kaggle Dataset containing only encrypted opaque backup artifacts.

Both are intentionally temporary bootstrap providers. The planned migration trigger is the first paying customer, or earlier if capacity, privacy/compliance, reliability, rate limits, contractual support, or restore requirements make either provider unsuitable.

Because provider replacement is already planned and near-term, SquiFlow **does use narrow provider interfaces here**. This is a justified abstraction, not a generic one-interface-per-class convention.

## 2. Stable provider boundaries

### `IObjectStore`

Application/runtime code that needs retained business objects depends on a narrow SquiFlow object-store contract rather than Hugging Face APIs directly.

Conceptual operations are limited to SquiFlow needs such as:

```text
Put/OpenRead
Exists/GetMetadata when required
Delete/Retire when policy permits
```

The interface uses SquiFlow-owned request/result types: object key, content stream, expected size/hash, metadata needed for lifecycle and authorization. It must not expose Hugging Face SDK/S3-specific request/response types.

Bootstrap adapter:

```text
IObjectStore
└── HuggingFaceObjectStore
```

After the paying-customer migration a new adapter can implement the same contract while the business/application code remains unchanged.

Do not bloat `IObjectStore` into every feature a future cloud provider might offer. Provider-specific migration/admin tooling may use provider APIs directly inside infrastructure tooling when the generic runtime contract is not appropriate.

### `IBackupTarget`

Backup upload/download/version-list/delete/retention-provider operations use a separate infrastructure-level contract:

```text
IBackupTarget
└── KaggleBackupTarget
```

This interface is **not a business-domain service**. It belongs to infrastructure/operations because backups include more than application rows and because the backup destination is explicitly expected to change.

Conceptual provider operations include only what backup orchestration needs:

```text
UploadEncryptedArtifact
DownloadArtifact
List/Inspect retained backup versions
Delete expired backup artifact according to policy
```

The target receives already packaged/encrypted opaque artifacts. It never receives raw database rows or unencrypted customer files from business modules.

Do not introduce a generic `IBackupSource` hierarchy unless multiple source implementations actually require one. The backup orchestrator can explicitly gather the known SquiFlow state that must be recoverable.

## 3. Where the interfaces live

These two interfaces are exceptions to the general anti-abstraction rule because provider replacement is a committed near-term event.

They can initially live in coherent infrastructure namespaces/projects alongside their consumer-facing contracts; a separate `abstractions` project is not required merely because interfaces exist.

Example target shape when implementation begins:

```text
infrastructure/
├── storage/
│   ├── IObjectStore
│   └── HuggingFaceObjectStore
└── backup/
    ├── IBackupTarget
    └── KaggleBackupTarget
```

If migration later requires two provider adapters to coexist in separate assemblies, split the projects then without changing the public contract.

## 4. Storage classes

### Workstation/local filesystem
Use for:
- local database;
- unsynced/pending attachments;
- bounded temporary processing/cache;
- import/export staging explicitly controlled by the user/application.

### Server node filesystem
Use only for bounded disposable temp/processing/staging and local backup packaging before transfer.

### Primary object store through `IObjectStore`
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

The database and authorization context, not the bucket pathname alone, decide which tenant/business record owns an object.

## 5. Immutable business-object behavior over mutable providers

SquiFlow protects historical object meaning independently from provider capabilities.

For issued/retained/versioned objects:
- use immutable/versioned application keys;
- do not silently overwrite bytes behind an issued document reference;
- store size/hash/version metadata;
- replacing an asset creates a new object reference when historical identity matters.

That rule must survive migration away from Hugging Face even if the paid provider supports native versioning.

## 6. Upload lifecycle

```text
authorize
→ validate size/type/path
→ stage/stream
→ IObjectStore.Put
→ verify size/hash
→ commit metadata/reference
→ publish availability
```

Handle both asymmetric failure windows:
- object upload succeeds, DB metadata fails → orphan reconciliation;
- DB reference exists, object missing/corrupt → explicit unavailable/repair state.

Do not load large customer artwork wholly into RAM merely for convenience.

## 7. Capacity is real

Treat the current ~100 GB Hugging Face private-storage envelope as finite.

Track at least:
- total stored bytes;
- retained business bytes;
- temporary/expiring bytes;
- orphan/GC-candidate bytes;
- bytes by tenant when useful;
- recent growth rate.

Do not invent thresholds before measuring real file sizes. But application/operations paths must be able to warn and eventually reject optional new large work before the provider account limit is hit.

Never silently delete retained customer objects to make room.

## 8. File/content safety

Client filename/content type is not proof of actual content.

Where applicable:
- bound upload and expanded/decompressed size;
- sanitize paths/names;
- do not execute uploaded customer content as code;
- isolate/limit risky parsers when needed;
- unsupported/suspicious input fails validation rather than receiving unlimited processing.

A later helper process is justified by an actual risky/native parser; Guard supervision of that helper does not make the parser business authority.

## 9. Workstation attachment sync

Large local attachments stay as files plus durable metadata rather than being duplicated inside the local DB.

Track enough to detect:
- local file missing/changed;
- upload already completed;
- server metadata accepted;
- retry/resume status.

A large file transfer must not starve small semantic synchronization operations.

## 10. Printing

Printing is a Workstation side effect, not business truth.

```text
committed invoice/order
→ print request
→ Windows printer/spooler path
→ success/failure/unknown physical output
```

Printer failure does not roll back the committed business transaction. Guard may supervise process lifecycle/errors, but it does not decide whether the business transaction exists.

## 11. Backup is infrastructure recovery, not only an application feature

Backup orchestration must capture every state required to restore a usable SquiFlow deployment, not merely rows exposed through the business application.

The recoverable set may include, as applicable:
- central database;
- object metadata and retained object bytes or a complete restorable object snapshot strategy;
- idempotency/outbox/job state required to prevent duplicate effects;
- versioned rules/workflows/configuration;
- deployment configuration required to reconstruct services;
- selected infrastructure configuration/manifests/runbooks that are not secrets;
- secret/key recovery material through a separate secure recovery mechanism rather than embedding secrets in the backup dataset;
- authorization model/configuration evidence needed to rebuild ZITADEL/OpenFGA integration, subject to the chosen deployment/backup method.

Do not claim a backup is complete until the restored environment can actually start and prove tenant/business/authorization correctness.

## 12. Kaggle backup bootstrap

Kaggle is a temporary off-site encrypted-artifact carrier, not the live object store and not a permanent production backup architecture.

Never upload raw readable customer data to Kaggle as normal dataset files.

Backup flow:

```text
collect required recovery state
→ package/compress locally
→ authenticated encryption locally
→ opaque .sqfbak
→ checksum/hash + manifest
→ IBackupTarget.UploadEncryptedArtifact
→ private Kaggle Dataset version
→ download verification
→ restore drill
```

Keep encryption/recovery key material outside Kaggle and make it independently recoverable.

`KaggleBackupTarget` is the only place ordinary runtime/backup orchestration should know Kaggle API details.

## 13. Backup validity

`upload succeeded` is not backup proof.

Test:
- remote artifact can be listed and downloaded through `IBackupTarget`;
- checksum matches;
- encryption key/recovery material is available;
- DB can restore;
- object metadata/bytes reconcile;
- tenant isolation remains intact;
- OpenFGA/ZITADEL integration can be restored/reconnected according to deployment design;
- idempotency/job state does not recreate completed effects unexpectedly;
- Core API/Workstation-facing behavior works after restore.

## 14. Deletion/retention

Logical business deletion and physical object deletion are separate.

Physical deletion respects:
- active references;
- issued/immutable document history;
- applicable retention;
- supported long-offline Workstation behavior;
- backup/recovery needs.

Do not build a generic lifecycle framework before the first real retention rules exist.

## 15. Paying-customer migration

The future paid providers are intentionally not selected yet, but the interface boundaries are selected now because replacement is already planned.

Migration:
1. choose paid primary object and backup providers from real workload/compliance requirements;
2. implement new `IObjectStore` and `IBackupTarget` adapters;
3. run contract/integration tests against old and new providers;
4. copy objects/backups while preserving application keys/hashes/manifests;
5. verify counts/hashes and authorized reads/restores;
6. switch configuration to the new adapters;
7. keep a rollback/read-only verification window as appropriate;
8. complete a full restore drill on the new backup path.

Business/domain code must not need rewriting because Hugging Face or Kaggle changes.
