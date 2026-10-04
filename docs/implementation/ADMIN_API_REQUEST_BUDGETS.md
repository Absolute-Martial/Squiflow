# Admin API protected-request cooperative deadline

**Owner:** AdminApi host, `AdminApiRequestBudgets` and `AdminApiConfiguration`.
**Product version:** v0.0.1.
**Scope:** cooperative request cancellation for explicitly classified protected AdminApi routes.
**State:** `PRODUCTION_HONEST` for the declared cooperative deadline and explicitly public-health authentication exclusion; `BLOCKED = none` within those scopes. GATE-001 remains blocked separately by the invoice-core review findings.

`AdminApi:ProtectedRequestTimeoutSeconds` is the single AdminApi-owned request-budget setting. It is required at startup, accepts only integer values from 1 through 120 seconds, and is checked in as 30 seconds. The existing `AdminApiConfiguration` validates it together with the host's existing connection, concurrency and allowed-host settings.

AdminApi now classifies its two health routes as public health and every current `/api/v1/platform/...` route as protected platform administration. After routing and the existing no-store registration, the native ASP.NET Core request-timeout middleware wraps only the protected classification. It therefore covers the existing zero-queue concurrency admission, authentication, registered Admin-device lookup, higher-consistency OpenFGA authorization, authoritative access audit, request parsing, provider calls and capability operations without moving business meaning into middleware. `/health/live` and `/health/ready` retain their existing anonymous behavior and are outside this deadline.

Public health also short-circuits bearer authentication through native `OnMessageReceived` / `NoResult`, before token extraction, validation or OIDC discovery. A supplied bearer header cannot add an identity-provider dependency to public health. The skip requires trusted public-health endpoint metadata and no `IAuthorizeData`; missing classification, unmatched routes and authorization-required endpoints continue through JWT authentication. This does not change device, platform authority or protected challenge behavior. The JWT package is pinned to 10.0.8; its [handler lifecycle](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Security/Authentication/JwtBearer/src/JwtBearerHandler.cs#L55-L62) returns an event result before discovery. The shared ASP.NET runtime inspected for this correction is 10.0.12, whose corresponding handler ordering is unchanged.

`PublicHealthBearerReviewTests` retains the real-host liveness reproducer and adds valid/malformed bearer health cases against a controlled blocking discovery manager, protected discovery cancellation, valid/invalid/missing protected identities, unknown-route non-disclosure and explicit metadata fail-closed checks. `PublicAuthMetadataHostReviewTests` additionally registers actual test-only authorization-required routes with public or absent classification: missing/invalid bearer identities cannot reach their handlers. These permanent guards join the normal gate. Requalify on JWT lifecycle/version, authentication ordering or access-metadata changes. The controlled discovery manager proves native host invocation/cancellation behavior, not a live identity-provider deployment; unmatched routes deliberately retain normal JWT processing.

The deadline cooperatively cancels `HttpContext.RequestAborted`. Current AdminApi operations already pass that token into certificate access, PostgreSQL-backed authority/audit, OpenFGA, bounded body reads, ZITADEL verification and capability commands, so no second cancellation contract was introduced. A caller disconnect remains caller cancellation; it is not converted into a request-timeout response. If the AdminApi deadline wins and cancellation escapes before response headers start, the timeout middleware writes fixed `504` Problem Details with code `request_timeout`, a server request trace identifier and the existing `no-store` header. The timeout logger category is suppressed because the native middleware can log the caught exception; provider, database, payload and nested exception details are not part of the HTTP timeout contract.

This deadline is not forced thread/process termination, transaction rollback, or proof that a durable command did not commit. Cooperative work that ignores cancellation can continue, and an operation can commit before cancellation is observed. For an uncertain mutation outcome, retry only the supported command using the same `Idempotency-Key` and unchanged semantic intent, including the same expected revision where that command is revision checked. The capability-owned retained receipt then decides replay versus conflict. Do not infer absence from `504`, and do not switch to a new idempotency key merely because the first response was lost or timed out.

If a protected response has already started, the host cannot replace it with a new
504 body or claim successful completion. A boundary inside native request-timeout
middleware catches `OperationCanceledException` or `IOException` only when the response has started, the linked
request token is canceled and the native deadline token has expired. It aborts
the partial response and emits a fixed warning with the server trace identifier,
without attaching the exception, nested payload or provider details. Catching
before native timeout middleware unwinds prevents that exception reaching the
outer exception-handler/server diagnostic and rethrow paths. Caller disconnect
before the deadline does not produce this deadline warning; unrelated exceptions
retain the existing safe failure handling.

The boundary placement follows the inspected executing ASP.NET Core
[10.0.12 timeout source](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Http/Http/src/Timeouts/RequestTimeoutsMiddleware.cs)
and [exception-handler source](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs):
after headers start, native timeout middleware rethrows, and exception middleware
reports the original exception before its suppression callback. The earlier
10.0.8 source review is historical; the installed receiving runtime is 10.0.12.
Dynamic evidence must record the actual executing runtime rather than treating
the dependency package version as the runtime version.

Access audit is the existing durable admission evidence, written before the
capability effect is invoked. An `authorized` audit entry is not the mutation
result. A deadline or caller disconnect during identity/device/permission
resolution or audit insertion can leave no completed access-audit entry; no
durable interrupted/unknown audit outcome is introduced here. If audit insertion
does not succeed, execution does not advance to the capability command. A command
that already committed remains recoverable through its retained receipt. There
is no unbounded audit finalization or second outcome authority.

## Focused regression evidence

`Application.AdminApi.Tests/RequestBudgetTests.cs` adds real-host cases for deadline expiration before headers and during authentication, successful protected completion through the existing registered-device/OpenFGA/audit path, caller cancellation, concurrency-permit release after pipeline unwinding, protected no-store, unchanged unexpected-exception handling and public health exclusion, plus direct validation of invalid startup configuration. Started-response deadline and caller-disconnect cases cover cancellation and I/O exceptions, observe actual aborted body reads, check raw/nested private canaries are absent from logs and exception diagnostic events, and distinguish the fixed deadline warning from caller cancellation. Route enumeration guards protected classification, absence of disable/override timeout metadata and anonymous denial across every current authenticated mapping. Ordinary internal and I/O errors retain a safe structured failure diagnostic. A real PostgreSQL lock blocks the actual authority lookup; its regression checks deadline cancellation releases that wait and one subsequent request succeeds. Controlled authorization cases prove host middleware behavior, while this database case checks actual Npgsql cancellation. They do not qualify all provider transport cancellation or mutation-after-commit response-loss windows.

The receiving environment should run:

```bash
dotnet test tests/integration/Application.AdminApi.Tests/Application.AdminApi.Tests.csproj -c Release -p:RestoreLockedMode=true
./eng/verify.sh
```

The recurring gate must pass against real PostgreSQL/OpenFGA containers, complete repository restore/format/Release-build/test verification, and skip no tests. A failing guard requires requalification rather than retrying until green.

### Focused receiving evidence, 2026-10-04

With SDK 10.0.401 and the installed .NET/ASP.NET Core 10.0.12 runtime,
the final focused Release build and format verification passed with zero build
warnings/errors. The filtered `RequestBudgetTests` run passed **20/20** cases,
zero failures/skips, against the real Admin host and PostgreSQL/OpenFGA fixture.
Its actual PostgreSQL lock case observed the blocked authority query, then proved
deadline cancellation removed the lock wait and a subsequent request succeeded.
The four started-response cases cover deadline/caller cancellation with both
cancellation and I/O exceptions, safe termination and non-disclosing diagnostics.
Deadline and caller cancellation capacity cases await pipeline unwinding before
one recovery request; they do not retry away admission failures.

The first focused run was retained: 18 passed and two failed because the new
caller-disconnect body-read assertions expected `IOException`; actual TestHost
disposal cancels that pending read with `OperationCanceledException`. The
regression now asserts that explicit caller outcome separately from deadline
termination, and the complete focused suite was rerun successfully. Logs and
TRX results are under `artifacts/verification/adm-002/`. That implementation
run is distinct from the independent receiving evidence below.

### Independent review and qualification, 2026-10-04

The [ADM-002 independent review](../../artifacts/verification/adm-002/reviewer/REVIEW.md)
records 39/39 covered host cases, three fresh processes with 11/11 hostile and
cancellation cases each, and a separately justified routing run with 23/23.
`RequestBudgetUnmatchedRouteReviewTests` permanently guards unknown, invalid-route
and method-rejection responses against disclosure and unintended access audit.
Budget coverage is 50/50 executable lines and 4/4 emitted classification branches;
the collector emits no exception-filter branches. The four actual started-response
OCE/IO and deadline/caller cases supply that behavioral evidence without claiming
all race orderings. Real PostgreSQL authority-lock cancellation and admission
recovery after pipeline unwinding also passed.

The [ADM-003 independent review](../../artifacts/verification/adm-003/reviewer/REVIEW.md)
records AdminApi 56/56 and CoreApi 37/37 focused cases, plus six actual-host
authorization-metadata cases in each host, all without failures/skips or retries.
The original public-liveness failure is retained in
[public-liveness-bearer.log](../../artifacts/verification/adm-002/reviewer/public-liveness-bearer.log);
the corrected reproducer passes. Changed public-auth guards each cover 8/8
emitted branches; Admin authentication whole-file coverage remains 78/83 lines
and 14/20 branches, with unrelated claim-validation paths unmeasured.

The receiving [combined gate receipt](../../artifacts/verification/foundation-baseline-combined-2026-10-04.json)
records the exact `COLLECT_COVERAGE=1 ./eng/verify.sh` exit 0: **726 passed,
0 failed, 0 skipped across 16 suites**, zero build warnings/errors. It includes
87 AdminApi cases. Its [retained transcript](../../artifacts/verification/foundation-baseline-combined-2026-10-04.log)
and [archived coverage](../../artifacts/verification/foundation-baseline-2026-10-04/coverage/report/Cobertura.xml)
qualify only these reviewed source revisions; subsequent changes require the
appropriate guards again. The archived report confirms the budget's 4/4 and
each public-auth guard's 8/8 emitted branches. Native framework/provider coverage,
live ZITADEL, actual TLS negotiation, all provider cancellation, mutation-commit
response-loss windows and deployment remain unqualified. Passing this gate does
not accept GATE-001 or resolve the separate invoice-core findings.

## Security boundaries and requalification

Authentication, active registered-device authority, pinned-model OpenFGA checks, authoritative access audit, tenant/idempotency/revision ownership, least-privilege database grants and existing no-store behavior are unchanged. No admission policy, readiness semantic, role administration, commercial feature, new dependency or capability-owned persistence contract is introduced.

Requalify when middleware ordering, endpoint access classification, the AdminApi timeout bound, admission behavior, cancellation propagation, exception/Problem Details handling, logging filters, ASP.NET runtime behavior, or any protected operation stops honoring `RequestAborted`.
