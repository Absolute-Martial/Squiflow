# Phase Gate Evidence, Regression, and Transition Contract

**Status:** Canonical implementation-governance owner  
**Applies to:** every phase, subphase, pull-forward, integration gate, and post-gate regression guard  
**Companion owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## 1. Core rule

A gate property is not production-honest merely because it was true during one review.

For every material gate claim, SquiFlow requires:

```text
CLAIM
→ SPECIFIC EVIDENCE THAT CAN FALSIFY IT
→ A PERMANENT OR RECURRING REGRESSION GUARD
→ A DEFINED REQUALIFICATION TRIGGER
```

The required wording is therefore not only:

```text
X is true
```

but:

```text
X is demonstrated by <evidence>
and protected from silent regression by <permanent/recurring check>.
```

A gate that cannot identify its evidence and regression guard is not ready to pass.

## 2. Evidence is owned by the claim, not by the current implementation

Tests, drills, static checks, provider exercises, and measurements prove a claim that already has an owner in requirements, architecture, security policy, protocol semantics, workload profile, or an explicit scope contract.

Do not reverse the direction:

```text
implementation shortcut
→ test written around shortcut
→ green test
→ shortcut declared correct
```

Use:

```text
owned invariant / contract / production intent
→ falsifiable claim
→ implementation
→ evidence at the layer that owns the property
→ permanent regression protection
```

## 3. Evidence classes

Every material claim names the strongest applicable evidence class.

### `STATIC`

Examples:

- forbidden dependency/package/reference checks;
- source/repository invariants;
- secret/configuration checks;
- schema/contract linting;
- generated endpoint/permission inventory checks.

### `UNIT_PROPERTY`

For deterministic business/value/state-machine semantics, edge cases, invariants, and property-based checks.

### `ARCHITECTURE`

For mechanical dependency direction, module ownership, forbidden host/provider leakage, and other compile-time/runtime structure constraints.

For .NET, an architecture-test library such as NetArchTest or an equivalent repository-owned mechanism may be selected when concrete assemblies exist. The rule is the architectural property, not the library choice.

### `INTEGRATION`

For behavior that depends on the real framework, database, protocol stack, provider adapter, identity/authorization engine, filesystem, encryption implementation, or another non-trivial boundary.

Mocks do not prove properties owned by the real dependency.

### `PROCESS_FAILURE`

For startup/shutdown, crash/restart, lease expiry, worker recovery, Guard supervision, helper-process isolation, response loss, and process-boundary compatibility.

### `MIGRATION_RESTORE`

For schema evolution, interrupted migration, backup restore, replacement environment, key recovery, resnapshot, and rollback/roll-forward.

### `LOAD_CAPACITY`

For latency/throughput/resource/backpressure/query-plan/capacity claims on representative workloads and target deployment classes.

### `SECURITY_HOSTILE`

For authentication, authorization, tenant isolation, CSRF/XSS/injection/SSRF, device/session misuse, privilege escalation, cross-tenant identifiers, secret leakage, and other threat-model-derived hostile cases.

### `OPERATOR_DRILL`

For procedures whose truth depends on real operational execution rather than code alone: break-glass, key recovery, restore, certificate/DNS recovery, provider migration, or physical/operator ceremonies.

## 4. Regression cadence classes

Every material claim gets at least one cadence appropriate to cost and risk:

```text
PER_COMMIT
PER_MR
SCHEDULED
PRE_RELEASE
RELEASE_CANDIDATE
OPERATOR_DRILL
PRODUCTION_MONITOR
```

Use the cheapest cadence capable of catching ordinary regressions quickly, while keeping expensive/destructive/provider/hardware exercises on a deliberate recurring or release schedule.

Examples:

- architecture dependency rules: `PER_MR`;
- deterministic domain invariants: `PER_COMMIT` or `PER_MR`;
- real DB tenant-isolation/idempotency tests: `PER_MR` where practical;
- process crash/fault injection: `PER_MR` for bounded local scenarios plus `SCHEDULED` for broader combinations;
- provider sandbox/integration behavior: `SCHEDULED` and/or `PRE_RELEASE` when continuous execution is impractical;
- replacement restore: `OPERATOR_DRILL` plus `PRE_RELEASE`/production-readiness freshness requirement;
- actual rack capacity: `RELEASE_CANDIDATE` and whenever workload/topology materially changes.

`Manual once` is not a regression strategy.

## 5. Permanent gate protection

After a gate passes, its claims remain active constraints.

A later phase does not inherit only the prose; it inherits the checks.

When code/config/provider/dependency/topology changes in a way that can invalidate prior evidence:

```text
material change
→ identify affected gate claims
→ invalidate stale evidence
→ rerun required checks/drills
→ block release/gate if evidence is not restored
```

Do not treat an old green pipeline or old restore drill as permanent proof after the relevant system changed materially.

## 6. Evidence record

A material claim should be traceable to a durable evidence record containing, as applicable:

```text
ClaimId / stable claim name
Owner
Declared scope
Source requirement / architecture owner
Evidence class
Verification command/test/drill/runbook
Regression cadence
Pass/fail condition
Environment/provider/topology assumptions
Last successful evidence reference/time when freshness matters
Requalification triggers
Known non-claims
```

Do not create empty evidence files for every theoretical future claim. Record evidence when the responsibility exists and the gate is actually being qualified.

## 7. Systematic threat modeling

Whenever a phase/subphase introduces or materially changes a trust boundary, perform a structured threat review rather than relying only on ad-hoc creativity.

At minimum, consider categories equivalent to:

```text
spoofing / identity misuse
tampering
repudiation / missing authoritative audit
information disclosure
denial/resource exhaustion
elevation of privilege
cross-tenant / scope confusion
replay / duplicate / stale authority
```

STRIDE is an acceptable vocabulary, not a mandatory ceremony. The requirement is systematic coverage tied to the actual data flows and authority boundaries.

Threat-model findings that affect current claimed scope are either fixed before the gate passes or become `BLOCKED`. Future-only threats may be `NOT_INTRODUCED` only when the vulnerable surface truly does not exist.

## 8. Failure injection is recurring evidence

Failure tests are not one-time gate theatre.

For responsibilities that depend on process/network/provider/storage behavior:

1. define the expected steady/healthy state;
2. inject a realistic bounded failure;
3. verify the protected invariant and degraded/recovery outcome;
4. keep the experiment reproducible;
5. assign a recurring cadence appropriate to blast radius and cost.

Examples include:

- dependency timeout/unavailability;
- response loss after commit;
- process termination between durable steps;
- disk full/low space/lock contention;
- stale lease/duplicate delivery;
- network interruption during resnapshot/upload;
- provider success with local completion failure;
- telemetry/export outage;
- certificate/DNS/time failure in controlled environments.

Do not run destructive experiments against real customer traffic merely to imitate large-scale chaos practices. Start in isolated/representative environments, minimize blast radius, and expand only when justified by the deployment profile.

## 9. Hard invariants versus SLOs/error budgets

Do not convert correctness/security invariants into probabilistic targets.

These have **no product error budget**:

```text
cross-tenant data leakage
unauthorized privileged effect
payment semantic double-charge caused by SquiFlow retry logic
corruption/loss of acknowledged durable state within the declared durability contract
secret/key disclosure
silent reinterpretation of unsupported contract versions
```

Operational targets may use SLIs/SLOs/error budgets when measured operation exists, for example:

```text
request latency
availability of non-safety-critical surfaces
sync backlog age
Worker completion latency
provider transfer latency
recovery-time targets
```

When an operational error budget is exhausted, the owning team pauses or limits risky feature/release work as appropriate and prioritizes restoring the reliability target. Exact SLO numbers are workload/product decisions, not invented by this governance document.

## 10. Transitional contracts between subphases

Subphase ordering can create real intermediate states. Those states require an explicit **transitional contract** whenever a partially matured surface is reachable.

Record:

```text
TRANSITIONAL STATE
- what exists now

ALLOWED
- operations safe under the currently qualified guarantees

FORBIDDEN / DISABLED
- operations that require a later subphase guarantee

CURRENT GUARANTEES
- security/durability/authority/recovery properties actually qualified

ABSENT GUARANTEES
- what must not be claimed yet

ENFORCEMENT
- feature/route/permission/build/deployment mechanism that prevents accidental use

CLOSING GATE
- subphase/phase that removes the transitional restriction
```

A transitional state must not rely on developer memory or roadmap prose alone. Where the risk is material, enforce the restriction mechanically.

## 11. Deferred-item safe-absence contract

Every material carry-forward item now records not only why it is deferred but what the system does **while it is absent**.

Required fields:

```text
Item
State = NOT_INTRODUCED
Why deferred
Owner
Behavior while absent
Source of that behavior
Why the absence behavior is safe for current scope
Evidence that the absence behavior is enforced/tested
Preservation constraint
Trigger
Latest closing gate
Current regression guard
```

If the absence behavior comes from a provider/framework/default configuration, that default must be consciously accepted, pinned/configured where feasible, and tested. Accidental inherited defaults are not an architecture decision.

If no safe absence behavior exists for the current reachable scope, the item is not deferrable; the responsibility is `BLOCKED`.

## 12. Policy/authorization/configuration change versus active operations

For every mutable authority/policy that can change while operations are pending or executing, define the **decision point** explicitly.

Examples:

- authorization revocation;
- device revocation;
- feature disablement;
- hard-limit change;
- rule/configuration revision;
- key/policy rotation;
- provider/account status change.

For each operation class, record whether authority is evaluated:

```text
at local proposal
at server admission
at durable acceptance
each execution attempt
before external side effect
before final commit
continuously / at explicit checkpoints
```

There is no universal answer. The owning invariant determines the correct point.

A stale local decision never overrides a later authoritative admission check where current authority is required. Conversely, a change after an already-committed authoritative transaction does not retroactively make the committed historical fact disappear; correction/reversal follows the owning domain semantics.

Ambiguous authority-change state must have an explicit behavior. Security-sensitive operations normally fail closed or wait/reconcile when current permission cannot be established, while already-admitted atomic work follows its declared transaction semantics.

## 13. Small, falsifiable batches

Large phase documents are not permission to create large implementation batches.

Prefer the smallest slice that yields one falsifiable production intent and production-honest result.

If a subphase contains multiple independent claims that cannot be implemented/reviewed/verified coherently, split the work further inside the subphase without renumbering the whole roadmap.

A smaller batch is not lower quality. It should make the quality bar easier to prove.

Avoid both extremes:

```text
one giant phase branch with many unrelated incomplete responsibilities
```

and:

```text
tiny commits that individually leave the claimed product path unsafe
```

Use coherent slices whose merged state preserves all reached gates.

## 14. Exit-gate wording rule

Every subphase exit statement is interpreted as shorthand for:

> This claim is demonstrated by named evidence, all currently applicable hostile/failure cases pass, and the property is protected from silent regression by named permanent/recurring checks.

If a subphase file only says `X is true`, the evidence/regression contract in this document still applies. Parent phase READMEs should identify the phase-specific evidence and permanence strategy so this requirement is visible rather than implicit.

## 15. Architecture enforcement in .NET

When compile-time project/namespace/reference boundaries exist, enforce important dependency rules mechanically in tests/static checks and run them on every MR.

Possible implementation mechanisms include a .NET architecture-testing library (for example NetArchTest or an equivalent), custom Roslyn/static checks, or repository-specific tests. Do not select a tool before the concrete boundary exists, and do not confuse tool adoption with proving the architecture rule.

Typical permanent rules include:

```text
Foundation does not depend on host/provider/UI/data SDKs
ordinary capability modules do not call each other through HTTP/gRPC in-process
provider SDK types do not leak into capability contracts
Guard does not reference business/persistence/key-management responsibilities
host adapters do not become duplicate business implementations
```

## 16. Phase inheritance

Every file under `docs/implementation/phases/phase-*` inherits both:

- `PHASE_GATE_PRODUCTION_HONESTY.md`;
- this evidence/regression/transition contract.

A phase README may strengthen the rules for its specific risks. A subphase may add narrower evidence. Neither may weaken the global production-honesty or permanence bar.
