# Workstation Instructions

These rules apply below `apps/desktop/workstation/` in addition to the root instructions.

## Current state

The principles-first reset currently has **no Workstation project implemented**. This directory reserves Workstation ownership/instructions only. Reintroduce the Avalonia host when a real current Workstation slice earns it; do not restore the old project by symmetry or memory.

## Role

When implemented, the Avalonia Workstation is a primary product host with local-first/offline capability where explicitly supported. Local state is not automatically central authority.

Do not fake future SQLite/sync/encryption responsibilities with temporary production paths merely to make a screen work.

## Presentation structure

- View/ViewModel code owns presentation state, commands, navigation, and user-understandable status.
- Reusable business meaning belongs in the capability core/application layer.
- Keep platform-specific Windows/Avalonia dependencies at the Workstation adapter/host edge.
- Prefer explicit UI state names (`PendingRemote`, `Conflict`, etc.) once those states exist; avoid vague labels that hide authority.

## Local-first rules once persistence/sync is introduced

- A local-capable mutation that must survive restart commits local business/provisional state and its durable operation/outbox atomically where the architecture requires it.
- UI success must distinguish local acceptance from server-authoritative acceptance.
- Normal offline startup must not require Platform Admin/OpenBao once local key provisioning is legitimately complete.
- Preserve pending user intent across restart/update/resnapshot/rebase; never “fix sync” by deleting pending work.
- Stale local permissions/config/stock/credit/limit snapshots cannot grant current central authority.

## Security

- Never embed reusable central DB credentials, OpenFGA admin credentials, OpenBao/Vault root/KEK credentials, or a native client secret.
- Use system-browser Authorization Code + PKCE when the accepted identity phase is implemented.
- Local sensitive persistence must follow the selected encrypted SQLite/WAL/key-recovery design once real business data is stored.
- Logs/diagnostics must not contain DEKs, tokens, protected key blobs, or unrestricted customer rows.

## Resource behavior

- Bound local queues, retries, staging bytes, logs, sync batches, and transfer concurrency when introduced.
- Treat disk-full, locked DB, sleep/hibernate, clock jumps, network loss, and abrupt process exit as normal failure scenarios to test.

## DO NOT

- Do not create the Workstation project just because the folder exists.
- Do not make a view model the source of business truth.
- Do not call PostgreSQL directly from Workstation.
- Do not make Workstation tenant-admin changes locally authoritative/offline.
- Do not use in-memory wakeups/channels as durable acceptance of business work.
- Do not store SQLite/key material in plaintext configuration.
- Do not claim remote device revocation securely erases bytes already available to an unlocked compromised machine.

## Validation

When relevant foundations exist, test abrupt termination after local commit, restart, duplicate/retried operations, sign-out with pending work, DB lock/disk full, old-client compatibility, long-offline return, resnapshot/rebase preservation, and local-vs-authoritative status UX.
