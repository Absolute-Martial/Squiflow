# Pricing policy and publication owner

**Owner:** COM-006 and COM-007. **Decision:** 2026-10-06. **Product:** v0.0.1.

Pricing owns source selection, publication and override policy. Orders and Quotations own their document totals and validation, using the shared
`Application.Pricing/SellingPriceArithmetic.cs` line arithmetic described by
[`PRICING_COMPONENT_BOUNDARY.md`](PRICING_COMPONENT_BOUNDARY.md). No host copies
that arithmetic. Current repository inventory is in `README.IMPLEMENTATION.md`.

## State and qualification

| Responsibility | State | Exact scope |
|---|---|---|
| Host-neutral facts, selection, envelopes and revalidation comparison | `PRODUCTION_HONEST` | Deterministic capability behavior, guarded by focused unit tests. |
| PostgreSQL prices, immutable receipts/policy history, contextual bounded reads | `PRODUCTION_HONEST` | Real PostgreSQL regressions; not a deployment qualification. |
| Pricing HTTP ingress and shared host integration | `PRODUCTION_HONEST` | Composed independent authorization, strict DTOs, migration and restricted grants; exact normal local receiving gate passed. |
| Orders pre-commit application integration | `PRODUCTION_HONEST` | Frozen facts, compatibility comparison and effect-transaction-bound publication fencing; real backend-loss/race guards and normal gate passed. |
| Quotation/agreement owning-fact integration | `NOT_INTRODUCED` | COM-009 issuance is receiving qualification; the accepted-offer reader/conversion remains COM-010. No client ID or price-book publication may pretend to be a committed fact. |
| Discount calculation, approval workflow, arbitrary wholesale tiers | `NOT_INTRODUCED` | No expression language, guessed eligibility GUID or universal maker/checker rule. |

The normal **1011-test** repository gate supplies combined receiving evidence;
earlier standalone checks alone did not qualify the HTTP surface. This does not
claim remote CI, live deployment or absent quotation/discount/approval capabilities.

## Identity, immutable revisions and compatibility

The dimensions are tenant, stable Catalog `ItemId`, stable Catalog `UnitId`,
currency, scope and half-open validity `[validFrom, validTo)`. `UnitCode` is
retained display metadata, never unit identity or a conversion rule. The exact
base unit needs no conversion; a non-base pricing unit requires an explicit
tenant-owned Catalog `ConversionRevision` to the item's active base unit.
Compatibility evidence is retained, but never converts quantities or reprices
an amount. The host adapter checks current Catalog/Customers public queries before draft creation,
publication and selection. Unit codes retain Catalog's 64-character ASCII
letter/digit/hyphen/underscore contract. Money is nonnegative decimal(19,4).

`PriceId` identifies an economic family; `RevisionId` identifies one immutable
revision, and `RevisionNumber` is a monotonic database-issued sequence value.
New family IDs are server-created. Supplying `PriceId` creates a new draft for
an existing same-key family, not an edit of earlier economic history. Lifecycle:

```text
Draft -> Published -> Superseded or Retired
```

Economic fields never change, including in a draft. Publication may explicitly
supersede one published same-key/same-family revision; otherwise a same-scope
validity overlap is a conflict. No newest-record-wins selection exists.
Current lifecycle may change; complete immutable command snapshots do not.

The additive `202610060002_StableUnitsAndPolicy` migration preserves older rows
and assigns their existing revision identity as their family identity. It does
**not** guess Catalog IDs from old unit strings. Legacy unit-less facts remain
readable by revision but are not candidates or newly publishable. Explicit new
stable-unit revisions are required. Version-2 receipts contain full snapshots;
the earlier unqualified revision-ID-only receipt format is retained but safely
reported as unsupported for replay, never reconstructed from changed lifecycle.
No release/deployment compatibility is inferred for that former standalone code.

## Selection and trusted context

Precedence is committed owning quotation/agreement fact, Customer, Program,
Organization, explicit Wholesale applicability, Default. Committed facts are
not price-book tiers. `ICommittedPricingFactReader` names the future trusted
owning-capability seam; no implementation/composition exists until COM-009.
Current public ingress rejects committed IDs and arbitrary wholesale tier IDs.

Wholesale applicability is an explicit commercial mode (`WholesaleApplicable`),
not an authority boolean, inferred customer eligibility or arbitrary tier GUID.
Price-book wholesale uses one capability-owned scope identity; publication does
not create a tier registry. Organization and program context must belong to the
tenant and the program must match its submitted organization. Customer context
means an active, non-redirected tenant-owned individual record, not billing or
debtor authority. Provider input is tenant/context filtered before selection.

Results are exactly `PriceResolved`, `PriceMissing`, `PriceExpired`,
`PriceConflict`, or `PriceOverrideRequired`. Future-only facts are missing;
ended applicable published facts distinguish expiry. Multiple current facts at
the winning precedence conflict rather than falling back to a lower source.
Explanations retain source/family/revision, why, context, evaluation instant,
observed immutable policy envelope and separate base/override facts. Candidate
amounts other than the selected fact are redacted; HTTP candidate metadata also
omits scope target IDs and customer contact/name data.

`PricingApplication.ResolveAsync` reads the current durable server-owned policy
and `TimeProvider` instant. Selection clients cannot submit policy revision,
minimum/maximum envelope, evaluation time, permission/approval assertions or
committed-fact IDs. A tenant without a policy fails explicitly; no fabricated
default grants overrides. Policy and reference/candidate reads are observations,
not an atomic distributed revocation barrier. Consumers must revalidate before
commit and preserve the observed evidence.

## Permissions and configurable policy

Capabilities are independent: `Pricing.View`, `Pricing.EditDraft`,
`Pricing.Publish`, `Pricing.Retire`, `Pricing.Override`,
`Pricing.OverrideBeyondPolicy`. Order creator/editor/manual-entry authority
does not grant price publication or an override. Draft editing means creating
a replacement draft revision, never mutating an economic snapshot.

Retained selection explanations pin the envelope semantics version that produced their
recorded override decision. Version 1 is frozen: inclusive absolute bounds plus percentage
decrease/increase against the selected base price. Changing envelope **meaning** requires a
new `PricingOverridePolicySemantics` version with a new evaluator and a moved `CurrentVersion`;
version 1 stays reachable, so already-issued quotations and committed Orders keep validating.
Retained-fact readers must route through the pinned version, never through the current engine
meaning. An unsupported retained version fails closed instead of being read with current
semantics. `PricingOverridePolicy` stores envelope numbers only, so the semantics version belongs
to the reading snapshot; the version defaults to 1 so facts written before the field existed read
back as version 1. `QuotationRulesTests.FrozenVersionOneEvaluatorsStayCharacterized` pins the
literal version-1 decisions, so editing the version-1 evaluator in place fails that test and must
be repaired by adding a version rather than by rewriting retained meaning.

Every override requires current `Pricing.Override` and a nonblank bounded reason.
The immutable policy combines inclusive absolute minimum/maximum and optional
maximum percentage decrease/increase relative to the selected base price. Both
absolute and percentage constraints apply. Outside that envelope v0.0.1 uses
the **additional `Pricing.OverrideBeyondPolicy` capability plus reason** branch.
It does not also require an approval reference or a maker/checker. A later
owning approval capability may earn the alternative approval branch separately.
Published base amounts and manual final-price/override evidence stay distinct.

Policy publication requires current publish authority, expected revision and
caller-scoped idempotency. Initial expected revision is 0. All policy revisions
are append-only; revision changes do not rewrite retained selection explanations.
Percent decrease is 0–100; increase is 0–10000; values use supported four-decimal
precision. These are validated configuration bounds, not an arbitrary formula.

## Persistence, races and resource bounds

The adapter embeds runtime SQL, sets transaction-local tenant context and forces
RLS on `price_revisions`, `command_receipts` and `override_policies`. Triggers
prevent economic/policy/receipt rewriting and illegal lifecycle transitions.
An exclusion constraint protects published same-key/scope validity under races.
Mutation and immutable receipt commit together. Fingerprints are invariant-culture.

Receipt-key advisory transaction locks serialize same caller/operation/key;
price-key locks serialize publication/supersession/retirement. After price locks,
current revision and receipt are read again. A loser returns replay or safe
revision/publication conflict, never a half-superseded revision. Failed commands
do not create receipt/economic effects. Permission checks precede receipt lookup,
so revocation/provider outage can block replay without erasing its original fact.

Catalog-priced Order create/revise replay additionally rechecks current elevated
authority when its retained receipt contains a beyond-policy override. The
retained evidence determines this requirement, even if the current policy has
widened. A retry does not reselect/reprice the historical offer. Both the initial
receipt read and a late receipt returned by the mutation store enforce this
check; denial/outage returns the existing safe 403/503 and no replayed effect is
changed. Ordinary in-envelope replay does not query elevated authority.

Candidate SQL probes only the five applicable tenant/item/unit/currency scopes,
filtering exact conversion-revision compatibility before applying any row bound,
with at most two facts per active/expired/future category: at most 30 returned
facts. It does not enumerate other customers, tenants or all historical rows.
The pure engine caps input at 64 facts. History uses contextual keyset paging,
limit 1–50, strictly after a revision; no unbounded history/read count is claimed.
Overlap probes stop at the first conflicting published fact.

## Composed HTTP contract and deployment hooks

`Composition/PricingRoutes.cs` exposes `app.MapPricingEndpoints()`. Under
`/api/v1/tenants/{tenantId}/pricing`:

| Method/path | Shared classification |
|---|---|
| `POST /drafts` | `AuthorizedPricingDraftEdit` |
| `POST /revisions/{revisionId}/publish` | `AuthorizedPricingPublish` |
| `POST /revisions/{revisionId}/retire` | `AuthorizedPricingRetire` |
| `GET /revisions/{revisionId}` | `AuthorizedPricingRead` |
| `POST /resolve`, `POST /history`, `GET /policy` | `AuthorizedPricingRead` |
| `POST /policy` | `AuthorizedPricingPublish` |

Endpoints reuse `TenantCustomerEndpoint.ResolveAsync` and the shared
`TenantCustomerResource`. Requests are strict, duplicate-property-free JSON
objects bounded to 4096 bytes; unknown authority fields are rejected. Mutations
require one bounded `Idempotency-Key`. Bodies for publish and retire are `{}`
(publish optionally accepts `supersedeRevisionId`). Responses are no-store;
typed selection statuses are HTTP 200, while malformed ingress, missing revision,
command conflict and required authority outage are safe 400/404/409/503 responses.

Current host composition calls `AddPricingPostgres()`, registers
`IPricingReferenceReader -> PricingReferenceReader`, and registers server
`TimeProvider.System`. The module registration supplies publication/candidate/
policy stores, `PriceResolver` and `PricingApplication`. CoreApi owns the pinned
OpenFGA permissions, `ITenantPricingAuthorization` (view/edit-draft/publish/retire/
override/beyond-policy), access metadata/middleware, normal solution membership,
migrator ordering, restricted grants and deployment model. Pricing needs grants
for SELECT/INSERT on policy/receipts, SELECT/INSERT and narrowly scoped lifecycle
UPDATE on revisions, sequence usage, and schema usage; no economic UPDATE or
DELETE grant is required. Existing schema dependencies are Tenancy/IdentityAccess.

## Orders integration and permanent guards

`RetainedPriceSelection` preserves item/unit/currency/final amount and full
explanation. `PricingRevalidation.Compare` distinguishes `Unchanged`, `Changed`,
`NotResolved`, and `ManualEntry`. The Orders commit path resolves current
policy/source again immediately before commitment and checks this comparison;
it retains the approved evidence with the committed record under its own
transaction-bound shared publication pin. A null retained
adaptive selection is the supported legacy/manual-entry branch, not a missing
price-book failure. Committed/issued facts are never recomputed from current policy.

Permanent guards live in `PriceSelectionEngineTests`, `PricingPostgresStoreTests`
and `PricingEndpointTests`. Focused runs prove stable unit identity, precedence,
redaction, envelopes, immutable replay/policy history, contextual bounds, RLS,
same-key races and mutation guards. Host tests cover distinct authority,
revocation/replay, outage before effects, malformed/oversized ingress and typed
resolved/missing/expired/conflict behavior. Provider facts are tested against
PostgreSQL 17, not the host's controlled test ports.

The 2026-10-07 focused Release runs passed **11/11 unit tests** and **11/11
PostgreSQL tests**, including conversion-incompatibility regressions first observed
failing against the prior engine/SQL. The original standalone host build block is
historical: the composed Pricing/manual/commercial Orders HTTP filter passed
**39/39**. The recurring focused commands are:

```sh
dotnet test tests/unit/Application.Pricing.Tests/Application.Pricing.Tests.csproj -c Release --no-restore
dotnet test tests/integration/Application.Pricing.Postgres.Tests/Application.Pricing.Postgres.Tests.csproj -c Release --no-restore
dotnet test tests/integration/Application.CoreApi.Tests/Application.CoreApi.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~PricingEndpointTests|FullyQualifiedName~PricingReferenceReaderTests'
```

Requalify on any change to
keys/schema/receipt version, precedence/envelope semantics, trusted reference
adapter, authority/model, route/serialization limits, migration/grants, or a
price-bearing consumer. Exact receiving/full-gate evidence is recorded in
`docs/development-tasks/TASK_STATUS.md` and `README.IMPLEMENTATION.md`.
