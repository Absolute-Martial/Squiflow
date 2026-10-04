# Later product boundaries absent from assignable runtime work

[DEV-03](reports/DEV-03.md) confirmed that the existing catalog lists Workstation/Sync/Guard coverage without task IDs. [GATE-005](../development-tasks/phase-06-qualification/GATE-005-qualify-the-whole-declared-web-and-backend-release.md) is intentionally not the complete-product gate. The planning correction here is the single assignable [FUT-001](assignments/FUT-001.md) outcome. The sequence below is an outline for that reviewer, not accepted implementation contracts or permission to create future scaffolding.

| Dependency order | Later outcome to turn into a bounded unit when workload is accepted | Authority / failure boundary | Qualification needed |
|---|---|---|---|
| 1 | Select one offline user journey and supported target devices | Explicit scope and product owner decision | Exact case and absent/selected branch disposition |
| 2 | Register/revoke one tenant workstation identity and least-privilege device action | Device authority separate from account membership and Platform Admin device authority | Forgery/revocation/replay/current membership; real credential path |
| 3 | Distribute one compatible immutable policy/profile snapshot | Cached evidence never becomes current central authority | Tamper/rollback/stale/incompatible snapshot denial and controlled refresh |
| 4 | Define one aggregate's operation envelope and conflict/retry contract | Local intent versus central authoritative admission | Same semantic key, revision conflict, incompatible client and response-loss behavior |
| 5 | Implement one durable device-authenticated sync admission/receipt/read path | Separate Sync host and capability owner; bounded batches/backpressure | Cross-tenant/device denial, partial batch/restart/ack replay, pagination/cursor isolation |
| 6 | Implement one local SQLite/WAL intent and projection transaction | Provisional effect and durable local outbox; no duplicated server meaning | Crash after commit, incomplete write, integrity/encryption/key recovery on actual target |
| 7 | Implement one Avalonia workstation view and reconcile that local intent | UX explains pending/rejected/authoritative result | Real offline edit/reconnect/conflict/receipt; target resource limits |
| 8 | Introduce bounded external Guard heartbeat/restart/safe-mode behavior | Supervision only, no business authority | Crash loop, hung process, authenticated IPC, diagnostics, cleanup |
| 9 | Deliver one authenticated update/backup/restore handoff | Artifact provenance and compatibility; update never bypasses data authority | Interrupted install, failed migration, rollback/key recovery, process drain |
| 10 | Qualify required real peripherals and complete-product target journey | OS/device adapter separate from capability rules | Actual target device/hardware, reconnect, print failure and resource/recovery evidence |

Several contracts here remain open. FUT-001 must resolve the dependency order for the selected case without silently choosing policy. No encryption source generator, sync framework, broker, actor, installer or platform-specific IPC implementation is selected by this outline. Relevant owners: [local-first desktop](../workstation/LOCAL_FIRST_DESKTOP.md), [sync authority](../sync/SYNC_AND_AUTHORITY.md), [Guard/recovery](../workstation/GUARD_AND_RECOVERY.md), [encryption/key recovery](../workstation/WORKSTATION_ENCRYPTION_AND_KEY_RECOVERY.md).
