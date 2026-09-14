# Phase 0 Documentation-First Rewrite — Historical Transition Record

**Status:** MR !54 merged into `main`; this file records the reset event and is not the current Phase-0 status owner.

Current Phase-0 truth is owned by:

- `README.md` in this directory;
- `0A_ARCHITECTURE_BASELINE_RECONCILIATION.md`;
- `0A_BASELINE_STATUS.md`;
- focused architecture owners and `docs/decisions/CURRENT_DECISIONS.md`.

## What happened

The earlier live implementation was judged unsafe as a baseline because it had been created before the full architecture/file-structure context was reconciled.

The rewrite branch `rewrite/phase-0-docs-first` removed the previous application/source projects, tests, solution/build files and `.gitlab-ci.yml` in commit `82cbbf24101abaeefdb1d4c9f81bd762f07bed88`, preserved the documentation corpus/decision history, and rebuilt only currently justified Phase-0 boundaries.

MR !54 merged that rewrite into `main` at merge commit `a7f2ff9aea8cbe1431187219fbbfc7a18ffb33a9`.

## Rebuilt boundary

```text
foundation/
├── application-kernel/SquiFlow.ApplicationKernel
└── observability/SquiFlow.Observability

modules/customers/
├── SquiFlow.Customers
└── SquiFlow.Customers.Workstation

apps/
├── web/SquiFlow.Web
└── desktop/
    ├── workstation/SquiFlow.Workstation
    └── guard/SquiFlow.Guard

services/core-api/SquiFlow.CoreApi

tests/
├── SquiFlow.Phase0.Specs
└── SquiFlow.Architecture.Specs
```

No future WebApi, SyncApi, Worker, Admin, Diagnostics, Maintenance, Sync-helper, or Document-helper executable was scaffolded.

## Important non-claim

MR !54 established a clean implementation starting point. It did **not** complete Phase 0 and did not prove the 0A–0F gates.

In particular, repository CI/CD remained intentionally absent, later persistence/security/sync/Worker/Admin/provider responsibilities were not introduced, and build/spec execution was not claimed merely because source files existed.

## Why this file is retained

This record prevents future contributors from accidentally treating the deleted pre-rewrite code or closed MR !53 as current architecture evidence. It is historical context below the 0A/current-decision/focused-owner precedence levels.
