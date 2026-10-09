# Commercial source commit checkpoint — 2026-10-09

The owner explicitly requested local commits of all completed work on 2026-10-09. No push, pull request or shared-history rewrite is authorized or performed.

## Commit location and scope

The writable GitButler clone is `artifacts/gitbutler/repo`, on the dedicated branch `session/commercial-save-20261009`. The T3 worktree's `.git` points to `/cutiepie/Squiflow/.git/worktrees/t3code-66ed0681`, outside this session's writable roots. The commits below therefore preserve verified copies in the independent clone; they do not advance the original T3 branch, which retains its source changes. The clone has independent Git objects and a disabled push URL.

| Commit | Preserved work |
|---|---|
| `2ecc5970aebd5fc11e75eacf9ac2e51bdb5ca388` | COM-012 independent program Order approval and bounded COM-013 current-policy admission, with tests, migrations, grants, contracts and qualification records. |
| `2dc460f2320bb00e5b4ad32c8562a9a1a393b00b` | Two real-Kestrel test fixtures bind ephemeral IPv4 ports while retaining the configured synthetic host header. |
| `056213b3341a3b912c7a55290ce34348bb167f1c` | COM-014/015 partial service production, pickup, blocking dispositions, accountable corrections and protected history, with their native regressions and qualification records. |

The paired capabilities remain together because their implemented contracts and receiving evidence are coupled. Their behavior, regression tests, focused owners and necessary task hashes are committed together. The unrelated fixture correction is separate. Repository version-control instructions and this checkpoint are a subsequent documentation change; pending purchasing/audit proposals are separate from completed feature implementation.

## Source and evidence permanence

The COM-012/013 commit was reconstructed from the exact qualified 1,453-file source. Applying the qualified COM-014/015 source delta and the fixture correction reproduced all 1,498 final source files byte-for-byte. Original ZIP digests remain:

- COM-012/013: `13f85edaa6712d4eb1121db0f23a65adbd239cc44dc1d59c61ffe37c9f5a6fb4`.
- COM-014/015: `45f559bc37172c9d9a2ef75c6beadee90d7391028237886c750399b4ac430c5d`.

The retained [COM-012/013 receipt](COM_012_013_IMPLEMENTATION_RECEIPT.md) records 1,282 passing tests across 23 projects. The retained [COM-014/015 receipt](COM_014_015_IMPLEMENTATION_RECEIPT.md) records the final normal 1,347/1,347 gate across 25 projects and three independent host publishes. These were inspected prior receiving runs on the named frozen source; committing identical bytes does not turn them into newly executed checks. The historical receipts' uncommitted-source wording remains true for their receiving date and original T3 checkout.

A portable checkpoint archive, SHA-256 and source/commit receipt are retained under `artifacts/gitbutler/checkpoints/`. It contains the independent repository source and Git metadata, without downloaded tool binaries or build output. This local backup and the source-only packages support recovery; they are not off-machine backup, remote CI or deployment evidence. Existing live storage and broader production-gate boundaries remain with their canonical owners. Product stays `v0.0.1`.
