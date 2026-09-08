# Core API and Worker Architecture

**Version:** v0.0.15

## Core API

`services/core-api` is the ASP.NET Core HTTP/composition host.

It owns:
- request pipeline;
- authentication/session integration;
- tenant/platform context resolution;
- endpoint policy/authorization;
- input/schema validation;
- application command/query dispatch;
- rate limiting/admission control;
- health/readiness;
- correlation/trace context;
- dependency composition.

It does not own business-domain implementation merely because the HTTP request arrives there.

Business behavior belongs in modules/application services.

## Worker

`services/worker` executes durable asynchronous work that should not keep API requests open.

Examples:
- documents/reports;
- image processing;
- notifications/integrations;
- reconciliation;
- projection maintenance;
- scheduled jobs;
- rule/workflow snapshot distribution where asynchronous;
- diagnostic packaging.

## Durable work lifecycle

```text
Pending
→ Claimed
→ Running
→ Completed
```

Alternative states:

```text
RetryScheduled
Failed
Quarantined/DLQ
Cancelled
OutcomeUnknown
```

A claim has a lease/ownership expiry. Use fencing/claim generations for work where a stale previous owner could cause an unsafe duplicate effect.

## Worker loop requirements

A process may run indefinitely. A loop may not spin indefinitely.

Required:
- bounded queues;
- bounded concurrency;
- cancellation propagation;
- event/signal wait rather than hot polling;
- periodic reconciliation as fallback;
- deadline and no-progress detection;
- graceful drain/shutdown;
- retry classification with exponential backoff + jitter;
- poison-work quarantine;
- crash-loop protection.

## External effect safety

For a side effect such as an external payment, webhook or remote provider action:

1. before effect — cancellation can be safe;
2. request sent, response missing — `OutcomeUnknown`;
3. provider confirms success, local completion write fails — reconcile using provider idempotency/reference;
4. local completion committed — retry must return the same semantic result.

Never infer that cancellation undid an external effect.

## Server concurrency

The application handles independent work in parallel. Correctness is scoped to the relevant aggregate/resource, not one global writer.

Final correctness is enforced by the selected central store through transactions, constraints, optimistic concurrency and locking where appropriate.

## Platform-critical Worker controls

Pause/drain/resume/retry/quarantine/reconcile controls that can materially affect server operation are invoked only through Platform Admin Web and `/platform-admin/...` APIs.

Do not expose those controls through Workstation or ordinary tenant business endpoints.
