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

### Durable object storage
Use for retained business binary objects such as:
- customer artwork;
- generated documents;
- attachments;
- product/profile images;
- exports/backups/diagnostic archives according to policy.

### Business database
Stores metadata/reference:
- object ID/key;
- tenant/resource ownership;
- content type;
- size/hash;
- version;
- lifecycle state;
- business relationship.

Do not mount object storage as if it were the application's authoritative ordinary filesystem.

## 2. Upload lifecycle

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

## 3. Immutable/versioned keys

Prefer immutable/versioned/content-hash-style object identities for assets/documents where appropriate.

Updating a profile/product image should create a new object reference rather than silently replacing bytes behind a long-lived cache key.

## 4. CDN/cache

CDN/node cache is derived/rebuildable and never the master copy.

Use it to reduce repeated object-store reads/egress for hot assets.

## 5. Provider neutrality

SquiFlow code depends on a storage abstraction/policy rather than scattered B2/MEGA/provider calls.

Provider choice/tiering remains a current evaluation decision and should consider:
- access frequency;
- retention;
- egress economics;
- latency/region;
- durability/security;
- backup/restore needs;
- current contractual/compliance requirements.

## 6. Workstation attachment sync

Large local attachments can be referenced from the local outbox without duplicating the entire binary into the local database.

Required local metadata includes enough to detect:
- file missing;
- file changed since queueing;
- upload already completed;
- server metadata accepted;
- retry/resume status.

Do not mark the business attachment authoritative until the required durable object + metadata lifecycle completes.

## 7. Printing

Printing is a side effect, not business truth.

If an invoice/order commits but the printer fails/out-of-paper/driver crashes, the transaction remains committed and the print attempt remains separately retryable.

## 8. Deletion/retention

Logical business deletion/reference removal and physical object deletion are separate.

Physical garbage collection must respect:
- active references;
- retention/legal policy;
- backup/restore strategy;
- immutable issued documents;
- sync/offline references where applicable.
