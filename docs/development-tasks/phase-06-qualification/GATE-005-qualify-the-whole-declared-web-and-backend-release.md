# GATE-005 — Qualify the whole declared Web and backend release

Task ID: GATE-005
Phase: 06-qualification
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: GATE-002, GATE-003, GATE-004, OPS-021
Conditional dependencies: none
Release requirement: REQUIRED

## Outcome

Accept the declared backend plus tenant operator Web and separate Admin Web delivery scope as one coherent release; this does not complete the broader product or unlock its version.

## Current basis and canonical inputs

Read [current truth](../../../README.IMPLEMENTATION.md), [production-honest gate](../../implementation/PHASE_GATE_PRODUCTION_HONESTY.md), [evidence permanence](../../implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md), [verification strategy](../../testing/VERIFICATION_STRATEGY.md), [shared rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Focused owners take precedence; historical tests do not qualify incoming edits.

## Scope and exclusions

Allowed areas: Whole selected release case matrix, integrated verification evidence and named focused gate-owner updates only. Workstation, SyncApi and Guard remain separate future required product boundaries, not implemented here. No feature implementation, unrelated refactor, Git mutation or product-version change.

## Decisions/prerequisites

Accountable owners approve the exact case matrix including selected conditionals and visible exclusions. Product v0.0.1 stays locked until the distinct complete production-capable product gate is explicitly qualified. A missing Workstation/Sync/Guard responsibility cannot be disguised as completed by this Web/backend gate.

## Acceptance and edge cases

- Every required backend and both Web case has current accepted evidence for the integrated source/configuration.
- Direct and quoted journeys, three debtor types, partial/corrective effects, administration and tenant variation survive actual target operation.
- Forbidden authority, stale versions, provider outage, response loss, process restart and restoration preserve consistent durable facts.
- Normal full repository gate and independent host restore/build/publish pass on the exact release source.
- Deployment/rollback, provider-model migration, backup/restore, telemetry alerts and measured capacity meet declared target claims.
- Every material introduced responsibility is PRODUCTION_HONEST; BLOCKED = none within declared scope, with explicit non-claims and recurring guards.

## Security/static review

Review cross-host authority/secret separation, emitted assets/logs, financial retained facts, document quarantine/access and privileged recovery. Confirm no tax/fiscal/legal accounting, subscription/plan/license or portal claim escaped its accepted owner decision.

## Dynamic verification and unavailable-environment handling

Inspect GATE-002/003/004 and OPS-021 evidence, run integrated target smoke/recovery cases and one normal ./eng/verify.sh without competing builds. Independently build/publish each introduced host. Actual remote CI/deployment checks are named separately; never reuse an old commit’s result. Inspect actual commands, versions, exit codes, totals, failures and skips. Unavailable environments leave named evidence pending and corresponding introduced claims BLOCKED; static review cannot substitute for real runtime boundaries.

## Handoff

Return the reviewed changed-file list, safe evidence, source-only ZIP with SHA-256 and unresolved blockers. Map each material claim to its owner, permanent/recurring guard and requalification trigger. No commit/push. Requalification is required when relevant code, contract, provider/model, runtime, host topology or operating configuration changes.

## Assignable prompt

```text
Confirm the exact backend and two-Web release case matrix.
Keep v0.0.1 and future Workstation/Sync/Guard scope visible.
Inspect accepted gates against integrated source/configuration.
Run real target business and recovery smoke cases.
Inspect authority separation and safe assets/logging.
Run normal full gate and independent host checks.
Get accountable owner acceptance with no introduced blockers.
Return declared-release qualification and all non-claims.
```
