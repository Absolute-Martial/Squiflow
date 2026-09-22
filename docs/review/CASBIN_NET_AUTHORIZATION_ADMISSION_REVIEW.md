# Casbin.NET Authorization Admission Review

**Reviewed:** 2026-09-22  
**Product version:** v0.1.0  
**Status:** Source-backed replacement-candidate review. No Casbin runtime, policy store, authorization-management surface, or protected business operation is implemented.

## 1. Decision boundary

The current focused owner, `docs/security/TENANT_PERMISSIONS.md`, selects OpenFGA for application roles, permissions, and resource relationships. Casbin.NET therefore cannot be added as an unrelated helper. It would replace part or all of that selected authorization engine.

Running Casbin and OpenFGA as parallel authorities is rejected. It would create two policy languages, two mutation paths, two freshness models, and an unsafe question whenever they disagree.

This review admits Casbin.NET as a **POC-gated replacement candidate**. OpenFGA is now the selected and narrowly introduced engine for the tenant-workspace read. Casbin would have to replace that provider boundary and reproduce its current evidence plus the broader proof below; it must not run as a parallel authority.

## 2. Product workload being tested

The candidate must cover the actual application contract rather than a generic RBAC demo:

- stable module-owned permission IDs;
- tenant-created role instances composed from supported permissions;
- multiple tenants and memberships per account;
- tenant, branch/location, program/project, own, and assigned scopes where a real capability requires them;
- feature availability independent from authorization;
- delegation ceilings and Owner-lockout protection;
- current server authorization for Web, API, Worker, and synchronized Workstation operations;
- versioned offline permission snapshots for UX, never local security authority;
- fail-closed behavior during policy-load, persistence, invalidation, and evaluation failure;
- durable, auditable, idempotent role and grant changes;
- bounded memory and initialization for many mostly inactive tenants;
- correct revocation and policy change behavior across multiple application replicas.

Tenant administrators may compose trusted permission definitions into roles. They do not upload Casbin model files, expressions, functions, executable scripts, or arbitrary policy semantics.

## 3. Source inspected

| Source | Pinned revision | Relevant evidence |
|---|---|---|
| Casbin.NET | `30b142f0f5c4598852e8258d638bded3e24caf2c` | enforcer/model/policy store, tenant-domain RBAC, resource roles, filtered policy loading, cache invalidation hooks, watcher contracts, concurrency tests |
| Casbin EF Core adapter | `1cc2c9ae985e48a93c38d1b884095502d15d52f8` | PostgreSQL-capable persistence, SQL-applied filtered loads, policy schema, AutoSave behavior, multi-context transaction behavior, unit/integration tests |
| OpenFGA .NET SDK | `ec8ee04761b41e2400693b911a17463877e500c3` | explicit store/model IDs, relationship APIs, consistency selection, retry/auth/client behavior |

The retained source-only snapshots and licenses are listed in `reference-sources/SOURCES.md`. They contain no upstream Git history and are not product dependencies.

### Disposable compatibility observation

On 2026-09-22, a disposable project outside the repository verified that released `Casbin.NET` `2.21.3` restores and runs on the repository's .NET `10.0.401` SDK. The scenario exercised one tenant-created role, tenant-domain isolation, denial of an unsupported permission, and local revocation. A second disposable `net10.0` project compiled `Casbin.NET.Adapter.EFCore` `2.12.0` with `Microsoft.EntityFrameworkCore` `10.0.8` with zero build warnings or errors.

This is feasibility evidence only. It is not a repository regression guard and does not qualify persistence, replica freshness, policy-model rollout, performance, or production failure behavior.

## 4. What Casbin.NET provides

Casbin.NET is an embedded .NET policy evaluator. It supports configurable ACL/RBAC/ABAC models, tenant/domain-aware RBAC, role inheritance, resource roles, allow/deny effects, policy management APIs, filtered policy loading, and watcher interfaces.

That gives this application three concrete possible advantages:

1. Permission checks execute in the application process without an authorization-service network call.
2. Policy can live in PostgreSQL beside other application state, making one local transaction possible if the persistence boundary is deliberately designed for it.
3. Active-tenant policy snapshots can be bounded, constructed on demand, retired, and rebuilt using the same process-local runtime discipline already proven for application profiles.

Casbin does not own authentication, tenant membership, business workflow validity, database isolation, feature entitlement, audit meaning, or authoritative role metadata. Those remain application responsibilities.

## 5. Material costs and hazards

### 5.1 Policy is process-local after loading

An enforcer evaluates an in-memory model and policy snapshot. Persisting a change does not by itself prove that every replica has stopped using the old snapshot. Casbin supplies watcher contracts and an ecosystem of watcher/dispatcher implementations, but the core library does not provide this application's durable multi-replica freshness guarantee.

A lossy notification alone is insufficient for revocation. Any accepted design needs a durable authorization revision and a recovery/reconciliation path that detects missed invalidation after restart, disconnect, or replica partition.

### 5.2 Fail-open switch

The inspected enforcer returns `true` when enforcement is disabled. Product code must never expose that raw behavior as a production bypass. A narrow application adapter must fail startup for an invalid model, fail closed for unavailable/invalid policy state, and make an explicit deny/unavailable distinction for telemetry and support without granting access.

### 5.3 Generic adapter schema

The EF Core adapter stores policy using `ptype` plus generic `v0...v13` fields. That is a valid Casbin persistence format, but it does not automatically provide application-owned role names, permission-catalog compatibility, delegation validation, authorization revision, lifecycle state, audit evidence, or migration intent.

The adapter can apply a tenant filter at the database query and supports PostgreSQL. Its documented atomic `SavePolicy` behavior concerns Casbin policy persistence using the required connection arrangement; it does not prove an atomic transaction with arbitrary application audit/role tables. AutoSave also persists individual mutations separately.

The POC must decide between:

- using the generic adapter behind application-owned durable change records and a proven transaction/invalidation protocol; or
- implementing a narrow adapter/projection from structured application authorization tables into immutable Casbin evaluation snapshots.

Neither choice is accepted by this review alone.

### 5.4 Model flexibility can become ungoverned behavior

Casbin's configurable matcher language is useful to application developers. It must not become a tenant scripting surface. The application owns and versions the model, allowed functions, permission vocabulary, and mapping from validated business facts. Tenant-owned policy remains bounded data.

### 5.5 Relationship breadth

Casbin can express tenant-domain RBAC, resource roles, ABAC, and relationship-style checks. OpenFGA supplies a purpose-built relationship graph, tuple APIs, object/user listing, immutable authorization-model versions, and explicit consistency modes. Casbin only wins if the application's measured relationship shapes remain understandable, bounded, queryable, and testable without rebuilding those OpenFGA capabilities.

## 6. Comparison for this application

| Concern | Casbin.NET | OpenFGA | Current implication |
|---|---|---|---|
| Execution | embedded in each .NET process | external authorization service | Casbin removes request-network latency and service availability from checks |
| Primary fit | policy evaluation, RBAC/ABAC/domain models | fine-grained relationship authorization | actual capability resource graphs decide the fit |
| Tenant custom roles | policy/grouping data | role objects and tuples | both can support the bounded role requirement |
| Policy persistence | adapter/application-owned | OpenFGA store | Casbin can share PostgreSQL, but the application owns more correctness |
| Model versioning | application must define and persist it | immutable authorization-model IDs | Casbin needs an explicit equivalent before production use |
| Replica freshness | watcher/dispatcher plus application recovery | service consistency modes | Casbin needs durable revision and missed-event recovery proof |
| Atomicity with application state | potentially one PostgreSQL transaction | separate-system reconciliation | Casbin may simplify changes if one transaction is actually proven |
| Memory | policy snapshots live in each process | mainly service-side | Casbin needs tenant-cardinality, load, eviction, and GC measurements |
| Relationship queries | model/API dependent; application may need indexes/projections | Check, Batch Check, List Objects/Users, Expand | do not recreate a second relationship service accidentally |
| Offline Workstation | snapshot may aid UX only | snapshot may aid UX only | neither becomes offline authority |
| Operational burden | in-process lifecycle, storage, invalidation, rollout | deploy/operate external service and reconcile writes | compare total burden, not package count |

## 7. Required POC and acceptance evidence

Casbin can supersede OpenFGA only if one focused POC proves all of the following with production-shaped tests:

1. **Model correctness:** Owner, Staff, one tenant-defined role, multiple roles, delegation ceiling, feature-disabled permission, own/assigned scope, and one real resource relationship use stable application permission IDs.
2. **Tenant isolation:** concurrent cross-tenant substitutions cannot obtain a policy, resource, cache entry, or decision from another tenant.
3. **Durability:** role/grant changes have one semantic operation identity, durable audit evidence, and a known committed outcome after crash/retry.
4. **Replica freshness:** at least two API processes observe grants and revocations within a declared bound; a killed notification path and a restarted/stale replica recover without accidental allow.
5. **Model rollout:** the policy model has an immutable application version/fingerprint, old/new compatibility rules, rollback behavior, and tests that prevent silently loading incompatible policy.
6. **Resource bounds:** measured cold load, filtered tenant load, steady-state check latency, memory per active tenant, maximum active enforcers, idle retirement, and rebuild behavior fit declared limits.
7. **Failure behavior:** missing/invalid model, unavailable PostgreSQL, malformed policy, failed invalidation, and internal evaluation errors deny or return an explicit unavailable result; the raw disabled-enforcement allow path is unreachable.
8. **Query needs:** required permission-aware resource listing is efficient without loading an unbounded result set or duplicating a home-grown relationship graph.
9. **.NET 10 compatibility:** released package versions restore, build, and pass the repository's tests under the current toolchain.
10. **Provider containment:** Casbin types remain inside the authorization adapter; capability/domain contracts use application-owned permission and decision types.

The comparison should run an equivalent OpenFGA model and include network/service operation in its measured and failure-tested cost. A microbenchmark of `Enforce()` alone cannot close the decision.

## 8. Current decision

- Do not add Casbin.NET and OpenFGA together.
- Do not add a Casbin package to production projects before the POC has a real protected capability operation.
- Retain Casbin.NET and its EF Core adapter as source-only `poc-gated` candidates.
- Keep the current OpenFGA selection until a POC produces the evidence required for a deliberate focused-owner and current-decision update.
- If Casbin wins, update `TENANT_PERMISSIONS.md`, active Phase-1 authorization plans, current decisions, implementation truth, deployment/recovery requirements, and tests in the same decision change. Record OpenFGA as superseded rather than rewriting its historical reviews.

## 9. Revisit triggers

Prefer Casbin when production-shaped evidence shows that tenant-domain RBAC plus bounded application attributes cover the real workload, same-PostgreSQL atomicity materially simplifies authorization changes, and replica freshness can be guaranteed with less total operational burden.

Keep OpenFGA when real capabilities require deep or changing object relationships, permission-aware object/user listing, centrally consistent checks across many replicas or languages, or when the Casbin solution starts recreating a relationship service, dispatcher, policy query engine, and model-management platform around an embedded library.
