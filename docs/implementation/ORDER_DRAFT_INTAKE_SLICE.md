# Order Draft Intake Slice

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

The draft contains a bounded summary, one to one hundred bounded lines, decimal quantity, unit code, unit price, one currency code, calculated line totals, calculated order total, authoritative creation timestamp, creating account and revision `1`. Input is normalized before its semantic fingerprint is calculated. Monetary values use decimal precision/scale `19,4`; each line total is explicitly rounded to four decimal places with `MidpointRounding.ToEven` before totals are summed. This rule belongs to this draft-pricing contract and does not introduce exchange rates, accounting, tax, discounts or multi-currency documents.

`Order` is the implemented contract term for this hypothesis. It does not settle every discovery-sensitive distinction among request, job, work, transaction and sale, and those aliases are not added as parallel entities or fields.

## Authority and execution path

```text
validated JWT identity
→ active application account
→ current tenant membership
→ immutable TenantContext
→ pinned OpenFGA create/view permission check
→ host-neutral Application.Orders validation and intent fingerprint
→ Application.Orders.Postgres transaction
→ transaction-local tenant context + explicit tenant predicates + PostgreSQL RLS
→ order rows and idempotency receipt committed atomically
```

CoreApi owns HTTP parsing, headers, status mapping and authorization-framework integration. `Application.Orders` owns draft input meaning, normalization, calculation and idempotency intent identity. `Application.Orders.Postgres` owns parameterized SQL, the transaction, receipt/effect atomicity, RLS context and persistence mapping. There is no SQL microservice, generic repository, generic unit of work or provider type in the host-neutral capability.

## Retry and concurrency contract

The idempotency scope is:

```text
tenant ID + account ID + create-order-draft operation + Idempotency-Key
```

The receipt stores the normalized intent fingerprint and the original successful response snapshot in the same PostgreSQL transaction as the order and lines.

- same scope/key and same intent returns the committed result without a second order;
- same scope/key and changed intent returns `409 idempotency_key_conflict`;
- simultaneous commands sharing a scope/key commit one order and one receipt;
- the same key may represent separate operations for another current account or tenant;
- a client retry after losing the successful response receives the stored outcome.

Receipts are retained indefinitely in this initial slice, alongside the order they protect. No cleanup or order deletion behavior is introduced. Any future retention, archival, deletion, draft-edit or correction policy must preserve the declared duplicate-suppression window or introduce an explicit compatible replacement and requalify response-loss behavior before removing receipts.

Revision `1` is durable output identity for this immutable create/read slice. Draft editing and expected-revision conflict handling remain absent; the revision field does not claim an implemented update contract.

## Tenant isolation and database role contract

All three Orders tables carry `tenant_id`. Every runtime query includes an explicit tenant predicate, and every insert supplies the tenant from `TenantContext`. Each operation opens a transaction and sets `app.current_tenant` with transaction-local `set_config`; PostgreSQL RLS uses the same value for `USING` and `WITH CHECK`, is enabled and forced, and returns no tenant rows when context is absent.

The CoreApi runtime database identity requires only schema usage plus the exact `SELECT`/`INSERT` rights used by this slice. It must not own the schema, be superuser, have `BYPASSRLS`, or receive DDL/delete rights. Migration credentials remain separate and the one-shot DatabaseMigrator applies IdentityAccess, Tenancy and Orders migrations under one bounded advisory lock.

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

Responses are `no-store`. Provider exception details and SQL values are not exposed to clients.

## Source admission

No new framework was needed. The slice uses the already admitted Npgsql/EF Core provider boundary and one shared process-wide bounded data source. FullStackHero's pinned tenant-isolation tests remain a test donor for explicit tenant keys and hostile cross-tenant cases; its generic repository/UoW conventions and request-selected tenant authority remain rejected. Finbuckle continues to resolve only the untrusted route candidate. OpenFGA remains the permission decision provider and does not own tenant context, draft state, idempotency or database reachability.

## Evidence and regression guards

| Claim | Falsifiable evidence / permanent guard |
|---|---|
| host-neutral business meaning and calculation | `Application.Orders.Tests` normalization, precision, rounding, range, fingerprint and forbidden-dependency tests |
| authenticated tenant/API behavior | `Application.CoreApi.Tests` real pipeline tests for membership ordering, allow/deny/outage, payload/header bounds, replay, cross-account and cross-tenant behavior |
| pinned provider authorization | real OpenFGA 1.21.0 container test for workspace/order permission tuples, contextual membership and explicit model ID |
| migration/model agreement | real PostgreSQL model-change and migration lifecycle tests |
| atomic caller-scoped idempotency | real PostgreSQL replay, mismatch, concurrent duplicate, response-loss replay and independent-account tests |
| tenant isolation | real PostgreSQL explicit-scope, forced-RLS, no-context, wrong-tenant, pooled-reset and hostile-write tests |
| least privilege | real PostgreSQL runtime-role tests deny DDL/delete and operate only through granted Orders statements |

Requalification triggers include schema/RLS policy or tenant-setting changes; pooling/pooler mode changes; receipt scope/retention changes; draft mutation or deletion; pricing precision/rounding changes; OpenFGA relation/model changes; a new host calling the capability; or a new retry/execution path.

## Explicit non-claims

The slice does not implement customer/party records, document numbers, tax, discount, quotations, submission/acceptance, draft editing, approval, fulfillment, inventory, invoicing, payment, refunds, printing, attachments, audit ledger, local-first Workstation state, synchronization, outbox, Worker execution, reporting, search/listing, profile-driven implementation variants or tenant role administration. These remain `NOT_INTRODUCED`, not deferred hardening of an active path.
