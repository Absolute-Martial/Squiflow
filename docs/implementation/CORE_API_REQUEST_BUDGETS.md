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
the same ordering. The public-auth correction is implemented for this explicitly public scope and
its focused guards are green, but ADM-003 independent post-fix acceptance is
The required reviewer receipt is retained at [ADM-003-INDEPENDENT-REVIEW-RECEIPT.md](../production-completion-review/evidence/ADM-003-INDEPENDENT-REVIEW-RECEIPT.md) with an explicit `ACCEPTED` disposition for its declared narrow scope; its recorded reviewer-independence caveat remains a gate-owner decision.
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

The public-auth correction is present in commit `6735370`, and the retained focused regression suites cover bearer-bearing public health, protected discovery/cancellation, and trusted endpoint-metadata skip conditions. The source archive does not retain the historical `artifacts/verification/adm-003/reviewer/REVIEW.md` independent post-fix receipt or the historical combined-gate artifacts that older notes referenced. Those absent files must not be treated as current acceptance evidence.

The Core focused fixture uses controlled account/authorization ports and does not supply new live identity-provider or PostgreSQL/OpenFGA qualification. Current focused evidence supports the implementation, while independent ADM-003 acceptance remains pending under [the tracked review assignment](../production-completion-review/assignments/ADM-003-INDEPENDENT-REVIEW.md). Native JWT/framework coverage, live OIDC, actual TLS and deployment remain unqualified.
