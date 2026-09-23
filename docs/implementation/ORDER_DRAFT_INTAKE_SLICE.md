# Order Draft Intake, Browse, and Abandonment Slice

**Product version:** v0.1.0
**State:** `PRODUCTION_HONEST` for the narrow scope below
**Business evidence status:** repository-backed product hypothesis; not yet validated as the universal first-customer journey

## Declared scope

This slice introduces the first tenant-owned business mutation without claiming the complete Orders capability.

An authenticated current tenant member with the persisted OpenFGA `order_creator` relation can create one priced order draft through:

```text
POST /api/v1/tenants/{tenantId}/orders
Idempotency-Key: caller-generated stable operation key
```

An authenticated current tenant member with `order_viewer` can retrieve a draft in the same tenant through:

```text
GET /api/v1/tenants/{tenantId}/orders/{orderId}
```

The same permission can browse current-tenant draft summaries through:

```text
GET /api/v1/tenants/{tenantId}/orders?limit=25&after=<opaque-cursor>
```

The browse result is ordered newest first by `(created_at, order_id)`, returns at most fifty items, and uses a versioned tenant-bound opaque continuation cursor. The cursor is untrusted paging input rather than authorization authority; current membership, OpenFGA and tenant-scoped persistence remain mandatory on every page. Each item contains only order identity, summary, currency, total, revision, state, creation timestamp and abandonment timestamp when present. It does not expose lines, creating-account identity or a total count. Text search, filtering and selectable ordering are outside this slice.

An authenticated current tenant member with a separate persisted `order_abandoner` relation can close a mistaken or no-longer-needed draft through:

```text
POST /api/v1/tenants/{tenantId}/orders/{orderId}/abandon
Idempotency-Key: caller-generated stable operation key
{"expectedRevision": 1}
```

Abandonment is a one-way transition from `draft` to `abandoned`. It preserves the priced content, lines, creator, original creation timestamp and original create receipt. The server records the abandoning account and authoritative timestamp, and advances the revision to `2`. It does not delete the draft or reverse an issued, financial, inventory or fulfillment effect; those effects are outside this slice. The abandon response and its replay disclose only order ID, state, revision and abandonment timestamp, because `order_abandoner` does not imply `order_viewer`. Detail and browse require the separate view permission and return current state; replaying the original create key returns its stored original creation response.

The draft contains a bounded summary, one to one hundred bounded lines, decimal quantity, unit code, unit price, one currency code, calculated line totals, calculated order total, authoritative creation timestamp, creating account and initial revision `1`. Input is normalized before its semantic fingerprint is calculated. Monetary values use decimal precision/scale `19,4`; each line total is explicitly rounded to four decimal places with `MidpointRounding.ToEven` before totals are summed. This rule belongs to this draft-pricing contract and does not introduce exchange rates, accounting, tax, discounts or multi-currency documents. Browse reads the already committed draft header and performs no business mutation.

`Order` is the implemented contract term for this hypothesis. It does not settle every discovery-sensitive distinction among request, job, work, transaction and sale, and those aliases are not added as parallel entities or fields.

## Authority and execution path

```text
validated JWT identity
→ active application account
→ current tenant membership
→ immutable TenantContext
→ pinned OpenFGA create/view/abandon permission check
→ host-neutral Application.Orders validation and intent fingerprint
→ Application.Orders.Postgres transaction
→ transaction-local tenant context + explicit tenant predicates + PostgreSQL RLS
→ order rows and idempotency receipt committed atomically
```

Browse follows the same identity, membership and OpenFGA path, then executes one capability-owned keyset query under the same transaction-local tenant context, explicit tenant predicate and forced RLS policy. Abandonment follows that path with its separate permission, then uses an expected revision and a one-way state guard in a tenant-scoped transaction.

CoreApi owns HTTP parsing, headers, status mapping and authorization-framework integration. `Application.Orders` owns draft input meaning, normalization, calculation and idempotency intent identity. `Application.Orders.Postgres` owns parameterized SQL, the transaction, receipt/effect atomicity, RLS context and persistence mapping. There is no SQL microservice, generic repository, generic unit of work or provider type in the host-neutral capability.

## Retry and concurrency contract

The idempotency scope is:

```text
tenant ID + account ID + operation + Idempotency-Key
```

The create receipt stores the normalized intent fingerprint and the original successful response snapshot in the same PostgreSQL transaction as the order and lines. The abandon receipt uses a distinct operation name, fingerprints order identity plus expected revision, and commits atomically with the lifecycle transition.

- same scope/key and same intent returns the committed result without a second order;
- same scope/key and changed intent returns `409 idempotency_key_conflict`;
- simultaneous commands sharing a scope/key commit one order and one receipt;
- the same key may represent separate operations for another current account or tenant;
- a client retry after losing the successful response receives the stored outcome.
- the same abandon key and intent replays the committed abandoned response; the same key with a different order/revision conflicts;
- a different abandon key cannot close an already abandoned draft; a stale expected revision conflicts;
- a create-key replay after abandonment still returns the original creation response, while a current read reports abandonment.

Receipts are retained indefinitely in this initial slice, alongside the order they protect. No cleanup or order deletion behavior is introduced. Any future retention, archival, deletion, draft-edit or correction policy must preserve the declared duplicate-suppression window or introduce an explicit compatible replacement and requalify response-loss behavior before removing receipts.

Revision `1` identifies a newly created draft; successful abandonment advances it to `2`. Expected-revision comparison exists only for this one-way transition. Draft content editing remains absent.

## Tenant isolation and database role contract

All three Orders tables carry `tenant_id`. Every runtime query includes an explicit tenant predicate, and every insert supplies the tenant from `TenantContext`. Each operation opens a transaction and sets `app.current_tenant` with transaction-local `set_config`; PostgreSQL RLS uses the same value for `USING` and `WITH CHECK`, is enabled and forced, and returns no tenant rows when context is absent.

The CoreApi runtime database identity requires schema usage plus the exact `SELECT`/`INSERT` rights and narrowly scoped lifecycle-column `UPDATE` rights used by this slice. It must not own the schema, be superuser, have `BYPASSRLS`, or receive DDL/delete/priced-column update rights. Migration credentials remain separate and the one-shot DatabaseMigrator applies IdentityAccess, Tenancy and Orders migrations under one bounded advisory lock.

## Stable failure behavior

| Condition | Result |
|---|---|
| unauthenticated | `401 authentication_required` |
| no current tenant membership | `403 tenant_access_denied` |
| missing OpenFGA permission | `403 tenant_permission_denied` |
| OpenFGA timeout/unavailable | `503 authorization_unavailable` |
| malformed, null, structurally invalid or out-of-range input | `400` with a stable validation code |
| missing, repeated, oversized or invalid `Idempotency-Key` | `400 idempotency_key_invalid` |
| request body above the route limit | `413 request_too_large` |
| same key with changed intent | `409 idempotency_key_conflict` |
| order absent from the current tenant | `404 order_not_found` |
| missing, repeated, malformed or out-of-range browse limit | `400 page_size_invalid` |
| repeated, malformed or unsupported browse cursor | `400 cursor_invalid` |
| malformed JSON abandon request | `400 request_invalid` |
| missing, non-integer or nonpositive expected revision | `400 expected_revision_invalid` |
| abandoned draft missing from current tenant | `404 order_not_found` |
| stale expected revision | `409 revision_conflict` |
| draft already abandoned under another operation | `409 order_already_abandoned` |
| same abandon key with changed order/revision | `409 idempotency_key_conflict` |

Successful Order representations and browse pages are `no-store`. Provider exception details and SQL values are not exposed to clients.

## Source admission

No new framework was needed. The slice uses the already admitted Npgsql/EF Core provider boundary and one shared process-wide bounded data source. Browse uses ordinary parameterized PostgreSQL keyset pagination and an index matching tenant plus descending creation/identity order; no search engine, pagination framework or count projection is introduced. Abandonment is a conditional tenant-scoped PostgreSQL transition with the existing durable receipt pattern, not a generic workflow engine. FullStackHero's pinned tenant-isolation tests remain a test donor for explicit tenant keys and hostile cross-tenant cases; its generic repository/UoW conventions and request-selected tenant authority remain rejected. Finbuckle continues to resolve only the untrusted route candidate. OpenFGA remains the permission decision provider and does not own tenant context, draft state, idempotency or database reachability.

## Evidence and regression guards

| Claim | Falsifiable evidence / permanent guard |
|---|---|
| host-neutral business meaning and calculation | `Application.Orders.Tests` normalization, precision, rounding, range, create/abandon fingerprint, old-receipt compatibility and forbidden-dependency tests |
| authenticated tenant/API behavior | `Application.CoreApi.Tests` real pipeline tests for membership ordering, allow/deny/outage, payload/header/query bounds, create/abandon replay and conflicts, browse continuation, cross-account and cross-tenant behavior |
| pinned provider authorization | real OpenFGA 1.21.0 container test for workspace/order permission tuples, contextual membership and explicit model ID |
| migration/model agreement | real PostgreSQL model-change and migration lifecycle tests, plus historical target-model shape assertions for each Orders migration |
| atomic caller-scoped idempotency | real PostgreSQL create/abandon replay, mismatch, concurrent duplicate, response-loss replay, old-create-receipt replay and independent-account tests |
| tenant isolation and bounded browse | real PostgreSQL explicit-scope, forced-RLS, no-context, wrong-tenant, pooled-reset, hostile-write, stable keyset-order and no-duplicate/gap tests |
| least privilege | real PostgreSQL runtime-role tests deny DDL/delete and priced-column updates while permitting only the lifecycle transition |

Requalification triggers include schema/RLS policy, browse index, cursor format or ordering changes; pooling/pooler mode changes; receipt scope/retention changes; draft mutation or deletion; pricing precision/rounding changes; OpenFGA relation/model changes; a new host calling the capability; or a new retry/execution path.

## Explicit non-claims

The slice does not implement customer/party records, document numbers, tax, discount, quotations, submission/acceptance, draft content editing, approval, fulfillment, inventory, invoicing, payment, refunds, printing, attachments, audit ledger, local-first Workstation state, synchronization, outbox, Worker execution, reporting, text/full-text search, filters, selectable ordering, total counts, profile-driven implementation variants or tenant role administration. These remain `NOT_INTRODUCED`, not deferred hardening of an active path.
