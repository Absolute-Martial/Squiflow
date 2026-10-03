# Core API mutation outcome diagnostics

**Owner:** CoreApi host, `CoreApiMutationDiagnostics`.
**Product version:** v0.1.0.
**Scope:** observed successful customer organization/program creates and Orders
draft create/revise/abandon, after the application command returns its committed
or replayed result.
**State:** `PRODUCTION_HONEST` for this observed-result scope; `BLOCKED = none`.

Each supported successful result invokes one structured `MutationSucceeded`
event and increments one outcome counter. Replays are explicitly labelled
`replayed`, not counted as a fresh `committed` outcome. Logs include only the
fixed operation name, membership-derived tenant/account IDs, committed resource
ID, replay flag and server-generated request ID. They exclude names, descriptions,
prices, request/response payloads, idempotency keys, tokens and provider details.
Logs are operator diagnostics and are not a tenant-facing data API.

The meter `Application.CoreApi.Mutations` exposes
`application.core_api.mutation.outcomes` with finite `operation` and `outcome`
tags. It exposes `application.core_api.mutation.diagnostic_failures` with a
finite `channel` tag (`logging` or `metrics`). No resource, account, tenant,
request or idempotency identifier enters metric labels.

Non-fatal logger/listener failures are isolated from the already completed
command and from the other diagnostic channel. Failure-counter delivery is also
best effort and cannot recursively fail the business response. Process-fatal
failures are outside the guarantee. Logging follows native `ILogger` with
`LoggerMessage.Define`; metrics use native `System.Diagnostics.Metrics`.
No new package, source generator, generic command pipeline or provider bundle
is introduced.
Meters use the host's native `IMeterFactory`; each DI provider owns its meter
scope and disposal. Parallel host observations do not share meter instances.

## Evidence ownership and limits

The real-host `MutationDiagnosticsTests` cover all five operations and their
idempotent replays, reject/validation/conflict paths, payload/key/token exclusion,
and sink failures. Native meter listener tests prove bounded labels, independent
host meter scopes and independent sink failure behavior. The 2026-10-01 local
`./eng/verify.sh` run passed locked restore, formatting, Release analyzer build
and all 330 tests with zero failures or skips, including the normal parallel
PostgreSQL suite. These checks remain in that repository regression gate.
Coverage, remote CI and production deployment were not qualified by this run.
Existing real PostgreSQL transaction/receipt tests
remain the evidence for durable business effects; host diagnostic doubles do not
replace that evidence.

Requalify on command outcome semantics, diagnostic fields/labels, endpoint
wiring, logger or meter dispatch changes, and any new host observing these
commands. A counter represents an observed host result, not an exact global
business ledger. A process can fail after database commit and before observation;
log and metric sinks can lose events. This is not durable audit, exactly-once
emission, guaranteed delivery, an exporter, alerting, or cross-replica accounting.
Those responsibilities remain `NOT_INTRODUCED` and require their own durable
owner when introduced.

## Source admission and operation

Use the native [ASP.NET metrics mechanism](https://learn.microsoft.com/en-us/aspnet/core/metrics/overview?view=aspnetcore-10.0)
and [.NET DI-scoped meter guidance](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation).
The implemented `LoggerMessage.Define`/`ILogger` and `IMeterFactory`/counter calls
remain in the host. Framework dependencies and telemetry types never enter the
Customers or Orders capability projects. FullStackHero's bundled Serilog or
generic mediator/observability pipeline is not imported.

With `dotnet-counters` available, an operator can observe live measurements with
`dotnet-counters monitor --process-id <pid> --counters Application.CoreApi.Mutations`.
Information-level filtering can suppress log delivery; no listener means there
is no retained metric history. Protect diagnostic sinks as operator-only data.
No monitoring deployment or remote exporter was qualified by this slice.
