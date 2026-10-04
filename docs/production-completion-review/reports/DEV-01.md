# DEV-01 — Strict boundary maintainer

Reviewer: `/root/development_boundaries_review`, GPT-6 Luna (high). Read-only review; root-edited summary. Dynamic checks: NOT_RUN. No additional static cross-host project-reference gap found in the inspected host scripts/workflow.

| Finding | Classification / unit | Evidence | Bounded acceptance / guard |
|---|---|---|---|
| Folder-only structure has stale duplicated inventory and omits introduced hosts/projects | CATALOG_MISMATCH; BAS-001 documentation reconciliation | `docs/architecture/REPOSITORY_FOLDER_STRUCTURE.md:6` | Link to the single README inventory rather than duplicate counts; reconcile tracked current folders separately from future-only ownership. Recheck whenever a project is moved/added/removed |
| Current README names blockers, later says BLOCKED=none, then names blockers again | CATALOG_MISMATCH; BAS-001/GATE-001 evidence reconciliation | `README.IMPLEMENTATION.md:19`; `README.IMPLEMENTATION.md:130`; `README.IMPLEMENTATION.md:132` | One unambiguous current gate scope; keep prior qualification as dated historical evidence. Never mark current blockers closed using a prior source pass |
| Migration registry discovery examines model snapshots, so a hand-authored migration project without a snapshot could evade inventory comparison | EVIDENCE_GAP hypothesis; OPS-016 | `tests/integration/Application.IdentityAccess.Postgres.Tests/MigrationRegistryTests.cs:18` | Either detect a migration-only fixture as unregistered or explicitly prohibit that convention with a guard; do not claim a currently omitted module |

Root independently inspected the named lines. The first two are confirmed static documentation irregularities; the third is a test-discovery limitation, not a reproduced omitted migration. Repairs remain assignments, not applied production changes. Existing `eng/build-host.sh`, `eng/verify-host.sh` and CI project graphs support host independence; actual current host builds and future-host independence still require their own runs.
