# Application UI component and token governance

**Scope:** the implemented Web business and Web/Workstation entry components and their shared semantic tokens and host styles.  
**Product version:** `v0.0.1`, governed by [Product Identity and Versioning](../product/PRODUCT_IDENTITY_AND_VERSIONING.md).  
**Source baseline:** `8e4f4be` before the 2026-10-15 UI restructuring described below. The committed source and regression guards identify the resulting implementation.

This owner records current reuse, token ownership and how those assets change. Runtime claims remain owned by [public entry](PUBLIC_APPLICATION_ENTRY.md) and [signed-in entry](SIGNED_IN_APPLICATION_ENTRY.md). The latter still requires live ZITADEL and supported-Windows qualification. Project/test/host inventory remains in [README.IMPLEMENTATION.md](../../README.IMPLEMENTATION.md).

## Current component scope

| Asset | Source and actual consumers | Reuse decision |
|---|---|---|
| `ApplicationShell` | [Shell](../../apps/web/Application.Web/Components/Shared/ApplicationShell.razor), used by Entry, Workspace, LoginResult, NotFound, unauthorized routing and the unavailable-page fallback | Shared Web page structure, skip link, focusable main landmark and header/footer composition. |
| `ApplicationHeader` / `ApplicationFooter` | [Header](../../apps/web/Application.Web/Components/Shared/ApplicationHeader.razor) and [footer](../../apps/web/Application.Web/Components/Shared/ApplicationFooter.razor), composed once by the shell and rendered by its consumers | Shared within Web; validated identity controls branding and conditional information links. |
| `StatusMessage` / `ArrowIcon` | [Status](../../apps/web/Application.Web/Components/Shared/StatusMessage.razor) and [icon](../../apps/web/Application.Web/Components/Shared/ArrowIcon.razor), used across entry, workspace and result pages | Consistent polite status announcements and decorative vector icons. Status text conveys outcomes independently of color. |
| `BusinessShell` / `PendingRequestNotice` | Web commercial pages: Customers, Individuals, Representatives, Catalog, Pricing, Orders, Billing, Quotations, Approvals, Fulfillment, Purchasing, Tenant administration and Program order policy | Shared checked-workspace navigation, status focus, safe saved-request recovery and departure acknowledgement. |
| `BusinessPage` / `BusinessApiClient` | Web business pages, server-side request and lifetime coordination | Reuse presentation/request semantics only; CoreApi owns business decisions and authorization. |
| `TenantPicker` | [Picker](../../apps/web/Application.Web/Components/Shared/TenantPicker.razor), used by Web Workspace | Keep within this journey while it has one consumer. Its directory does not establish a product-wide contract. |
| `TenantEntryPanel` | [Panel](../../apps/desktop/workstation/Application.Workstation/TenantEntryPanel.axaml), composed by [MainWindow](../../apps/desktop/workstation/Application.Workstation/MainWindow.axaml) | Keep within native entry; reuse native controls, shared styles and presentation view model. |

The shared [application-entry adapter](../../infrastructure/application-entry/Application.ApplicationEntry) owns bounded HTTP contracts and safe outcome messages. It supplies no visual controls or business permission authority. Web and Workstation remain separate presentation hosts over the same server meanings.

## Current token ownership

The application-owned [DTCG source](../../apps/design/tokens.json) defines the current light palette, semantic color roles, typography sizes, spacing, radii, borders and dimensions. [The token builder](../../eng/build-ui-tokens.py) resolves aliases and generates [Web variables](../../apps/web/Application.Web/wwwroot/tokens.css), [Web CSS](../../apps/web/Application.Web/wwwroot/app.css) from its [stylesheet source](../../apps/design/web.css), and [Avalonia resources](../../apps/desktop/workstation/Application.Workstation/Styles/DesignTokens.axaml). Edit the sources and regenerate; do not hand-edit generated files.

| Role | Web property | Avalonia resource | Value |
|---|---|---|---|
| Main text / primary action | `--color-text` / `--color-action` | `Color.Text` / `Color.Action` | `#20352E` |
| Secondary text | `--color-muted` | `Color.Muted` | `#59685F` |
| Page / surface | `--color-page` / `--color-surface` | `Color.Page` / `Color.Surface` | `#F7F6F1` / `#FFFFFF` |
| Decorative / control border | `--color-line` / `--color-control` | `Color.Line` / `Color.Control` | `#D8DFD5` / `#79877C` |
| Accent / focus | `--color-accent` / `--color-focus` | `Color.Accent` / `Color.Focus` | `#D9EF86` / `#43664F` |
| Error / warning | `--color-danger` / `--color-warning` | `Color.Danger` / `Color.Warning` | `#963D32` / `#74501F` |

Use semantic colors in consumers. The native adapter also emits typed Color, panel GridLength, Thickness and CornerRadius resources; CSS pixel dimensions map to Avalonia logical units. Font families remain host adapters: the system Web stack and bundled native Inter/Segoe UI. The [source guide](../../apps/design/README.md) owns regeneration commands and the intentional host mappings. [Native controls](../../apps/desktop/workstation/Application.Workstation/Styles/Controls.axaml) share button/input styles; Fluent's accent palette uses the same semantic action color for focus and selection.

The former Web `--ink`, `--muted`, `--paper`, `--line` and `--accent` remain aliases. Native `Ink`, `Muted`, `Paper` and `Line` were private application resources; all current consumers migrate together to semantic keys. They were never an external package contract. No public parameter, command, endpoint, business permission or tenant-selection contract is removed.

Preserve the validated deployment brand and current light theme. Theme modes and broader branding variation require their own requested scope. The upstream reference kit remains design input; its demo tokens, aliases and release schedule do not version or deprecate application assets. Validate the actual application JSON and generated host consumers. The normal gate checks generation freshness and token-builder behavior; validating bundled reference tokens proves only the reference library.

## Contribution and promotion

1. Identify the user task and existing consumers before adding or extracting an asset. Keep a one-use pattern with its journey.
2. A candidate for broader sharing needs at least two actual call sites and a stable shared meaning. A folder name or similar appearance does not establish that meaning. Header/footer reuse is evidence for their Web scope, not cross-host reuse.
3. Reuse the existing host theme and accessible controls. Add only the variants, sizes and states the current journey needs.
4. Document the anatomy, parameter/command contract, token roles and applicable interaction states. Keep business rules and authorization in their existing owners.
5. Verify the changed consumers, update this inventory and add a change-log entry below. Promote a stable contract only after its actual usage and evidence support the wider scope.

No contribution needs a new package, registry, component abstraction or process merely to advance a promotion label. Review and integration follow the repository's existing source and GitButler workflow.

## Component state and accessibility bar

| Asset | Anatomy and supported presentation states | Evidence to retain when changed |
|---|---|---|
| Header | Entry link and brand symbol/name, host/version label; configured identity and neutral unconfigured identity; responsive text/layout | Encoded branding, meaningful entry-link name, keyboard focus, long names and narrow-width rendering. Loading, error and selection are owned by the surrounding journey. |
| Footer | Legal name, conditional information navigation and context label; configured/unconfigured identity and responsive wrapping | Safe encoded links, navigation name, keyboard access and readable wrapping. Selection/loading/input-error states do not apply to its own contract. |
| Tenant picker | Heading, labeled radio group, explicit entry action and paging; empty/unselected/selected/busy states; denial and failures displayed by `Workspace` | Native radio keyboard model, visible focus, disabled/busy behavior, safe text, paging, explicit selection and current server-authorized entry. |
| Native access panel | Status, system-browser sign-in, labeled ComboBox, entry/recheck/switch/paging commands, progress/cancel/sign-out; signed-out, busy, selected, empty, denied, disabled-account and expired states | Actual bindings and keyboard actions, cancellation/late-response protection, labels/AutomationProperties, scaling, OS theme/control states and supported-target evidence. |

For interactive controls, consider default, hover, focus, pressed, disabled, loading, error and selected states. Record a state as inapplicable only with its reason. Framework defaults must be inspected where relied on; source declarations alone do not prove rendered contrast, focus visibility or assistive-technology behavior. Loading must remain distinguishable from unavailable, and status text must reflect the actual outcome.

Existing public parameter shapes, command enablement, focus/default-action behavior and tenant-selection semantics are part of the consumer review. UI hiding never grants authority. Switching or rechecking must retain the existing cancellation, credential custody and current-server-check guarantees.

## Change classification and version lock

Use these compatibility classes in reviews and change-log entries:

| Class | Application to current assets |
|---|---|
| Major / breaking | Remove or rename a supported shared token/parameter/command contract, change its default behavior or keyboard model incompatibly, or restructure a consumed component contract. Provide consumer impact and migration evidence first. |
| Minor / additive | Add a backward-compatible component, token role, optional parameter or applicable variant. Repeated-use evidence is required before promoting it into broader shared scope. |
| Patch / corrective | Fix a defect, documentation or a value within the accepted visual/behavioral intent. Material appearance or behavior changes require reviewing whether they are additive or breaking. |

These are change classes, not permission to assign a product release. While the product lock remains in force, retain `v0.0.1` in version markers and build metadata and use Git revisions to identify changes. No separate application design-system package version is introduced here. The upstream kit's `2.9.8` is its dependency provenance only.

## Deprecation and removal

No application component/token deprecation is declared by this initial inventory. For a future supported-contract removal, record the reason, exact consumers, replacement, old-to-new mapping and an explicit approved removal milestone. Keep the old contract working through at least one actual minor release of the owning contract before a breaking release removes it. While no such release sequence exists under the product lock, keep compatibility or obtain an explicit reviewed compatibility decision; do not invent a release cycle to expire it.

Apply migrations to every current consumer, retain relevant behavior/interaction checks and communicate the change in the log. Private implementation changes with no supported consumer contract do not require an artificial deprecation mechanism. Never add a speculative alias or warning for a removal nobody has requested.

## Verification, evidence and requalification

Use the existing Web and Workstation test projects, actual browser/native journeys and the repository verification contract for the property changed. `EntryPageTests` guards encoded public branding and truthful availability. `TenantEntryScreenTests` guards native selection/binding/keyboard behavior, account outcomes and late responses. The controlled browser guard covers the real Web process, synthetic-protocol entry journey, axe checks, keyboard focus, state screenshots and responsive reflow; it does not qualify live ZITADEL or Windows.

Workstation tests use the actual Skia renderer on the headless platform, exercise wide/compact layout changes and keep controls reachable by keyboard in the scrolling panel. Optional `APPLICATION_UI_ARTIFACTS` captures frames. These frames are native rendering evidence on the test platform, not Windows or screen-reader qualification. The separate `eng/test-workstation-x11.py` guard launches the actual Linux process, sends the keyboard connection action, verifies branding/readiness requests and title, and checks the painted right margin for card overflow. It needs Xvfb, python-xlib, ImageMagick and a built Workstation host. Run it after changes to initial measurement, platform rendering or selection colors; inspect its retained frame. It does not qualify Windows login.

For a changed color, measure its actual foreground/background pairing and inspect hover/focus/pressed/disabled/loading/selected states that use it. For layout or control changes, retain screenshots and actual keyboard/control results at relevant widths or OS scaling. Treat browser, screen-reader or target-device checks that did not run as pending evidence.

The product-version regression guard remains `ProductVersionMarkersMatchTheLockedProductVersion`. Requalify changed parameter/command or token meaning, focus/keyboard behavior, theme/resource mappings, framework versions, render mode and supported OS/browser. Follow the focused entry owners for security/provider/runtime requalification and the documentation instructions for repository-gate requirements.

## Change log

All entries remain within product `v0.0.1`; classifications do not assign new release numbers.

| Revision / entry | Class | Change and consumer impact |
|---|---|---|
| 2026-10-18 Owner and policy presentation | Minor / additive | Add tenant authorization proposal/reconciliation and exact-revision Owner handoff controls, plus two-setting program policy edit/publication. Reuse shared semantic controls and uncertainty recovery. CoreApi retains authority, provider holds and immutable policy meaning. |
| 2026-10-18 commercial continuation | Minor / additive | Add quotation/approval subject and offer views, program-reference editing, Fulfillment and Purchasing pages, shared navigation and deep-route skip-link recovery. Reuse existing semantic tokens; retain exact server revisions, frozen uncertainty envelopes, typed corrections and current authority. No Workstation changes. |
| 2026-10-16 Web commercial and Billing journeys | Minor / additive | Add protected online business pages using the existing semantic theme, shared native form controls, receipts and uncertainty feedback. Add explicit financial confirmations and fresh-identity initiation without client authority. No Workstation business surface or token meaning changes. |
| 2026-10-15 shared UI restructuring, based on `8e4f4be` | Minor / additive and Patch / corrective | Add application token generation, shared Web shell/status/icon presentation, consistent control states and readable typography. Reflow Web at narrow widths and native entry below 960 logical units; native minimum is 520 by 560 with a scrolling panel. Correct skip-link and result focus. Migrate private native resource consumers together; preserve existing public contracts and product version. Record governance and regression routes. |
| `8e4f4be` | Patch / tooling guidance | Add the pinned Codex UI reference kit and host adapters. Application runtime behavior is unchanged. |
| `5d2d767` | Minor / additive | Add configured login/account/tenant/workspace entry, shared Web header/footer, tenant picker and native access panel. Existing public entry remains available; live qualification stays with the signed-in owner. |
| `5645e34` | Minor / additive | Introduce the bounded public Web/Workstation launch, branded connection and availability journey under its focused owner. |
