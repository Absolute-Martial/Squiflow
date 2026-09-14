# Phase 1D — Authorization Change Reconciliation and Failure

## Why this subphase exists

SquiFlow state and OpenFGA tuple state are separate durable systems. A role/permission/device change therefore requires an explicit reconcilable lifecycle rather than an imaginary distributed transaction.

## Required pattern

For each implemented authorization mutation define:

```text
request/proposal
→ current permission/delegation check
→ validate permission ceiling
→ persist durable SquiFlow intent/status/audit
→ apply OpenFGA tuple/model operation
→ verify/read-back as required
→ persist applied/reconciled outcome
```

Retries must preserve semantic identity.

## Required states/evidence

Use only the states needed by the real flow, but distinguish at least known-applied, pending/retryable, failed, and ambiguous/reconciliation-needed outcomes where they can occur.

## Failure injection

- tuple write succeeds, SquiFlow completion persistence fails;
- request retries after response loss;
- provider timeout leaves outcome uncertain;
- model revision changes during operation;
- stale consistency read immediately after mutation;
- process crashes between durable steps.

## Audit/observability

Material role/device/permission changes create durable audit evidence. Operational telemetry may copy safe metadata but is not the authority.

## Exit gate

No authorization-management UI says `Applied` until the authoritative outcome is known; ambiguous cross-system effects have reconciliation rather than guesswork.