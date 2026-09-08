# API Contract, Idempotency, Retry, and Long-Running Operations

**Version:** v0.0.15

This document turns the Stripe, AWS Builders' Library, ASP.NET Core, and Azure Architecture guidance into the SquiFlow API contract without adding a new service or framework.

## 1. Idempotency is part of command semantics

For every mutating command that can be retried after an uncertain network outcome, the caller supplies a stable idempotency key for the **intended business operation**.

Examples:

```text
CreateOrder
RecordPayment
RefundPayment
SubmitQuotation
PublishRuleSet
RequestDocumentGeneration
PlatformControlProposal
```

Do not derive the idempotency key only from request parameters. Two identical-looking requests can legitimately represent two different business intents.

The key is scoped at least by:

```text
caller/tenant authority
+ operation kind
+ idempotency key
```

For Workstation synchronization, every logical outbox item has its own stable semantic idempotency key independent of the transport request/batch ID.

## 2. Same key, same intent

The server stores enough request identity to detect whether a repeated key represents the same semantic request.

If the same key is replayed with the same semantic payload:
- do not perform the business effect again;
- return the prior/semantically equivalent result or current operation status.

If the same key is reused with materially different parameters:
- reject it as an idempotency-key mismatch;
- do not guess which request the caller intended.

## 3. Atomicity of receipt and effect

Where the selected authoritative store supports the business mutation and idempotency receipt in one transaction, commit them atomically:

```text
BEGIN
  check/create idempotency receipt
  validate current business state
  apply mutation
  write audit/outbox
  persist semantic result/reference
COMMIT
```

Never intentionally create either of these windows:

```text
business effect committed
but idempotency receipt missing
```

or:

```text
idempotency receipt committed
but business effect never happened
```

If an external provider effect cannot participate in that transaction, use provider idempotency/effect references plus `OutcomeUnknown` reconciliation.

## 4. Idempotency result states

A receipt may conceptually move through:

```text
Accepted/Processing
Succeeded
FailedFinal
OutcomeUnknown
```

A duplicate request must not enqueue a second long-running job merely because the first is still running.

For an asynchronous command, the duplicate returns the same operation/status resource.

## 5. Idempotency retention

Idempotency records are not necessarily retained forever.

Every command family declares a retention rule based on:
- plausible retry/late-arrival window;
- business/legal significance;
- lifetime of the created effect/resource;
- storage cost;
- whether replay after expiry is safe.

High-risk financial/effect receipts can require much longer durable evidence than low-risk operational commands.

## 6. Transport IDs are separate

Keep distinct identifiers for different purposes:

```text
RequestId          one HTTP attempt
CorrelationId      traces an end-to-end flow
CausationId        links derived work to its cause
MessageId          one durable message/envelope
IdempotencyKey     one intended semantic business operation
BusinessId         order/payment/etc. identity
```

Do not use a transient HTTP request ID as the business idempotency key.

## 7. Retry classification

A client retries only when the failure contract says retry can plausibly succeed.

Typical retry candidates:
- network I/O interruption;
- selected timeout cases;
- 429 with `Retry-After`;
- selected 5xx/dependency-transient failures.

Do not automatically retry:
- validation errors;
- authentication failures;
- authorization failures;
- stale-version/business conflicts;
- malformed payloads;
- unsupported protocol/schema;
- deterministic domain rejection.

A retry must still use the same idempotency key when it represents the same intended operation.

## 8. Retry budget, backoff and jitter

Retries are bounded by:
- per-attempt timeout;
- maximum attempts;
- maximum elapsed duration;
- dependency/client retry budget.

Background/deferred retries normally use exponential backoff plus jitter and honor `Retry-After`.

Do not layer independent retry loops blindly at HTTP client + application service + Worker + provider SDK. Coordinated retries are required so a small dependency failure cannot multiply into a retry storm.

Never use an endless retry loop.

## 9. HTTP method semantics

Use HTTP semantics where they naturally match the resource operation, but do not force complex business commands into generic CRUD shapes merely to look RESTful.

- GET/HEAD do not produce business side effects.
- PUT/DELETE are implemented idempotently when used.
- POST business commands that can cause duplicate effects use SquiFlow idempotency semantics.
- Explicit command resources/endpoints are valid for semantic transitions such as approval, refund, publication, reconciliation or administrative proposals.

Examples:

```text
POST /api/orders
POST /api/quotes/{id}/approval
POST /api/payments/{id}/refunds
POST /tenant-admin/rule-set-publications
POST /platform-admin/worker-control-proposals
```

The endpoint name should express business intent; authorization and domain state still decide whether it can execute.

## 10. Optimistic concurrency and ETags/version tokens

Normal collaborative editing uses explicit versions.

For HTTP clients, ETags/`If-Match` can expose the version contract where useful. The underlying domain/application command still carries/validates the expected version.

A stale write returns a stable conflict/precondition result and does not silently overwrite newer state.

Do not use one global last-write-wins policy for SquiFlow aggregates.

## 11. Pagination and query bounds

Every unbounded collection API requires server-enforced limits.

Use:
- default page size;
- maximum page size;
- stable ordering;
- filtering/sorting allow-list;
- tenant/permission scope before pagination;
- cursor/keyset pagination where offset pagination becomes incorrect or expensive under large/changing datasets.

Client-selected projections cannot expose fields the caller is not authorized to see.

## 12. Long-running request-reply

Long operations do not hold an HTTP request open indefinitely.

Baseline flow:

```text
POST command
→ authenticate/authorize/validate/idempotency
→ durable operation/job accepted
→ 202 Accepted
   Location: /api/operations/{id}
   Retry-After: ... where useful
```

Operation resource states are explicit:

```text
Pending
Running
Succeeded
Failed
Cancelled
OutcomeUnknown
```

The status resource includes stable timestamps and a structured error/result where applicable.

If completion creates a separate resource, the status resource can direct the caller to that resource after completion.

Cancellation is only exposed when the underlying operation has a safe cancellation or compensation contract.

## 13. Synchronous versus asynchronous threshold

Keep a command synchronous when its authoritative transaction/validation is expected to finish within the interactive request budget and the user needs the result immediately.

Use async request-reply when:
- work is long-running or resource-heavy;
- an external dependency can take too long;
- document/report/image processing is involved;
- platform maintenance/control work has durable progress;
- request-thread occupancy would create avoidable saturation risk.

Do not queue every command merely because a Worker exists.

## 14. Problem details and error classification

HTTP errors expose safe structured machine-readable results, preferably based on Problem Details semantics, with SquiFlow failure codes such as:

```text
Validation
Unauthenticated
Forbidden
Conflict
PreconditionFailed
AlreadyApplied
RateLimited
DependencyTransient
DependencyPermanent
ResourceExhausted
UpgradeRequired
OutcomeUnknown
InternalDefect
```

Internal stack traces/provider details stay out of ordinary client responses.

## 15. API implementation gate

A new mutating endpoint is incomplete until reviewers can answer:

1. What exact business intent does it represent?
2. Can the client retry it?
3. What is the idempotency-key scope?
4. What happens for same key + changed parameters?
5. What is the expected-version/concurrency rule?
6. What is the authoritative transaction boundary?
7. Does it create async work?
8. What happens if the response is lost after commit?
9. What happens if an external effect succeeds but receipt persistence fails?
10. Which 4xx/5xx results are retryable?
11. What are request/page/payload/resource limits?
12. What authorization/resource requirement applies?
13. What audit/trace identifiers are recorded?
