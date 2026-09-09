# ByteByteGo Exhaustive Sequential Study — URL Entries 011-020

**Coverage:** PDF pages `255-264`, URL occurrences `011-020`. Every page was rendered and visually inspected. Public URLs were checked directly; paid content was not bypassed. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` remain separated.

Detailed studies:

- `STUDY_URL_011.md` — Database Schema Design Simplified: Normalization vs Denormalization
- `STUDY_URL_012.md` — Database Indexing Demystified: Index Types and Use-Cases
- `STUDY_URL_013.md` — Top 5 Common Ways to Improve API Performance
- `STUDY_URL_014.md` — What is the SOLID Principle?
- `STUDY_URL_015.md` — A Guide to Rate Limiting Strategies
- `STUDY_URL_016.md` — GraphQL 101: API Approach Beyond REST
- `STUDY_URL_017.md` — API Gateways 101: The Core of Modern API Management & Security
- `STUDY_URL_018.md` — Top Service-to-Service Communication Patterns
- `STUDY_URL_019.md` — Top Strategies to Share Data Between Services
- `STUDY_URL_020.md` — How to Design Good APIs

## Checkpoint — strongest architecture findings

1. **Normalization and denormalization are complementary representations, not competitors.** SquiFlow keeps transactional authority normalized for integrity/evolution and may add denormalized read models only for measured read paths with explicit freshness/rebuild/tenant authorization.
2. **Indexes are query/invariant-specific.** Every index must earn its write/WAL/storage/rebuild cost under reconnect/import workloads and real tenant cardinality.
3. **API performance techniques solve different bottlenecks.** Pagination, bounded async telemetry, caching, compression and connection pooling may coexist, but none is a mandatory stack.
4. **SOLID is a review lens, not interface-count architecture.** Real provider/replacement seams get narrow abstractions and contract tests; ordinary business code stays concrete/cohesive where that is simpler.
5. **Rate limiting protects concrete finite resources.** It remains separate from authorization and durable quota/usage accounting; edge-only RPS limits cannot protect Worker/provider/DB capacity by themselves.
6. **GraphQL is treated as a positive, surface-specific option rather than the loser/winner in REST comparisons.** Task-oriented HTTP remains useful for explicit commands/resources; GraphQL becomes a strong candidate for tenant Web/Admin read composition if actual screens prove nested client-driven queries reduce round trips/maintenance enough to justify query-cost, N+1, authorization, cache and schema-governance complexity. Federation is a separate decision.
7. **API gateway capability is justified by real edge needs, not microservice fashion.** TLS/custom-domain routing/exposure/size/rate controls are concrete; backend application authority remains independent.
8. **Service communication begins by challenging the service boundary.** Same-host modules remain in-process; true synchronous process boundaries evaluate HTTP/gRPC; after-commit/long-running consequences use durable Worker/outbox.
9. **Data ownership is stronger than database-per-service dogma.** Core/Admin/Worker may share the central DB because they are runtime hosts of one modular monolith. A future independent service gets explicit authoritative data ownership and stable APIs/events/read models rather than shared private-table mutation.
10. **Good API design is semantic, not method-label purity.** HTTP method idempotence is not proof of backend retry safety; semantic idempotency, authorization, concurrency, bounded pagination and compatibility remain explicit contracts.

## Critical what/why map

```text
central business authority
    -> normalized relational model
    -> because constraints/transaction clarity protect current invariants

measured dashboard/report bottleneck
    -> projection/materialized/denormalized read shape candidate
    -> because read composition may deserve a separate reconstructable representation

ordinary explicit commands/resources
    -> task-oriented HTTP
    -> because business intent, status/idempotency/tooling/external compatibility are clear

complex client-driven Web/Admin reads
    -> GraphQL candidate
    -> because selected/nested fields can reduce round trips/client composition

real synchronous process boundary
    -> HTTP/gRPC selected from workload evidence

after-commit/long-running consequence
    -> durable Worker/outbox

public edge
    -> reverse-proxy/API-gateway capability
    -> because TLS/routing/limits/exposure are real deployment responsibilities

current Core/Admin/Worker persistence
    -> shared central DB with module ownership
    -> because these hosts are one modular-monolith authority, not independent microservices
```

## Material architecture decision check

No owner-architecture change is justified by this batch. In particular, it does not newly select GraphQL, GraphQL Federation, Redis, a new database/index type, denormalized authority, a heavyweight API-management platform, a service mesh/broker, database-per-service, or a universal service transport. It strengthens adoption gates and records where these technologies could be beneficial.

`LAST FULLY COMPLETED PDF PAGE: 264`

`LAST COMPLETED ARTICLE: URL 020 — How to Design Good APIs`

`NEXT PDF PAGE: 265`

`NEXT ARTICLE: URL 021 — What is a REST API?`

`COVERAGE STATUS: 264 / 308 pages sequentially completed`
