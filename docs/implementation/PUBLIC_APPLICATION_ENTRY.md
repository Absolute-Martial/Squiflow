# Public Web and Workstation entry

**Product version:** v0.0.1

**Owner-selected scope, 2026-10-13:** launch, backend connection and branded application entry on both hosts.

**State, inspected 2026-10-14:** the bounded public implementation and repository build/test contract are `PRODUCTION_HONEST` for the evidence below. This does not qualify WEB-002, GATE-002 or a supported Windows Workstation release. The real desktop launch was exercised on Linux/X11; the Windows artifact was cross-published and has no live Windows execution evidence.

## Intent and ownership

An operator can launch the Web entry page or Workstation screen, check one configured CoreApi service, see validated public identity and distinguish service availability from unavailable, not-ready, malformed, timed-out or canceled checks. The result never grants account/tenant access or reports a business mutation.

The owner explicitly pulled this public journey forward. GATE-002 and the login/session gates still govern authenticated business workflows. Existing backend blockers remain owned by [the implementation inventory](../../README.IMPLEMENTATION.md).

`Application.Web` uses Blazor Interactive Server. `Application.Workstation` is an Avalonia executable. `Application.ApplicationEntry` is a shared HTTP adapter under `infrastructure/application-entry`; its separate project earns cross-host reuse without putting HTTP/UI dependencies into Branding. It references only the existing host-neutral `BrandProfile`. Neither host references the other.

The only remote operations are `GET /api/v1/application/bootstrap` and `GET /health/ready`. Readiness is a public service check, not authentication, permission, migration or completed-business-journey evidence.

## Contract and failure behavior

- Require an absolute HTTPS origin without credentials, query, fragment or path. HTTP is permitted only for loopback development. Web gets its server-side target exclusively from deployment configuration; Workstation accepts an operator-entered address.
- Apply one eight-second budget across bootstrap headers/body and readiness. Caller cancellation stays cancellation; deadline expiry returns an explicit timeout. No retries or offline queue are introduced.
- Read at most 16 KiB of JSON, including bodies without Content-Length, with bounded depth. Reconstruct using `BrandProfile.Create`, require its canonical revision and validate links before rendering.
- Refuse redirects and HTTP cookies; bound the pool to two connections per origin. No token, credential or business record crosses this adapter.
- Resolve relative branding links against the CoreApi origin. Reject backslashes and unsafe schemes. Razor encodes names and Avalonia binds them as plain text.
- Clear previous identity before a new check and on failure. Not-ready may retain freshly validated identity but cannot show available/success. Cancel/close never imply success.

The visual baseline uses neutral colors, status text, labeled controls, keyboard access and responsive Web layout. Names, legal identity and Web support/privacy/terms/favicon come from validated deployment branding. There is no codename, invented tenant data or placeholder business navigation. One desktop action does not earn module registries or docking.

## Host lifetime and security

Web checks public state during initial rendering and explicit refresh. Circuit state is disposable; a new page/circuit checks again. Framework antiforgery is enabled. There are no authentication sessions or cookie-backed business mutations in this slice.

Responses use no-store, scripts/connections from the Web origin, no embedded frames/objects, nosniff and no-referrer. HTTPS branding images are permitted; loopback development additionally permits images from its one configured API origin. Inline styles remain permitted for Blazor framework UI; inline scripts/eval are not. Circuit loss shows a reconnect/reload overlay. Production Web hosting requires HTTPS. Disconnected circuits retain at most 16 entries for 30 seconds; these are initial lifecycle bounds, not a measured capacity target. Transparent multi-node failover is not claimed.

Workstation keeps only address/public result in process memory. The command disables duplicate actions/address edits while pending, exposes cancellation and cancels on close. Restart requires a fresh check. No SQLite database, secret store, outbox, sync process or Guard is introduced.

## Run and verify

Use the SDK from `global.json` and the actual independent host roots:

```sh
./eng/verify-host.sh web
./eng/verify-host.sh workstation
./eng/build-host.sh web --publish
./eng/build-host.sh workstation --publish
./eng/verify.sh
```

Launch against an existing configured CoreApi:

```sh
ApplicationEntry__CoreApiBaseUrl=https://api.example.com \
  dotnet run --project apps/web/Application.Web -- --urls https://localhost:7100

APPLICATION_CORE_API_URL=https://api.example.com \
  dotnet run --project apps/desktop/workstation/Application.Workstation
```

URLs are illustrative: supply the real origin. Web rejects absent/invalid startup configuration. Workstation can use its labeled input instead. Loopback CoreApi can use `http://127.0.0.1:<port>`; its existing branding/provider requirements still apply. Tests and screenshots use explicit synthetic identities, never a product fallback.

## Source admission

| Mechanism | Pin and inspected source | Admission / license / reason |
|---|---|---|
| Blazor | SDK 10.0.401 Interactive Server template: `Program.cs`, `App.razor`, `Routes.razor` | Standard .NET host, Razor/HTML/CSS; no component suite. |
| Avalonia Desktop, Fluent, Inter | 11.3.22; revision `627ae9ef921621e27e7aa58df2796fbadb50af88` | MIT package declarations; direct admission under the [Workstation review](../review/WORKSTATION_FRAMEWORK_ADMISSION_RESEARCH.md). Maintained 11.x preserves existing xUnit 2; a major upgrade requires requalification. |
| Avalonia.Headless.XUnit | Same version/revision; `tests/Avalonia.Headless.UnitTests/InputTests.cs` keyboard/click/focus cases | MIT; test-only real controls/bindings/input. |
| CommunityToolkit.Mvvm | 8.4.2; revision `35dfe7b12da0e64384bfaec7ebc0883196a89f9f`; `ObservableObject`, `AsyncRelayCommand` | MIT; presentation notifications/concurrency/cancellation only, no Messenger/business authority. |
| HTTP/JSON/cancellation | .NET `SocketsHttpHandler`, `HttpClient`, `System.Text.Json`, `CancellationTokenSource` | Platform mechanisms with a narrow owned outcome mapper and reused Branding validation; no generic client framework. |

Locks retain the transitive graph. Requalify versions, licenses and advisories on upgrade. Primary guidance: [Avalonia headless tests](https://docs.avaloniaui.net/docs/testing/headless-xunit), [compiled bindings](https://docs.avaloniaui.net/docs/data-binding/compiled-bindings), [Blazor CSP](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/content-security-policy?view=aspnetcore-10.0).

## Evidence and guards

| Claim / owner | Permanent guard | Requalification trigger |
|---|---|---|
| Public contract and bounded safe HTTP / shared adapter | `Application.ApplicationEntry.Tests`: real loopback HTTP, readiness, malformed/unsafe/oversized bodies, redirects, separate cancellation/deadline paths | API contract, validation, pool, budget, URI handling |
| Encoded branded Web entry / Web host | `Application.Web.Tests`: ASP.NET pipeline, safe links/headers, hostile names, outage/reload; browser evidence | Razor/render mode, CSP, bootstrap/session custody, browser |
| Desktop action and clear state / Workstation | `Application.Workstation.Tests`: real headless controls, keyboard/default action, resize, busy/cancel, stale identity; native launch evidence | Avalonia/Toolkit, lifetime, target OS, layout |
| Placement, identity and executable graphs / repository | `Application.Architecture.Tests`, host manifest, independent builds/publishes, normal `eng/verify.sh` | References/packages/host inventory |

Exact inspected results/screenshots are retained under `artifacts/application-entry-2026-10-13/`. The final source's `./eng/verify.sh` completed all phases in 2,207 seconds: **1,691 passed, zero failed, zero skipped across 31 test projects**, with zero build warnings/errors. This is the normal repository gate, with test cases retaining each project's normal parallel behavior. It does not supersede the separate backend case/provider acceptance gates.

| Inspected evidence | Result / practical limit |
|---|---|
| `verify-web.log`, `verify-workstation.log` | Each independent host passed format/build and its five UI tests plus 31 shared public-client tests. |
| `browser-smoke.log`, `browser-results.json` | Real Chromium 156 / Playwright 1.64: desktop/mobile rendering, hydrated actions, readiness failure, cancellation, deadline, inert hostile names, actual Web stop/restart and fresh reload. Expected connection errors occurred during the deliberate outage only. |
| `native-smoke.log`, `native-results.json` | Actual Linux/X11 Workstation process, keyboard action, HTTP branding/readiness and rendered result/title. Avalonia headless tests separately guard bindings, validation, effective command disabling, cancellation and status accessibility metadata. |
| `publish-web.log`, `publish-workstation.log`, `publish-workstation-win-x64.log` | Independent framework-dependent publish artifacts, including a Windows x64 GUI executable; requires .NET 10. Cross-publish is build evidence only. |
| `catalog-check.log`, `login-preflight-tests.log`, `login-inventory-static.json` | 119-task catalog validated; topology static regressions passed; placeholder inventory explicitly reports `NOT_RUN_STATIC_ONLY`. No live login evidence. |

The shared adapter, public Web journey, desktop presentation state and repository host graphs are `PRODUCTION_HONEST` for this bounded implementation claim. Authentication and business/local-state responsibilities listed below are `NOT_INTRODUCED`. `BLOCKED = none` applies only to this declared public implementation scope; it does not clear the repository's existing blockers or certify an untested target.

Linux/native or headless evidence does not qualify live Windows accessibility, installation or supported-machine behavior; those need the actual target before a Windows release claim. No Linux product-support policy is introduced by using that environment for local execution evidence. Requalification requires the real target OS/browser/deployment, and any changed contracts, dependencies or lifetime/security behavior named in the guard table.

Authentication, account/tenant selection, protected business screens, valuable drafts, native PKCE callback, credential storage, local durability/encryption, sync, Guard, signing and updates remain `NOT_INTRODUCED`. No open session/callback decision is closed by this public implementation.

The owner-selected next task, login/tenant-entry preparation for both hosts, is
recorded in [the readiness handoff](LOGIN_AND_TENANT_ENTRY_READINESS.md).
