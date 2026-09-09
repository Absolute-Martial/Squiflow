# URL 061 — How to Learn API Development

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `061`
- **PDF page:** `305`
- **Source URL:** `https://blog.bytebytego.com/p/ep158-how-to-learn-api-development`
- **Source access:** public newsletter section accessible; exact archive-title overlap with archive 026 is still reviewed independently.
- **Related supplied visual:** archive page 116, closely matching API-development roadmap.
- **Visual inspected:** PDF page `305` at full size.

## B. Core concept

### SOURCE

The newsletter provides an API-development learning map: API fundamentals and styles such as REST, SOAP, GraphQL and gRPC; HTTP methods/status/headers; authentication mechanisms including JWT, OAuth 2, API keys and Basic Auth; REST principles, versioning, pagination and documentation tooling; API testing; and deployment/integration with third-party APIs and gateways.

### INFERENCE

The roadmap says what an API engineer should understand, not which style, authentication mechanism, testing product, gateway, or provider SquiFlow should standardize on. Its value is coverage: each API surface should deliberately answer contract, security, compatibility, failure, testing and operational questions.

### EXTERNAL KNOWLEDGE / CAVEAT

JWT, OAuth 2, API keys and Basic Auth are not equivalent authentication ‘products’ for one slot; principal type and credential lifecycle matter. REST, GraphQL and gRPC can coexist. OpenAPI/Postman/Swagger describe or exercise APIs but do not prove authorization, retry, recovery or tenant isolation. API gateways provide edge capabilities and do not become business authority.

## C. Important concepts

- API styles/protocols by boundary;
- HTTP mechanics;
- authentication versus authorization;
- versioning and pagination;
- documentation/inventory;
- contract/hostile/integration testing;
- third-party API failure/idempotency;
- gateway/edge versus backend authority;
- learning map versus architecture selection;

## D. Diagram / visual explanation

The visual branches from API fundamentals to request/response mechanics, authentication/security, design/development, testing, deployment/integration, third-party APIs and gateways. For SquiFlow the same map becomes a completeness checklist: a new endpoint is not complete merely because Postman can call it; it must also have tenant/resource authorization, idempotency/concurrency where relevant, bounds, stable errors, compatibility and failure tests.

## E. How it works — step by step

1. Identify the principal/client and business operation.
2. Choose API style/transport from the actual boundary and interaction shape.
3. Define HTTP/RPC request-response semantics and stable errors.
4. Select authentication according to principal/lifecycle, then apply current SquiFlow authorization separately.
5. Define versioning/pagination/idempotency/concurrency for the operation.
6. Generate documentation/inventory where useful.
7. Test positive, hostile, retry, version-skew, provider-failure and cross-tenant paths.
8. Operate through the real edge/deployment rather than relying only on local API-tool success.

## F. Why it matters

SquiFlow exposes several API-like surfaces and external provider integrations. A disciplined API-development skill set helps keep those boundaries explicit and testable while avoiding the opposite error of standardizing one protocol/auth mechanism everywhere.

## G. Trade-offs / limitations

A broad API toolchain improves discoverability and testing but can create duplicate specifications and tool dependence. Supporting several protocols increases operational surface. Over-standardizing one API style can force awkward client/server behavior. The selection should remain per surface.

## H. Alternatives / comparisons — fit, not winner/loser

```text
REST/task HTTP
    -> ordinary explicit commands/resources

GraphQL
    -> flexible read composition candidate

gRPC
    -> Workstation/real-service RPC candidate

OIDC/OAuth through ZITADEL
    -> interactive human identity

API key / client credentials / mTLS
    -> future machine integration depending on lifecycle

OpenAPI/tooling
    -> contract inventory/documentation

contract + hostile + provider + recovery tests
    -> correctness proof
```

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** the boundary-specific API/transport method rather than a universal REST/GraphQL/gRPC decision.
- **KEEP:** ZITADEL/OIDC for human identity and TenantContext/OpenFGA/domain rules for application authorization.
- **KEEP:** stable Problem Details/failure codes, semantic idempotency, bounded collections and compatibility as API-contract concerns.
- **IMPROVE NOW:** when implementation exists, generate endpoint inventory/OpenAPI where appropriate and add contract/hostile/retry/version/provider tests beyond manual Postman/cURL checks.
- **LATER / SCALE TRIGGER:** API gateway/API-management features, GraphQL, gRPC or machine credential types only when a concrete client/integration requirement exists.
- **AVOID:** selecting a protocol/authentication mechanism or gateway simply because it appears in an API-learning roadmap.

**What are we actually doing and why?** SquiFlow learns and supports several API styles because different clients and boundaries have different needs. We currently use task HTTP where it fits and ZITADEL/OIDC for human identity because those solve concrete requirements; alternatives remain open for other surfaces.

**What would falsify/change this?** If client usage proves a different protocol or authentication lifecycle is better for a specific surface, that surface can change without forcing the whole system to change. If documentation tooling diverges from the running endpoint inventory, generated/verified sources should replace hand-maintained claims.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What API topics does the roadmap say developers should learn?
2. Why are authentication and API authorization separate concerns?
3. What does API documentation prove—and what does it not prove?

**Critical reasoning**

1. Which SquiFlow APIs should remain task HTTP and where might GraphQL or gRPC fit better?
2. How does principal type determine OIDC, API key, client credentials or mTLS choice?
3. Why is Postman success insufficient evidence for tenant security?
4. What contract properties must survive an old Workstation retry?
5. What gateway responsibilities remain outside business authority?

**Trade-off**

1. When is supporting a second API protocol worth operational complexity?
2. When is generated OpenAPI more valuable than handwritten documentation?
3. When can API-key simplicity outweigh OAuth lifecycle features for a machine integration?

**Failure / edge**

1. Third-party API times out after accepting a side effect. What API design handles ambiguity?
2. A pagination token is reused by a user whose permission changed. What happens?
3. Gateway says authentication succeeded but OpenFGA is down. What is the backend behavior?
4. An old client sends an unsupported protocol version. How should failure be surfaced?

**Implementation**

1. What automated inventory proves Core versus Admin route ownership?
2. How are API contract tests tied to stable failure codes?
3. Which hostile tests cover mass assignment and excessive field exposure?
4. How are provider retries budgeted and made idempotent?

**System design interview**

1. Design the API-development lifecycle for one SquiFlow business command from contract to production verification.
2. Choose authentication and API protocol for a browser user, Workstation, and machine partner without one global winner.

**Challenge**

1. A team has excellent OpenAPI docs and Postman collections, but no cross-tenant negative tests, retry tests, or restore tests. Is the API production-ready? Build the missing evidence.
