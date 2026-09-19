# Phase 0D — Engineering Safety, Observability, and Reproducibility

**Purpose:** Ensure safety, observability, verification, and reproducibility grow with implementation rather than being postponed to late hardening.

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Starting point after reset

There is currently no `SquiFlow.Observability` project. Branding, IdentityAccess, Tenancy, CoreApi and DbMigrator are the active production scope. Observability/library boundaries are introduced only with real consumers. `eng/verify.sh` is the repository-owned format/Release build/test entry point after restore and `.github/workflows/verify.yml` is its thin GitHub wrapper. The workflow file does not itself prove that a remote CI run passed.

## Core rule

> Introduce the safety mechanism with the class of behavior that needs it; do not create every possible safety abstraction in advance and do not postpone necessary current safety to a later phase.

The same applies to governance evidence: do not preassign failure experiments, cadence or observability checks to future runtime paths that do not exist.

## CI/build foundation

Repository verification should be locally runnable and shared by thin GitHub/GitLab wrappers. Under current build-minute constraints, self-hosted/self-managed runners are preferred primary compute.

As code appears, CI/local verification covers the applicable subset of:

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

Cheap deterministic/static/architecture protections should remain blocking on normal change cadence once their claims exist. Expensive provider/process/restore/hardware evidence receives an explicit recurring/release/operator cadence only when the corresponding real responsibility exists.

## Observability rule

Do not create a large observability framework before consumers exist. When the first executable/provider/process flow appears, add the smallest product-owned instrumentation boundary that preserves stable semantics and can expand.

Use structured events with stable names/codes for material operational outcomes. Add logs, metrics, and traces according to the questions and boundaries that actually exist; not every method needs all three.

Telemetry must be bounded, redacted, non-authoritative, and unable to break business correctness merely because export fails.

## Failure vocabulary

Introduce stable `FailureCode`, event names/IDs, correlation/causation concepts only as real failures/flows need them. Never make free-form exception text the stable support/API contract and never preallocate hundreds of unused codes.

## Failure injection permanence

When a real current responsibility depends on process/network/provider/storage behavior, its qualifying fault scenarios become reproducible regression evidence with a cadence appropriate to cost/blast radius.

Do not create speculative chaos matrices for future Worker/Sync/provider/restore paths merely because later roadmap phases mention them.

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
- permanent/recurring protection for the material claims it actually qualified;
- no observability/security claim stronger than the evidence actually run;
- no speculative future evidence map created merely to make later phases look complete.
