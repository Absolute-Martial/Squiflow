# DEV-03 — Impatient consumer integrator

Reviewer: `/root/development_consumers_review`, GPT-6 Luna (high). Read-only review; root-edited summary. Dynamic checks: NOT_RUN.

| Finding | Classification / unit | Evidence | Acceptance / guard |
|---|---|---|---|
| Blazor selection does not choose cookie/session/render/circuit or registered-device transport topology | OPEN_DECISION; WEB-001/UIA-001 | `docs/decisions/OPEN_DECISIONS.md:11`; `docs/web/WEB_RUNTIME_AND_STORAGE.md:85`; `docs/development-tasks/phase-05-admin-web/UIA-001-decide-separate-platform-admin-web-topology.md:24` | Real browser/provider expiry/revocation/reconnect and exact Admin device path; no fabricated trusted identity header |
| Independent Admin runtime delivery is scheduled after tenant Web gate | CATALOG_MISMATCH candidate; UIA-002/009 and GATE-003 | `docs/development-tasks/phase-05-admin-web/UIA-009-independent-admin-web-build-and-browser-qualification.md:7`; same file `:24` | Clarify sequencing versus runtime/build dependency; Admin operations must survive CoreApi outage |
| Later Workstation/Sync/Guard and complete-product gate have owners but no assignable catalog units | Confirmed CATALOG_GAP; new bounded FUT-001 planning assignment | `docs/development-tasks/COVERAGE_MATRIX.md:69`; `docs/development-tasks/phase-06-qualification/GATE-005-qualify-the-whole-declared-web-and-backend-release.md:13` | Create one selected desktop journey's dependency/task map and separate whole-product gate; do not assert Web release completes desktop |

Root independently confirmed the named catalog edges. Admin-after-tenant delivery follows the user's earlier requested sequence; it is not evidence of a compiled dependency or an automatically invalid edge. Preserve the DAG and explicitly document that reason until changed deliberately. The later-product planning gap is addressed by [FUT-001](../assignments/FUT-001.md), not runtime scaffolding.

Offline commit/crash/revocation cases live in `docs/workstation/LOCAL_FIRST_DESKTOP.md:217`; duplicate/forged sync cases in `docs/sync/SYNC_AND_AUTHORITY.md:363`; interrupted updates in `docs/workstation/GUARD_AND_RECOVERY.md:209`. These are required future tests, not executed evidence. Requalification follows UI/session/device transport, compatibility, local schema/protocol or recovery changes.
