# Workstation and private Admin Web review — 2026-10-19

Product version: **v0.0.1**. The requested order is Workstation, private Admin
Web, then publication of the completed changes. This review uses the
current source and existing capability owners. It does not qualify a complete
desktop product or replace the private Admin transport prerequisite.

## Workstation result

The current Avalonia host implements public connection/branding and configured
system-browser sign-in, account resolution, bounded tenant selection, independent
workspace admission/recheck/switch and application sign-out. Its owners are
[public entry](../implementation/PUBLIC_APPLICATION_ENTRY.md) and
[signed-in entry](../implementation/SIGNED_IN_APPLICATION_ENTRY.md).

Two confirmed state defects are corrected:

| Trigger and prior effect | Correction | Permanent guard |
|---|---|---|
| Editing the service address retained the previous branding, connection result and signed-in workspace confirmation | Clear those observations and credentials immediately, cancel the previous check and require a fresh check even when changing back; late work cannot overwrite the new unchecked state | Two added `ConnectionScreenTests` and two added `TenantEntryScreenTests` |
| Closing the access model left its service-ready admission set; a later connection update could enable sign-in on the closed instance | Idempotent disposal clears admission, cancels pending work and ignores subsequent service updates and late credentials | `ClosingDuringSignInCannotBeUndoneByLateCredentialsOrConnectionChecks` |

Changing the public input never changes the authentication adapter's configured
API origin. No token is sent to an operator-entered replacement origin. The fixes
change presentation/lifetime state, not account/tenant authorization or token
validation. Existing native controls, automation metadata, keyboard bindings,
responsive layout and semantic design tokens are retained.

Protected desktop business screens, valuable draft persistence, encrypted
SQLite/WAL, outbox/sync, Guard, signing/update/installation and supported-Windows
qualification remain outside the implemented entry scope. Client Web business
completion is not evidence that those native responsibilities exist. The signed-in
entry aggregate remains **BLOCKED** under its existing owner.

## Private Admin Web result

The selected [topology](../admin/PLATFORM_ADMIN_WEB_TOPOLOGY.md) remains an
independent private Interactive Server app with its own origin, cookie and identity
client, consuming AdminApi directly. There is currently no Admin Web executable.
Its runtime and trusted per-person device-proof transport remain **NOT_INTRODUCED**.
The owner reconfirmed per-person **mTLS** as the device-proof requirement. The
gateway is still pending; readiness review is the selected Admin Web work for this
pass. Its product-specific propagation contract is not yet implemented or qualified.

The transport prerequisite is confirmed in current source:

- [AdminClientCertificateProvider](../../services/admin-api/Application.AdminApi/AdminClientCertificateProvider.cs)
  reads `HttpContext.Connection.GetClientCertificateAsync`; it accepts no browser
  claim or forwarded certificate/identity header.
- [PlatformAdminRequestAuthorizer](../../services/admin-api/Application.AdminApi/PlatformAdminRequestAuthorizer.cs)
  requires HTTPS, one exact issuer/subject, the observed certificate fingerprint,
  an active principal-bound registered device, the endpoint's declared platform
  permission and durable access-audit admission.
- An Interactive Server outbound connection proves possession of its own TLS
  private key. A generic server certificate cannot stand in for the human device,
  and the browser certificate's public bytes do not supply its private-key proof.

The current [AdminApi test environment](../../tests/integration/Application.AdminApi.Tests/AdminApiTestEnvironment.cs)
injects a fixed observed-certificate provider for its boundary tests. Those tests
exercise principal/device/permission/audit decisions; they do not demonstrate an
actual browser-to-gateway-to-AdminApi mTLS handshake or delegated device proof.
That target transport evidence remains required.

The source-backed UI task map is ready for the transport decision; these are
candidate journeys, **not implemented screens**:

| Operator task | Existing AdminApi surface | UI obligation |
|---|---|---|
| Confirm administrator access | `GET /api/v1/platform/access` | Distinguish identity, device/authorization denial and unavailable dependencies; no tenant-session fallback |
| Browse tenants, accounts and tenant memberships | The six registry GET routes in [Program.cs](../../services/admin-api/Application.AdminApi/Program.cs) | Resource-typed cursors, explicit next/reload/detail, no invented search/count/identity-binding directory |
| Provision tenant | `POST /api/v1/platform/tenants` | Explicit intent/key, safe server result, no automatic business/profile defaults |
| Import existing human or link identity | `POST /api/v1/platform/accounts`, `POST .../accounts/{accountId}/identities` | Exact existing provider subject; no provider-user creation claim or disclosure through the account directory |
| Invite/designate initial Owner; activate/suspend/remove membership | `POST .../tenants/{tenantId}/memberships`, `.../memberships/initial-owner`, `.../memberships/{accountId}/{operation}` | Server revision on transitions; explain conflicts and protected initial Owner; no inferred grant from a visible button |
| Suspend/reactivate tenant | `POST .../tenants/{tenantId}/lifecycle/{operation}` | Explicit target/confirmation and expected revision; stale intent requires review |
| Publish/activate profiles or assign a legacy Order policy | [TenantProfileEndpoints](../../services/admin-api/Application.AdminApi/TenantProfileEndpoints.cs) and [LegacyOrderProfileAssignmentEndpoint](../../services/admin-api/Application.AdminApi/LegacyOrderProfileAssignmentEndpoint.cs) | Typed bounded input, immutable publication and explicit activation/legacy assignment; no general workflow/form authoring |

The focused backend owners retain wire validation, operation permissions, durable
idempotency and concurrency semantics. A future UI must retain an exact retry
intent/key when an outcome is unknown and must not report a timeout as a failed
durable mutation. This task does not select new recovery/session infrastructure.

To admit the private runtime, first provide the actual gateway product/version and
its authenticated upstream certificate-proof contract, or deliberately select and
qualify another per-admin transport. Required proof includes actor/device binding,
forged-header rejection, replay/substitution resistance where delegation exists,
revoked-device denial and separation from tenant credentials. Private reachability
alone supplies none of those application guarantees. The topology owner's
independent review requirement remains open; this agent's source review is not
that independent review. No security adapter or placeholder executable was added.

## Rendered Web follow-up

The full six-journey browser wrapper passed on the real Release Web host with
controlled identity/API fixtures. Manual review of its forced-color screenshots
then confirmed that the current navigation link lost its visual distinction;
axe left the associated color result incomplete. The shared theme now uses system
`Canvas`/`LinkText` colors and an underline for the current page in forced-color
mode. The rendered guard requires a visible cue independent of color and at least
4.5:1 measured current-link contrast in that mode. Normal theme tokens and
application permissions are unchanged. The updated six-journey wrapper passed
with all of its original assertions and the new forced-color check. It retained 90 screenshots and 86 accessibility
reports, with zero reported violations. Eighteen reports still contain manual
checks (mostly partly obscured form controls, plus initial heading/forced-color
button checks); they are not reported as complete accessibility qualification.
The corrected current-navigation screenshot was visually inspected.

The initial attempt overlapping container work failed its first keyboard-focus
assertion. Diagnostic attempts also encountered Chromium `ERR_NETWORK_CHANGED`;
a later isolated diagnostic and sequenced full run passed with the same focus
assertions. The exact cause of the first focus result is unconfirmed. Concurrent
browser/container reliability is not claimed and no assertion was removed.

## Deployment addresses and AGENTS.md

Active Web and Workstation API/identity origins come from validated deployment
configuration (`ApplicationEntry:CoreApiBaseUrl`, the Web authentication keys and
`APPLICATION_CORE_API_URL`/`APPLICATION_OIDC_*`). There is no `api.example.test`
or `api.example.com` runtime fallback. Workstation starts with an empty input when
its API address is absent; its `app.example.com` watermark/error example is an
illustration, not the input value or a network target. Tests deliberately retain
synthetic addresses, including different trusted/untrusted origins, to prove
origin isolation without contacting real deployments.

The root `AGENTS.md` now explicitly requires validated configuration for deployment
origins and keeps synthetic addresses out of runtime defaults. Framework XML
namespace URIs, the native dynamically bound loopback callback, design-time-only
PostgreSQL strings and the disabled object-store adapter's canonical provider
endpoint retain their separate meanings. No environment, token or credential was
copied into committed configuration. The exact scan is `runtime-uri-audit.json`.

Eight actual-process startup checks passed (`configuration-startup/results.json`):
Web rejects missing, non-loopback HTTP, path-bearing and credential-bearing API
origins; an explicitly configured dynamic loopback origin starts and serves
`/health/live`. Native startup rejects partial login and insecure API settings;
with no deployment origins it renders an empty connection input and no login
admission. The actual native unconfigured screenshot was inspected. Existing
`ServiceAddressesRequireASafeOrigin` and native/Web protocol tests remain the
regression owners; these supplemental probes are not live-provider qualification.

## Current verification and remaining work

Current command receipts and exact source hashes are retained in
`artifacts/workstation-admin-review-2026-10-19/`. The offline `ui-review.html`
gallery provides the 98 retained Web/native captures for visual review.

- **20 Workstation tests passed, 0 failed, 0 skipped**, including all five new
  regressions. The actual `Application.Workstation.Tests` run compiles the current
  host/AXAML and exercises its native controls, two-way address binding, keyboard
  entry, resize/focus, cancellation and controlled system-browser protocol flows.
- Full native host and test-project format checks passed. Independent Workstation
  restore/build/publish passed with zero build warnings/errors. The real Linux/X11
  process guard passed keyboard input, actual HTTP branding/readiness and rendered
  title/state. Changed-address, compact 520px and actual X11 screenshots were
  inspected. These are Linux/headless results, not a supported-Windows claim.
- All executable hosts listed in `eng/hosts.tsv` published successfully from the
  exact Release build. `all-host-publication-results.json` retains each command
  and exit code; no bootstrap or migration executable was run against a deployment.
- Windows x64 cross-publish succeeded in an isolated copy of the current source.
  The framework-dependent .NET 10 package is
  `Application.Workstation-v0.0.1-win-x64.zip` (SHA-256 and size in
  `windows-artifact.json`). The initial locked RID restore correctly rejected a
  missing RID graph; only the isolated copy's RID lock entries were derived, with
  every original dependency group and repository lock verified unchanged. This is
  build evidence; supported-Windows execution, signing/installation and live
  browser callback remain unrun for the owner's later target test.
- The user approved the required package restoration. Locked Avalonia 11.3.22,
  MVVM 8.4.2 and native OIDC dependencies were restored without changing the package
  graph. Existing Docker images are reused for repository checks; no image pull is
  needed. Other cached framework versions were not substituted.
- The initial offline supplemental model harness passed nine method bodies before
  native dependencies were available. Its recovered published assemblies, source
  hashes and initial wrapper-only analyzer failure are retained. Those checks are
  separate from the actual native suite and do not qualify AXAML or Windows.
- The exact canonical `COLLECT_COVERAGE=1 ./eng/verify.sh` passed on the final
  source: **2,004 tests across 31 projects, 0 failed, 0 skipped**. Full-solution
  format and Release build passed with zero warnings/errors. All 15 named
  money/authority-path coverage checks passed; their uncovered lines/branches
  remain reported, not claimed covered. The run completed in 2,890.24 seconds,
  with all 871 compiled/build/verification source files unchanged. Receipts are
  `canonical-result.json`, `canonical-source-manifest.json`,
  `final-verification-summary.json` and `material-coverage-review.txt`.
  The first restored attempt found corrected Quotation editor whitespace. Earlier
  interrupted attempts (including exit 143 and the deliberate stop for the
  forced-color fix) remain separately retained and do not supply this pass.
- All 11 verification-wrapper regression tests passed. They check command failure
  propagation, host publication requirements, browser prerequisites, fresh evidence
  and per-journey failure rejection; they are separate from actual application tests.

Requalification triggers are address binding, connection/result cancellation,
native close/session lifetime, authentication-origin configuration, framework
dependencies, target OS and any Admin device-proof/topology decision. The existing
Web results remain separate under
[the client functionality receipt](WEB_CLIENT_FUNCTIONALITY_2026_10_19.md).
Repository verification is complete for this source snapshot. Execution on a
supported Windows target and live-provider evidence remain pending; private Admin implementation
waits on the named transport prerequisite. This repository pass does not qualify
the complete product or remove the signed-in entry owner's existing **BLOCKED**
claims. Backend source, all 69 package lockfiles, unrelated incoming
skills/configuration and the verification script were preserved. No swarm or
additional model was used.
