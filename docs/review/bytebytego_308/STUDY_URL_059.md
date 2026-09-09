# URL 059 — How to Learn Backend Development?

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `059`
- **PDF page:** `303`
- **Source URL:** `https://blog.bytebytego.com/p/ep157-how-to-learn-backend-development`
- **Source access:** public newsletter section accessible; exact archive-title overlap with archive 024 is still reviewed independently.
- **Related supplied visual:** archive page 108, closely matching backend-development roadmap.
- **Visual inspected:** PDF page `303` at full size.

## B. Core concept

### SOURCE

The accessible newsletter presents a backend learning roadmap covering fundamentals, several programming languages, SQL/NoSQL/NewSQL databases and ORMs/caching, REST/GraphQL/gRPC/SOAP and authentication, cloud/server hosting, containerization, Nginx/Apache, CI/CD, IaC, and monitoring/observability tools. It is explicitly a map of topics a backend developer should learn.

### INFERENCE

This is a capability-learning map, not a product stack. For SquiFlow, the correct use is to check whether the team understands the consequences of the technologies already chosen and the alternatives likely to be evaluated later.

### EXTERNAL KNOWLEDGE / CAVEAT

Roadmaps often place products side by side without showing lifecycle, failure, cost, or boundary fit. Learning Redis, Kubernetes, Kafka, MongoDB, cloud platforms, GraphQL, or gRPC does not mean SquiFlow should deploy them. Conversely, not using a technology now is not a claim that it is inferior; it may simply solve a problem SquiFlow does not yet have.

## C. Important concepts

- backend fundamentals and client/server/networking;
- programming-language depth;
- relational/NoSQL/distributed database literacy;
- API/protocol literacy;
- identity/authentication knowledge;
- deployment/container/cloud understanding;
- CI/CD and IaC;
- observability and operations;
- roadmap versus architecture decision;

## D. Diagram / visual explanation

The visual puts languages, databases, caching, API styles, security, hosting, containers, CI/CD, IaC and monitoring around ‘Backend Dev’. The architectural danger is to interpret every branch as a layer the product should instantiate. SquiFlow instead uses the diagram as a learning checklist and selects only the components justified by actual runtime/security/data/recovery requirements.

## E. How it works — step by step

1. Identify the backend capability being implemented.
2. Use the roadmap to identify knowledge required to design and operate that capability.
3. Check current SquiFlow foundation and why it was selected.
4. Study alternatives where an open decision or real constraint exists.
5. Run a POC/benchmark/contract test when product evidence is needed.
6. Do not add a product merely to complete the roadmap.
7. Record implementation and operational proof separately from architecture intent.

## F. Why it matters

The current SquiFlow team needs enough backend breadth to reason about APIs, databases, security providers, offline sync, deployment and observability. The roadmap is useful for competence-building, especially because the project is pre-implementation and many future POCs must be run credibly.

## G. Trade-offs / limitations

Over-specializing in one technology can hide alternatives; chasing every roadmap branch can delay the product and multiply operational dependencies. The project needs deep understanding of the current .NET/data/security/deployment path plus targeted breadth for open decisions.

## H. Alternatives / comparisons — fit, not winner/loser

```text
current accepted foundation
    -> C# / modern .NET, ASP.NET Core, modular monolith

current identity/authorization direction
    -> ZITADEL/OIDC + OpenFGA + TenantContext/domain rules

persistence direction
    -> PostgreSQL strongest central reference candidate; SQLite/libSQL local candidates, closed by POC

deployment
    -> owned rack, reproducible definitions/runbooks; exact container/IaC mechanism open

observability
    -> OpenTelemetry/OTLP boundary

other roadmap technologies
    -> study/evaluate only when a concrete SquiFlow problem exists
```

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** the accepted .NET/Avalonia/Blazor/ASP.NET modular-monolith foundation; this roadmap provides no evidence to reopen it.
- **KEEP:** relational central/local-first persistence direction and provider POCs rather than database-type shopping.
- **KEEP:** ZITADEL/OpenFGA and OTel boundaries for the actual identity/authorization/observability responsibilities.
- **LATER / SCALE TRIGGER:** Redis/Memcached, NoSQL/NewSQL additions, Kafka, Kubernetes, cloud platform services, GraphQL/gRPC deployment only for specific workloads.
- **IMPROVE NOW:** use the roadmap to plan implementation competence and POCs, not to create empty infrastructure/projects.
- **AVOID:** stack-by-roadmap or assuming a popular backend product is required because it appears in a learning diagram.

**What are we actually doing and why?** SquiFlow uses C#/.NET and a modular monolith because that is the accepted application foundation for the current small-team product, while security, persistence, sync and deployment choices are tied to real responsibilities. Other backend technologies remain legitimate options for future boundaries, not rejected competitors.

**What would falsify/change this?** If a new requirement—such as replayable event streams, a specialized search workload, independently scalable service, or customer-mandated cloud environment—cannot be satisfied reasonably by the current stack, the relevant roadmap branch becomes an active evaluation rather than a learning-only topic.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What categories does the backend roadmap cover?
2. Why is a roadmap different from an architecture stack?
3. Why should a backend engineer understand technologies the product does not currently use?

**Critical reasoning**

1. Which roadmap items are current SquiFlow dependencies, candidates, or only background knowledge?
2. Why does current use of .NET not mean Go/Python/Rust are inferior?
3. What real workload would justify adding Redis or a NoSQL store?
4. What product boundary could justify another runtime without replacing the whole stack?
5. Which current open decisions genuinely need POCs rather than roadmap popularity?

**Trade-off**

1. How much technology breadth should a small team operate at once?
2. When is a specialized service/runtime worth polyglot complexity?
3. When can a managed cloud service reduce burden enough to justify external dependency/cost?

**Failure / edge**

1. A developer adds Redis because the roadmap lists caching but no measured slow query exists. What is wrong?
2. A cloud-managed service goes down while the owned rack is healthy. Which dependency/recovery questions matter?
3. A new language is introduced for one trivial Worker task. What operational cost appears?

**Implementation**

1. Which Phase-0/Phase-1 POCs should prove the accepted stack?
2. How should provider SDK types be isolated from domain code?
3. Which performance/security tests should accompany a technology evaluation?
4. How is an ‘open’ technology decision prevented from silently becoming implementation?

**System design interview**

1. Use the roadmap to identify knowledge needed for SquiFlow while keeping the deployed stack minimal.
2. Explain how you would decide whether to add a new database or cache technology.

**Challenge**

1. Design a backend-learning plan for the SquiFlow team that improves architecture quality without turning every learned technology into a dependency.
