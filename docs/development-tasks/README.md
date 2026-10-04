# Application development task catalog

This is an assignment package, not permission to begin implementation and not evidence of a completed product. Attach one complete task file and the shared instructions when assigning work. Current executable truth belongs to [README.IMPLEMENTATION.md](../../README.IMPLEMENTATION.md); focused owners and accepted decisions take precedence over this plan. Product version stays **v0.1.0**.

## Choose and assign a task

Start with the [orchestrator guide](ORCHESTRATOR.md), then use the [task index](TASK_INDEX.md) or machine-readable [tasks.json](tasks.json) for stable IDs, model routing, exact dependencies and permitted areas. Read [agent rules](AGENT_RULES.md), [handoff and integration](HANDOFF_AND_INTEGRATION.md), [model routing](MODEL_ROUTING.md), [verification commands](VERIFICATION_COMMANDS.md), and [coverage matrix](COVERAGE_MATRIX.md). Every task has a copyable prompt and task-specific acceptance, static review and dynamic checks. [Baseline notes](BASELINE_NOTES.md) identify the incoming work this revision accounts for.

| Phase | Work | Scheduling meaning |
|---|---|---|
| [00 Baseline](phase-00-baseline/README.md) | BAS-001; GATE-001 | Snapshot existing/incoming work; fresh gate after ADM-001/002 |
| [01 Admin/platform](phase-01-admin-platform/README.md) | ADM-001–034 | Qualify incoming boundary, then administration, profiles and selected platform controls |
| [02 Commercial backend](phase-02-commercial-backend/README.md) | COM-001–032 | Required commercial journey and explicit conditional branches |
| [03 Runtime operations](phase-03-runtime-operations/README.md) | OPS-001–022 | Earned durable execution, files, observation, deployment and current container-startup qualification |
| [04 Tenant Web](phase-04-tenant-web/README.md) | WEB-001–016 | Tenant operator and catalog/policy-authoring UI after backend readiness |
| [05 Admin Web](phase-05-admin-web/README.md) | UIA-001–009 | Separate private platform UI after tenant Web qualification |
| [06 Qualification](phase-06-qualification/README.md) | GATE-002–005 | Backend readiness, independent Web gates, whole declared release |

Phase folders group ownership. They do not impose a serial order on independent backend branches. WEB-001 and UIA-001 topology/session decisions can be prepared early; frontend runtime implementation waits its explicit gates. Phase 06 contains checkpoints that occur between implementation phases. A task directory existing never proves a project, host or capability exists.

The catalog contains **119 assignments**. IDs identify work, not chronological positions: WEB-015/016 are implementation prerequisites of the already allocated WEB-014 qualification ID; OPS-022 resolves the currently observed baseline harness failure. Generated per-phase indexes and [execution order](EXECUTION_ORDER.md) carry current dependencies; assign an entire task file, not a table row.

Read [operating flows](OPERATION_FLOWS.md) for actors, business effects, permitted variations and recovery. Use the [functionality acceptance scenarios](FUNCTIONALITY_ACCEPTANCE.md) to check connected outcomes rather than only individual endpoints. [Current task status](TASK_STATUS.md) distinguishes your existing work from remaining implementation and qualification; [source observations](source-observations.json) identify the inspected source files without exporting their contents. Reconcile these again when you supply a newer ZIP.

Planning status is exactly `VERIFY_EXISTING`, `READY_AFTER_DEPENDENCIES`, `DECISION_REQUIRED` or `CONDITIONAL`. These describe assignment prerequisites, not runtime states. Runtime owners use `NOT_INTRODUCED`, `PRODUCTION_HONEST` or `BLOCKED`. No planning status means passed, authorized, deployed or complete.

## Parallel scheduling and review

Run independent read-only inventory work in parallel. Parallel editing requires disjoint named files and agreed contracts; reserve shared solution/project manifests, migration order, authorization model, host composition and focused decision owners to one integrator. Dependencies require accepted handoffs, not just existing files. Avoid concurrent repository builds/tests against a shared working tree and avoid broad commits that mix unrelated agents' work. Use isolated copies for runnable tasks. The [handoff rules](HANDOFF_AND_INTEGRATION.md) define source ZIPs and integration review.

Required decisions remain release prerequisites even when implementation is conditional. GATE-002 includes all required backend branches and accepted optional workloads; unselected conditional scope stays absent with a reason. Customer portal is separately conditional, and Workstation/Sync/Guard remain later future boundaries. Neither tax/fiscal compliance nor full ERP/general-ledger accounting, subscription plans or licensing is implied.

## Validate and verify package integrity

Run `python3 docs/development-tasks/validate_catalog.py` from this repository. `python3 docs/development-tasks/validate_catalog.py --refresh` regenerates indexes, coverage, execution layers and the SHA-256 manifest after catalog edits, then validates. [TASK_HASHES.sha256](TASK_HASHES.sha256) covers catalog files except itself; the catalog ZIP has a separate SHA-256 sidecar. Run `sha256sum -c TASK_HASHES.sha256` from this folder to check the files. `python3 docs/development-tasks/package_catalog.py` produces and verifies the catalog-only archive under `artifacts/development-tasks/`. Relative owner links resolve against the assigned repository baseline; attach that source separately. The ZIP contains assignments and templates only.
