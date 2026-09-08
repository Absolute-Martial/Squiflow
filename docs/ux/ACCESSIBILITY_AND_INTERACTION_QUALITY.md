# Accessibility and Interaction Quality

**Version:** v0.0.15

Accessibility is a release-level product requirement, not a late visual-polish task.

The exact legal/certification target by jurisdiction remains a product/compliance decision, but the implementation baseline must be compatible with a serious accessibility target rather than requiring a rewrite later.

## 1. Surfaces covered

Accessibility applies to:

- tenant Staff Web;
- tenant Owner Settings/Administration;
- SquiFlow Platform Admin Web;
- client-client/customer portal when introduced;
- Windows Workstation/Avalonia UI;
- generated documents where their purpose requires accessible digital use.

A privileged/internal screen is not exempt merely because fewer people use it.

## 2. Core interaction requirements

For Web and Desktop core business journeys:

- complete keyboard operation without requiring a mouse;
- visible, predictable focus state/order;
- no keyboard traps;
- semantic labels/names for interactive controls;
- accessible validation/error association;
- status changes announced in a way assistive technology can understand where applicable;
- status is not communicated by color alone;
- text can scale without making core actions unusable;
- navigation/heading structure is predictable;
- dialogs/modals manage focus correctly;
- destructive/high-risk confirmations state the exact action;
- timeouts/session expiry do not silently destroy valuable user input where a safe recovery path exists.

## 3. SquiFlow-specific state communication

The product has unusual states that must remain understandable without relying only on icons/color:

```text
LocalCommitted
PendingRemote
Authoritative
Conflict
AuthorizationChanged
UpgradeRequired
OutcomeUnknown
PendingApproval
Overdue
```

For each state define:

- visible text label;
- screen-reader/accessible name;
- actionable explanation;
- next safe action;
- whether it blocks progress.

A red/green dot alone is never sufficient for payment, sync, approval or health state.

## 4. Dynamic forms and rule-driven UI

Tenant-configurable forms/rules cannot bypass accessibility.

The form-definition model must be able to represent:

- visible label;
- accessible name/description when needed;
- field type/semantic purpose;
- required/optional state;
- validation message;
- grouping/section semantics;
- logical focus/order.

Do not allow tenant configuration to inject arbitrary HTML/script as a workaround for form flexibility.

## 5. Permission-aware UI

Hiding a button for lack of permission is UX only; server authorization remains authoritative.

When an action is unavailable, distinguish:

- not permitted;
- unavailable in current business state;
- requires network/server authority;
- temporarily unavailable due dependency;
- unsupported until upgrade.

Where useful, explain why instead of leaving an unexplained disabled control.

## 6. Workstation/local-first UX

Offline/local-first behavior must be accessible too.

The Workstation must expose:

- connectivity state without noisy repeated alerts;
- pending sync count/state;
- conflict/rejection queue needing action;
- printer/device failure state;
- last successful synchronization;
- distinction between local save and server acceptance.

Notifications of background state changes must not steal focus unexpectedly.

## 7. Platform Admin accessibility

High-risk administration screens must be keyboard/screen-reader usable and show material differences as structured information.

An approval screen must expose the actual before/after change, not just `Approve`/`Reject` with inaccessible visual diff coloring.

## 8. Branding/custom domains

Tenant branding is bounded so contrast/readability is not destroyed by arbitrary CSS/script.

Theme variables need safe defaults and validation/fallbacks. Exact formal contrast/conformance thresholds follow the chosen accessibility target before public release.

## 9. Testing

Accessibility verification combines:

- automated Web accessibility checks where useful;
- keyboard-only journey tests;
- focus-order/modal/error-state tests;
- screen-reader/manual smoke tests for core flows;
- scalable-text/zoom tests;
- non-color status tests;
- Avalonia accessibility/platform API verification on supported Windows versions.

Automated scanners alone are not sufficient.

## 10. Minimum journey gate

The first end-to-end release must test accessibility for at least:

```text
login
→ tenant/context selection
→ customer/order creation
→ validation/error
→ submit/save
→ permission-denied state
→ sync/pending state on Workstation
→ printing failure/retry
→ Owner role/permission change in Web
```

Client-client portal accessibility is added to the release gate when that surface becomes implementation scope.
