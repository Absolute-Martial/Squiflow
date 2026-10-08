# Catalog-priced Orders: COM-005/006/007 integration

**Current state:** `PRODUCTION_HONEST` for the declared local scope after integrated
host checks and the **1011-test** normal receiving gate. The transaction-bound publication pin and backend-loss
regressions below are locally verified; no independent pin session remains.
Manual-entry Orders retain their existing contract. Product version remains v0.0.1.

## Owned scope

Orders accepts a separate catalog-priced create/full-revision request. The client
supplies stable item/unit IDs, quantity, optional direct conversion revision,
customer/program/organization applicability and explicit wholesale applicability.
It cannot supply catalog labels, converted quantities, published prices,
publication/policy revisions, explanations or permission flags. The new strict JSON
boundary rejects unknown fields and case-insensitive duplicate members at every
object depth. Current membership and declared command/view permissions run before
body parsing or source selection.

Routes are `POST /api/v1/tenants/{tenantId}/orders/catalog-priced` and
`PUT /api/v1/tenants/{tenantId}/orders/{orderId}/catalog-priced-draft`.
Create requires CreateOrder + ViewCatalog + ViewPricing; revision requires
EditOrder + ViewCatalog + ViewPricing. Neither requires ApplyManualPrice.
An override separately requires current Pricing.Override and a nonblank bounded
reason; exceeding the current policy additionally requires
Pricing.OverrideBeyondPolicy. No client approval assertion grants authority.
The trusted adapter reads beyond-policy authority only when the selected policy
actually requires it; an unrelated elevated-authority outage does not reject a
within-policy override.
Commit retains its existing CommitOrder permission and expected-revision contract;
retained overrides are checked against current override authority during validation.

Catalog selection uses `SelectCatalogLineFacts`; price selection uses
`PricingApplication`. No Orders or host implementation reads Catalog/Pricing
private tables. Orders keeps decimal(19,4) quantities/amounts and the existing
single `OrderDraftPriceCalculator`, including final line-total ToEven rounding.
Catalog's wider quantity precision is not implicitly admitted into the existing
Orders monetary/storage contract: an Orders quantity must still fit its existing
four-decimal rule. Trusted catalog selection admits Catalog's stable 64-character
ASCII letter/digit/hyphen/underscore unit codes. Manual ingress remains constrained
to its existing 1–16-character ASCII alphanumeric rule; the single calculator's
amount arithmetic is identical in both modes.

Each commercial line freezes complete `CatalogLineFacts`, the selected published
`PriceRevision`, and `RetainedPriceSelection`. This includes item/unit labels and
revisions, source/base-unit precision, direct conversion revision/ratio, base
quantity and arithmetic version/rounding, stable price family/revision/source,
validity, policy values/revision, applicability context, selection candidates,
explanation and optional override reason/elevation evidence. Historical detail,
command receipts, history, abandonment and commitment retain these facts without
consulting current Catalog/Pricing. Label renames do not rewrite history.
The HTTP source-price DTO projects retained economic fields and publication time;
it does not expose the price author's account identity or a provider entity.

The host owns the commercial-facts wire records and maps Catalog/Pricing/Orders
records explicitly; module enum types are never part of the public v1 contract.
Price scope kind and price publication state serialize as explicit lowercase
strings, so inserting a new module enum member cannot silently renumber an
existing stored draft's response. `PriceScope.Precedence` is an internal selection
ranking and is not published; it stays visible only through selection behavior and
the ordering tests. Member names and member order of the commercial facts are
unchanged; only the enum representation and the removal of `precedence` differ.
The permanent guard is
`OrderCommercialEndpointTests.CommercialFactsWireShapeCarriesNoModuleEnumOrdinalOrInternalPrecedence`,
which asserts string `kind`/`state` values and the absence of any `precedence`
member in the served commercial-facts subtree.

Optional `ICustomerCanonicalDirectory` redirects customer IDs only while resolving
new mutable content, after replay lookup. The original request fingerprint stays
stable; retained price applicability contains the resolved current ID. Existing
immutable receipts/history never redirect historical IDs. A later redirect may
invalidate a draft's current pricing context and require explicit revision.

## Replay and compatibility

The original free-description/manual routes remain separate and use existing
CreateOrder/EditOrder + ApplyManualPrice authority. Their fingerprint computation,
legacy receipts and mathematical rules are unchanged. Wire ingress cannot attach
commercial facts to those manually priced lines: legacy unknown fields remain
ignored and are never mapped to server facts. New catalog-priced ingress is strict.
The added legacy-ignored-fields host regression protects this compatibility;
making existing manual DTOs universally strict was reverted after three old
manual-permission/replay tests falsified that change in the first combined gate.

Catalog requests have a namespaced, length-delimited semantic fingerprint using
culture-independent decimal normalization. A replay checks the same durable
caller/tenant/operation/key receipt before source queries; it returns the first
immutable result even after source retirement or policy changes. Manual and
catalog requests share the existing operation/key namespace, so switching entry
modes with the same key conflicts rather than creating another order. Concurrent
same-key observations converge through the receipt's existing unique key and
rollback of the losing order/revision transaction.

The additive Orders migration `202610070001_OrderCommercialFacts` adds nullable
`orders.order_draft_lines.commercial_facts` JSONB and widens retained unit-code
storage to Catalog's code boundary. Existing rows remain manual.
New commercial receipts use schema version 4; direct legacy snapshots and envelope
versions 1–3 remain supported. The new writer keeps versions 1–3 for manual facts.
Receipt readers validate version/operation/lifecycle, line facts and frozen
selection identity/arithmetic. Schema rollback refuses to discard any retained
commercial line or version-4 receipt, including an older replaced draft snapshot.
Existing table-level SELECT/INSERT line grants cover the new column; no Catalog or
Pricing private-table grant to Orders is needed.

## Commitment and the pin boundary

Orders locks its current header before reading lines and rechecks a waited-on
same-key receipt. Draft revision/abandon/commit cannot alter those retained lines
while revalidation runs. For an eligible commercial draft, Orders acquires the
tenant shared PostgreSQL **transaction** advisory pin on the **same connection and
transaction** that will update the header and insert its receipt, before invoking
`IOrderCommercialCommitGuard.IsCompatibleAsync`. The neutral contract is comparison-only:
its caller owns protection through effect/receipt COMMIT or ROLLBACK, and the guard
returns a compatibility boolean rather than a lease. A missing guard or false
comparison yields
`CommercialFactsConflict`, mapped to HTTP 409 `order_commercial_facts_conflict`,
without state/revision/receipt changes. It never silently refreshes or reprices.
Same-key replay/conflict precedes pin acquisition and comparison. Manual lines and
ineligible lifecycle/revision paths do not acquire this pin or invoke this port.

`OrderCommercialCommitGuard` reads Catalog compatibility and current published
price/policy through their public contracts and uses `PricingRevalidation.Compare`.
Retirement, unavailability,
conversion/precision/arithmetic incompatibility, missing/expired/conflicting price,
changed selected revision/policy/context or revoked required override authority
rejects commitment. Catalog label/revision changes alone remain compatible when
the commercial conversion meaning is unchanged; frozen labels remain intact.

**Implemented provider binding:** Orders.Postgres embeds `PinCommercialPublication.sql`
and executes `pg_advisory_xact_lock_shared` inside its existing effect/receipt
transaction. PostgreSQL retains the lock until that transaction ends, not merely
until comparison returns. Exceptions/cancellation, including while waiting for
the pin or a public comparison query, unwind the existing Orders session and roll
back the transaction. There is no dedicated pin connection, provider lease,
`IOrderCommercialPublicationPins`, or CoreApi pin adapter. No session advisory
lock, process-local mutex, private-table access, new table, package or migration
is introduced.

**Lock order (part of the contract):** Orders checks replay first, then locks its
own header, rechecks replay and reads retained lines; only an eligible commercial
draft acquires the shared publication pin, then compares public sources and writes
the header/receipt before ending the transaction. Comparison queries may use
their own ordinary read transactions but must acquire **neither shared nor exclusive
publication pins**. Never acquire a second shared pin on another backend while
Orders holds one: PostgreSQL queues it behind a waiting exclusive publisher, which
is itself waiting for Orders, creating a deadlock. The guard is deliberately not
a lock-acquisition abstraction.

Catalog unit/item creation, rename, retirement, conversion publication and
availability changes obtain the exclusive `pg_advisory_xact_lock` on their own
mutation transaction **before** tenant setup, receipt/pair/key locks, queries or
row locks. Pricing publication (including supersession), retirement and policy
publication do likewise. Nonpublished Pricing draft creation does not alter
selectable facts. Read/selection/reference queries do not request an exclusive
publication lock, including when an exclusive publisher is already waiting.
Both writer adapters and Orders embed their SQL resources, using the identical
signed 64-bit key:
the first 16 hexadecimal characters of MD5 over
`application:commercial-publication:v1:` plus PostgreSQL's canonical UUID text.
This mapping is versioned, locale-independent and does not use randomized .NET
hashing or PostgreSQL's version-dependent text-hash implementation. All participants
must connect to the same PostgreSQL database. A theoretical hash collision only
over-serializes unrelated tenants; it cannot under-protect one tenant's facts.
Direct privileged SQL/nonparticipating writers are not a supported mutation path.
Changing the key/namespace requires a coordinated rollout without mixed writers.
Publication writers must never take Orders header/receipt locks. The present order
is safe because writers take their exclusive pin before Catalog/Pricing row locks,
and no writer waits on Orders rows. A future cross-capability mutation that breaks
this condition must redesign and requalify the common lock order, not add an
Orders row lock beneath an existing exclusive publication pin.
Default PostgreSQL PUBLIC execution rights cover these built-ins; a hardened
deployment revoking them must explicitly grant the runtime role EXECUTE on
`pg_catalog.pg_advisory_xact_lock(bigint)`,
`pg_catalog.pg_advisory_xact_lock_shared(bigint)` and `pg_catalog.md5(text)`.

The current comparison uses at most **two simultaneous runtime connections**:
the pinned Orders effect/receipt transaction and one sequential public query.
Reference/policy/candidate queries finish before the next opens. Configure at least
two for a single commercial commit (tested with `MaxPoolSize=2`); additional
headroom and aggregate admitted-operation budgets remain host-owned. With `C`
concurrent commits each holding one connection, `C + 1` is the conservative
progress floor before other pool consumers, including connections retained by
waiting publishers and health/administration work. Two alone does not prove
saturation safety. A newly nested reference query adds its connection to this
budget and requires requalification.

**Backend-loss guarantee:** the only pin backend is the Orders effect/receipt
backend. Terminating it after successful comparison, either before the UPDATE or
after UPDATE while receipt insertion waits, rolls back the Order and receipt and
releases the waiting publisher. The pre-UPDATE case verifies publisher progress
before resuming the paused guard; both cases verify that retry of the same key
is not a replay and rejects the now-retired price. There is no separate lease whose loss can leave an Orders
transaction alive, and no heartbeat or final-read illusion. A lost response after
PostgreSQL has already committed remains outcome-unknown to the caller; replay
through the atomic durable receipt resolves that existing retry contract.
Time validity is evaluated at the pinned comparison instant, not continuously
until transaction completion: a price expiring after that instant does not undo
an otherwise valid atomic commitment.

## Composition hooks

- Map `app.MapCatalogOrderEndpoints()`.
- Declare `AuthorizedTenantCatalogOrderCreation` and
  `AuthorizedTenantCatalogOrderRevision` with the permission sets above; ensure
  mixed Orders/Catalog/Pricing requirements receive compatible trusted resources.
- `AddOrdersPostgres` adds `IOrderDraftReceiptReader` and
  `CatalogOrderDraftApplication` over the existing Orders store.
- Call `services.AddCatalogOrderIntegration()` to register the trusted
  `OrderPricingAuthorityReader` and neutral `OrderCommercialCommitGuard`.
- Register `IOrderCommercialCommitGuard` with `OrderCommercialCommitGuard` over
  `SelectCatalogLineFacts`, `PricingApplication` and `IOrderPricingAuthorityReader`,
  alongside existing public Catalog/Pricing applications. No publication-pin DI
  registration or host adapter is needed. The PostgreSQL Orders store accepts the guard as
  its optional third constructor argument so existing manual-only callers remain
  source-compatible and commercial commits fail closed when not composed.
- Register Customers' canonical directory when current-customer redirection is
  enabled; no immutable-history migration is introduced.

## Evidence and requalification

Permanent guards: `CatalogOrderDraftTests` (selection/replay/authority/revalidation),
`OrderCommercialFactsPostgresTests` (real PostgreSQL retained facts, receipt races,
tenant isolation, row-lock binding and production comparisons),
`OrderPublicationPinsPostgresTests` (actual full migrator and restricted runtime
roles, all eleven writer paths blocked before row locks across independent data
sources/backends, concurrent readers/shared commit pins, unrelated tenants,
cancellation/pool cleanup, pin-and-effect backend termination before/after UPDATE,
publisher-first rejection, replay/manual paths under a held exclusive pin, and real
commercial comparison/receipt insertion racing a real Pricing retirement), and
`OrderCommercialEndpointTests` (real host pipeline and strict protected ingress).
The scheduling decorator forwards to the production comparison and returns its
actual boolean; it never replaces serialization/comparison or supplies a fake pin.
Writers are also queued before comparison to guard against a second-pin fairness
deadlock. Independent backend instances prove database-owned exclusion; no separate
client OS-process crash/HA qualification is inferred. The local focused run on
2026-10-07 passed Orders PostgreSQL **71/71**, including all **20** publication-pin
cases and both backend-loss cases; Orders unit tests passed **104/104**,
Catalog PostgreSQL **8/8**, and Pricing PostgreSQL **10/10**. These are focused
receiving evidence, not a combined host/normal-gate qualification.

Requalify when Catalog/Pricing contracts, conversion arithmetic, precedence,
policy/override semantics, pin participants, receipt serialization/migrations,
Orders transaction ordering or endpoint permissions change. No pricing tax,
discount engine, inventory reservation/movement, fulfillment, invoice issuance,
committed quotation/agreement authority or customer credit authority is introduced.
The scoped verification receipts are reported to the integrating session; only an
inspected exact normal repository gate can qualify the combined current source.
