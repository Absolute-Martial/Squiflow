# Orchestrator execution guide

Use this guide to assign the catalog one bounded task or slice at a time. The requested delivery ends at qualified backend, tenant operator Web and separate Admin Web. [Coverage](COVERAGE_MATRIX.md) records conditional and later-product scope. This guide schedules work; focused owners decide behavior.

## 1. Prepare each assignment

1. Complete BAS-001 against the actual source supplied to agents. Record Git SHA **and** hashes of included dirty files; inspect incoming work before dispatch.
2. Copy [SESSION_TEMPLATE.json](SESSION_TEMPLATE.json) outside the immutable catalog as a live ledger. Fill accepted dependencies, decisions, selected conditional tasks and concrete source/artifact digests. Template placeholders are not evidence.
3. Choose the full task from [TASK_INDEX.md](TASK_INDEX.md). `Dependencies` must have accepted handoffs on a compatible baseline. Evaluate each conditional prerequisite against the approved release cases and record the result.
4. For `VERIFY_EXISTING`, inspect and qualify present work. For `DECISION_REQUIRED`, obtain owner acceptance before introducing affected contracts. For `CONDITIONAL`, record a selected workload or a reason for remaining absent. `READY_AFTER_DEPENDENCIES` is not automatic dispatch permission.
5. Assign an isolated source copy. Attach the full task, AGENT_RULES, HANDOFF_AND_INTEGRATION, accepted dependency contracts and the single-task wrapper. Replace capability-area descriptions with an exact relative-path write allowlist for that slice. Required owner docs come from the same source copy.

## 2. Model routing and review

Use the task's model label or an explicitly approved equivalent capability tier. Luna or an available Flash model handles inventories, fixed-contract presentation and read-only review. Sol handles identity, money, persistence, concurrent effects, host/session design and qualification. [MODEL_ROUTING.md](MODEL_ROUTING.md) gives escalation rules; labels do not assert API availability.

After every changed task, assign [REVIEW_PROMPT.md](REVIEW_PROMPT.md) to an independent reviewer. A smaller model can check paths, fixed contracts and test/evidence completeness; a strong reviewer decides authority, financial/concurrency or recovery findings. Review findings become bounded correction slices under the same task. The implementation agent does not approve its own release claim.

## 3. Bounded slices for wider assignments

Several tasks describe a complete vertical outcome. Dispatch their responsibilities sequentially rather than telling one low-power agent to complete a module:

| Slice suffix | Deliverable | Default model | Must wait for |
|---|---|---|---|
| `.contract` | Exact owner-approved inputs, facts, failures, permissions and examples | Sol; Luna only for transcription of accepted decisions | Parent dependencies; accountable decision owner |
| `.core` | Host-neutral invariants/application behavior and meaningful pure tests | Sol | Accepted contract |
| `.store` | Durable effects/receipts, SQL/migrations, grants, RLS and real DB regressions | Sol | Accepted core contract |
| `.api` | Classified HTTP/current-authority ingress, bounded DTOs/errors and real host tests | Sol | Accepted capability/store |
| `.view` | Fixed-contract read/form presentation | Luna/Flash for bounded display; Sol for new mutation/session flow | Accepted API and appropriate frontend gate |
| `.review` | Independent findings against the supplied baseline | Luna/Flash for bounded fixed-contract checks; Sol for material authority/money | Returned patch and evidence |
| `.integrate` | Review fixes, shared-file registration and exact qualifying checks | Sol/integrator | Accepted slices and corrected findings |

These suffixes are dispatch labels under one stable catalog ID, not new capabilities or default projects. Skip a slice that the task excludes: COM-019 has no runtime slices; COM-020 has no store/API; COM-021 has no UI. Shared package/solution/model/migration changes belong to the integrator. A slice can add implementation/test files while returning its shared-file requests as a separate patch.

## 4. Delivery waves and parallel groups

The exact graph is in tasks.json and generated [EXECUTION_ORDER.md](EXECUTION_ORDER.md). The following are scheduling milestones, not blanket batches whose internal dependencies can be ignored:

| Milestone | Work groups | Exit |
|---|---|---|
| Baseline | BAS-001; OPS-022 resolves current parallel container-startup failure; ADM-001→002; read-only COM-001. Verify current bounded Invoice guards without requiring future allocation/date decisions. WEB-001 and UIA-001 topology decisions can run read-only in parallel. | GATE-001 accepts existing/incoming claims |
| Backend contracts | ADM-004/006→007/008; ADM-034; COM-005→006; OPS-001 selects one existing committed-Order consequence. | Accepted identity, permission, price, guard and execution decisions |
| Backend implementation | Permissions ADM-009→010→011→012→032; profiles ADM-018→019→020→021→024; platform operator/device ADM-014→015/017→016; commercial and runtime chains below. | Required capabilities and chosen conditional cases integrated |
| Commercial chains | Customer COM-002→003; pricing COM-007→008; quotation COM-009→010; variation COM-011→012→013→014→015; invoice COM-020→021→022→023→024→025→028→029→030→031→032. Billing and fulfillment progress independently except named decision/authority dependencies. | Real API commercial case evidence |
| Operations | Durable OPS-001→002→003→004; files OPS-007→011→009 and selected OPS-008; usage OPS-011→012; audit OPS-013; deployment/CI/roles/recovery/capacity OPS-014–021 in graph order. | OPS-020 and GATE-002 accept backend readiness |
| Tenant Web | WEB-002 shell after GATE-002; customer/draft/command flow WEB-003–007; billing WEB-010–011; settings WEB-012; catalog/policy authoring WEB-015/016; selected supplier/stock/portal branches. | WEB-014 then GATE-003 |
| Admin Web | UIA-002 after GATE-002/003; UIA-003 shell then provisioning, lifecycle, devices, controls and recovery UIA-004–008. | UIA-009 then GATE-004 |
| Whole requested release | Both Web gates, current backend contracts and operating evidence on the combined source. | GATE-005 owner acceptance, explicit selected scope and no introduced blockers |

Parallel groups use disjoint approved files in separate copies. Customer, invoice core, deployment decision, profile and independent review work often overlap in time; host composition, shared authorization models, migrations, package versions and full-gate execution require single ownership. A CoreApi outage must not become a normal AdminApi runtime dependency just because Admin Web is delivered later.

## 5. Completion and conditional scope

Track dispatch as `PENDING → ASSIGNED → SUBMITTED → REVIEWED → ACCEPTED`; use `EVIDENCE_PENDING` or `NEEDS_CORRECTION` when needed. Runtime states remain the owners' `NOT_INTRODUCED`, `BLOCKED`, `PRODUCTION_HONEST` and are not inferred from this ledger. Acceptance records reviewer, baseline/returned ZIP digests, decision versions, commands/results, guards and limitations.

Every conditional task needs an owner-selected applicability disposition. Selecting purchasing/stock also selects WEB-008/009; selecting a key operation selects ADM-028/029 and its relevant Admin screens. Selecting a dependency makes that dependency a release obligation too. The catalog validator checks all required branches reach their gate; it does not accept a business decision or predict arbitrary condition text.

Do not release a dependent task on merely submitted files. Integrate its dependency, resolve findings and verify the property before providing the next baseline. Missing .NET/Docker/provider/browser/target access leaves exact evidence pending. The complete assignment folder is a planning deliverable; development completion is the accepted GATE-005 result on real source and target infrastructure.
