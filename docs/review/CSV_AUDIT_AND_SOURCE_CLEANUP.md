# CSV Audit and Source Cleanup

**Version:** v0.0.15

## 1. Why the old CSVs are not design authority

Earlier architecture ZIPs contained generated inventory/review CSVs. They became stale as later architecture changed and could make old classifications look authoritative.

Current policy:
- do not use generated CSV inventory/review ledgers as design sources of truth;
- generate inventory/hash/coverage data in CI/release if useful;
- keep semantic architecture decisions in reviewed Markdown/current code/tests.

## 2. Source precedence

1. `MASTER_IMPLEMENTATION_PLAN.md`;
2. `docs/decisions/CURRENT_DECISIONS.md`;
3. focused current owner documents;
4. `docs/decisions/OPEN_DECISIONS.md`;
5. review/source material only as reasoning/traceability.

`docs/review/DECISION_AUDIT.md` records why current decisions were kept/simplified/deferred/removed, but it does not override `CURRENT_DECISIONS.md`.

## 3. Markdown can drift too

Removing CSV does not solve duplication automatically.

Use this rule:
- one focused document owns detailed semantics for a topic;
- master/current decisions summarize rather than restating a competing full specification;
- implementation/test docs reference the owner where possible;
- when a decision is removed, delete the obsolete current doc/reference rather than leaving two contradictory baselines.

Current topic owners include:

```text
Decision audit               docs/review/DECISION_AUDIT.md
Tenant isolation             docs/architecture/MULTI_TENANCY_ISOLATION.md
Identity/session             docs/security/IDENTITY_AND_SESSIONS.md
Permissions                  docs/security/TENANT_PERMISSIONS.md
API idempotency/retry        docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md
Workstation local-first      docs/workstation/LOCAL_FIRST_DESKTOP.md
Object/backup bootstrap      docs/data/FILES_AND_OBJECT_STORAGE.md
Physical capacity/recovery   docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md
Verification/testing         docs/testing/VERIFICATION_STRATEGY.md
Rules                        docs/rules/NATIVE_RULE_ENGINE.md
Workflow                     docs/workflow/WORKFLOW_DESIGN.md
```

There is intentionally no current Guard owner document because Guard was removed from the baseline. There is intentionally no dedicated accessibility owner document because dedicated accessibility work was removed from the v0.0.15 baseline.

## 4. Architecture does not imply scaffolding

A documented future boundary is not permission to create placeholder projects/interfaces.

Particularly avoid reintroducing:
- `SquiFlow.Guard` without a real process-isolation need;
- generic repository/unit-of-work interfaces;
- provider-neutral wrapper hierarchies before migration/dual implementation exists;
- empty `packages/`, `contracts/`, `persistence/abstractions/`, Worker or Admin projects solely because older trees listed them.

## 5. Current decision-change discipline

When a new source/article suggests a pattern:
1. identify the actual SquiFlow problem;
2. compare it to current accepted decisions;
3. classify KEEP / SIMPLIFY / DEFER / REMOVE / OPEN;
4. update the focused owner/current decision if accepted;
5. do not create another permanent document unless it owns a genuinely distinct concern.
