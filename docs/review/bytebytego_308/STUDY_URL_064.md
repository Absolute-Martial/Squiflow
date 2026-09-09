# URL 064 — A Cheatsheet on REST API Design Best Practices

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `064`
- **PDF page:** `308`
- **Source URL:** `https://blog.bytebytego.com/p/ep161-a-cheatsheet-on-rest-api-design`
- **Source access:** public newsletter section accessible; exact archive-title overlap with archive 033 is still reviewed independently.
- **Related supplied visual:** archive page 143, closely matching REST API design best-practices visual.
- **Visual inspected:** PDF page `308` at full size.

## B. Core concept

### SOURCE

The newsletter recommends resource-oriented paths and proper HTTP verbs, an API versioning approach, standard error codes, idempotent API behavior with idempotency keys for side-effecting operations, pagination for large result sets using offset/cursor/keyset approaches, and production authentication/authorization plus HTTPS. It presents these as practical REST design best practices.

### INFERENCE

The source is useful as a contract checklist, but SquiFlow should preserve explicit business command semantics rather than forcing every operation into CRUD-shaped resource purity. Idempotency, pagination, security and compatibility are end-to-end behaviors whose implementation cannot be inferred from HTTP labels alone.

### EXTERNAL KNOWLEDGE / CAVEAT

The statement that ‘APIs should be idempotent’ is too broad if read literally: some operations are intentionally non-idempotent, while retry-sensitive non-idempotent business effects can be engineered to be idempotent under a semantic key. Repeated requests need the same intended effect, not necessarily an identical HTTP response. Pagination strategy depends on stable ordering and mutation rate. API key, JWT and OAuth mechanisms fit different principals/lifecycles and do not replace TenantContext/OpenFGA/domain authorization.

## C. Important concepts

- resource-oriented naming with explicit command exceptions;
- HTTP method semantics;
- semantic idempotency keys;
- stable errors and Problem Details;
- bounded pagination with stable ordering;
- versioning/evolution;
- authentication versus authorization;
- HTTPS transport security;
- REST/task HTTP coexisting with GraphQL/gRPC/live/async;

## D. Diagram / visual explanation

The visual combines versioning, status codes, resource naming, idempotency, pagination/security and JWT structure. The strongest SquiFlow interpretation is layered: each box solves a different concern. A bearer token does not authorize an object; a version label does not define compatibility; offset pagination does not guarantee stable iteration; and the HTTP method table does not prove retry-safe business effects.

## E. How it works — step by step

1. Define the business operation/read and its current authority.
2. Use resource-oriented HTTP naming where it is clear, and explicit action/command subresources where business intent is clearer.
3. Define stable request/response/error contracts.
4. Apply semantic idempotency and concurrency rules to retry-sensitive commands.
5. Bound list APIs and choose pagination from ordering/mutation/UX requirements.
6. Authenticate via the appropriate identity mechanism, then derive TenantContext and run current authorization/domain rules.
7. Version/evolve contracts additively where possible and inventory old clients before retirement.
8. Test retries, changed-intent keys, pagination mutation, cross-tenant access, version skew and dependency failure.

## F. Why it matters

This is directly relevant to Core/Admin/Web/external API surfaces because predictable contracts reduce client ambiguity and operational support cost. But SquiFlow’s correctness comes from domain semantics and authority, not from strict REST style.

## G. Trade-offs / limitations

Resource consistency and standard HTTP behavior improve tooling and discoverability; explicit command endpoints better express material domain transitions. Offset pagination is simple but can drift under mutation; cursor/keyset approaches can be more stable but require careful token/order design. Version overlap costs maintenance but protects old clients.

## H. Alternatives / comparisons — fit, not winner/loser

```text
resource-oriented HTTP
    -> ordinary entity/resource operations

task/action HTTP
    -> explicit material business transition

GraphQL
    -> flexible complex read composition

gRPC
    -> measured typed/streaming RPC boundary

offset pagination
    -> simple/small/low-mutation lists

cursor/keyset pagination
    -> larger or mutation-sensitive ordered lists
```

Selection is per surface and workload.

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** resource-oriented naming as the default and explicit semantic command/action endpoints for approvals, refunds, publication, reconciliation and similar transitions.
- **KEEP:** semantic idempotency keys, stable Problem Details/failure codes, bounded pagination, HTTPS, and layered authorization.
- **KEEP:** versioning as compatibility/migration/retirement, separate from SemVer and from sync/schema/message versions.
- **NEEDS MEASUREMENT:** offset versus cursor/keyset pagination per concrete list size, ordering, mutation rate and UX.
- **LATER / SCALE TRIGGER:** GraphQL/gRPC/live protocols on surfaces where their specific properties are proven useful.
- **AVOID:** HTTP-method purity as proof of business retry safety, or token/gateway success as proof of tenant/resource authorization.

**What are we actually doing and why?** SquiFlow uses task-oriented HTTP because explicit commands/resources, standard tooling, HTTP status/cache/precondition semantics and external familiarity fit ordinary application surfaces. It keeps GraphQL/gRPC/live/async mechanisms for different problem shapes. The REST comparison does not decide the entire architecture.

**What would falsify/change this?** If a specific read surface becomes difficult to express efficiently with server-owned HTTP projections, GraphQL may be better there. If a synchronous process boundary benefits materially from streaming/generated binary contracts, gRPC may be better there. REST/task HTTP remains where its properties continue to fit.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What REST best practices are listed in the source?
2. What problem do idempotency keys solve for POST-like side effects?
3. What pagination strategies does the source name?

**Critical reasoning**

1. Which SquiFlow operations need explicit action semantics rather than generic CRUD?
2. Why can POST be retry-safe under SquiFlow’s semantic idempotency contract?
3. How does authorization re-run on every paginated page/cursor?
4. Why is API version transport separate from product SemVer?
5. What property would make GraphQL or gRPC better on a different SquiFlow surface?

**Trade-off**

1. When is offset pagination simpler enough and when does mutation make cursor/keyset preferable?
2. How long should an old API contract be supported?
3. When does strict resource naming obscure business intent?

**Failure / edge**

1. An idempotency key is reused with changed intent. What happens?
2. Rows are inserted/deleted between offset pages. What user-visible drift is acceptable?
3. A cursor is copied across tenants. What rejects it?
4. A deprecated API version remains in use by one long-offline client. What migration/upgrade path applies?

**Implementation**

1. How are pagination tokens bound to query/sort/tenant scope?
2. What endpoint inventory proves Core versus Admin ownership?
3. How are stable error codes maintained across versions?
4. Which tests prove authentication, TenantContext, OpenFGA and domain rules are all enforced independently?

**System design interview**

1. Design SquiFlow’s quotation/list/refund HTTP contracts with versioning, pagination and idempotency.
2. Explain how REST/task HTTP can coexist with GraphQL, gRPC and durable async without contradiction.

**Challenge**

1. An API review passes because routes are RESTful and all traffic uses HTTPS, but a guessed Tenant B resource ID is accepted and retrying a refund duplicates the effect. Identify why protocol/style compliance failed to prove API correctness.
