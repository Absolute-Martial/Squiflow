# URL 058 — The Art of REST API Design: Idempotency, Pagination, and Security

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `058`
- **PDF page:** `302`
- **Source URL:** `https://blog.bytebytego.com/p/the-art-of-rest-api-design-idempotency`
- **Source access:** paid article with a public preview; no subscription controls bypassed.
- **Related supplied visual:** archive page 143, REST API design best-practices cheat sheet.
- **Visual inspected:** PDF page `302` at full size.

## B. Core concept

### SOURCE

The public preview says APIs are long-lived contracts rather than thin access points. It highlights failure, retries, evolution, security, duplicate effects, backwards-compatible change, and syncing data between systems as major sources of pain. It frames good API design as defensive and dependable, primarily discussing REST while also considering some gRPC concepts.

### INFERENCE

The article supports treating SquiFlow APIs as semantic contracts that survive retries and version skew. It does not establish that every SquiFlow boundary should be strict REST, nor that gRPC should replace HTTP; those remain surface-specific decisions.

### EXTERNAL KNOWLEDGE / CAVEAT

REST purity and good API design are not identical. Business commands such as approval, refund, publication, reconciliation, or sync can have explicit task semantics even when they do not look like simple CRUD resources. HTTP method idempotence alone does not provide semantic retry safety. Pagination requires stable ordering and authorization-safe continuation. Authentication method lists also do not decide current authorization.

## C. Important concepts

- API as durable contract;
- semantic command/resource naming;
- idempotency and retries;
- pagination and bounded collections;
- versioning/backwards compatibility;
- stable errors/Problem Details;
- authentication versus resource/business authorization;
- concurrency/preconditions;
- REST and gRPC as complementary boundary choices;
- client/version inventory before retirement;

## D. Diagram / visual explanation

The visual groups core REST principles, API versioning, status codes, resource names, idempotency, pagination/security and JWT structure. For SquiFlow each box belongs to a different contract dimension. Versioning does not replace compatibility testing; JWT structure does not become authorization; a method table does not replace semantic idempotency; pagination must preserve tenant/resource scope and stable ordering.

## E. How it works — step by step

1. Define the business operation/read and its authority first.
2. Choose the surface: task HTTP/REST-style, GraphQL read composition, gRPC candidate, live channel, or durable async.
3. Define request/response contracts and stable error semantics.
4. For mutating retry-sensitive commands, define semantic idempotency and concurrency/precondition rules.
5. Bound collections with stable pagination/filter/sort contracts.
6. Authenticate then derive TenantContext and current authorization separately.
7. Version/evolve additively and keep old-client inventory before breaking retirement.
8. Test network loss, retries, duplicate requests, stale versions, authorization negatives and dependency failure.

## F. Why it matters

SquiFlow has long-lived Workstation clients, future partner integrations, Web/Admin surfaces, durable jobs, and potentially skipped versions. API design therefore has to preserve business semantics under retry, migration and partial failure rather than only produce convenient endpoint names.

## G. Trade-offs / limitations

More explicit contracts, version overlap, pagination, idempotency and stable error taxonomies cost design and test effort. Over-generalizing everything into generic REST resources can hide business intent; overusing task endpoints can reduce discoverability if naming/status semantics are inconsistent.

## H. Alternatives / comparisons — fit, not winner/loser

```text
task-oriented HTTP / REST-style
    -> explicit business commands/resources

GraphQL
    -> complex client-driven read composition

gRPC
    -> measured typed/streaming RPC boundary

WebSocket/SSE/SignalR
    -> live state notification

durable operation/status + Worker
    -> long-running work
```

API-quality properties such as authorization, idempotency, compatibility and boundedness apply across these where relevant.

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** task-oriented HTTP as ordinary application API fit, without strict REST-purity claims.
- **KEEP:** semantic idempotency, expected-version/concurrency, bounded pagination, stable Problem Details/failure codes, TenantContext/OpenFGA/domain authorization.
- **KEEP:** API/sync/message/schema compatibility treated separately from product SemVer.
- **LATER / SCALE TRIGGER:** GraphQL or gRPC only on surfaces where their concrete properties outperform simpler HTTP.
- **IMPROVE NOW:** when implementation exists, generate endpoint inventory/OpenAPI and prove retry/authorization/pagination/version behavior with contract and hostile tests.
- **AVOID:** deriving retry safety from HTTP method alone or putting business authority in JWT/gateway/client input.

**What are we actually doing and why?** SquiFlow uses task HTTP where explicit command/resource semantics, broad tooling and bounded server-owned contracts fit. It keeps GraphQL/gRPC/live/async alternatives open for different surfaces. The choice is driven by the shape of the operation, not by a REST-versus-gRPC comparison.

**What would falsify/change this?** If an ordinary HTTP read surface repeatedly causes costly client composition and unstable projection proliferation, GraphQL/BFF-style composition may earn adoption. If a real sync/service RPC shows measured streaming/binary/generated-contract benefit, gRPC may be better there. Neither change requires abandoning task HTTP elsewhere.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. Why does the source call APIs contracts rather than access points?
2. What failures make idempotency a design property rather than polish?
3. Why do pagination and versioning belong to API reliability?

**Critical reasoning**

1. Which SquiFlow endpoints are better modeled as explicit commands than CRUD resources?
2. How does semantic idempotency differ from GET/PUT/DELETE method semantics?
3. What must a paginated tenant query preserve across pages?
4. How do old Workstations change API evolution strategy?
5. Why can GraphQL or gRPC be adopted without replacing the same authorization rules?

**Trade-off**

1. When is a task endpoint clearer than resource mutation through generic PATCH?
2. When is cursor/keyset pagination worth more complexity than offset pagination?
3. How long should old API versions coexist for external clients versus controlled Workstations?

**Failure / edge**

1. A POST refund times out after commit. What makes retry safe?
2. A page cursor is reused after permissions change. What must be reauthorized?
3. An old client sends a field the server no longer understands. What compatibility policy applies?
4. A live notification says a job is complete but the durable status still says running. Which state wins?

**Implementation**

1. What generated endpoint inventory proves Core API versus Admin API ownership?
2. How are stable errors/failure codes versioned?
3. How are pagination cursors bound to sort/filter/scope?
4. Which contract tests prove same-key/changed-intent behavior?

**System design interview**

1. Design a versioned, retry-safe, tenant-authorized quotation API.
2. Compare task HTTP, GraphQL and gRPC for SquiFlow API surfaces without choosing one universally.

**Challenge**

1. A reviewer says ‘REST says POST is non-idempotent, so retries are impossible.’ Show how a POST business command can be safely retryable while preserving explicit domain intent.
