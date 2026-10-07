# Quotations and accepted-offer conversion

**Status:** Accepted focused owner; owner-selected on 2026-10-07 in this session.
**Baseline:** `03406c45c69f5012e19402827e3cdcfba88d41c3` plus the captured
2026-10-07 incoming commercial source manifest. Product remains `v0.0.1`.
**Runtime state:** COM-009 drafting/issuance/history is `PRODUCTION_HONEST` for
its declared bounded scope. Its exact normal gate passed 1,089/1,089 across 22
projects on 2026-10-07, with no skips or Release warnings/errors. COM-010
responses/conversion is `PRODUCTION_HONEST` for its declared bounded scope, with
`BLOCKED = none` for COM-010. Its receiving evidence and exact normal gate are
recorded in the [COM-010 qualification receipt](../review/COM_010_IMPLEMENTATION_RECEIPT.md).
Accepted decisions and runtime qualification are separate.

The next requested commercial sequence is COM-009 followed by COM-010. Existing
COM-006/007 pricing and the COM-008 gap review are inputs, rather than features
to rebuild. The current focused pricing owners are
[`PRICING_POLICY_AND_PUBLICATION.md`](PRICING_POLICY_AND_PUBLICATION.md)
and [`ORDER_CATALOG_PRICED_DRAFTS.md`](ORDER_CATALOG_PRICED_DRAFTS.md).
Quotations stay optional; direct Orders remain independently usable.

## Accepted first issuance contract (COM-009)

| Decision | Accepted bounded contract |
|---|---|
| Fields | Summary; explicit currency; 1–100 priced lines; optional existing individual and/or organization/program attribution; required expiry instant; optional bounded offer terms. Attribution does not designate a legal debtor. |
| Prices and units | Two explicit entry modes, matching existing Orders: supplied-price lines with independent manual-pricing authority, or Catalog/Pricing-selected lines with retained item/unit/conversion/source/policy facts and independently authorized overrides. No client supplies retained evidence or approval assertions. |
| Arithmetic | Preserve decimal(19,4), four-decimal `ToEven` line rounding and sum of rounded lines. Extract the existing arithmetic into the earned neutral pricing boundary when the second consumer is implemented; preserve Orders validation, fingerprints and historical receipt compatibility. Currency remains explicit; invoice-only NPR scope does not silently restrict offers. |
| Numbering | Server-created quote GUID; a unique monotonically allocated tenant-wide number at first issue; per-quotation issued revision number and immutable revision GUID. Later issues keep the family number and append a revision. No yearly reset, organization sequence, fiscal numbering or gap-free promise. Printable formatting is presentation. |
| Validity and timezone | Issue time is authoritative UTC and becomes `validFrom`; the operator supplies `validUntil` with an explicit offset, normalized to UTC at PostgreSQL microsecond precision. Require `validUntil > issuedAt`; validity is `[issuedAt, validUntil)`. No inferred local midnight or default lifetime. Tenant business-calendar/date-only rules remain open until earned. |
| Authority | Independent quotation create, edit, view and issue permissions, in addition to the applicable manual-pricing or Catalog/Pricing permissions. Issue rechecks current source/policy/override authority under the existing database-owned commercial publication fence. Owner status alone grants no bypass. |
| Revision | Full replacement of a draft requires expected revision. Issuance freezes the complete offer, actor/time, pricing and attribution facts. A later draft does not affect the current issued offer; issuing its replacement supersedes the earlier unaccepted offer atomically. An accepted version cannot be superseded or rewritten; changed terms need a new quotation family. |
| Retry and artifacts | Caller-scoped idempotency, current authorization on replay, and atomic issue/number/revision/receipt persistence. Issued facts succeed independently of PDF rendering, object storage or notification delivery. |

Draft detail and bounded issued-history reads preserve prior facts after price,
Catalog label, customer or permission changes. Current view authority still
applies. Empty/foreign identities, unsupported units, invalid decimals, invalid
expiry and unresolved price selections fail before an effect. Issue/revise races
have one revision-checked winner; no winning command overwrites issued history.

## Accepted response and conversion contract (COM-010)

| Decision | Accepted bounded contract |
|---|---|
| Acceptance evidence | A separately authorized tenant operator records the exact issued revision, response time, bounded reason/evidence note and responding customer's claimed identity. Retain the recording operator distinctly. This is recorded external evidence, not authenticated customer-portal acceptance or a verified signature. |
| Expiry and supersession | Acceptance succeeds only for the latest unsuperseded offer while `issuedAt <= serverNow < validUntil`. Acceptance, rejection and expiry are separate revision-checked commands with retained actor/time/evidence. A read may report elapsed validity but never writes an expiry event. No scheduler is required. |
| Price revalidation | Issue is the price/policy revalidation point. Acceptance and conversion honor the exact frozen offer amount and source, without substituting later price-book values. Current tenant/account/action authority still applies; no historical pricing permission authorizes a new override. |
| Conversion | Convert only an accepted immutable revision, with independent quotation-convert plus Orders-create authority. Create one linked priced Order draft; commitment remains a separate command. Accepted evidence remains valid for conversion after the acceptance window closes. |
| Atomic boundary | The owned conversion/link/order-create/receipt operation commits in one PostgreSQL transaction through a reviewed Orders application/store surface, with a unique quotation-revision-to-order link. Never use two independent commits or private cross-module table access from CoreApi. |
| Concurrency and recovery | A second authorized conversion, even with another key/caller, returns the already linked Order. Response loss is resolved through retained receipts/link identity. Cancellation/crash before commit leaves neither Order nor conversion link; replay after commit preserves both. |

Accepted quotation origin is a distinct authoritative pricing fact, not a
price-book tier. COM-010 must deliberately extend Orders' current-source
revalidation branch so conversion/commitment preserves the accepted offer. The
trusted owning capability supplies this fact through a reviewed in-process
contract; arbitrary client quotation IDs never grant a price or authority.

## Implementation and qualification boundary

The repository owner accepted the complete proposal on 2026-10-07, selecting
COM-009 first, then COM-010 after its dependency is implemented and verified.
The choices above are closed. Current/open decision records link to this owner;
implementation and real-boundary qualification remain separate obligations.

Permanent checks must cover exact expiry boundaries, supersession/acceptance
races, immutable historical reads, current permissions on replay, pricing changes,
real PostgreSQL numbering/rollback/receipt atomicity, cross-tenant denial and
duplicate conversion with crash/response-loss recovery. Run the exact normal
`./eng/verify.sh` after each integrated scope; requalify on authority, pricing,
time, numbering, schema, serialization or cross-capability transaction changes.

Adaptive workflows still need tenant-profile prerequisites and one selected
variation before COM-011–013. Fulfillment needs its admission/approval decisions
before COM-014–015. COM-016–018 require explicit case selection. OPS-013 remains
the distinct general durable-business-audit slice. Live object storage remains
blocked; this proposal earns no storage/delivery or whole-product qualification.

## COM-009 implementation and local verification

`Application.Quotations` owns normalized offer inputs, current action/pricing
checks, selection and issue compatibility; `.Postgres` owns tenant-scoped
transactional heads, immutable issued revisions, tenant numbering and caller
receipts. These are earned neutral/provider boundaries rather than new hosts.
The adapter uses existing Npgsql/EF migration infrastructure and embedded SQL.
No queue, rendering provider, storage dependency or new authorization framework
is introduced. The shared commercial publication pin lives on the same PostgreSQL
backend as the quotation effect and receipt; public Catalog/Pricing queries do
not take an independent pin. The write also checks retained source validity at its authoritative issue instant,
because a publication lock cannot stop expiry while comparison runs.
A changed source/policy rejects issue without
substituting another price. Issued facts include the selected published price,
conversion, policy and override explanation.

CoreApi exposes `POST /api/v1/tenants/{tenantId}/quotations`,
`PUT /{quotationId}/draft`, `POST /{quotationId}/issue`, detail `GET /{quotationId}`
and `GET /{quotationId}/issued?afterRevision=0&limit=20` under that root. Draft
payloads explicitly select `manual` or `catalog`; only replacement includes
`expectedVersion`. Issue receives only `expectedVersion`. Commands require
`Idempotency-Key`; case-insensitive duplicate/unknown members, client authority
fields, mixed pricing modes, missing explicit timestamp offsets and oversized
input fail safely. Body limit is 64 KiB, nesting limit eight, and history pages
contain 1–50 revisions in ascending order. Detail/history DTOs expose retained
offer facts and selected identifiers, without private rows or candidate records.
Reads perform no durable expiry mutation. All protected responses use existing
no-store, correlation, finite operation/relation telemetry and safe failure
mapping. Offer text, price evidence, authorization credentials and database
exceptions are not added to logs or telemetry labels.

Deployment must publish and pin the updated tenant OpenFGA model and explicitly
provision the intended `quotation_creator`, `quotation_editor`,
`quotation_viewer` and `quotation_issuer` grants. Pricing grants remain separate.
An old model fails closed; readiness is not a proof of permitted quote commands.
DbMigrator registers `QuotationDbContext` after Orders, with its own migration
history and additive schema. Apply `deploy/database/grant-core-api-runtime.sql`
with the restricted runtime role after migration; the runtime cannot own the
schema/tables, rewrite issued facts/receipts, delete heads or create schema
objects. Provisioning checks effective table and column privileges, including
PUBLIC-derived grants, and rejects inherited TRUNCATE, DELETE and unsupported
UPDATE. Provisioner failure rolls back all grants in its transaction. Forced RLS
and explicit tenant predicates protect all quotation tables.
No destructive down migration is supported. A deployment rollback must retain
the additive schema and prevent unsupported new writers, rather than discard
issued history.

| Claim / owner | Falsifiable permanent regression under the normal gate |
|---|---|
| Normalized explicit instants, bounds, shared arithmetic and neutral core | `QuotationRulesTests`; existing Orders arithmetic/fingerprint/receipt suites |
| Independent current action/pricing authority and safe replay/outage behavior | `QuotationEndpointTests`; actual pinned-model `OpenFgaTenantAuthorizationTests` |
| Immutable issued history, unique tenant-wide number and atomic issue/receipt | Real PostgreSQL `QuotationPostgresTests`, including concurrent issue/revise/number allocation, receipt failure, RLS and hostile historical storage; `CoreApiRuntimeRoleProvisioningTests` checks destructive inherited grants and complete provisioning rollback |
| Current-source issue guard and one publication pin on the effect backend | Real Catalog/Pricing/Customers/Orders adapter integration in `QuotationPublicationTests` |
| Backend loss releases the pin and rolls back all issue facts | `QuotationPublicationTests` before header write and between effect and receipt; retry after publication change fails explicitly |
| Named protected API contracts, safe malformed input and bounded history mapping | `QuotationEndpointTests`, `OpenApiContractTests` and existing protected-route host tests |
| Migration registration, embedded runtime SQL and compiler dependency ownership | `MigrationRegistryTests`, `ProjectBoundariesTests`, real migration pending-model checks |

Requalification triggers are changes to arithmetic, normalization, retained fact
shape/version, permissions/model/consistency, quote routes/budgets, time source,
number allocation, migration/grants/RLS, receipt sequencing or publication
locking. Focused suites and the exact normal repository gate passed. The retained
[COM-009 receipt](../review/COM_009_IMPLEMENTATION_RECEIPT.md) identifies commands,
evidence, independent review and known limits. COM-010 receiving qualification
is recorded separately below.

The slow-body host regression now observes explicit body-read entry/exit before
applying its completion watchdog. Its native one-second deadline, strict 504,
no-store, cooperative cancellation and no-business-effect assertions remain
unchanged; the correction avoids treating TestServer dispatch time as the body
read completion bound. The receiving gate had exposed the earlier watchdog.

## COM-010 receiving contract

The operator records acceptance or rejection with an independent `quotation_responder` grant; explicit expiry uses `quotation_expirer`; conversion uses `quotation_converter` and current Orders-create authority. None requires historical pricing/override grants. Current membership and each action grant are checked before receipts on every retry. The claimed customer identity is normalized, control-free text of 1–300 characters; the evidence/reason is normalized, control-free text of 1–2,000 characters. Accept/reject require both. Expiry requires evidence and accepts no customer claim. Recorded time is server UTC at PostgreSQL microsecond precision, and the recording account is distinct from the customer's unverified claim. These fields never become telemetry/log labels.

Each command names the exact issued revision GUID and expected quotation head version. One terminal response may be recorded per issued revision. All new responses require the latest unsuperseded revision; acceptance additionally requires `[issuedAt, validUntil)`. Owner clarification on 2026-10-07: rejection may be recorded after expiry if that latest revision has no terminal response yet. Expiry is available at or after `validUntil` and never arises from a read. Acceptance and a replacement issue serialize on the same head lock and expected version. Acceptance blocks further draft replacement or issue for that family, including a dormant newer draft; changed terms require a new family. Rejected/expired facts remain immutable when a later draft/replacement is issued.

Protected command responses expose response evidence and linked Order identity/revision, without priced offer or Order line projections. Full quotation detail and exact historical response reads require quotation view; Order detail/history require Orders view. The immutable conversion link retains the original created Order snapshot for stable duplicate results after expiry, commitment or abandonment. Later distinct-key/caller conversions retain receipts and return that same original Order. They check the accepted revision/link before expected-version conflict; reused keys with changed intent still conflict.

Orders owns the SQL for creating a draft and immutable quotation-origin association in a caller-supplied PostgreSQL transaction. A provider-specific quotation port and a SQL-free CoreApi composition bridge call that public Orders surface; neither capability adapter references the other's adapter. The stateless bridge avoids a composition cycle with the accepted-offer commit reader. The borrowed Orders session never commits, rolls back or disposes caller-owned connection/transaction resources. Order header, lines, origin, initial Order history receipt, quotation link/head revision and quotation receipt commit together.

Quoted Order prices and attribution cannot be replaced. Their commitment checks trusted accepted-offer/link facts through the quotation-owned reader, including manual-priced lines, and compares complete frozen facts with canonical decimal value semantics. It preserves the accepted source even after Catalog/Pricing source retirement, expiry or policy changes. Direct unquoted Orders retain their existing current-source publication fence and revalidation. New quotation-origin receipt envelopes use version five; legacy literal and version-one through version-four Order receipts keep their supported meanings. Individual customer attribution and offer terms remain in the linked immutable quotation; no legal-debtor meaning is added to Orders.

Quotation response/conversion receipts use version two (version-one draft/issue receipts remain supported). Version two allows at most 40 MiB for the combined retained draft, issued offer and original linked Order; offer rows remain 8 MiB, original Order links 16 MiB, and response evidence rows 32 KiB. Existing operations write version one when no response/link extension is present. Unsupported versions fail closed; deployment rollback must retain schema and drain writers whose receipts the target reader cannot support.

Quoted Order detail, each retained history snapshot and matched-key commitment/abandonment replays also require the trusted accepted-offer/link reader and complete frozen-fact comparison. Fresh abandonment checks the returned quoted snapshot before its receipt; a failed check rolls back the transition. A missing reader, contradictory origin, damaged retained price or unsupported receipt version fails closed. Browse exposes bounded header/origin summaries rather than claiming to validate complete line facts.

Quotation-origin and response migrations reject whole-chain downgrade without discarding retained accepted facts. Earlier commercial-price and commitment downgrade guards remain separately exercised using their actual EF-generated historical SQL against PostgreSQL, including the non-superuser schema identity. Those guards do not imply that COM-010 schema rollback is supported.

## COM-010 implementation state

COM-010 is `PRODUCTION_HONEST` for this declared bounded scope, with `BLOCKED = none` for COM-010. The receiving [qualification receipt](../review/COM_010_IMPLEMENTATION_RECEIPT.md) records the exact normal `./eng/verify.sh` result, focused PostgreSQL/CoreApi suites and independent host publishes. COM-009 remains the qualified dependency. This does not claim remote CI, production deployment or whole-product qualification.

CoreApi maps `POST /{quotationId}/accept`, `/reject`, `/expire` and `/convert`, plus exact historical response `GET /{quotationId}/issued/{revisionId}/response`, under `/api/v1/tenants/{tenantId}/quotations`. Response commands use `quotation_responder`; expiry uses `quotation_expirer`; conversion requires `quotation_converter` and current Orders-create. Historical response reads and quotation detail require quotation-view. Commands identify the exact issued revision and expected head version and require caller idempotency. The shared strict transport boundary rejects malformed, duplicate, unknown and oversized JSON. Response/convert command projections contain retained response evidence and, when linked, only Order identity and revision; they do not expose offer or Order prices/lines. Full quotation detail adds current response facts and a conversion reference, never the retained full Order snapshot.

The Orders surface accepts quote origin only through the trusted in-process conversion contract. It persists the original linked Order snapshot and authoritative `{ quotationId, issuedRevisionId, number, revisionNumber }` origin with the Order, lines and initial receipt in the caller-owned transaction. Normal manual and Catalog/Pricing replacement routes reject quotation-bound Orders with `409 order_quotation_bound`; action guidance reports the same reason without probing edit or manual-price permission. Order detail, browse and retained history expose the named origin to current Order viewers. Client Order payloads do not accept or establish quotation origin.

Commitment of a quote-origin Order re-reads trusted accepted-offer/link facts through the quotation-owned contract and compares the complete frozen price and attribution facts, including manual-priced lines, with canonical decimal-value semantics. It preserves the accepted source after Catalog/Pricing retirement, expiry or policy changes. This changes neither the direct-order current-source publication fence nor the meaning of quotation acceptance as operator-recorded external evidence. Orders receipt version five distinguishes quoted Order facts; legacy literal and versions one through four retain their prior meanings. Quotation response/conversion receipts use quotation version two while version-one draft/issue receipts remain readable.

Permanent receiving evidence is owned by `QuotationEndpointTests`, `OpenFgaTenantAuthorizationTests`, real PostgreSQL `QuotationPostgresTests` and conversion/commitment regressions, plus `OrderDraftHistoryEndpointTests`, `OrderDraftActionGuideTests` and Orders receipt-compatibility suites. Controlled CoreApi probes establish transport mapping; provider and exact normal-gate evidence is recorded in the qualification receipt. That evidence qualifies the local PostgreSQL/OpenFGA-backed implementation and does not claim production deployment or remote CI. Requalify on the transaction/link contract, trust boundary, commit price comparison, authorization relation/model, either receipt version, transport projection or Order-origin representation.
