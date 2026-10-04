# ADM-003 independent post-fix reviewer receipt

**Disposition: `ACCEPTED`** — the correction is correct for its declared narrow scope, with one
non-blocking observation recorded below.

| Field | Value |
|---|---|
| Assignment | `docs/production-completion-review/assignments/ADM-003-INDEPENDENT-REVIEW.md` |
| Reviewer | opencode (Claude), acting as the retained acceptance reviewer for this repository |
| Reviewed HEAD | `d7780894fb06375f50112073c2d15f656b454920` |
| HEAD deviation | The assignment fixed the HEAD under qualification at `18aae19`. This review ran at `d778089`, a later commit. Both production authentication files are **byte-identical at `18aae19`, `d778089` and current `7deba82`**, so the reviewed production source is unchanged; only the test/doc paths listed below differ. |
| Implementation under review | `6735370` "Skip bearer validation on public health routes in both hosts" (author: Shadow-Martial) |
| Product version | `v0.0.1` |
| Review date (UTC) | 2026-10-04 |
| Environment | .NET SDK 10.0.401 (`global.json`), Docker 29.8.1, real Testcontainers hosts |

Source hashes of the two reviewed files at `d778089`:

```
9bf70d077e134c2a914a7dd2d737b57167f12909055243814caa8bf5815d3272  CoreApiAuthenticationRegistration.cs
a968c03d80dce2b47c368c591484e94c9fb2170c084ba41f3b19641e74876e25  AdminApiAuthenticationRegistration.cs
```

Complete set of reviewed paths, so the executed evidence set is reproducible. The four
non-production paths were also reviewed and have since changed:

| Path | Reviewed hash | Current at `7deba82` | Provenance of reviewed value |
|---|---|---|---|
| `tests/integration/Application.AdminApi.Tests/PublicHealthBearerReviewTests.cs` | `3edc2031…` | differs | `3edc2031` = commit `4dc4082` |
| `tests/integration/Application.CoreApi.Tests/PublicHealthBearerTests.cs` | `dff53e4f…` | differs | `dff53e4f` = commit `4dc4082` |
| `docs/implementation/ADMIN_API_REQUEST_BUDGETS.md` | `36c943e6…` | differs | no commit in history (implementer source ZIP state) |
| `docs/implementation/CORE_API_REQUEST_BUDGETS.md` | `bec8d00b…` | differs | no commit in history (implementer source ZIP state) |

Those four deltas are test-harness watchdog separation and owner-document prose. **No
assertion was weakened** — `Assert.False(discovery.Entered.IsCompleted)`,
`Assert.Same(pending, completed)`, the caller-cancellation assertion and the `discovery.Exited`
drain check are all intact — and they were re-verified on current source (§ Commands).

### Load-bearing assumption, verified against primary source

The `GetOrderedMetadata<IAuthorizeData>().Count == 0` conjunct is the single point that
prevents a mis-tagged protected route from skipping bearer validation. It assumes that
ASP.NET Core's `RequireAuthorization()` really contributes `IAuthorizeData` metadata — including
its policy and callback overloads. This was verified against ASP.NET Core `v10.0.12`
primary source (`JwtBearerHandler` message-received ordering, and
`AuthorizationEndpointConventionBuilderExtensions`) rather than assumed. A behavioural
backstop also exists: `PublicAuthMetadataHostReviewTests` registers a real route carrying both
a public classification and `RequireAuthorization()` in both hosts and observes the handler
being entered only for an authenticated caller.

## Reviewer independence disclosure

This reviewer is **not fully independent** of the change: it has committed and verified work in
this repository during the same working period, although it did not author commit `6735370`. Per the
assignment's intent, a reviewer independent of the implementer should confirm this receipt. Treat the
disposition below as a technically-grounded recommendation, not a substitute for a second-party
sign-off, if the gate requires organizational independence.

## Source inspected

25 added lines across two files, no other production change:

- `services/core-api/Application.CoreApi/Authentication/CoreApiAuthenticationRegistration.cs` — adds
  `OnMessageReceived` to the bearer options.
- `services/admin-api/Application.AdminApi/Authentication/AdminApiAuthenticationRegistration.cs` — same shape.

Both use an identical two-part guard:

```csharp
var endpoint = context.HttpContext.GetEndpoint();
if (endpoint?.Metadata.GetMetadata<TMetadata>() is { <public> } &&
    endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
{
    context.NoResult();
}
```

CoreApi keys on `EndpointAccessMetadata.IsProtected == false`
(`EndpointAccess.cs:32-35`: true unless the access is one of `PublicApplicationBootstrap`,
`PublicApiDescription`, `PublicLiveness`, `PublicReadiness`).
AdminApi keys on the narrower `AdminEndpointAccessMetadata.Access == AdminEndpointAccess.PublicHealth`
(`AdminEndpointAccess.cs:9`).

## Security review findings

Trusted endpoint metadata is the only skip condition. Confirmed, and the design fails safe in every
branch that is not an explicitly public, authorization-free route:

1. **Unknown / unmatched route** — `context.GetEndpoint()` is `null`, so the pattern match fails and
   `NoResult()` is **not** called. Bearer validation proceeds normally. No fail-open path.
2. **Public route that also carries authorization** — the `GetOrderedMetadata<IAuthorizeData>().Count == 0`
   conjunct blocks the skip, so a mis-tagged protected route still validates the token.
3. **Mis-tagged protected route** — a protected access value fails the `IsProtected == false` test,
   so no skip. Classification error degrades toward *more* validation, never less.
4. **Anonymous override misuse** — `EndpointAccessValidationTests.AuthenticationAndAnonymousOverridesMustMatchClassification`
   fails closed when classification and `Authorize`/`AllowAnonymous` disagree, and
   `MissingDuplicatedAndUnknownClassificationsFailClosed` rejects missing, duplicate and
   out-of-range `(EndpointAccess)int.MaxValue` classifications.
5. **Forged privileged headers on public routes** — `EveryProtectedRouteRejectsAnonymousRequestsAndForgedAdminHeaders`
   proves protected routes answer `401` with `no-store`; a forged admin header cannot promote a
   public or anonymous caller.
6. **No public route varies its response by identity.** The four CoreApi public routes
   (bootstrap, OpenAPI, liveness, readiness) and the two AdminApi health routes return
   identity-independent content, so `NoResult()` cannot silently degrade an
   identity-dependent response into an anonymous one.

## Behavior inspected with real-host tests

The suites substitute a blocking `IConfigurationManager<OpenIdConnectConfiguration>` and assert on
its `Entered`/`Exited` signals, so they observe whether identity-provider discovery actually ran —
they do not merely assert a `200`.

| Property | Test | Result |
|---|---|---|
| Public routes do not invoke discovery when a bearer is present (valid or malformed), across all 4 CoreApi public routes × 2 bearer states | `PublicHealthBearerTests.PublicHealthDoesNotInvokeDiscovery` | pass |
| Protected route **does** still invoke discovery and honors caller cancellation | `PublicHealthBearerTests.ProtectedRouteStillInvokesDiscoveryAndHonorsCallerCancellation` | pass |
| Public route bearer handling (both hosts) | `Application.AdminApi.Tests.PublicHealthBearerReviewTests` | pass |
| Metadata classification fail-closed behavior | `EndpointAccessValidationTests` | pass |
| Public route authentication metadata (both hosts) | `PublicAuthMetadataHostReviewTests` (CoreApi + AdminApi) | pass |
| Unmatched/rejected routes do not enter platform authority, `no-store`, empty body, zero audit rows | `RequestBudgetUnmatchedRouteReviewTests` | pass |

### Commands and results

```
dotnet test tests/integration/Application.CoreApi.Tests -c Release \
  --filter "FullyQualifiedName~PublicHealthBearer|FullyQualifiedName~PublicAuthMetadata|FullyQualifiedName~EndpointAccessValidation"
  → exit 0
  → Passed!  Failed: 0, Passed: 26, Skipped: 0, Total: 26

dotnet test tests/integration/Application.AdminApi.Tests -c Release \
  --filter "FullyQualifiedName~PublicHealthBearer|FullyQualifiedName~PublicAuthMetadata"
  → exit 0
  → Passed!  Failed: 0, Passed: 20, Skipped: 0, Total: 20
```

Logs: `ADM-003-COREAPI-FOCUSED.log`, `ADM-003-ADMINAPI-FOCUSED.log`.

Whole-repository normal gate on the reviewed HEAD, no overlapping build of this tree:

```
./eng/verify.sh
  → exit 0 · restore --locked-mode pass · format pass
  → Release build: 0 warnings, 0 errors
  → 741 passed, 0 failed, 0 skipped, 16/16 projects
```

Per-project results, verbatim from `evidence/verify.log`:
Architecture 14 · Branding 11 · Customers 21 · IdentityAccess 12 · Invoices 25 · Orders 86 ·
AdminBootstrap 3 · PlatformAdministration 4 · Profiles 18 · IdentityAccess.Postgres 19 · Tenancy 29 ·
CoreApi 333 · AdminApi 87 · Tenancy.Postgres 19 · Customers.Postgres 17 · Orders.Postgres 43.

**Failed-first-run evidence: none.** Every run executed for this review exited 0 on the first attempt.
No retry, no serialization, no provider or framework behavior was replaced or weakened to obtain
these results.

## Non-claims

- Local Testcontainers only. **No remote CI, live ZITADEL/OpenFGA provider, deployment or production
  claim** follows from this receipt.
- The gap was not reproduced against a live identity provider; the discovery boundary is proven
  through the real ASP.NET Core bearer pipeline with a substituted configuration manager.
- This receipt covers **only** the public-route authentication correction in `6735370`. It does not
  accept GATE-001, and it does not substitute for the ADM-001/ADM-002/OPS-022 retained handoffs or the
  independent post-fix ADM-003 acceptance receipt still outstanding for that gate.
- `context.NoResult()` skips bearer validation only. It does not grant, imply or substitute any
  authorization decision; protected routes remain governed by their existing authentication,
  current-authority and OpenFGA checks.

## Residual observations (non-blocking, no correction required)

Recorded from the second technical review pass; neither is a live defect at this baseline.

1. **AdminApi has no startup classifier validation.** `services/admin-api/Application.AdminApi/Program.cs`
   reaches `RunAsync()` without an equivalent of `CoreApiEndpointAccessValidation`. AdminApi's
   mis-tag defense therefore rests solely on the `IAuthorizeData` conjunct. Current code is correct
   — all eight protected routes carry `.RequireAuthorization()` — but a future protected AdminApi
   route that both omitted it and was mis-tagged `PublicHealth` would silently skip bearer
   validation. Mirroring the CoreApi startup classifier would close that.
2. **CoreApi's OpenFGA authorization is in-handler** (`IAuthorizationService.AuthorizeAsync` from the
   endpoint handlers), so it is invisible to the `IAuthorizeData` conjunct. A future route relying
   only on in-handler authorization would pass startup classification validation and be skipped —
   it would still deny an anonymous caller, so this is not an exposure, but it is worth recording.
3. CoreApi's skip condition covers four access values through `IsProtected == false`, while AdminApi
   targets the single value `PublicHealth`. The CoreApi form is broader by construction. It is correct
   today because the fail-closed classifier validation rejects unknown, duplicate and mismatched
   classifications, and because a mis-tagged protected route fails the `IsProtected` test. If public
   route kinds are ever added, the broader CoreApi condition will include them automatically — which is
   the desired behavior, but it means the safety of that route depends on the classifier validation
   remaining exhaustive. No change is requested by this review.

## Retained evidence

This receipt is retained at
`docs/production-completion-review/evidence/ADM-003-INDEPENDENT-REVIEW-RECEIPT.md`, with the focused
gate logs at `ADM-003-GATE-LOG.txt`, `ADM-003-COREAPI-FOCUSED.log` and
`ADM-003-ADMINAPI-FOCUSED.log`, and the full repository gate output at
`docs/production-completion-review/evidence/verify.log`. GATE-001 may cite this receipt for
the ADM-003 condition; the remaining ADM-001/ADM-002/OPS-022 handoffs are still outstanding.

## Requalification trigger

Re-run both focused filters plus the unmatched-route review suite, and re-inspect both
authentication registrations and their metadata types, if any of the following change:

- either `*AuthenticationRegistration.cs` in `services/core-api` or `services/admin-api`;
- `EndpointAccess.cs` / `EndpointAccessMetadata.IsProtected` or
  `AdminEndpointAccess.cs` / `AdminEndpointAccessMetadata`;
- `CoreApiEndpointAccessValidation` or the public-route registrations in either `Program.cs`;
- the public-health bearer or public-auth-metadata test suites.

A watchdog or harness change alone does not require requalification of this claim; a
production authentication change does.