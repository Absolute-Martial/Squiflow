# Core API protected-request cancellation budget

**Owner:** CoreApi host, `CoreApiRequestBudgets`.
**Product version:** v0.0.1.
**Scope:** cooperative request cancellation for current classified protected HTTP operations.
**State:** `PRODUCTION_HONEST` for the cooperative cancellation scope; `BLOCKED = none`.

`RequestBudget:ProtectedRequestTimeoutSeconds` is required and bounded from 1
through 120 seconds; the checked-in policy is 30 seconds. Invalid or missing
configuration fails startup. After routing and protected no-store registration,
the native request-timeout middleware wraps admission, authentication,
authorization, body parsing and application execution for protected endpoints.
Public bootstrap, OpenAPI, liveness and readiness retain their own policies.

For those explicitly public endpoints, native JWT `OnMessageReceived` returns
`NoResult` before token extraction, validation or OIDC discovery, even when a
caller supplies a bearer header. The skip requires trusted public access
classification and no authorization metadata. Protected, unclassified and
unmatched endpoints still follow JWT authentication; `AllowAnonymous` alone is
not authority to skip it. Bootstrap caching/ETag, readiness dependencies and
protected account/tenant checks retain their existing owners.

`PublicHealthBearerTests` exercises every public surface with valid/malformed
bearer input and a blocking native discovery dependency, checks protected
discovery cancellation and valid/invalid/missing protected identity, and guards
unknown-route non-disclosure plus authorization-required/missing-classification
metadata. The JWT package is 10.0.8; its inspected native
[message-received lifecycle](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Security/Authentication/JwtBearer/src/JwtBearerHandler.cs#L55-L62)
returns the event result before discovery. Shared ASP.NET runtime 10.0.12 has
the same ordering. The public-auth correction is `PRODUCTION_HONEST` for this
explicitly public scope, qualified by the receiving evidence below.
`PublicAuthMetadataHostReviewTests` also exercises actual public-classified and
unclassified routes with native `RequireAuthorization`: missing/invalid bearer
identities cannot reach their handlers. Both suites are permanent normal-gate
guards. Requalify on authentication ordering, metadata or JWT
lifecycle/version changes. Controlled discovery is host evidence, not live
OIDC deployment qualification.

The budget cancels `HttpContext.RequestAborted`; endpoint and provider operations
must cooperate with that token. If cancellation escapes the pipeline before
response headers start, the native middleware returns safe `504` Problem Details
with code `request_timeout`, server-generated `traceId` and protected `no-store`.
The bounded problem body is written without the already-expired processing token.
Client disconnection remains transport cancellation, not a fabricated timeout
response. Admission permits are released when the protected pipeline unwinds.

This is not a hard thread/process kill or a guarantee that every request returns
within that wall-clock duration. Work that ignores cancellation can continue;
response transmission, already-started headers and a debugger-attached runtime
are outside this guarantee. Native middleware disables its timer under a debugger.
Existing downstream timeouts remain independent. A timeout is not evidence of
rollback: a command may already have committed. Retry supported mutations with
the same semantic idempotency key and unchanged intent; never infer an absent
effect from `504` or automatically retry with a new key.

## Source admission and security

Use the native [ASP.NET Core request-timeout middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/timeouts?view=aspnetcore-10.0).
The inspected [v10.0.8 middleware source](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Http/Http/src/Timeouts/RequestTimeoutsMiddleware.cs)
links cancellation, distinguishes client abort and restores request state in
`finally`. Its timeout log includes the caught exception, so CoreApi suppresses
that exact logger category. Provider messages, SQL, request payloads and nested
exception details must not become timeout diagnostics. The response exposes only
fixed safe text, code and server request correlation. No new package, custom
timer engine, source generator or provider dependency enters capability code.

## Evidence and regression owner

`RequestBudgetTests` uses real ASP.NET hosts and native timers. It checks deadline
and client-cancellation behavior, capacity recovery, safe response/log content,
hostile trace-parent exclusion, public bootstrap/liveness preservation, invalid
configuration and protected-only OpenAPI `504` descriptions. Its slow body test
blocks a real request stream until cancellation and proves no customer mutation
is attempted. PostgreSQL transaction and receipt tests retain durable-effect
ownership; request-budget doubles do not prove database rollback.

These tests join `eng/verify.sh`. Requalify on middleware ordering, endpoint
classification, cancellation propagation, response writing, logging filters,
configured bounds or ASP.NET/runtime upgrades. Distributed deadlines, edge/TLS
timeouts, tenant fairness, exporter/alert deployment and full backend production
qualification remain separate responsibilities.

The 2026-10-02 local full parallel `./eng/verify.sh` run passed locked restore,
format verification, Release build and all 348 tests, with zero failures or
skips and zero build warnings/errors. The normal PostgreSQL suites also passed.
A focused read-only review found no concrete cancellation, disclosure or scope
defect. Coverage, remote CI and production deployment were not qualified by this run.

The 2026-10-04 [independent public-auth review](../../artifacts/verification/adm-003/reviewer/REVIEW.md)
passed CoreApi 37/37 and AdminApi 56/56 focused cases, followed by six actual-host
authorization-metadata cases per host, with zero failures/skips and no retries.
The Core focused fixture uses controlled account/authorization ports; it does
not supply new live identity-provider or PostgreSQL/OpenFGA qualification.
The [original Admin liveness failure](../../artifacts/verification/adm-002/reviewer/public-liveness-bearer.log)
is retained; the corrected reproducer passes. Native discovery blockers verify
public exclusion and protected discovery cancellation. Core bootstrap cache/ETag,
readiness status/no-store and valid/invalid/missing protected identities remain
guarded.

The [combined receiving gate](../../artifacts/verification/foundation-baseline-combined-2026-10-04.json)
ran `COLLECT_COVERAGE=1 ./eng/verify.sh`: exit 0, **726 passed, 0 failed,
0 skipped in 16 suites**, zero build warnings/errors, including 333 CoreApi cases.
The [transcript](../../artifacts/verification/foundation-baseline-combined-2026-10-04.log)
and [archived Cobertura report](../../artifacts/verification/foundation-baseline-2026-10-04/coverage/report/Cobertura.xml)
retain source-specific evidence. Core authentication covers 81/81 executable lines
and 15/20 emitted branches; the changed public-auth guard covers 8/8. Existing
null/claim-condition branches remain partial, and native JWT/framework assemblies
are outside the repository coverage claim. Unmatched routes still invoke normal
authentication; independence from discovery is not claimed for them. Live OIDC,
actual TLS and deployment remain unqualified. GATE-001 remains separately blocked
by invoice-core review findings; this narrow qualification is not whole-product
acceptance.
