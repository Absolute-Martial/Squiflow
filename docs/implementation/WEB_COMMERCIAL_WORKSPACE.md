# Web commercial workspace

Product version remains v0.0.1. This is the focused owner for the bounded online Web
commercial implementation. CoreApi and the existing capability owners retain all
business meaning, authorization, arithmetic and durable state.
The main integrator owns admission, repository inventory updates and the normal
gate. This document does not qualify the whole product or live identity providers.

## Declared scope

After explicit tenant selection, the workspace provides working navigation to:

- Organizations: bounded directory, immutable create/read, child-program
  directory/create/read, and representative management.
- Individuals: create, explicit reference lookup, revision-checked contact
  replacement and active/inactive transition. Historical consolidated records
  display their redirect and cannot be edited here. There is no individual
  directory endpoint; the UI does not invent one.
- Representatives: link an individual to an organization or its program, lookup
  a saved relationship by reference, and confirmation plus revision-checked
  unlink. A relationship grants no operator or billing authority.
- Catalog: bounded item/unit directories, create/read, display changes, permanent
  retirement with confirmation, availability-only transitions, direct conversion
  publication and immutable revision lookup. Unit identity and precision remain
  immutable; stock-mode flags do not introduce inventory.
- Pricing: contextual current-price selection, typed missing/expired/conflicting/
  override-required results, bounded historical revisions, new price drafts or
  family revisions, explicit publication/supersession and retirement, and
  revision-checked policy read/publication. Dates are explicitly UTC. No UI
  arithmetic or eligibility engine is introduced.
- Orders: bounded saved-order directory and deep links; manual or Catalog/Pricing
  draft creation; explicit full replacement while an ordinary draft; current
  detail and bounded historical snapshots; current server action guidance;
  confirmed exact-revision commitment or abandonment. Manual prices require the
  independent server permission. Catalog selection and frozen monetary totals
  come from CoreApi. Up to ten new lines can be added in one editor; existing
  supported snapshots may contain up to 100. Requests remain bounded to 8 KiB.
  Quotation-origin drafts retain origin facts and cannot use this ordinary
  replacement editor. Current profile/approval/program-reference requirements
  remain enforced by CoreApi. Saved Orders link to the explicit program-reference
  and approval journeys, and committed Orders link to fulfillment.

- Billing: exact committed-Order invoice issue and frozen invoice lookup; bounded
  receivable/payment directories; pending cash/bank evidence recording; separately
  authorized reconciliation, allocation, dispute, rejection, full allocation
  release and receipt reversal; payment history and exact debtor balances.
  Mutation receipts reveal only the IDs/revision that the server returned.
  Saved facts and expected revisions remain server-owned. Correction requires a
  typed confirmation and the backend's independent current-Owner, permission and
  strong-auth checks. No tax, credit, refund, cross-debtor pooling or bank provider
  is supplied by the UI.

- Quotations: explicit reference lookup, manual or Catalog/Pricing drafts, exact-version
  replacement, immutable issuance/history, response evidence and accepted-offer
  conversion with an Order link. There is no quotation directory endpoint. Validity
  requires an explicit timestamp offset. The backend does not return the original
  non-base-unit conversion revision; the operator supplies compatible evidence for
  replacement. Issued facts, numeric values and external claims retain server meaning.
- Order approvals: bounded inbox/detail, the complete frozen submitted subject,
  exact-revision submission or stale-request replacement, independent approve/reject,
  requester cancellation, immutable history and current-Owner recovery. Existing
  numeric enum contracts remain numeric. Separate program-reference editing sets or
  clears the supplied reference against the saved draft revision and shows its pin.
- Fulfillment: bounded progress inbox, explicit Order lookup, retained progress and
  protected history; start against a committed Order revision, record completion or
  pickup, disposition/resolution and typed event correction. This does not establish
  delivery, customer acceptance, payment, returns or refunds.
- Purchasing: suppliers, exact-revision create/revise/retire, service-purchase requests,
  receipt/payment/refund evidence, cancellation of the remaining quantity, linked
  financial corrections, supplier balances and retained history. Purchase directory
  pages are limited to three records to keep the supported response bounded.

- Tenant administration: current compiled permission catalog and retained custom roles,
  direct grant/revoke proposals, custom-role create/revise/retire, assignment/removal,
  saved proposal lookup and explicit bounded reconciliation. Pending/Uncertain is
  displayed separately from Applied; the UI cannot expire, force-complete or bypass
  a held provider operation. Every mutation uses the current authorization revision.
  Initial-Owner handoff requires an existing active successor reference, a supplied
  exact tenant revision, typed successor confirmation and fresh strong authentication.
  The private platform member/tenant registry is not proxied into this screen.
- Program order policy: independently protected current policy read, full replacement
  of the two supported draft requirements and immutable publication of an exact saved
  revision. Publication advances the policy revision. It does not select/activate a
  private platform profile or replace policy pinned to existing Orders. Authorized
  editing/publication can use known revisions when the independent read is denied.

Customer attribution on an Order is not debtor or payment authority. Customer
duplicate/import and Workstation business screens remain outside this admitted patch.
Private Platform Admin Web remains pending by user decision. No navigation points
to absent pages.

Canonical backend owners:
[Customers and programs](CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md),
[contacts and representatives](CUSTOMER_CONTACTS_REPRESENTATIVES_SLICE.md),
[Catalog](CATALOG_AND_UNIT_BOUNDARY.md),
[Pricing](PRICING_POLICY_AND_PUBLICATION.md),
[Orders](ORDER_DRAFT_INTAKE_SLICE.md),
[Catalog-priced Orders](ORDER_CATALOG_PRICED_DRAFTS.md),
[Order history](ORDER_DRAFT_HISTORY.md) and
[action guidance](ORDER_DRAFT_ACTION_GUIDANCE.md), [invoice issue](INVOICE_ISSUE_CONTRACT.md) and
[customer settlement](CUSTOMER_RECEIVABLES_PAYMENTS_AND_SETTLEMENT.md),
[Quotations](QUOTATIONS_AND_CONVERSION.md),
[Order approvals](ORDER_APPROVAL_AND_ADMISSION.md),
[program-reference policy](ORDER_PROGRAM_REFERENCE_POLICY.md),
[Fulfillment](ORDER_SERVICE_FULFILLMENT.md) and
[Purchasing](PURCHASING_SUPPLIER_SERVICE_SLICE.md).

## Boundary and recovery guarantees

The existing configured OIDC, opaque server session tickets, fixed CoreApi origin,
Blazor Interactive Server and shared semantic theme remain. Credentials are added
only to each server-side request. No browser token storage, service worker,
offline database, new API ingress or client-granted rights exists. CoreApi
rechecks current membership and the independent operation permission for every
request and retry. Workspace entry is checked before business loads.

The server-only client restricts relative tenant paths, pins the configured
origin, never follows redirects, uses explicit response DTOs, caps response
bodies at 256 KiB (including chunked bodies), caps ordinary requests at 8 KiB and
Quotation requests at their existing 64 KiB backend boundary, and gives each
request an eight-second budget. Unsupported shapes, identity/parent mismatches,
invalid enums and page bounds fail closed. Caller cancellation differs from a
request deadline. Ticket validity is checked before dispatch, after headers and
after consuming the body; signing out during a read discards the result.

A mutation freezes its HTTP method, tenant, serialized payload, session and
idempotency key. A timeout, connection loss, redirect, malformed successful
response or ambiguous server failure keeps the same request for explicit retry.
Controls stay locked until its result is known. A denied or conflicting retry
does not prove an earlier unknown attempt failed, so it preserves the recovery
envelope and states that uncertainty until a receipt is recovered or the user
acknowledges leaving. Retry never creates a replacement
intent or automatically runs in the background. Explicit 400/401/403/404/409
outcomes have safe fixed messages; private response bodies never reach the UI.
A replay is identified as the original receipt, not a fresh write. Conflict
requires operator review/reload; the UI never advances an expected revision by
itself. Read-only Pricing POST queries carry no mutation key.

The retry envelope lives only in the current server circuit. A recovery notice
shows its reference and blocks navigation until the operator acknowledges the
record check. Business links use full navigation so the browser's leave-page
confirmation also applies; enhanced navigation is intentionally disabled inside
this shell. A reconnect within the retained circuit can keep its envelope; a
lost circuit, restart or deliberate departure can lose it. The notice explicitly
says so. Saved business drafts and receipts remain CoreApi-owned. Unsent form
input is transient; no promise of automatic restoration or cross-device retry
is made. Reload the saved record or directory before creating a replacement.

HTTP login/logout retain antiforgery. Business page URLs do not accept HTTP
writes. Interactive event traffic uses the existing circuit, with an added
configured-origin fence on `/_blazor` negotiation/WebSocket traffic. A regression
first demonstrated that a foreign Origin received 200 from negotiation; the
fence rejects foreign origins and preserves the same-origin browser circuit.
This test does not claim a full cross-site exploit was demonstrated.

The optional `Authentication:StrongAuthenticationAcr` is one deployment-selected
ACR value matching the CoreApi strong-authentication policy and configured
identity provider. `POST /auth/verify-identity` requires a current session and
antiforgery, accepts only its bounded token/tenant form, invalidates the prior
server ticket and requests `prompt=login`, `max_age=0` and that ACR. Its protected
OIDC state returns only to the exact tenant Billing route. Missing configuration
does not start a weaker challenge. Asking for ACR does not prove assurance:
CoreApi validates actual signed token claims. A provider refusal or ignored
request cannot grant correction rights. This is a fresh sign-in; old circuit
credentials stop working, and unsent forms must be reviewed after returning.
Approval recovery and Fulfillment/Purchasing corrections use the same fixed Billing
return and explicitly disclose that unsent entries will be lost. Missing strong-ACR
configuration shows verification unavailable. Live provider ACR/auth-time qualification
remains pending.

All displayed input is Razor encoded. Forms use associated labels, native
controls, explicit disabled/loading states, polite status feedback and shared
semantic CSS. Forms remain disabled until the interactive render is ready.
Retirement, full draft replacement, commitment, abandonment and unlinking have
explicit review/confirmation. Business state does not grant permissions.

## Evidence and qualification

The local client/framework/UI contract scope is guarded by
`BusinessClientTests`, `BusinessPageTests`, their financial/strong-auth regressions,
Quotation/Approval/Operations and tenant administration/policy tests, plus the
entry, business, quotation, operations and tenant administration browser scripts
combined by `eng/verify-web-browser.sh`. The shared skip link preserves
the current route and query before replacing its fragment with `#main`; the actual
browser guard exercises keyboard activation on a nested route.
The browser guard runs the actual Release Web process over HTTPS, real OIDC
middleware/PKCE with a controlled local issuer, and a controlled API-contract
fixture. It drives actual rendered Blazor forms and navigation and uses external
Playwright/Chromium and axe. The fixture is test-only and is never a runtime
persistence adapter. Provider atomicity, OpenFGA, SQL/RLS and real customer data
journeys are not established by this fixture.

The evidence bundle accompanying the patch records inspected commands, exits,
counts, screenshots and SHA-256 hashes. Local declared UI/client checks must pass
before admission. Integrated qualification is `BLOCKED` until the main integrator
admits this source and runs the canonical normal gate; it is not deferred
hardening or a claim that the user's separate baseline gate tested this patch.
Live ZITADEL/provider journeys remain outside this evidence. Requalify on API or
wire-contract changes, authorization/session/origin changes, retry or disposal
changes, Blazor/SDK upgrades, CSS/token changes or added Web journeys.

Run from the repository root (SDK/cache variables below are task setup examples):

```sh
export DOTNET_CLI_HOME=/tmp/application-entry-dotnet
export NUGET_HTTP_CACHE_PATH=/tmp/application-entry-nuget-cache
export NUGET_PACKAGES=/tmp/application-entry-packages
dotnet restore tests/integration/Application.Web.Tests/Application.Web.Tests.csproj --locked-mode
dotnet test tests/integration/Application.Web.Tests/Application.Web.Tests.csproj -c Release --no-restore
python3 eng/build-ui-tokens.py --check
APPLICATION_ENTRY_PLAYWRIGHT_PATH=/tmp/application-entry-browser/node_modules/playwright \
APPLICATION_ENTRY_AXE_PATH=/tmp/application-entry-browser/node_modules/axe-core/axe.min.js \
PLAYWRIGHT_BROWSERS_PATH=/tmp/application-entry-browsers \
APPLICATION_UI_ARTIFACTS=/tmp/application-business-web-evidence \
node eng/test-business-web.cjs
```

The browser guard requires OpenSSL and external installed browser dependencies;
absence is failure, not a skipped pass. Screenshots and JSON audits are separate
from the source-only ZIP. No normal gate, Docker, live provider, Workstation,
Git mutation or integration was performed for this isolated patch.

Financial browser fixtures seed another operator's receipt before exercising
reconciliation. They prove UI requests and presentation; the real PostgreSQL /
OpenFGA native tests establish independent-account and current-authority rules.
The Orders directory is invalidated after a successful mutation so an old draft
summary cannot contradict the saved committed/abandoned state. Multiline controls
use the shared input tokens, focus and disabled states; evidence remains the
backend's bounded one-line text contract.
