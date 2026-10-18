# Client Web functionality checkpoint — 2026-10-19

Product version: **v0.0.1**. Scope: the online tenant client and tenant-Owner
administration over currently implemented CoreApi capabilities. The user explicitly
kept private Platform Admin Web pending and excluded Workstation/backend expansion.
The [focused Web owner](../implementation/WEB_COMMERCIAL_WORKSPACE.md) owns behavior;
[implementation truth](../../README.IMPLEMENTATION.md) owns current gate state.

## Supported journey map

| Client area | Available operator functionality | Recurring rendered guard |
|---|---|---|
| Entry | Configured sign-in/out, current account, tenant selection, workspace recheck/switch, expired/denied states | `eng/test-signed-in-entry-browser.cjs` |
| Customers | Organizations/programs, individual contact/availability, representatives, duplicate review/consolidation/history, retained CSV planning/decisions/work/source | `eng/test-business-web.cjs`, `eng/test-customer-operations-web.cjs` |
| Catalog and Pricing | Item/unit directories and lifecycle, availability, direct conversions, published-price selection/history/revisions and pricing policy | `eng/test-business-web.cjs` |
| Orders | Saved directory/detail/history/actions, supplied or catalog-priced create/replacement, unsaved server preview, program-reference/approval continuation, commitment/abandonment | `eng/test-business-web.cjs`, `eng/test-quotation-web.cjs` |
| Quotations and approvals | Supplied/catalog drafts, issue/history, acceptance/rejection/expiry, conversion into linked Order; exact-subject inbox, independent decision, requester cancellation and Owner recovery | `eng/test-quotation-web.cjs` |
| Fulfillment | Progress inbox/detail/history, start, completion, pickup, dispositions/resolutions and linked correction | `eng/test-operations-web.cjs` |
| Purchasing | Supplier lifecycle, service-purchase requests and history, receipt/payment/refund evidence, remaining-quantity cancellation, linked corrections and supplier balances | `eng/test-operations-web.cjs` |
| Billing | Committed Order continuation, issue/frozen lookup, browser record print view, exact current receivable, debtor-filtered directories, manual receipt evidence, reconciliation, allocation, dispute/rejection, release/reversal, history and exact balances | `eng/test-business-web.cjs` |
| Tenant Owner | Compiled permissions, direct grants, custom-role lifecycle/assignment, retained proposals and explicit reconciliation, initial-Owner handoff and fixed return after verification | `eng/test-tenant-administration-web.cjs` |
| Program policy | Current requirements, exact-revision draft edit and immutable publication | `eng/test-tenant-administration-web.cjs` |

The shared saved-customer and Catalog controls are presentation conveniences over
existing protected directories. They preserve manual exact-reference entry for
roles without browse authority. Every save remains authoritative in CoreApi.
No private platform registry or membership lifecycle is proxied through tenant Web.

## Scope boundaries and evidence

Frontend implementation and local presentation checks are complete for the supported
journey map above; this is not complete application/deployment qualification.
The aggregate client release remains **BLOCKED** by the existing live identity and
integrated gate responsibilities in the inventory. Local fixture journeys do not
qualify real identity, PostgreSQL effects or object-store deployment. Private Admin
Web and Workstation are **NOT_INTRODUCED** for this task's new functionality.
Numerical stock, credit/returns, general document delivery, dynamic profile forms,
an external customer portal, provider user creation and device administration need
their own backend/architecture scope; a frontend does not invent those operations.

Permanent guards are the current Web/architecture tests, token generation checks
and the six-journey `eng/verify-web-browser.sh` wrapper. Requalify when API/permission
contracts, session custody, tenant binding, shared selectors, price-preview inputs,
invoice presentation/print CSS or Owner verification destinations change. Unknown
saves retain their existing exact recovery envelopes. Accessibility incomplete
rules and real provider/deployment evidence remain distinct from local passes.

## Inspected current results

- Web Release tests: **147 passed, 0 failed, 0 skipped**.
- Architecture tests: **17 passed, 0 failed, 0 skipped**.
- Full Web graph format verification and independent Web restore/build/publication:
  **exit 0**, with zero build warnings/errors. Token checks passed **4/4** and
  quality-wrapper self-checks passed **11/11**; the latter prove wrapper behavior.
- Final combined six-journey browser run: **exit 0**, **90 screenshots**,
  **86 accessibility reports**, **0 reported violations**. **22 reports** retain
  incomplete rules requiring manual assessment. No accessibility conformance is
  inferred. Actual 320px price-preview and invoice print-media screenshots were
  inspected. The new selection, stale-preview, denied/malformed browse, financial
  filter, current receivable, browser print invocation and Owner return controls
  ran in the real Web host against the controlled HTTPS issuer/API fixtures.
- The exact `COLLECT_COVERAGE=1 ./eng/verify.sh` attempt with the offline NuGet
  configuration exited **1 at restore**, because excluded Workstation Avalonia,
  MVVM and OIDC dependencies are not cached. No full repository test/coverage pass
  is inferred. The incoming verification script was preserved. No desktop package,
  browser binary, container image or additional model was downloaded for this task.

Evidence is retained in `artifacts/web-client-completion-2026-10-19/`:
`final-results.json`, `combined-six-browser-final-result.json`, their command logs,
`browser-summary.json`, copied `combined-six-browser-final/SHA256SUMS`,
`canonical-offline-result.json`, `canonical-offline.log` and `source-manifest.json`.
The first expanded test run's prerender-only assertion and the first browser
fixtures' render synchronization/route-dispatch failures are retained separately;
only the inspected final passes supersede those results. `final-results.json`
retains the first combined-browser failure; the explicit final browser receipt
records its successful rerun after the fixture correction.

Backend/capability source, package graph, Workstation source, private Admin Web,
product version and unrelated incoming skills/configuration were not changed.
No swarm or independent reviewer was used; review here is the integrating agent's
source inspection plus the recorded falsifiable checks. Existing live identity,
provider, dependency audit, mutation and complete production gates remain pending
under their current owners; the fixture results do not close those claims.
