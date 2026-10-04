# GATE-002 — Qualify backend readiness for frontend work

Task ID: GATE-002
Phase: 06-qualification
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: GATE-001, ADM-013, ADM-016, ADM-023, ADM-024, ADM-025, ADM-032, ADM-034, COM-032, OPS-006, OPS-009, OPS-012, OPS-013, OPS-020
Conditional dependencies: ADM-003 when additional route/readiness/admission safeguards are needed; ADM-005 when provider onboarding is selected; ADM-022 when a real runtime implementation variant is selected; ADM-026 when identity recovery is selected; ADM-027 when custom domains are selected; ADM-028 when a key-lifecycle contract is selected; ADM-029 when a key-lifecycle runtime is selected; ADM-030 when support access is selected; ADM-031 when resource scope is selected; ADM-033 when feature release controls are selected; COM-004 when customer import is selected; COM-016 when purchasing is selected; COM-017 when outsourcing is selected; COM-018 when tracked stock is selected; COM-026 when provider payments are selected; COM-027 when credit is selected; OPS-005 when scheduling is selected; OPS-008 when managed uploads are selected; OPS-010 when external notification is selected
Release requirement: REQUIRED

## Outcome

Accept the complete declared backend case matrix and operating boundaries before tenant Web runtime implementation consumes those contracts.

## Current basis and canonical inputs

Read [current truth](../../../README.IMPLEMENTATION.md), [production-honest gate](../../implementation/PHASE_GATE_PRODUCTION_HONESTY.md), [evidence permanence](../../implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md), [verification strategy](../../testing/VERIFICATION_STRATEGY.md), [shared rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Focused owners take precedence; historical tests do not qualify incoming edits.

## Scope and exclusions

Allowed areas: Backend release matrix, integration evidence and named focused gate-owner updates; inspect API/provider/operating contracts without building frontend features. No feature implementation, unrelated refactor, Git mutation or product-version change.

## Decisions/prerequisites

Required Admin/commercial/operations branches must be accepted, not merely submitted. Record each conditional branch as selected-and-qualified or absent-with-reason. WEB-001/UIA-001 topology decisions may precede this gate; frontend implementation cannot be its prerequisite. Backend guard/step-up contract tests qualify handoff restrictions before later Web consumption.

## Acceptance and edge cases

- Real API journeys cover organization default, independent program and separately assigned individual debtor through supported fulfillment, NPR invoice and manual allocation/settlement.
- Optional-use quotation capability, direct order without quotation, customer policy/approval, partial effects and corrections are qualified.
- Current authority, stale versions, concurrent allocation, same-key replay and response-loss recovery preserve retained facts.
- Files/Worker/audit/notifications needed by the release have actual crash/restart and denied-access evidence.
- Identity/secrets/private ingress, restore/rollback, observability and target capacity evidence match the declared deployment.
- Independent API/Worker builds and normal integrated repository gate pass; no UI prerequisite cycle exists.

## Security/static review

Review tenant and platform authority separation, permission-specific grant recovery, financial snapshots, document access and operational least privilege. Worker is required for selected automatic execution, never as a generic prerequisite for all permission proposals.

## Dynamic verification and unavailable-environment handling

Run backend authenticated smoke journeys, real PostgreSQL/OpenFGA/provider checks, earned Worker/file/recovery drills, independent introduced-host build/publish and normal ./eng/verify.sh. Target infrastructure evidence cannot be replaced by local unit tests. Inspect actual commands, versions, exit codes, totals, failures and skips. Unavailable environments leave named evidence pending and corresponding introduced claims BLOCKED; static review cannot substitute for real runtime boundaries.

## Handoff

Return the reviewed changed-file list, safe evidence, source-only ZIP with SHA-256 and unresolved blockers. Map each material claim to its owner, permanent/recurring guard and requalification trigger. No commit/push. Requalification is required when relevant code, contract, provider/model, runtime, host topology or operating configuration changes.

## Assignable prompt

```text
Confirm required backend and selected conditional release scope.
Inspect GATE-001 and every required branch handoff.
Run real authenticated commercial case journeys.
Test denial, stale versions, concurrency and uncertain responses.
Inspect files, durable work and operating recovery evidence.
Verify independently built hosts and normal full gate.
Confirm backend readiness has no frontend implementation cycle.
Return owner-accepted contracts, blockers and regression mapping.
```
