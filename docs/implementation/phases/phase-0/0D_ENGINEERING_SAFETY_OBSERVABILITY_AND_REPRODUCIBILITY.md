# Phase 0D — Engineering Safety, Observability, and Reproducibility

**Purpose:** Ensure safety, observability, verification, and reproducibility grow with implementation rather than being postponed to late hardening.

## Starting point after reset

There is currently no `SquiFlow.Observability` project and no production/test project. Observability/library boundaries are reintroduced only with real consumers. CI/CD is allowed again.

## Core rule

> Introduce the safety mechanism with the class of behavior that needs it; do not create every possible safety abstraction in advance and do not postpone necessary current safety to a later phase.

## CI/build foundation

Repository verification should be locally runnable and shared by thin GitHub/GitLab wrappers. Under current build-minute constraints, self-hosted/self-managed runners are preferred primary compute.

As code appears, CI/local verification should cover the applicable subset of:

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

## Observability rule

Do not create a large observability framework before consumers exist. When the first executable/provider/process flow appears, add the smallest product-owned instrumentation boundary that preserves stable semantics and can expand.

Use structured events with stable names/codes for material operational outcomes. Add logs, metrics, and traces according to the questions and boundaries that actually exist; not every method needs all three.

Telemetry must be bounded, redacted, non-authoritative, and unable to break business correctness merely because export fails.

## Failure vocabulary

Introduce stable `FailureCode`, event names/IDs, correlation/causation concepts only as real failures/flows need them. Never make free-form exception text the stable support/API contract and never preallocate hundreds of unused codes.

## Secrets/configuration

No production secret, provider token, DB credential, private key, OpenBao root/unseal material, or reusable native client secret belongs in source, artifacts, ordinary logs, or committed configuration.

## Reproducibility

For every current executable/project, a developer should be able to discover required SDK/tooling, restore/build/test/run commands, non-secret configuration, architecture owners, and relevant failure/recovery procedures from the repository.

`deploy/` documents only components that actually exist plus accepted deployment direction; it must not pretend target processes are running.

## Resource safety

Queues, retries, restart loops, buffers, telemetry, payloads, concurrency, connections, and retained files/logs are bounded whenever exhaustion is possible. Obvious unbounded behavior is a correctness defect, not “performance tuning for later.”

## Exit gate

0D is complete when the implementation that exists at that time has:

- repository-owned local/CI verification;
- mechanical architecture checks for material boundaries;
- safe secret/configuration handling;
- structured diagnosability appropriate to real runtime paths;
- bounded telemetry/retry/resource behavior;
- reproducible build/start/deployment instructions;
- no observability/security claim stronger than the evidence actually run.
