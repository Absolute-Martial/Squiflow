# Multi-Tenancy Isolation Strategy

**Version:** v0.0.17

**Current implementation:** the global tenant registry, account-membership schema/query and membership-derived immutable `TenantContext` exist. These are security/control data, not tenant-owned business rows. Client-selected tenant execution, tenant-owned persistence and PostgreSQL RLS remain `NOT_INTRODUCED`.

## 1. Isolation is a spectrum, not one permanent topology

SquiFlow treats tenant isolation as several independent axes rather than one binary architecture choice.

The two source models reviewed for this decision use different vocabulary but point to the same conclusion:

- the supplied Edward Grundy article describes shared-table, schema-per-tenant, database-per-tenant and fully separate-infrastructure patterns;
- the AWS SaaS isolation whitepaper describes **pool**, **silo**, **bridge** and **tier-based** isolation and explicitly separates tenant isolation from ordinary authentication/RBAC.

SquiFlow therefore does not assume that all tenants must always use one physical isolation pattern. Composition variation is another independent axis: a Tenant Application Profile may select capabilities and, where earned, trusted implementation variants without implying a different data or process placement. `TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md` owns that model.

## 2. Current v0.0.15 baseline

For ordinary small and medium tenants, the implementation target is a **pooled application + pooled authoritative data model**:

```text
shared Web/API/Worker compute
        ↓
authoritative TenantContext
        ↓
shared central relational model
with TenantId on tenant-owned records
        ↓
defense-in-depth isolation at persistence boundary
```

This is the baseline because it minimizes provisioning, migration, backup, deployment and monitoring complexity while SquiFlow is still proving the product and workload.

PostgreSQL is selected as the initial central transactional database. The pooled tenant baseline still requires Phase-3 isolation, runtime-role, connection-pool, workload, and recovery qualification before production use.

## 3. Tenant identity and tenant isolation are different

Authentication answers who the actor is.

Application authorization answers what the actor may do.

Tenant isolation answers which tenant resources can be reached at all.

These must not collapse into one check.

A request flow is conceptually:

```text
OIDC authentication
→ resolve SquiFlow account
→ resolve authoritative tenant membership/context
→ create immutable request TenantContext
→ tenant-scoped resource access
→ resource/action authorization
→ domain/workflow validation
→ transaction
```

A client-provided TenantId, hostname, route value, local Workstation value or stale token claim is never by itself authoritative tenant scope.

## 4. TenantContext must be difficult to bypass

The AWS whitepaper explicitly warns against leaving isolation to individual service developers. SquiFlow adapts that by making tenant context a shared application/persistence boundary rather than relying on every query author to remember a filter.

The request/application layer should expose a typed immutable tenant execution context, for example conceptually:

```text
TenantContext
- TenantId
- ActorId
- Membership/authorization revision
- optional Branch/Program scope
- correlation/audit context
```

Repositories/query services for tenant-owned data require this context or receive an already tenant-scoped data access object.

Avoid general-purpose repository APIs such as:

```text
FindOrder(orderId)
GetAllCustomers()
```

when the correct contract is tenant-scoped, for example:

```text
FindOrder(tenantContext, orderId)
QueryCustomers(tenantContext, ...)
```

The goal is that an omitted tenant filter is harder to express accidentally.

## 5. Pooled relational storage requirements

Every tenant-owned authoritative table in the pooled model must deliberately classify its tenancy:

- tenant-owned: requires `TenantId`/equivalent partition key;
- platform-global: deliberately not tenant-owned;
- shared reference: explicitly global and read-only/controlled;
- cross-tenant platform operation: restricted to Platform Admin/control-plane code paths.

Do not allow ambiguous tables whose tenancy is inferred from distant joins without a documented reason.

Tenant-aware indexes and uniqueness constraints must include tenant scope when uniqueness is tenant-local.

Example conceptually:

```text
UNIQUE (TenantId, ExternalNumber)
INDEX  (TenantId, UpdatedAt)
INDEX  (TenantId, Status, CreatedAt)
```

Global identifiers can still be globally unique, but global uniqueness must not replace tenant scoping.

## 6. Selected PostgreSQL adapter: RLS is a defense-in-depth proof requirement

The selected PostgreSQL adapter's Phase-3 POC must prove PostgreSQL Row-Level Security for tenant-owned pooled tables.

The intent is:

```text
application tenant scope
+
resource authorization
+
database RLS boundary
```

not RLS instead of application authorization.

Important PostgreSQL-specific requirements:

- application runtime credentials must not be superuser or `BYPASSRLS`;
- table-owner bypass behavior must be addressed; the runtime role should not silently own tenant tables, or `FORCE ROW LEVEL SECURITY` must be deliberately evaluated where appropriate;
- policies must protect both reads and writes (`USING` / `WITH CHECK` semantics as applicable);
- missing policy/configuration should fail closed in tests;
- maintenance/migration/backup roles that legitimately bypass RLS are separate privileged infrastructure/control-plane identities, never ordinary API/Worker credentials.

If tenant context is passed to PostgreSQL through a custom setting, it must be scoped safely to the transaction. A pooled connection must never retain Tenant A's context and accidentally serve Tenant B.

A reference POC therefore must prove the equivalent of transaction-local context setting/reset and deliberate connection-pool reuse tests before RLS is accepted as implemented correctly.

## 7. RLS does not close the central database decision

RLS is a strong PostgreSQL mechanism, but provider-specific code remains contained outside domain/business models. Selecting PostgreSQL does not justify leaking Npgsql/EF/provider types through module contracts or bypassing application TenantContext and authorization.

Any selected central provider must prove an equivalent isolation story appropriate to that provider:

- native row/partition policy where available;
- strongly scoped data-access boundary where native row security is unavailable;
- cross-tenant negative tests;
- write-path isolation, not read-only filtering;
- migration, backup and administrative behavior;
- clear privileged bypass identities.

A candidate that makes safe pooled isolation too fragile may be rejected even if its ordinary query performance is good.

## 8. Why schema-per-tenant is not the baseline

Separate schemas reduce one class of missing-filter mistakes but still share the same database process/resources and multiply migration/version-management work.

SquiFlow currently has no requirement for per-tenant table definitions. Tenant variation is represented through the Tenant Application Profile: capability selection, configuration, dynamic forms/fields, rules, workflows, integrations and optional trusted implementation variants. Core identity, lifecycle, money, stock, security and issued-business truth remain explicit relational structures; supplementary tenant information may use versioned typed extension schemas with validated JSONB/document values and deliberate indexes/projections. This avoids a column or table per tenant without turning protected core meaning into an ungoverned property bag.

Therefore schema-per-tenant is **not** a current implementation target.

Do not create tenant-specific schemas merely to feel more isolated.

Revisit only if a concrete database/provider/compliance requirement makes it materially better than pooled tables or separate databases.

## 9. Dedicated database profile — future isolation step

A tenant may later require dedicated authoritative storage because of:

- data residency;
- contractual isolation;
- compliance;
- workload/noisy-neighbor pressure;
- enterprise backup/restore/RPO/RTO requirements;
- customer-managed hosting.

That profile can keep shared SquiFlow Web/API/Worker compute while routing the tenant to a dedicated database/storage boundary.

Conceptually:

```text
TenantContext
→ TenantPlacementRegistry
→ pooled datastore OR dedicated datastore
```

This routing layer should be introduced only when the first real dedicated-data tenant exists. Do not build a pool-per-tenant connection manager now.

The important current requirement is that domain/application code does not assume one hard-coded physical database location.

## 10. Dedicated stack profile — future enterprise/customer-managed option

Some future enterprise customers may require a fully dedicated deployment:

```text
Web/API/Worker
Database
Object storage
private network/runtime
```

for that tenant.

This is a **silo** profile, not a fork of the product.

Where possible, dedicated tenants run the same SquiFlow release/contracts as pooled tenants so operations, support and upgrades do not become a collection of custom products.

Customer-managed enterprise hosting remains compatible with this direction.

## 11. Bridge isolation is allowed without microservices

AWS's bridge model shows that some resources can be pooled while others are siloed. SquiFlow adopts the principle but not the implication that resources must become microservices.

Examples that may later be justified:

```text
shared API + dedicated tenant database
shared API + dedicated object bucket
shared Worker fleet + dedicated high-cost workload worker
shared identity/control plane + dedicated tenant stack
```

Isolation is a resource/deployment property, not a reason to split every business module into a network service.

## 12. Processing isolation is independent from data isolation

A pooled database does not require completely unbounded pooled processing.

The Worker baseline uses one durable tenant-aware work system with:

- TenantId on every tenant-owned job;
- bounded global concurrency;
- bounded per-tenant concurrency where needed;
- fair scheduling/aging;
- workload classes/priorities;
- queue-age and per-tenant backlog metrics;
- admission limits so one tenant cannot consume the whole Worker or provider budget.

Do **not** create one physical queue and one worker pool per tenant initially. That reproduces the operational scaling problem of silo infrastructure without evidence that it is needed.

A dedicated worker pool/queue partition can be introduced for a tenant/tier only when SLA, compliance, expensive workload or noisy-neighbor evidence justifies it.

Profile-specific dependency composition does not control CPU time, allocations, thread-pool use, database connections, provider consumption or process-fatal faults. Per-tenant admission, concurrency, queue fairness, budgets and observability still apply in a shared process. When a trusted tenant extension or workload needs a stronger blast-radius boundary, SquiFlow may place it in a separate Worker or OS process with explicit contracts, limits and recovery. That escalation does not require Kubernetes or a separate full application stack per tenant.

## 13. Tenant placement is platform control-plane state

If SquiFlow later supports multiple isolation profiles, tenant placement is not editable from the Workstation and is not an arbitrary tenant-owner setting.

Conceptual platform-managed state:

```text
TenantPlacement
- TenantId
- IsolationProfile
- DataPlacementId
- ObjectStoragePlacementId
- ProcessingClass
- Region/Residency policy
- ActiveConfigurationVersion
```

Changes are Platform Admin/control-plane workflows with validation, migration/cutover planning, verification and rollback/recovery evidence.

## 14. Migration direction and triggers

The normal evolution path is toward more isolation only when evidence requires it:

```text
Pooled
→ targeted dedicated resource / dedicated database
→ dedicated stack
```

A move is triggered by a recorded requirement such as:

- regulatory/residency rule;
- contractual dedicated-isolation requirement;
- repeated noisy-neighbor breach of SLO;
- tenant scale dominating shared-resource budgets;
- enterprise customer-managed deployment requirement;
- unacceptable cross-tenant blast-radius risk for a resource.

"We may need it eventually" is not a migration trigger.

## 15. Isolation tests are mandatory

The pooled baseline is incomplete until automated tests prove at least:

- Tenant A cannot read Tenant B by guessed IDs;
- Tenant A cannot update/delete Tenant B;
- list/search/report/export cannot leak Tenant B;
- background jobs cannot change Tenant B after context mix-up;
- object-storage paths/metadata cannot cross tenants;
- Web custom-domain routing cannot manufacture another TenantContext;
- Workstation-supplied TenantId is ignored/revalidated server-side;
- cached/read-model data cannot cross tenant boundaries;
- admin/support cross-tenant access requires explicit platform authority and audit;
- connection reuse cannot preserve a previous tenant's database isolation context;
- backup/restore/migration tooling operates with deliberate privileged identity and does not accidentally omit or mix tenant data.

The PostgreSQL RLS POC also attacks runtime-role/table-owner/BYPASSRLS behavior and verifies write-side policy checks.

## 16. What is explicitly not being built now

- no schema per tenant baseline;
- no database per tenant baseline;
- no deployment per tenant baseline;
- no physical queue per tenant baseline;
- no separate isolation microservice;
- no per-tenant cloud account/VPC machinery;
- no dynamic tenant-database connection pools before a dedicated-data requirement exists;
- no tenant-specific product forks.

The current goal is a pooled model that is difficult to misuse and has a documented path toward targeted/dedicated isolation when real requirements appear.

## Source notes

This decision incorporates the supplied Edward Grundy article's spectrum framing and its separation of storage versus processing isolation. The AWS SaaS Tenant Isolation Strategies whitepaper contributes the pool/silo/bridge/tier vocabulary, the distinction between RBAC and tenant isolation, the recommendation to keep isolation mechanics away from individual developer convention, and the use of PostgreSQL RLS as an example pooled-storage isolation mechanism.
