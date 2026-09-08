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

`docs/review/DECISION_AUDIT.md` records why current decisions were kept/simplified/deferred/removed/restored, but it does not override `CURRENT_DECISIONS.md`.

## 3. Markdown can drift too

Removing CSV does not solve duplication automatically.

Use this rule:
- one focused document owns detailed semantics for a topic;
- master/current decisions summarize rather than restating a competing full specification;
- implementation/test docs reference the owner where possible;
- when a decision changes, update/remove stale references rather than leaving contradictory current guidance.

Current topic owners include:

```text
Decision audit               docs/review/DECISION_AUDIT.md
Tenant isolation             docs/architecture/MULTI_TENANCY_ISOLATION.md
Identity/ZITADEL             docs/security/IDENTITY_AND_SESSIONS.md
Permissions/OpenFGA          docs/security/TENANT_PERMISSIONS.md
API idempotency/retry        docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md
Workstation local-first      docs/workstation/LOCAL_FIRST_DESKTOP.md
Workstation Guard            docs/workstation/GUARD_AND_RECOVERY.md
Object/backup providers      docs/data/FILES_AND_OBJECT_STORAGE.md
Physical capacity/recovery   docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md
Verification/testing         docs/testing/VERIFICATION_STRATEGY.md
Rules                        docs/rules/NATIVE_RULE_ENGINE.md
Workflow                     docs/workflow/WORKFLOW_DESIGN.md
```

There is intentionally no dedicated accessibility owner document because dedicated accessibility work is outside the v0.0.15 baseline.

## 4. Architecture does not imply indiscriminate scaffolding

A documented boundary should be created when it is accepted and its phase needs it. Do not use `no scaffolding` as a reason to delete a justified boundary.

Current justified early boundaries include:
- `SquiFlow.Guard`, because Workstation supervision/recovery must survive Workstation failure;
- `IObjectStore`, because primary object-storage migration at the first paying customer is already planned;
- `IBackupTarget`, because backup-provider migration at the first paying customer is already planned;
- ZITADEL and OpenFGA integration boundaries.

Still avoid:
- generic repository/unit-of-work interfaces;
- one-interface-per-class/provider-SDK mirroring;
- forwarding-only Manager/Service/Helper layers;
- empty `packages/`, `contracts/`, Worker/Admin/module projects solely to make an old diagram look complete;
- additional helper processes without a specific fault/isolation need.

The rule is **disciplined completeness**: remove ceremony, not required capability.

## 5. Current decision-change discipline

When a new source/article or user correction changes architecture:
1. identify the actual SquiFlow problem/requirement;
2. compare it to current accepted decisions;
3. classify KEEP / SIMPLIFY / DEFER / REMOVE / OPEN / RESTORE;
4. update the focused owner/current decision if accepted;
5. search current docs for stale contrary wording;
6. do not create another permanent document unless it owns a genuinely distinct concern.
