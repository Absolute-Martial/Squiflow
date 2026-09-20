# Structured Logging, Event IDs, and Failure Codes

**Version:** v0.1.0
**Status:** Accepted implementation direction

## 1. Purpose

SquiFlow operational telemetry must be queryable and stable across releases. Exception text and ad-hoc message strings are evidence, not durable identifiers.

Significant operational events therefore use:

```text
EventId       stable numeric/application identifier
EventName     stable readable identifier
FailureCode   stable failure classification when an abnormal outcome exists
```

A log message may change for clarity without breaking alerts, support queries, dashboards, runbooks, or tests.

## 2. Registry ownership

Maintain a source-controlled registry owned by `Application.Observability`.

The registry must enforce:
- unique EventId;
- unique EventName within the governed namespace;
- unique FailureCode;
- no silent reuse of retired identifiers for different meanings;
- compatibility tests in CI;
- owner/component and runbook linkage for high-value failures.

Conceptual families:

```text
AUTH.*
TENANT.*
API.*
DB.*
SYNC.*
OUTBOX.*
WORKER.*
RULE.*
DOCUMENT.*
STORAGE.*
GUARD.*
CONFIG.*
OBS.*
```

Examples:

```text
SYNC.CONFLICT.DETECTED
SYNC.ACK.OUTCOME_UNKNOWN
SYNC.RECONCILIATION.RECOVERED
WORKER.LEASE.EXPIRED
WORKER.RETRY.BUDGET_EXHAUSTED
DB.CONNECTION.POOL_SATURATED
GUARD.RESTART.BUDGET_EXHAUSTED
OBS.EXPORT.DROPPED
```

These examples establish naming style; exact registry values are implementation data.

## 3. Logger/category taxonomy

Use governed categories rather than one global logger or class-name-only taxonomy.

Recommended top-level categories:

```text
Application.Api
Application.Application
Application.Domain
Application.Database
Application.Sync
Application.Outbox
Application.Worker
Application.Rules
Application.Documents
Application.Storage
Application.Security
Application.Tenancy
Application.Diagnostics
Application.Guard
Application.Configuration
```

Subcategories may be added when they improve filtering without becoming arbitrary per-class noise.

## 4. Required structured fields

Use the fields that materially apply to the event. Do not populate every field with null/default values.

Common fields:

```text
service.name
service.version
deployment.environment
component
module
operation
result
EventId
EventName
FailureCode
TraceId
SpanId
CorrelationId
CausationId
OperationId
RequestId
IdempotencyKey
JobId
MessageId
SyncChangeId
TenantId
WorkstationId
RuleVersion
ConfigVersion
duration_ms
retryable
attempt
```

High-cardinality IDs are appropriate in controlled logs/traces when needed for investigation. They are **not** ordinary metric labels.

## 5. Severity contract

### Trace / Debug
Developer detail and temporary deep diagnostics. Not enabled centrally by default in production.

### Information
Meaningful lifecycle or business/operational boundary that is useful in normal investigation.

### Warning
Operation succeeded or can recover, but there is retry, backpressure, degraded dependency, slow path, conflict, resource pressure, or other condition needing attention.

### Error
The operation failed or entered a failure/reconciliation state, but process/business integrity is still controlled.

### Critical
Integrity/security/corruption or unrecoverable process-level condition requiring immediate operator attention.

Do not log the same failure independently at every layer. The owning boundary records the semantic failure; lower layers may attach exception/dependency evidence.

## 6. State-transition logging

For sync, workers, outbox, Guard recovery, configuration changes, and other durable state machines, log the transition rather than only a generic failure string.

Example:

```text
SyncChangeId=...
FromState=Sending
ToState=OutcomeUnknown
FailureCode=SYNC.ACK.OUTCOME_UNKNOWN
```

Later reconciliation:

```text
SyncChangeId=...
FromState=OutcomeUnknown
ToState=Applied
FailureCode=SYNC.RECONCILIATION.RECOVERED
```

This mirrors durable state and makes distributed failures diagnosable without treating telemetry as the source of truth.

## 7. Correlation and causation

Keep these concepts distinct:

```text
TraceId       distributed telemetry execution
CorrelationId SquiFlow business/conversation grouping
CausationId   operation/message/event that caused this one
```

Async boundaries preserve correlation and causation explicitly while propagating standard trace context.

`TraceId` is not a permanent business receipt.

## 8. Privacy and security

Never log by default:
- passwords;
- access/refresh tokens;
- cookies/session secrets;
- private/encryption keys;
- raw payment credentials;
- unrestricted customer documents;
- arbitrary request/response bodies;
- unnecessary PII/free-form text.

Prefer opaque IDs over names/contact details when IDs are enough.

Redaction is centrally testable and should occur before external export where feasible.

## 9. Time semantics

- wall-clock timestamps are UTC;
- duration uses monotonic timing;
- Workstation wall clock is not distributed ordering authority;
- severe client clock skew should be observable;
- business timezone/effective-date rules remain a separate domain concern.

## 10. Stable client-facing errors

Internal FailureCodes do not imply that privileged diagnostics are returned to ordinary clients.

API/client error contracts expose only safe stable error information appropriate to the caller. Detailed provider/security/root-cause evidence remains in privileged telemetry/audit/support paths.

## 11. Acceptance

Before this contract is considered implemented:
- registry uniqueness/compatibility tests pass;
- representative API, sync, worker, DB, Guard and external-provider paths emit stable events;
- redaction tests prove sensitive data cannot leak through common enrichers;
- event/failure codes remain queryable after message-text changes;
- state-machine tests assert transition-event behavior;
- high-value FailureCodes have an owner and recovery/reconciliation guidance.
