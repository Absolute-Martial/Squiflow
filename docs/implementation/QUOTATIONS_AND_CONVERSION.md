# Quotations and accepted-offer conversion

**Status:** Accepted focused owner; owner-selected on 2026-10-07 in this session.
**Baseline:** `03406c45c69f5012e19402827e3cdcfba88d41c3` plus the captured
2026-10-07 incoming commercial source manifest. Product remains `v0.0.1`.
**Runtime state:** COM-009 drafting/issuance/history is `PRODUCTION_HONEST` for
its declared bounded scope. Its exact normal gate passed 1,089/1,089 across 22
projects on 2026-10-07, with no skips or Release warnings/errors. COM-010
responses/conversion remains `NOT_INTRODUCED`; it may now be implemented against
this qualified dependency. Accepted decisions and runtime qualification are separate.

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
evidence, independent review and known limits. COM-010 qualification will add
response/conversion evidence separately.

The slow-body host regression now observes explicit body-read entry/exit before
applying its completion watchdog. Its native one-second deadline, strict 504,
no-store, cooperative cancellation and no-business-effect assertions remain
unchanged; the correction avoids treating TestServer dispatch time as the body
read completion bound. The receiving gate had exposed the earlier watchdog.
