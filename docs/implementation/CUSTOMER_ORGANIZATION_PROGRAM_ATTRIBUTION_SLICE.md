# Customer Organization, Program, and Draft Attribution Slice

**Product version:** v0.1.0
**Scope:** tenant-owned customer context for the first organization-with-program case
**Business evidence:** owner-selected implementation anchor; customer workflow and legal billing meaning remain unvalidated

## Declared meaning

`CustomerOrganization` is a stable, tenant-owned customer-side grouping that an operator can associate with a priced order draft. `CustomerProgram` is a stable child of exactly one such organization. These identities and the bounded display names are immutable in this slice. A program may be omitted on a draft, but a program cannot be associated without its owning organization. The selected first commercial case exercises both. This association is **attribution only**: it does not identify a legal debtor, establish an account balance or credit limit, issue a quotation or invoice, reserve stock, accept an order, fulfill work, or settle payment.

The Customers capability owns organization and program identity, parentage, tenant isolation, create and query behavior. Orders asks the Customers public host-neutral query to resolve the pair before creating an attributed draft; it does not read Customers tables. Its own PostgreSQL schema has composite foreign keys so a wrong-tenant organization or a program under a different organization cannot be persisted even if a caller bypasses the application query. No generic Party or `PartyKind` representation is introduced.

## API and authority

The CoreApi tenant routes are:

```text
POST /api/v1/tenants/{tenantId}/customers/organizations
GET  /api/v1/tenants/{tenantId}/customers/organizations
GET  /api/v1/tenants/{tenantId}/customers/organizations/{organizationId}
POST /api/v1/tenants/{tenantId}/customers/organizations/{organizationId}/programs
GET  /api/v1/tenants/{tenantId}/customers/organizations/{organizationId}/programs
GET  /api/v1/tenants/{tenantId}/customers/organizations/{organizationId}/programs/{programId}
```

Create bodies contain a bounded `displayName` and require one caller-owned `Idempotency-Key`. Lists use a bounded `limit` of 1–50 and an opaque tenant-bound cursor; program cursors are also parent-bound. Creation returns `201`; exact retry returns the original entity with `200` and `Idempotency-Replayed: true`; the same key with changed intent returns `409 idempotency_key_conflict`. Missing, wrong-tenant and wrong-parent identities are indistinguishable `404` responses. Successful customer data is `no-store`.

Every route first validates the JWT, resolves the active application account and current tenant membership, then checks one separate persisted OpenFGA relation under the pinned model. The direct relations are `organization_creator`, `organization_viewer`, `program_creator`, and `program_viewer`; each computed permission intersects the direct relation with the verified current membership contextual tuple. Membership or another permission alone is insufficient. Provider outage fails closed. The application performs permission checks only; it does not write model/permission tuples or invent an Owner role.

The existing order-create route accepts optional `customerContext: { organizationId, programId? }`. Absence preserves the original draft contract. For an attributed draft, the pair is validated and resolved through Customers in the same current `TenantContext` before Orders persistence. A structurally invalid pair returns `400 customer_context_invalid`; an invisible organization or wrong/invisible program returns `404 customer_context_not_found`. Create response, current detail and browse summary include the association. An order creator still needs `order_creator`; customer-context create/view permissions do not grant order creation, and order creation does not grant customer-context administration.

## Durable and retry behavior

Customers uses caller-scoped receipts keyed by tenant, current account, operation kind and idempotency key. A transaction contains the created entity and receipt. Both Customers tables and their receipts use transaction-local `app.current_tenant`, explicit tenant predicates, and enabled/forced PostgreSQL RLS. CoreApi runtime needs `SELECT`/`INSERT` on those four tables and no update, delete, DDL, ownership, or `BYPASSRLS` right. Customer records have no delete or reparent operation in this slice, making an order's resolved reference stable across the Customers lookup and Orders commit. A future mutable lifecycle must requalify that transaction/constraint boundary.

The additive Orders migration introduces nullable organization and program IDs plus a parent-pair check and composite foreign keys. Old rows remain unattributed. An attributed create fingerprint includes both IDs; the original no-context fingerprint remains byte-for-byte unchanged so old create keys can replay. No-context receipts retain schema version 1; attributed create/abandon receipts use version 2 and require a valid customer context. The current reader accepts supported historical direct snapshots and version-1 envelopes, while unsupported or inconsistent versions fail closed. Deploy the Customers migration before the Orders attribution migration, then the scoped runtime grants and new pinned OpenFGA model/relations before using the new routes. Mixed-version/rollback operation across newly written version-2 receipts is not qualified.

## Source admission

This slice adds no new framework. Tenant-context resolution, scoped Npgsql transactions, EF migration ownership, and pinned OpenFGA permission checks reuse the already admitted repository patterns. Customer organization/program identity and their business boundaries are application-owned semantics, so importing a generic Party/customer framework or source generator would not settle their meaning. The adapter keeps small parameterized queries next to their operation; Orders' larger queries remain in its embedded SQL resources. Revisit a package or extraction only when a measured workload or a second real consumer earns it.

## Evidence and requalification

| Claim | Falsifiable evidence and recurring guard |
|---|---|
| Bounded names, immutable parent, stable normalized intent and validation | `Application.Customers.Tests` |
| Orders rejects missing/wrong-parent context before persistence and fingerprints attribution | `Application.Orders.Tests` |
| Tenant-owned atomic customer creates, replay/conflict, RLS and wrong-parent exclusion | `Application.Customers.Postgres.Tests` against PostgreSQL 17 |
| Orders additive migration, composite foreign keys, attributed read/list/replay and old receipt compatibility | `Application.Orders.Postgres.Tests` against PostgreSQL 17 |
| Current membership, separate OpenFGA permissions, safe HTTP failures, bounds and no-store | `Application.CoreApi.Tests` real ASP.NET pipeline and OpenFGA container tests |
| Exact restricted runtime rights and ordered migration contribution | provisioning and migration-registry PostgreSQL tests |

Requalify when organization/program identity or lifecycle becomes mutable; legal/account billing meaning is assigned; a new host creates/reads context; OpenFGA relations, pinned model, tenant membership, RLS, keys, receipt versions, cursor format, or database grants change; or mixed-version deployment is required.

## Explicit non-claims

The broader commercial journey selected in `docs/product/PRODUCT_FOUNDATION.md` is still in development. This slice does not deliver draft editing, quotation, order acceptance, fulfillment, invoice/receivable, payment allocation, credit, return/refund, legal debtor/account authority, contacts/addresses, supplier identity, customer self-service, Workstation offline behavior, sync, Worker execution, tuple administration, or a complete Web customer outcome. Those are `NOT_INTRODUCED`, not hidden effects of an attributed draft.
