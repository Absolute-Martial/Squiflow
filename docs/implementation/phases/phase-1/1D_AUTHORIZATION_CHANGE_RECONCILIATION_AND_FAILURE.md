# Phase 1D — Authorization Change Reconciliation and Failure

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Production intent

After 1D passes, every declared role/permission/device mutation that spans SquiFlow and OpenFGA has a durable, idempotent, reconcilable lifecycle; ambiguous provider/local outcomes do not become guessed success, and operations that overlap an authorization change follow an explicit tested authority decision point.

## Why this subphase exists

SquiFlow state and OpenFGA tuple state are separate durable systems. A role/permission/device change therefore requires an explicit reconcilable lifecycle rather than an imaginary distributed transaction.

## Required pattern

For each implemented authorization mutation define:

```text
request/proposal
→ semantic operation identity
→ current permission/delegation check
→ validate permission ceiling
→ persist durable SquiFlow intent/status/audit
→ apply OpenFGA tuple/model operation
→ verify/read-back as required
→ persist applied/reconciled outcome
```

Retries preserve semantic identity and do not silently create a second authorization effect.

## Required states/evidence

Use only the states needed by the real flow, but distinguish at least known-applied, pending/retryable, failed, and ambiguous/reconciliation-needed outcomes where they can occur.

The UI/API may say `Applied` only when the declared authoritative completion condition is established.

## Authorization change versus in-flight operations

The reconciliation lifecycle alone is insufficient. For every operation class that can overlap a role/device/permission change, define the authoritative decision point.

Record whether permission is evaluated/revalidated at:

```text
local proposal
server admission
durable acceptance
Worker/execution attempt
before external side effect
before final authoritative commit
explicit long-running checkpoints
```

Required rules:

- stale Workstation/UI permission state never overrides a current server admission check where current authority is required;
- short server-authoritative mutations normally establish current permission at admission immediately before the protected mutation/transaction;
- revocation after an already committed authoritative transaction does not retroactively erase historical truth;
- long-running/durable operations state whether permission is fixed at durable acceptance or must be revalidated at execution/checkpoints;
- if current permission is required and authorization state is ambiguous/unavailable, the protected operation fails closed, waits, or enters an explicit reconcilable state rather than assuming allow;
- the chosen policy is capability/operation-owned and permanently tested.

There is no universal `revoke cancels everything` or `authorization only once forever` rule.

## Failure injection

Permanent evidence includes as applicable:

- tuple write succeeds, SquiFlow completion persistence fails;
- SquiFlow durable intent exists, provider call times out;
- request retries after response loss;
- provider timeout leaves outcome uncertain;
- model revision changes during operation;
- stale consistency read immediately after mutation;
- process crashes between durable steps;
- duplicate reconciliation worker/run;
- role/device revocation overlaps a protected business operation at each declared decision point.

State-machine/idempotency cases run `PER_MR`. Real-provider/process interruption tests run on a recurring/pre-release cadence appropriate to cost and blast radius. A one-time fault-injection exercise does not permanently qualify reconciliation.

## Audit/observability

Material role/device/permission changes create durable audit evidence. Operational telemetry may copy safe metadata but is not the authority.

Audit evidence must distinguish requested/proposed, externally applied/observed, locally completed/reconciled, failed and ambiguous outcomes where those states exist.

## Transitional restriction

Before this lifecycle is qualified, authorization-management UI/API may not expose a mutation that can produce an ambiguous cross-system state and then claim success. Either keep the mutation surface disabled/restricted or implement the full production-honest lifecycle when the surface is introduced.

## Exit gate

1D passes only when:

- every declared authorization mutation has durable identity/state/reconciliation semantics;
- the failure-injection cases relevant to the implementation pass;
- every overlapping business-operation class has an explicit tested authorization decision point;
- UI/API success reflects the declared authoritative outcome rather than request acceptance;
- permanent/recurring regression guards remain active;
- `BLOCKED = none`.
