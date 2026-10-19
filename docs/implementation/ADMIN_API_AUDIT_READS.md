# Administrative audit reads

**Product version:** `v0.0.1`

The private AdminApi exposes two distinct retained-evidence views:

| Route | Meaning |
|---|---|
| `GET /api/v1/platform/audit/access` | The authorization decision before an operation is attempted |
| `GET /api/v1/platform/accounts/{accountId}/history` | Committed local account availability transitions |

Both require HTTPS, exact authenticated external identity, active registered Admin-device certificate and current pinned-model `can_read_audit` authority. This permission is independent of registry reads and mutations; the platform administrator inherits it and it may be granted directly to an audit reader. Roll out the updated platform OpenFGA model and configure its pinned model ID before enabling the new routes. Readiness rejects a model missing this relation.

Access decisions return opaque event/principal/device IDs, operation, `authorized` or `denied`, safe reason code and occurrence time. `authorized` does not mean the command succeeded. External issuer/subject, certificate fingerprint, credentials and request bodies are omitted. No attempt is made to infer a committed effect by matching timestamps. Account history returns account revision, availability, actor/device, retained reason and transition timestamps; idempotency keys and fingerprints are omitted. Failed/stale commands have no committed transition entry.

Pages default to 50 and accept `limit=1..100`. `cursor` is a versioned Base64Url continuation for the exact resource. Access pages sort by `(occurred_at, id)` descending and optionally accept `principalId` (nonempty UUID in D format) and `outcome=authorized|denied`. Continuations bind those filters. Account history sorts by revision descending and its continuation binds the account. Duplicate/unknown query parameters and malformed or cross-resource/filter cursors are rejected after authorization. Concurrent later appends do not restart an existing traversal; these are retained keyset views, not a frozen transaction snapshot or total-count/export service.

The PlatformAdministration migration adds ordered access indexes and a read-only security-definer function with a fixed search path, a maximum 101-row lookahead and a redacted column list. Public execution is revoked. The Admin runtime receives execution only, retaining its append-only access to the underlying evidence table. Account history uses IdentityAccess's existing immutable receipt projection and unique account/revision index. Apply migrations and reapply `deploy/database/apply-admin-api.psql` before starting the updated AdminApi.

Each request appends the existing mandatory admission audit before reading, including malformed requests by an otherwise authorized caller. The read itself does not change account state or command receipts. Cancellation propagates to database reads. Failure to retain admission evidence prevents execution.

`PlatformAuditBoundaryTests` exercises actual PostgreSQL/OpenFGA authorization, tied timestamps, concurrent append, cursor/filter isolation, redaction, retained command history and runtime privilege restrictions. Rerun it and the AdminApi regression suite when changing routes, projections, indexes, cursors, model permissions or grants.

`AdminTlsTransportTests` runs the actual AdminApi on a loopback Kestrel HTTPS listener with its production connection-certificate provider. It verifies registered-certificate admission, forged-header and unknown-certificate denial, and fresh database revocation despite an unchanged valid token. Identity tokens and server certificates are explicit test fixtures; live provider and deployment network qualification remain separate.

This scope does not claim a combined audit for every capability, provider reconciliation, bootstrap/operator/device history, general audit export, retention deletion policy or tenant-business audit access. Existing receipts retain their owning module's meaning and are not copied into a central history table.
