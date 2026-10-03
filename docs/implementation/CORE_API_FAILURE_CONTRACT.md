# Core API unhandled-failure contract

**Owner:** CoreApi HTTP host, `CoreApiExceptionHandler`.
**Product version:** v0.1.0.
**Scope:** failures reaching the exception middleware before response headers are committed.
**State:** `PRODUCTION_HONEST` for this scope; the 2026-10-01 full parallel
`eng/verify.sh` gate passed locked restore, format, Release build and 324 tests.

Temporary Npgsql failures (`IsTransient`) return `503`, with Problem Details code
`database_unavailable`. This includes a real connection failure and an expired
database command timeout. It does not classify every PostgreSQL exception as an
outage: non-transient schema/programming failures return `500`, `internal_error`.
Other unexpected failures also return `500`, `internal_error`. A framework
`BadHttpRequestException` retains its HTTP status with `invalid_request`.

Responses contain only a safe title, status, machine code and server-owned trace
identifier. Protected responses retain `Cache-Control: no-store`. A single
structured failure event contains code, status, exception type and trace ID;
neither the exception object nor its message, SQL, parameter values, credentials,
payload or request URL is logged by this handler. ASP.NET's handled-exception
diagnostics are suppressed so they do not duplicate an unsafe exception event.
The runtime data source explicitly uses a null provider logger factory: disabling
parameter logging alone still permits Npgsql command logs to emit SQL text.
Native pool metrics remain available; new SQL tracing/logging needs a separate
safe operational owner and regression proof.
The `traceId` field uses ASP.NET's generated request identifier, not an incoming
W3C trace ID or caller correlation header; distributed trace IDs may be inherited
from callers and are not treated as owned security evidence.

There is no automatic retry and no invented recovery time. A failed response is
not proof that a mutation rolled back: a client retry must retain the original
idempotency key and semantic command. The handler does not catch errors after
headers are committed, guarantee a response to disconnected callers, or replace
expected business outcomes already mapped by endpoints. Cancellation of the
caller is not classified as a database outage. Liveness remains independent of
the database. Durable audit history and an exporter pipeline remain
`NOT_INTRODUCED`.

## Evidence and regression ownership

`DatabaseFailureResponseTests.RealProviderFailuresReturnSafeProblems` drives the
real ASP.NET host with the production data-source factory, Npgsql and PostgreSQL: stopped database, actual command
timeout, and missing-table failure. It asserts status, code, media type, trace ID,
no-store, safe captured logs and independent liveness. The account binding seam
is replaced only to invoke the real failing provider operation; the property
under test is not mocked. `AuthenticatedAccountTests` guards generic safe `500`.
`FrameworkRequestFailuresRetainTheirStatusWithoutExposingMessages` proves safe
preservation of framework `400` and `413` failures through the same real host.
The wire test also sends a caller-chosen W3C trace ID and proves it cannot replace
the owned request identifier in the problem response.
These tests run in `eng/verify.sh`. Qualification requires that exact gate to pass.

Requalify on ASP.NET/Npgsql major changes, exception-middleware ordering or
diagnostic policy changes, new provider failure classes, and response-cache or
error-contract changes. These tests do not prove production availability,
cross-replica fairness, deployment readiness or the complete commercial backend.

## Source admission

The inspected FullStackHero snapshot is revision
`3f2959e683e9f83f13e55e1678c9119f63c7e8e5` (MIT),
`src/BuildingBlocks/Web/Exceptions/GlobalExceptionHandler.cs` and
`src/Tests/Integration.Middleware.Tests/Tests/GlobalExceptionHandlerTests.cs`.
The admitted idea is the native `IExceptionHandler` boundary and host-level
failure testing. Its exception-message exposure, stack-trace logging, incoming
correlation-header trust, Serilog integration and application exception hierarchy
are rejected. No framework source generator or runtime bundle is imported.

The diagnostics policy follows the [.NET 10 error handling contract](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0).
Provider classification follows [Npgsql exception semantics](https://www.npgsql.org/doc/diagnostics/exceptions_notices.html)
and [`NpgsqlException.IsTransient`](https://www.npgsql.org/doc/api/Npgsql.NpgsqlException.html).
