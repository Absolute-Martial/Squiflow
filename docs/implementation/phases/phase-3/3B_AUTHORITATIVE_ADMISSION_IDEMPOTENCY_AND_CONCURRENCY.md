# Phase 3B — Authoritative Admission, Idempotency, and Concurrency

## Required server path

```text
transport/device/session admission
→ authoritative TenantContext/resource scope
→ current OpenFGA/SquiFlow authorization
→ current facts/rules
→ semantic OperationId/idempotency
→ expected version/concurrency
→ domain invariants
→ PostgreSQL transaction
→ idempotency result + outbox where applicable
```

The API/Sync host owns transport/admission concerns; the authoritative capability application owns business meaning.

## Idempotency

Differentiate transport retry from semantic operation identity. Same semantic key + changed intent must be rejected/handled explicitly.

## Concurrency

Choose per invariant from expected version/conditional update, database constraint, suitable transaction isolation or narrowly justified lock. Generic last-write-wins is not acceptable for protected business facts.

Classify deadlock/serialization conflicts and retry the whole transaction only when safe under the semantic idempotency contract.

## Failure tests

Response lost after commit, duplicate operation, changed intent with reused key, concurrent edits, permission change while pending, DB conflict, process crash around commit/result persistence.

## Exit gate

A response loss/retry cannot duplicate a semantic effect and concurrency conflicts produce meaningful deterministic outcomes.