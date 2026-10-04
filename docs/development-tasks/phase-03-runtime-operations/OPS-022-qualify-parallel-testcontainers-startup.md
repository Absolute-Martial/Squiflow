# OPS-022 — Qualify parallel Testcontainers startup

Task ID: OPS-022
Phase: 03-runtime-operations
Status: VERIFY_EXISTING
Model: GPT-6.1 Sol
Dependencies: BAS-001
Release requirement: REQUIRED

## Outcome

Resolve the observed Testcontainers ResourceReaper initialization failure so the normal parallel repository gate reliably exercises real PostgreSQL authority rather than failing before tests reach it.

## Current basis and canonical inputs

Read [current verification](../VERIFICATION_RESULT.md), [verification strategy](../../testing/VERIFICATION_STRATEGY.md), [current truth](../../../README.IMPLEMENTATION.md) and [source review](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md). The current normal run failed 11 IdentityAccess PostgreSQL tests with TypeInitializationException caused by ResourceReaper → DockerImage/MatchImage → RegexMatchTimeoutException. Other suites reached their real containers. This is an observed harness failure, not proof of a domain or migration defect.

## Scope and exclusions

Allowed areas: the actual centrally pinned Testcontainers dependencies/lockfiles, narrowly relevant fixture/runtime configuration and permanent harness regression/evidence documents. Inspect the exact upstream initialization path first. Shared package changes require integrator ownership. Do not change identity/business semantics, upgrade the product/toolchain indiscriminately or delete runtime cleanup.

## Decisions/prerequisites

Capture SDK, host load, Docker/Reaper image configuration and package versions from BAS-001. Select a supported minimal fix from primary dependency source/documentation at an immutable version; fixture structured images alone do not prove ResourceReaper's internal image parser is corrected. Record package compatibility and the exit path if a dependency change is necessary.

## Acceptance and edge cases

- Reproduce and identify the exact initialization failure, preserving the original normal-run evidence.
- Concurrent fresh test processes initialize the actual Docker/Reaper path successfully.
- Container termination and cleanup still work after cancellation, failure and ordinary completion.
- IdentityAccess tests reach database invariants, historical migrations, receipts and restricted-role assertions.
- The normal parallel ./eng/verify.sh passes with complete suite summaries, zero failures/skips and no hidden retry/serialization workaround.
- No ResourceReaper disablement, fake provider, blanket regex-timeout suppression or weakened business assertion substitutes for qualification.

## Security/static review

Review Docker authority, image provenance, cleanup lifecycle, dependency changes and diagnostic disclosure. Do not collect runtime secrets or expose Docker publicly to repair tests.

## Dynamic verification and unavailable-environment handling

Run the affected real PostgreSQL suite and then the normal gate under representative concurrent startup. Repetition is justified only to reproduce/check this reliability claim; document every run rather than retaining only a passing retry. Without SDK/Docker, return source review and meaningful test/config changes with exact unrun checks; GATE-001 stays blocked.

## Handoff

Follow [shared rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Return source-only ZIP, exact baseline/patch digests, root-cause findings, inspected results and cleanup evidence. No commit/push or unrelated changes.

## Assignable prompt

```text
Execute OPS-022 on the supplied failing baseline.
Inspect the exact ResourceReaper/image parser initialization path.
Preserve normal parallel failure evidence and dependency versions.
Select a supported narrow correction with an explicit source basis.
Preserve real Docker cleanup and all identity/migration assertions.
Test fresh concurrent startup and actual PostgreSQL invariants.
Run the normal gate without disabling, serializing or retry-masking it.
Report exact results and every unrun obligation.
Return source-only ZIP and a bounded reviewed dependency patch.
No commit, push or feature implementation.
```
