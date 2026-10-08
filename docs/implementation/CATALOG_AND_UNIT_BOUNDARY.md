# Catalog and unit boundary

**Status:** COM-005 provider-free catalog, quantity arithmetic, and PostgreSQL
adapter are `PRODUCTION_HONEST` for the focused evidence below. CoreApi routes,
independent pinned-model permissions, migration ordering, runtime grants and
catalog-priced Orders integration are composed and `PRODUCTION_HONEST` for the
declared local receiving scope after the **1011-test** normal gate. Actual numerical inventory
authority remains `NOT_INTRODUCED`.

This is the focused owner for COM-005. It records the accepted v0.0.1
catalog/unit contract and the narrow implementation. Repository inventory and
global gate state remain owned by [`README.IMPLEMENTATION.md`](../../README.IMPLEMENTATION.md).

## Boundary and accepted meaning

The catalog owns tenant-visible identity and selection context for products and
services. It does not own prices, invoices, billing/debtors, fulfillment,
inventory movements, or settlement. It has no generic taxonomy: no mandatory
ready-made/custom-design/social category, MRP, BOM, supplier-cost model, or
arbitrary tenant script is represented.

Each catalog item has:

| Field | Contract |
|---|---|
| `Id` | Stable tenant-owned identity; it survives rename and retirement. |
| `Code` | Optional tenant-unique normalized code/SKU; it is immutable after creation. |
| `Name` | Required bounded display name; rename changes display only. |
| `Description` | Optional bounded display text. |
| `Kind` | `Product` or `Service`; it does not imply stock behavior. |
| `Status` | `Active` or `Retired`; retired rows remain readable and cannot be selected for new lines. |
| `BaseUnitId` | Required reference to an active tenant unit at creation; it is immutable. |
| `StockMode` | Independent `PreciseStock`, `AvailabilityOnly`, or `NonStock`; this flag is not inventory tracking. |
| `Availability` | Only `AvailabilityOnly` items have available/unavailable state. Creation and migration begin unavailable; an authorized catalog editor changes it with revision and idempotency checks. Other stock modes have no availability value. |

Each unit has a stable tenant-owned `Id`, unique normalized `Code`, bounded
`Name`, `Precision` from 0 through 9, `Status`, and revision. Code and
precision define mathematical meaning and cannot be changed in place. A
mathematical meaning change therefore creates a new unit identity; display
rename and retirement increment the existing revision. Supported conversion is
a direct, tenant-owned, explicitly published source-to-target pair with positive
decimal 19,9 numerator and denominator. Publication uses expected revision zero
for the first ratio, then the current pair revision. Each publication appends an
immutable revision; old ratios remain readable after later publication or unit
retirement. There is no conversion graph traversal, inferred reciprocal, composed
path, BOM, MRP, or generic unit-conversion service. Mathematical meaning changes
create a new unit identity or a new direct conversion revision, never mutate an
old fact. Intrinsic same-unit conversion is 1/1 at arithmetic revision one;
display revisions are retained separately.

Conversion arithmetic version 1 evaluates the exact decimal rational
`quantity * numerator / denominator`, then rounds **once** at the declared
target-unit quantity precision with `ToEven`. Integer decimal coefficients avoid
intermediate overflow and double rounding. Positive source quantity must fit
source precision 0–9 and the supported decimal range; out-of-range or rounded-zero
base quantity is rejected. Monetary arithmetic remains Orders-owned decimal
19,4, line-first `ToEven`, then sum. Catalog conversion does not reprice a line,
change its pricing unit, or round money.

Retirement is a retained state, not deletion. Database triggers reject deletes,
reject updates to retired rows, and reject changes to unit meaning or item
identity/kind/base-unit/stock-mode. Current display edits use an expected
revision and cannot silently win an edit race.

## Host-neutral application contract

`modules/catalog/Application.Catalog` contains the provider-free contracts and
operations:

- create/read/browse tenant units and catalog items;
- rename unit/item display fields;
- retire unit/item with revision checks;
- publish/read pinned direct unit-conversion revisions;
- change availability-only state with actor/time and revision/receipt semantics;
- select a new line's item/unit/quantity and explicit conversion revision and
  return immutable `CatalogLineFacts`.

`CatalogLineFacts` retains item ID/code/name, unit ID/code/name, quantity,
unit precision, item and source-unit revisions, base-unit code/name/precision/
revision, rounded base quantity, conversion source/target IDs and exact ratio/
revision, quantity arithmetic version, and rounding mode. The arithmetic version and
rounding mode are required parameters published as `QuantityArithmetic.Version1` and
`QuantityArithmetic.Version1Rounding`, so no consumer re-declares them and an absent
retained field cannot be read as the value the engine produced. Each understood
version gets its own constant beside version 1; a later version never overwrites it.
A new line requires an active item and both active units; a different source unit
requires an explicit
direct conversion revision to the item's base unit. Availability-only selection
also requires explicit available state. Retired records remain readable but
are unavailable for new selection. `Available` means this catalog selection is
permitted, not that precise numerical stock exists or is reserved.

All mutation commands have bounded caller-scoped idempotency keys and canonical
fingerprints. Successful effects persist a receipt and replay the original
snapshot; reusing a key for changed intent returns an idempotency conflict.
Create operations report code/base-unit outcomes. Rename and retirement report
not-found, stale-revision, and already-retired outcomes without overwriting a
newer revision.

The host must supply a membership-derived `TenantContext` and independently
authorize reads or management before body parsing or catalog effects. Catalog
identity itself is not authorization. Management does not imply browse/read;
display, retirement and availability responses disclose transition metadata
only. Create and publication responses return the manager's submitted facts and
created identities. Read DTOs explicitly project safe domain-query fields, not
provider rows or authority-account data.

## PostgreSQL adapter and migration

`modules/catalog/Application.Catalog.Postgres` is provider-isolated. It owns
the `catalog.units`, `catalog.items`, `catalog.unit_conversions`, and
`catalog.command_receipts` tables,
parameterized embedded SQL, transaction-local `app.current_tenant`, forced
tenant RLS, tenant-scoped unique indexes, foreign keys to existing tenancy and
IdentityAccess authority rows, revision predicates, and atomic receipt/entity
effects. Missing or foreign tenant context cannot read or write rows through a
runtime role. Every mutation obtains a transaction-local advisory receipt-scope
lock before master-data effects, so same-key creates converge on the winner's
receipt before code uniqueness can produce a false conflict. Direct pair
publication also serializes its revision. Hash collisions only over-serialize;
they do not share receipts. Reads/browse validate identity, limit 1–50 and UTC
nonempty cursors before opening a provider session.

Item creation locks its base-unit status with `FOR SHARE`; line selection holds
the item and its units' status/availability/meaning stable until completion.
Units are locked in canonical ID order. Retirement is therefore ordered against
create/select rather than admitted from an obsolete unit check. These are
transactional selection facts, not a reservation spanning a later Orders commit.
Conversions and receipts have immutable-fact triggers and runtime SELECT/INSERT
only grants. No generic automatic retry is introduced. Cancellation and provider
deadlines propagate; ambiguous commit recovery uses the original receipt key.

Migration `202610060001_CatalogFoundation` creates the baseline schema;
`202610060002_CatalogConversionsAndAvailability` adds explicit availability,
direct conversions, immutable receipts/conversion facts, active-unit publication
guards, and strengthened master-data retention guards. Its destructive downgrade
fails closed; recovery is forward migration or restore, never erasure of history.
The original target model retains its old shape. The adapter exposes
`AddCatalogPostgres`, `CatalogPostgresMigrations.CreateContext`, and
`PostgresCatalogStore`. Shared solution, CoreApi, DbMigrator, restricted deployment
grants and pinned OpenFGA relations now compose the adapter. Deployment must:

1. apply the catalog migration after the existing tenancy and identity schemas;
2. grant catalog schema USAGE; SELECT/INSERT on units/items/receipts/conversions;
   units UPDATE only name/revision/status/retired_at/retired_by_account_id; items
   UPDATE only name/description/revision/status/retired_at/retired_by_account_id
   plus availability/availability_changed_at/
   availability_changed_by_account_id (no DELETE, unit meaning UPDATE, REFERENCES,
   table-owner, schema-owner or RLS-bypass privilege);
3. register the adapter and application commands in the host's existing
   composition root;
4. classify `AuthorizedCatalogRead` and `AuthorizedCatalogManage` with independent
   ViewCatalog/ManageCatalog requirements using `TenantCustomerResource`, and
   call `app.MapCatalogEndpoints()`; and
5. add real-host admission/error/authorization tests before claiming an API.

Existing free-description/manual Orders are not silently reinterpreted. The
separate catalog-priced entry uses this public line-facts surface and freezes
it with selected price facts. Orders owns pre-commit revalidation under the
transaction-bound shared publication pin; Catalog mutations participate with an
exclusive pin. See [ORDER_CATALOG_PRICED_DRAFTS.md](ORDER_CATALOG_PRICED_DRAFTS.md).
HTTP line-facts guidance alone remains neither trusted commit evidence nor a stock
reservation. Historical reads never resolve current labels or reinterpret ratios.

## Composed HTTP contract

Prefix: `/api/v1/tenants/{tenantId}/catalog`. `MapCatalogEndpoints` declares:

| Method/path | Authority / request |
|---|---|
| `POST /units` | Manage; code/name/precision |
| `POST /items` | Manage; optional code, name, optional description, kind, baseUnitId, stockMode |
| `GET /units`, `GET /items` | Read; limit and after keyset cursor bound to tenant and resource |
| `GET /units/{unitId}`, `GET /items/{itemId}` | Read; history includes retired records |
| `POST /units/{unitId}/display`, `POST /items/{itemId}/display` | Manage; expectedRevision, name, optional item description |
| `POST /units/{unitId}/retire`, `POST /items/{itemId}/retire` | Manage; expectedRevision |
| `POST /items/{itemId}/availability` | Manage; expectedRevision and availability |
| `POST /units/{sourceUnitId}/conversions/{targetUnitId}` | Manage; expectedRevision, numerator, denominator |
| `GET /units/{sourceUnitId}/conversions/{targetUnitId}?revision=N` | Read one pinned historical ratio |
| `POST /line-facts` | Read; itemId/unitId/quantity and optional conversionRevision; no mutation or Idempotency-Key |

Mutation bodies require one bounded Idempotency-Key. JSON bodies are strictly
bounded to 8 KiB declared and actual length, including unknown-length streams;
objects only, unknown and duplicate fields rejected, maximum depth two, strict
numeric tokens, and finite allowed string enums. Wire kinds are `product|service`,
stock modes `preciseStock|availabilityOnly|nonStock`, availability
`available|unavailable`, status `active|retired`. Query fields/counts/cursors are
bounded. Tenant membership, current independent authority, no-store, admission,
request budget and safe global provider-failure handling are inherited from the
existing CoreApi pipeline. No new runtime configuration key is needed.

## Non-claims

This slice does not implement or imply:

- stock balances, movements, reservations, adjustments, numerical stock truth, or
  COM-018 inventory authority;
- generic conversion graphs, wastage, MRP, BOM, manufacturing, purchasing,
  supplier cost, or tax;
- price publication/selection, discount or override authority;
- invoice/debtor/payment/credit/settlement behavior;
- Web, Workstation, sync, generic import or catalog-owned background processing;
- retrofitting item identity into existing Orders drafts or receipts.

## Evidence and recurring guard

The focused unit suite
`tests/unit/Application.Catalog.Tests` proves bounded normalization and
precision, independent item kind/stock mode, expected-revision/fingerprint
construction, exact conversion arithmetic/range/rounding, independent availability,
provider-neutral dependencies, and retained line/conversion facts.

The real PostgreSQL suite
`tests/integration/Application.Catalog.Postgres.Tests` proves migration/model
compatibility, forced RLS, tenant isolation, create replay and changed-intent
conflict, same-key create races, conversion publication races and pinned historical
ratios, availability revisions/receipts, stale revision rejection, unit-retirement
against blocked create/select, restricted grants/RLS writes, direct-store bounds,
immutable conversion facts, receipt-failure atomic rollback, original snapshot
replay through a new store/data source, pooled tenant-context reset, and original
migration shape. It applies actual IdentityAccess/Tenancy owner migrations, not
placeholder authority schemas.
Docker PostgreSQL 17 is required.

`CatalogEndpointTests` and `CatalogHostDoubles` add isolated real-host regression
cases for independent denial before parsing/provider, revocation/replay, safe
outages/errors, explicit DTOs, retirement reads, pinned selection, metadata-only
management responses, strict enum/JSON handling, declared/undeclared body bounds,
and tenant/resource-bound cursors. They use per-test service seams, never change
the shared factory, and do not claim PostgreSQL or OpenFGA properties.

Checks run for this slice:

```text
dotnet test tests/unit/Application.Catalog.Tests/Application.Catalog.Tests.csproj --configuration Release --no-restore  (18 passed)
dotnet test tests/integration/Application.Catalog.Postgres.Tests/Application.Catalog.Postgres.Tests.csproj --configuration Release --no-restore  (8 passed)
```

The original standalone host attempt was build-blocked and remains historical.
After shared composition, the receiving-host filter covering Catalog, Pricing,
duplicates/import, Orders commercial entry and autonomous import passed **87/87**
on 2026-10-07. The Catalog-specific recurring command is:

```text
dotnet test tests/integration/Application.CoreApi.Tests/Application.CoreApi.Tests.csproj --configuration Release --filter FullyQualifiedName~CatalogEndpointTests
```

Normal-gate evidence is recorded in `docs/development-tasks/TASK_STATUS.md` and
`README.IMPLEMENTATION.md`; local checks do not qualify deployment or the whole product.

## Requalification triggers

Requalify this owner and its evidence when any of the following occurs:

- a catalog route, permission/relation, host registration, migrator registration,
  deployment grant, or client surface is added;
- Orders or another capability persists catalog identity or line facts;
- item code, base unit, kind, stock mode, unit precision, or conversion meaning
  becomes mutable or gains a new revision rule;
- stock/availability results begin depending on this flag or a movement store;
- a second unit-conversion arithmetic consumer is introduced;
- receipt schema, RLS context, tenant authority, or supported bounds change.

Until those triggers are qualified, COM-005's production-honest claim is
limited to the provider-neutral module and its actual PostgreSQL adapter/tests;
free-description Orders entry remains the supported host path.
