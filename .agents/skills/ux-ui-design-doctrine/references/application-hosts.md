# Application host adapter

This adapter applies the upstream translation contract to the current repository.
The root and scoped `AGENTS.md` files and focused implementation owners remain
authoritative. The product version is v0.0.1; design-library versions are separate.

## Web — Blazor and CSS

- Extend `apps/web/Application.Web/Components/` and its existing shared header,
  footer and tenant picker. Use Razor semantics and bound presentation state.
- Extend application-owned `apps/design/tokens.json` and `apps/design/web.css`, then
  run `python3 eng/build-ui-tokens.py`. Consume generated semantic CSS variables and
  Avalonia resources for text, surfaces, controls, actions and focus. Upstream
  tokens are reference input, not a replacement brand.
- Keep business and permission decisions in CoreApi. UI state can initiate a
  request, display a safe outcome and clear prior workspace state; it cannot grant
  access. Preserve antiforgery, encoded output and server-only token custody.
- Use native HTML semantics, associated labels, appropriate live regions and focus
  management. Reuse buttons/links according to their actual behavior. Verify small
  viewports, zoom, long names, keyboard controls and asynchronous states in a real
  browser. Preserve the application's CSP and font/asset custody.

## Workstation — Avalonia and MVVM

- Extend `apps/desktop/workstation/Application.Workstation/` views, resource
  dictionaries and presentation view models. Keep commands bound to the actual
  current state, and preserve cancellation and late-response protection.
- Map semantic design roles to Avalonia resources/styles and applicable
  pseudoclasses such as focus, pointer-over, pressed, disabled and selected. Share
  definitions; do not paste a new palette into each XAML view.
- Prefer Avalonia controls, labels, AutomationProperties, keyboard navigation,
  focus and OS scaling behavior. ARIA/CSS and HTML browser gates are not native
  accessibility implementations. Use native state/interaction tests and actual
  target screenshots and assistive-technology checks when qualifying that claim.
- Keep provider credentials, authorization and durable business state outside
  presentation authority. Do not introduce a WebView, React/Tailwind layer,
  plaintext token persistence, SQLite or sync to satisfy a visual example.

## Component and token governance

Use [Application UI Governance](../../../../docs/implementation/APPLICATION_UI_GOVERNANCE.md)
for current assets, actual reuse, host token ownership, contribution, compatibility
and deprecation. Record changes in its change log. Validate the actual
application DTCG source, generation freshness and CSS/AXAML consumers. Preserve
the product-version lock and the focused entry owners' qualification boundaries.

## Verification route

Use the existing Application.Web.Tests, Application.Workstation.Tests and shared
application-entry tests for their declared properties. The controlled browser
guard is `eng/test-signed-in-entry-browser.cjs`; it requires built Web binaries,
external Playwright/Chromium and OpenSSL. Read its configuration before running.
Use the repository's normal `./eng/verify.sh` when the changed responsibility or
repository rules require it. Run checks once at the appropriate scope and inspect
the output; do not broaden into unrelated work merely because the kit lists a gate.

Read `docs/implementation/SIGNED_IN_APPLICATION_ENTRY.md` for the session scope.
Its Windows system-browser/callback and live ZITADEL qualification remain pending.
Guideline installation, headless controls or a local issuer cannot qualify them.
Business capability pages and other future responsibilities require their own
accepted scope; a design-library example does not introduce them.
