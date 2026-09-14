# Phase 0D — Engineering Safety, Observability, and Reproducibility

**Purpose:** Ensure safety, observability, verification, and reproducibility grow with implementation rather than being postponed to late hardening.

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Production intent

After 0D passes, a developer can change the implemented system and receive fast permanent feedback for ordinary regressions, while expensive/provider/process/recovery properties have an explicit recurring verification cadence instead of relying on a one-time gate review or tribal knowledge.

## Starting point after reset

There is currently no `SquiFlow.Observability` project and no production/test project. Observability/library boundaries are reintroduced only with real consumers. CI/CD is allowed again.

## Core rule

> Introduce the safety mechanism with the class of behavior that needs it; do not create every possible safety abstraction in advance and do not postpone necessary current safety to a later phase.

## CI/build foundation

Repository verification should be locally runnable and shared by thin GitHub/GitLab wrappers. Under current build-minute constraints, self-hosted/self-managed runners are preferred primary compute.

As code appears, local/CI verification covers the applicable subset of:

```text
restore
Release build
unit/property/contract/integration specs
architecture dependency checks
secret detection
package/dependency review
artifact/version identity
```

CI success never proves provider/recovery/security/hardware properties the pipeline did not exercise.

## Permanent versus recurring checks

Each material gate claim receives an explicit cadence:

```text
PER_COMMIT
PER_MR
SCHEDULED
PRE_RELEASE
RELEASE_CANDIDATE
OPERATOR_DRILL
PRODUCTION_MONITOR
```

Use fast blocking checks for ordinary regressions and recurring/release/operator evidence for expensive/destructive/provider/hardware properties.

Examples:

- deterministic invariants and architecture rules: `PER_MR`;
- secret/configuration checks: `PER_MR`;
- real local/DB integration that is cheap enough: `PER_MR`;
- process/provider fault combinations: `SCHEDULED` plus `PRE_RELEASE` when relevant;
- restore/key recovery/hardware capacity: later `OPERATOR_DRILL`/`RELEASE_CANDIDATE` owners.

`Passed once` is not a permanent quality mechanism.

## Observability rule

Do not create a large observability framework before consumers exist. When the first executable/provider/process flow appears, add the smallest product-owned instrumentation boundary that preserves stable semantics and can expand.

Use structured events with stable names/codes for material operational outcomes. Add logs, metrics, and traces according to the questions and boundaries that actually exist; not every method needs all three.

Telemetry must be bounded, redacted, non-authoritative, and unable to break business correctness merely because export fails.

## Failure vocabulary

Introduce stable `FailureCode`, event names/IDs, correlation/causation concepts only as real failures/flows need them. Never make free-form exception text the stable support/API contract and never preallocate hundreds of unused codes.

## Failure injection as a permanent practice

When a real process/network/storage/provider dependency exists:

1. define the protected steady-state/invariant;
2. inject a bounded realistic fault;
3. verify degraded/recovery behavior;
4. make the exercise reproducible;
5. assign its recurring cadence.

Start with isolated/representative environments and controlled blast radius. Do not imitate large-scale production chaos practices when the current topology does not justify them.

## Secrets/configuration

No production secret, provider token, DB credential, private key, OpenBao root/unseal material, or reusable native client secret belongs in source, artifacts, ordinary logs, or committed configuration.

## Reproducibility

For every current executable/project, a developer should be able to discover required SDK/tooling, restore/build/test/run commands, non-secret configuration, architecture owners, and relevant failure/recovery procedures from the repository.

`deploy/` documents only components that actually exist plus accepted deployment direction; it must not pretend target processes are running.

A normal clean-checkout verification path should be practical for a developer unfamiliar with the repository. External/operator ceremonies may require separate documented access, but undocumented tribal setup is not acceptable evidence.

## Resource safety

Queues, retries, restart loops, buffers, telemetry, payloads, concurrency, connections, and retained files/logs are bounded whenever exhaustion is possible. Obvious unbounded behavior is a correctness defect, not “performance tuning for later.”

## SLO/error-budget rule

Do not assign an error budget to hard correctness/security properties. Operational targets such as latency/availability/backlog/recovery time may gain measured SLOs/error budgets only when the workload exists. Exhausted operational error budget becomes an input to release/change policy rather than being ignored until a later hardening phase.

## Evidence invalidation

When a material dependency, provider, topology, persistence mechanism, process boundary, or recovery path changes, identify which previous evidence is stale and rerun it. Old green evidence is not permanent proof of a changed system.

## Exit gate

0D passes when the implementation that exists at that time has:

- repository-owned local/CI verification;
- mechanical architecture checks for material boundaries;
- safe secret/configuration handling;
- structured diagnosability appropriate to real runtime paths;
- bounded telemetry/retry/resource behavior;
- reproducible build/start/deployment instructions;
- named recurring evidence for properties too expensive/destructive for every MR;
- no observability/security claim stronger than the evidence actually run;
- no material gate property that depends only on a one-time manual review.
