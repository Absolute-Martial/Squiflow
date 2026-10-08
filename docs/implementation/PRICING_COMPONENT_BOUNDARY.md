# Pricing component boundary

**Current scope:** separate host-neutral calculation of supplied draft selling
prices inside `Application.Orders/Pricing/OrderDraftPriceCalculator.cs`, exposed
through a protected, non-persisting order-entry preview. The accepted adaptive
pricing contracts and provider adapter are owned by
[`PRICING_POLICY_AND_PUBLICATION.md`](PRICING_POLICY_AND_PUBLICATION.md); their
host integration is locally qualified; deployment acceptance remains separate.

The separated calculation and protected preview are `PRODUCTION_HONEST` for
their declared arithmetic/compatibility and API scopes. Separate manual-entry
permission control is `PRODUCTION_HONEST` for the current CoreApi routes and
replay scope described below. Adaptive policy, protected HTTP exposure and
catalog-priced Orders integration are `PRODUCTION_HONEST` for the narrow scopes
and local receiving evidence in the focused Pricing/Orders owners.

This owner separates pricing responsibilities for the end-to-end business map.
Detailed business meaning remains in `docs/domain/BUSINESS_MODEL.md` section 5
and `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md` NFR-BIZ-005.

## Separate responsibilities

| Concern | Owned result / boundary |
|---|---|
| Price selection | Applicable standard, organization/program, wholesale, quotation, permitted manual-final or outsourced-resale price, with the source and effective revision |
| Override authority | Current permission, reason and versioned envelope; out-of-envelope uses additional capability in v0.0.1, not an invented mandatory approval; an entered unit price or hidden UI field does not prove authority |
| Selling-price calculation | Validated quantity/unit and applied price, rounded line amounts and document total |
| Historical facts | Applied values, currency, source/revision and authorized override evidence retained by the issued quotation/invoice or other owning record |
| Supplier cost/payable | Actual supplier work/cost and payable, owned by purchasing/outsourcing; customer selling price need not equal cost plus a fixed markup |
| Tax, payment allocation and stock valuation | Separately owned meanings, not methods smuggled into a general price calculator |

## Implemented separation

`OrderDraftIntent.Create` now owns order summary/currency/customer-context
validation, intent assembly and fingerprinting. Its dedicated pricing component
owns priced-line validation, quantity-times-price multiplication, rounding and
total calculation. The component performs no I/O, permission lookup, persistence
or business mutation. Adaptive source/policy selection lives separately in
the focused Pricing owner; it does not replace this arithmetic component.

Existing behavior stays compatible: 1–100 lines; positive quantity; nonnegative
unit price; decimal 19,4 range/scale; each line rounded to four decimals using
`ToEven`, then summed. Currency, price and quantity remain input facts; no tenant
name, customer GUID, fixed currency or arbitrary workflow selects them. Current
error codes, normalized values, fingerprints and persisted receipt formats remain
unchanged. These arithmetic rules reflect the accepted current contract, not
invented tenant-specific price policy.

## Order-entry price preview

`POST /api/v1/tenants/{tenantId}/orders/price-preview` accepts UTF-8 JSON with
`currencyCode` and `lines` (description, quantity, unitCode, unitPrice). It returns
normalized currency and lines, rounded line totals and their sum. It uses the
same host-neutral calculator as draft creation; the API contains no arithmetic.
No summary, customer attribution, order ID, expected revision or Idempotency-Key
is needed because this operation neither reads nor changes a draft. Repeating it
with the same supported inputs produces the same calculation, not a stored receipt.

The route requires a validated account, current tenant membership and both
`can_create_order` and `can_apply_manual_price` before reading the body. Preview
uses the same manual-entry permission as creation, but does not publish a policy
or approve an override. An editor or pricer without create permission does not
gain this route implicitly. Permission failures return safe 403/503 responses,
and the central no-store, admission and cooperative request-budget policies apply.

Both declared and actual body length are bounded to 64 KiB, including streams
without Content-Length. JSON deserialization uses the existing platform serializer
with its default nesting bound; capability validation limits calculation to 1–100
lines and the existing text/decimal bounds. Malformed input returns a stable 400
Problem Details code; an oversized body returns 413 / `request_too_large`.
No SQL, credentials, payload diagnostics, background work or new persistence is
introduced. Bounded native stream reading and JSON handling use the existing .NET
host stack; no framework or generic transport abstraction is earned by this route.

This is useful guidance for entering a priced draft, not a binding quotation,
stored customer/program policy, tax calculation, approval, stock reservation,
debtor selection or charge. Creation still receives and validates the complete
draft under current authority. Preview results are never trusted as commit evidence.
Adaptive selection is separately introduced by the Pricing owner; this preview
does not select or authorize it.

## Controlled manual price entry

The owner selected operator-entered prices first on 2026-10-02, then explicitly
required a separate pricing permission for **both initial entry and later changes**.
The checked-in OpenFGA model adds persisted `manual_pricer` and computed
`can_apply_manual_price = member AND manual_pricer`. This does not imply create,
edit, view or abandonment authority, and none of those grants implies pricing.

Create requires create plus pricing; full draft replacement requires edit plus
pricing. Replacement resubmits the complete priced lines, so it requires pricing
even when numerical prices are unchanged. There is no unpriced patch route or
stable catalog-item identity for guessing which replacement lines count as an
override. The host checks both permissions before parsing, validating or storing
a command, including idempotent retries. Preview also requires create plus pricing.
Read/detail/history and abandonment keep their own permissions; revoking pricing
does not erase existing prices, receipts or the independently allowed read/abandon
path. Guidance marks revision available only when edit and pricing both succeed.

Each operation obtains current higher-consistency checks under the configured
immutable model. A denial is a safe 403; a required check failure is a safe 503,
never a grant or an inferred denial in a partial action guide. Existing request
and provider deadlines apply. Membership remains application-owned. Sequential
permission checks are not an atomic distributed revocation barrier with commit;
no such guarantee is introduced. A future host must independently earn its
authorization boundary; the host-neutral calculator is not an authority service.

Existing transactions commit the priced effect and its caller-scoped receipt
together. Retained snapshots, actor and recorded times provide attributable draft
change facts; fingerprints, money rules, schema and receipt formats are unchanged.
Old receipts remain readable as historical facts, without being retroactively
asserted to have passed the new permission. Replay requires current permissions:
after revocation it can return 403 even though the effect already committed.
After authorized recovery the original key returns the original committed result;
a client must not blindly submit a new key to work around a denial.

Deployment must publish the new model and explicitly provision intended
`manual_pricer` tuples, pin its model ID, then deploy the corresponding API.
A previous model without this relation fails closed; no fallback or automatic
grant exists. Readiness proves model read access only, so an authorized smoke
check of create/preview/revise is still required. Model/tuple administration,
reason capture, approval, floors/ceilings, stored adaptive price policy and issued
documents are outside this permission-control scope.

## Adaptive pricing owner

The decisions and bounded implementation for durable price publication,
precedence, typed resolution, override evidence, revision retention and
PostgreSQL persistence are now owned by
[`PRICING_POLICY_AND_PUBLICATION.md`](PRICING_POLICY_AND_PUBLICATION.md).
The current Orders calculator remains unchanged and is not a policy selector.
That owner defines stable item/unit price identity, durable configurable policy,
isolated CoreApi routes and neutral draft-revalidation comparison. Shared host,
OpenFGA, DbMigrator/grants and Orders receiving integration remain main-owned;
their exact current qualification state is stated there.

### Phase 02 COM-005 through COM-007 disposition

- **COM-005** is owned by `CATALOG_AND_UNIT_BOUNDARY.md`; consult its current
  accepted stable item/unit and stock-mode contract. Manual free-description entry
  remains a supported compatibility path.
- **COM-006 is owner-accepted and implemented for host-neutral contracts.** The
  focused owner closes the priced identity/context, explicit precedence,
  missing/expired/conflict outcomes, override reason/evidence, policy envelope,
  additional-capability override branch and draft revalidation contract.
- **COM-007 is implemented for the bounded provider adapter.** Publication is
  immutable and revisioned, same-scope overlaps are rejected, and selection
  explanations retain the selected fact and policy context. Public host
  exposure is introduced but receiving qualification remains `BLOCKED` rather
  than being inferred from focused provider tests.

Requalify this boundary when a pricing host route, OpenFGA relation, migration
registration, Orders revalidation caller or another price-bearing consumer is
introduced.

## Regression and requalification

The preview qualification run on 2026-10-02 passed the full normal parallel
`./eng/verify.sh` gate: locked restore, formatting, Release build and all **373
tests**, zero failures/skips and zero build warnings/errors, including PostgreSQL
17 provider regressions. This includes five new pure preview tests and fifteen
new host cases. The focused host run passed all fifteen cases independently.
Remote CI, coverage and deployment/load qualification were not claimed by this run.

`OrderDraftIntentTests` permanently tests normalization, bounds, negative/zero
prices, midpoint rounding and intent fingerprint behavior. Additional tests prove
that line rounding precedes summation and an aggregate exceeding decimal 19,4 is
rejected even when its individual lines are valid. Existing PostgreSQL historical
receipt and mutation tests guard persistence compatibility through `./eng/verify.sh`.

`OrderDraftPricePreviewTests` guards pure preview/create parity, invalid inputs,
overflow and independence from later changes to the input list. Real-host
`OrderPricePreviewTests` guards membership/permission checks before parsing, safe
authorization outages, malformed input, exact declared/undeclared body-size bounds,
no order-store calls, matching creation results and the OpenAPI contract. The
existing all-protected-routes test covers anonymous rejection and no-store on this
route too. These tests recur under `./eng/verify.sh`; requalify preview on route,
permission, serialization, body limit or any introduction of price-source I/O.

Requalify on precision/rounding, input normalization, pricing ownership/types,
fingerprint format, price selection/override, issued-fact locking or introduction
of another consumer. Passing arithmetic tests does not qualify pricing authority,
customer-specific policy, invoices or the end-to-end commercial operation.

The 2026-10-02 local full parallel `./eng/verify.sh` passed locked restore,
formatting, Release build and all 353 tests across the twelve test projects,
with zero failures/skips and zero build warnings/errors. This includes the
47 Orders unit tests and actual PostgreSQL receipt/persistence regressions.
No coverage collection, remote CI or deployment qualification is claimed.

## Manual-entry source admission and regression guard

The control extends the already admitted native ASP.NET resource authorization
and pinned OpenFGA check adapter. It uses the same finite relation telemetry,
safe failure classification, cancellation and provider deadlines; no authorization
framework, database policy table, source generator or alternate container is added.
Application membership, lifecycle, revision and transactional receipts retain
their existing ownership. No SQL, role grant, schema or historical format changes
are needed for this permission boundary.

| Claim | Recurring falsifiable evidence under `./eng/verify.sh` |
|---|---|
| Pricing and order authority are independent; denial precedes body/storage | `OrderManualPricingPermissionTests` grant/denial cases |
| Missing membership cannot be replaced by pricing tuples | HTTP nonmember case; real OpenFGA contextual-membership check |
| Required provider failure cannot commit or disclose diagnostics | HTTP create/revise/preview outage cases; pricing outage in action guidance |
| Revocation applies to both create and revision retries | `RevocationDeniesCreateReplayWithoutLosingTheCommittedResult`; `RevisionReplayRechecksPricingAndPreservesItsHistoricalSnapshot` |
| Guidance observes current edit-plus-pricing authority; view and abandon remain independent | `RevocationDisablesRevisionGuidanceAndExecutionButNotViewingOrAbandonment`; terminal guidance skips pricing checks |
| Actual provider default denial, explicit independent grant, tenant boundary, revocation, pinning and old-model failure | `RealServerRequiresPersistedOrderPermissionsAndUsesPinnedModel` against OpenFGA 1.21.0 |
| Existing priced receipts, revision races and RLS remain compatible | Existing actual PostgreSQL Orders suite; no persistence contract changed |

The HTTP tests use deliberate provider/store seams and prove pipeline behavior,
not OpenFGA or PostgreSQL internals; the real-provider suites own those guarantees.
Requalify this control when adding any price-bearing route/host, changing the
relation/model, permission or replay sequencing, adapter consistency/timeouts,
pricing ownership, receipt facts or guidance composition. Real identity-provider,
tuple-administration and deployment qualification remain separate.

The final 2026-10-02 normal parallel `./eng/verify.sh` passed locked restore,
format verification, Release build and all **450 tests** across twelve projects,
with zero failures/skips and zero build warnings/errors. This includes 263 CoreApi
cases, the real OpenFGA model/revocation checks and 37 actual PostgreSQL Orders
regressions. A separate read-only security review found no concrete bypass or
unsafe fallback. `BLOCKED = none` for this manual-entry authorization scope.
Coverage, remote CI, real OIDC/tuple administration, deployment rollout and the
complete commercial backend are not qualified by this local gate.
