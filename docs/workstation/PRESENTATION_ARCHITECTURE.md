# Workstation Presentation Architecture

**Version:** v0.1.0
**Status:** Accepted architecture and source-admission direction; no Workstation project or runtime currently exists, and exact visual layout remains prototype/evidence driven.
**Authority:** This document owns the Avalonia Workstation shell/presentation composition boundary: what the desktop host owns, what business modules contribute to the Workstation UI, how navigation/commands are registered, and the initial visual/interaction discipline. It does not replace `docs/architecture/APPLICATION_KERNEL_AND_MODULES.md`, domain/application ownership, Web presentation ownership, sync authority, or observability ownership.

## 1. Decision

SquiFlow shares **business meaning and application capabilities**, not one universal UI implementation.

The tenant Web and Windows Workstation are separate presentation hosts over compatible shared module semantics:

```text
shared module semantics
(domain / application / contracts / permissions / features / settings)
                    |
          +---------+---------+
          |                   |
          v                   v
Workstation presentation    Web presentation
Avalonia                    Blazor
local-first UX              online UX
          |                   |
          v                   v
Application.Workstation        Application.Web
```

The Workstation is not a container for every business feature file, and a business module is not forced to contain Avalonia and Blazor presentation in the same project.

Host-specific presentation projects are created only when an implemented slice needs them.

## 2. Repository/project direction

The intended shape is:

```text
Application.sln

apps/
  desktop/
    workstation/
      Application.Workstation/
        App.axaml
        Program.cs
        Shell/
        Navigation/
        Commands/
        Status/
        Themes/
        Platform/
    guard/
      Application.Guard/

  web/
    Application.Web/

services/
  core-api/
    Application.CoreApi/

foundation/
  application-kernel/
    Application.ApplicationKernel/

modules/
  orders/
    Application.Orders/                 # shared domain/application/contracts as compact as the slice permits

    Application.Orders.Workstation/     # create only when desktop Orders UI exists
      Views/
      ViewModels/
      Navigation/
      Commands/
      OrdersWorkstationContribution.cs

    Application.Orders.Web/             # create only when Web Orders UI exists
      Components/
      Pages/
      OrdersWebContribution.cs

infrastructure/
  persistence/
    sqlite/                          # Workstation persistence adapter when Phase 2 needs it
    postgres/                        # central persistence adapter when the central slice needs it
```

This is a dependency direction, not an instruction to scaffold empty projects. A simple module may stay in fewer projects until UI/framework dependency pressure earns a split.

## 3. Workstation host ownership

`Application.Workstation` owns desktop-host concerns that should not be repeated inside every module:

- Avalonia application lifetime and composition root;
- main shell/window behavior;
- global navigation host;
- global search/command surface when implemented;
- keyboard/focus routing that spans modules;
- theme/resources and shared presentation primitives;
- top-level dialog/toast/notification host;
- Workstation connection/sync/recovery/status presentation;
- window/panel state and desktop-only OS integration;
- composition of reviewed module Workstation contributions.

The host does **not** own Orders/Customers/Inventory business rules merely because their screens are rendered inside its window.

## 4. Module Workstation presentation ownership

A module-specific Workstation presentation contribution owns only that module's desktop interaction surface, for example:

- Avalonia Views/UserControls;
- ViewModels and presentation state;
- module-specific navigation entries;
- module-specific commands and keyboard actions;
- module-specific panels/dialogs;
- mapping application results and authority states into understandable desktop UX.

Host-independent domain/application logic stays outside Avalonia presentation projects.

A future Web presentation can express the same business capability differently without copying Avalonia ViewModels or `.axaml` controls into Blazor merely to keep pixels identical.

## 5. Trusted UI contribution model

The first useful Workstation slice uses one explicit composition root plus small SquiFlow-owned typed registries for navigation, workspaces and actions. There is no current ApplicationKernel project or module graph. Do not create either merely to host one contribution.

When several real capability adapters create dependency ordering, compatibility or duplicate-detection pressure, extract the smallest reusable composition graph under `APPLICATION_KERNEL_AND_MODULES.md`. Until then, ordinary compile-time references and explicit registration are the more truthful mechanism.

A Workstation-capable module may contribute a bounded descriptor conceptually containing:

```text
ModuleId
NavigationItems
Workspace/route factories
Context commands
Search/command registrations
RequiredFeature
RequiredPermission metadata for UX availability
Host compatibility/version
```

The Workstation composition root/shell validates introduced contributions during startup.

Reject as baseline:

```text
scan application directory
→ load arbitrary DLL that implements IModule
→ allow it to mutate the shell/service container freely
```

Initial releases compose only reviewed modules shipped in the verified SquiFlow artifact. Arbitrary third-party plugins, runtime hot loading/unloading, and folder-drop extensions remain deferred under `APPLICATION_KERNEL_AND_MODULES.md`.

### 5.1 Selected hybrid foundation

When the first real Workstation journey is activated, the preferred direct base is:

- Avalonia for desktop UI and headless UI testing;
- CommunityToolkit.Mvvm for ViewModel observable state, commands and presentation validation;
- the minimum required Microsoft.Extensions hosting, DI, configuration, options, logging and localization packages;
- SquiFlow-owned typed contribution/navigation/workspace/action contracts.

Focused packages are admitted only for the responsibility they prove. Dock.Avalonia is conditional on real persistent docking/floating needs. Duende IdentityModel.OidcClient is a candidate for the native OIDC protocol boundary after a ZITADEL/system-browser POC. Windows Credential Manager/DPAPI, the SQLite/encryption provider, and an updater such as Velopack each require their focused security, durability or recovery proof.

Prism, Eclipse RCP, NetBeans Platform, XAF, Uno.Extensions, CSLA, Tryton Desktop and Odoo POS/client are behavior and test donors. They do not become overlapping application, composition, business-object, authorization, persistence or sync runtimes. The evidence and exact admission gates are owned by `docs/review/WORKSTATION_FRAMEWORK_ADMISSION_RESEARCH.md`.

## 6. Navigation and rendering

Navigation has stable explicit identifiers and ownership.

The shell owns the navigation registry; modules contribute reviewed entries/workspaces through their host contribution.

Avalonia techniques such as `ContentControl`, data templates, compiled bindings, or a narrow ViewLocator may be used to render the active workspace. They are implementation techniques, not the authority for routing, feature availability, permissions, or module ownership.

Do not make reflection-based View discovery the only navigation contract.

The navigation model must be able to answer:

- which module owns this destination;
- which feature must be available;
- what stable navigation/workspace ID restores it;
- what happens when a feature is disabled while data still exists;
- how duplicate route/navigation identifiers fail at startup;
- what presentation the user receives when current authority changes.

UI hiding is never server authorization. Core API/application authority still enforces protected actions.

## 7. Shared UX semantics across Workstation and Web

Where both hosts show the same business/authority state, they use the same semantic vocabulary even if controls/layout differ.

Important Workstation states include:

```text
LocalCommitted
PendingRemote
Authoritative
Conflict
Rejected
Retryable
AuthorizationChanged
UpgradeRequired
```

The Workstation may expose richer local/offline status because it owns local persistence and synchronization. The Web is not required to imitate offline states it does not have.

Important state is not communicated by color alone. Use explicit text/icon/action semantics appropriate to the state.

## 8. Inter-module UI/application communication

Do not introduce a global event aggregator/message bus merely because the application is modular or stateful.

Prefer, in order:

1. direct in-process application contracts for required synchronous collaboration;
2. domain/application events where several observers genuinely need a committed fact;
3. shell-level presentation notifications for UI-only concerns;
4. durable outbox/Worker semantics only for true durable/after-commit work.

A generic `Channel<object>`, `IEventBus`, or UI messenger for every interaction is not baseline.

## 9. Visual and interaction direction

The Workstation is an operational desktop application for repeated daily work. It should feel:

- fast;
- trustworthy;
- compact without being cramped;
- keyboard- and mouse-capable;
- desktop-native;
- explicit about local versus server authority;
- visually calm enough for long sessions.

Do **not** use a generic generated SaaS/admin-template aesthetic as the default product design.

Reject as a starting assumption:

- a home screen dominated by arbitrary KPI cards;
- decorative gradients/hero sections unrelated to the task;
- a wall of rounded cards for every business object;
- mobile navigation stretched into a Windows window;
- an "AI insights" panel without a proven product requirement;
- copying one reference product's skin wholesale.

Reference products may contribute interaction lessons, not a literal mashup. Useful reference classes include:

- Square / Lightspeed / Shopify POS for high-frequency transactional focus and persistent current-work context;
- Odoo / QuickBooks for business-document lifecycle, line-item editing, totals, history and contextual actions;
- Linear for keyboard navigation, command/search interaction and calm information density;
- Lunacy and other mature Avalonia desktop applications for resizable multi-panel desktop workspace behavior;
- Fluent guidance for spacing, hierarchy, focus and accessibility discipline.

SquiFlow keeps one coherent visual language rather than visually switching style per borrowed pattern.

## 10. Initial shell hypothesis, not final visual contract

The first prototype should test a desktop workspace approximately like:

```text
+-----------------------------------------------------------------------+
| Tenant/location       Search / command surface        Sync / user     |
+---------------+-------------------------------------------+-----------+
| Primary       |                                           | Context / |
| navigation    |              Main workspace               | details   |
|               |                                           | optional  |
| Orders        | list / document / transaction / editor    | panel     |
| Customers     |                                           |           |
| Inventory     |                                           |           |
+---------------+-------------------------------------------+-----------+
| local/server state · pending sync · conflicts · recovery · connection |
+-----------------------------------------------------------------------+
```

The right/context panel is optional and exists only where it improves the current task.

This layout is a prototype hypothesis. Customer workflow evidence and usability testing may change it without requiring an architecture reversal, provided the host/module contribution boundaries remain intact.

## 11. Core interaction patterns to prototype

Before production styling, prove the interaction mechanics for:

1. shell navigation and window resizing;
2. global search/command navigation if retained;
3. list → inspect → act workspace;
4. high-frequency order/quotation transaction workspace;
5. local/server authority status presentation;
6. Conflict / AuthorizationChanged / UpgradeRequired recovery presentation;
7. dense table/grid + keyboard interaction where the workflow needs it.

Do not spend significant time polishing branding before these interaction assumptions are exercised.

## 12. Visual foundation and accessibility baseline

Start with a small SquiFlow token set rather than a full design-system program:

- typography scale;
- spacing scale;
- surface/border hierarchy;
- corner-radius scale where needed;
- one primary accent;
- semantic warning/danger/success/info/status tokens;
- focus states;
- light/dark support only where it remains inexpensive and coherent.

Avalonia's built-in theme/control foundations may be used as implementation primitives, then deliberately adapted for the small set of SquiFlow primitives that repeat.

Even without a separate formal accessibility workstream, the shell starts with:

- keyboard reachability;
- visible focus;
- logical tab order;
- meaningful labels;
- no color-only critical state;
- scaling-friendly layouts;
- automation/accessibility names for important controls where supported.

## 13. Diagnostics/logging relationship

Presentation code must not invent a separate logging/export path.

`docs/observability/WORKSTATION_SERVER_LOG_PIPELINE.md` already owns the accepted Workstation diagnostics direction:

```text
Workstation / Guard
→ local bounded durable operational evidence first
→ selective central export when policy/connectivity permits
```

Crashes, restart loops, sync/reconciliation failures, security-safe anomalies and integrity/resource warnings receive stronger capture. Routine success/debug traffic may remain local or sampled.

Diagnostic/support bundles are bounded and redacted; they do not include unrestricted customer databases/files, tokens, secrets, or arbitrary customer content.

The shell may surface diagnostics/recovery status, but operational telemetry never becomes business authority.

## 14. First Workstation slice proof obligations

The first activated Workstation slice should prove the smallest useful presentation composition rather than a fake dashboard.

Prove:

- Workstation shell launches through its real composition root;
- one real capability adapter contributes a useful navigation/workspace entry;
- host filtering prevents non-Workstation contributions from entering the desktop host;
- duplicate navigation/command/workspace identifiers fail predictably;
- disabled feature cannot be navigated to through the normal shell contribution;
- module Avalonia types do not leak into shared domain/application contracts;
- Workstation shell does not reference central PostgreSQL/OpenFGA administration/provider SDKs;
- local/server/sync status has one shared semantic presentation model even before full Phase-3 sync exists;
- keyboard/focus behavior is usable for the shell/sample workspace;
- no placeholder business dashboard is treated as a product requirement merely to fill the screen;
- startup/memory cost is measured before adding reflection scanning, dynamic UI generation, or a large component framework.

The activating change must record the direct dependency versions/licenses and add falsifiable ViewModel, headless UI, composition and boundary tests appropriate to its claims. Local persistence, sync, authentication, update, reporting and device claims each require their own focused proof when introduced.

## 15. Revisit triggers

Revisit the presentation project split or shell contribution mechanism if evidence shows:

- host-specific presentation assemblies create materially more dependency/build complexity than they prevent;
- a module genuinely shares a presentation model across Avalonia and Blazor without leaking framework assumptions;
- navigation/workspace contribution requirements exceed the simple descriptor model;
- plugin isolation/runtime loading becomes a real signed extension requirement;
- customer workflow evidence shows another shell information architecture substantially improves the first product outcome.

Do not change the business/domain boundaries merely to match a UI framework pattern.

## 16. Related owners

- `docs/architecture/REPOSITORY_STRUCTURE.md` — repository/executable/project-boundary rules.
- `docs/architecture/APPLICATION_KERNEL_AND_MODULES.md` — trusted module graph, host contributions, features/settings/permissions.
- `docs/workstation/LOCAL_FIRST_DESKTOP.md` — Workstation local-first behavior and authority states.
- `docs/sync/SYNC_AND_AUTHORITY.md` — server authority and sync results.
- `docs/observability/WORKSTATION_SERVER_LOG_PIPELINE.md` — local diagnostics and selective central export.
- `docs/workstation/GUARD_AND_RECOVERY.md` — Workstation supervision/recovery boundary.
- `docs/review/WORKSTATION_FRAMEWORK_ADMISSION_RESEARCH.md` — source comparison, package admission decisions, proof gates and phased route.
