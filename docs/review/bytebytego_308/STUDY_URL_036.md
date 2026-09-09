# URL 036 — REST API Cheatsheet

## Review method

This occurrence is reviewed independently from earlier archive/URL material. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: no comparison, pattern catalog, popularity claim, or source diagram selects architecture by itself.

## A. Identification

- **URL occurrence:** `036`
- **PDF page:** `280`
- **Source URL:** `https://blog.bytebytego.com/p/ep94-rest-api-cheatsheet`
- **Source access:** public newsletter section accessible.
- **Related visual:** REST API design best-practices cheat sheet from archive page `143`.
- **Visual inspected:** PDF page `280` at full size.

## B. Core concept

### SOURCE

The public newsletter says the cheat sheet explores the six REST principles, HTTP methods/protocols/versioning, and practical pagination, filtering and endpoint design.

### INFERENCE

The source is a study map, not a claim that every good API must be strict REST or that each HTTP convention solves business correctness.

### EXTERNAL KNOWLEDGE / CAVEAT

The related visual's simple table of method idempotence is protocol-level guidance, not proof that an application implementation is retry-safe. A `POST` can be engineered for semantic retry safety, while a badly implemented `PUT`/`DELETE` can still violate application guarantees. REST's Uniform Interface is also broader than noun-style paths.

## C. Important concepts

- REST constraints;
- resource/command endpoint clarity;
- HTTP method/status semantics;
- API version compatibility;
- pagination/filter/sort bounds;
- semantic idempotency;
- endpoint authorization/audience ownership;
- cacheability;
- Problem Details/error contracts.

## D. Diagram / visual explanation

The visual groups API versioning, status codes, resource naming, idempotency, pagination/security and JWT structure. It is useful as a compact checklist but mixes protocol conventions, authentication artifacts and application concerns. JWT structure, for example, does not make a REST endpoint authorized.

## E. How it works — step by step

For a SquiFlow HTTP surface, first identify the business/resource intent and audience, then map it to a clear request/response contract, apply authentication/TenantContext/OpenFGA/domain rules, add semantic idempotency/concurrency where needed, bound queries/payloads, define compatibility and cache/error behavior, and only then optimize stylistic consistency.

## F. Why it matters

SquiFlow controls several client types and benefits from predictable task/resource HTTP contracts, but strict REST purity must not weaken domain clarity or push Workstation/live/durable-async requirements into the wrong protocol.

## G. Trade-offs / limitations

REST conventions are simple and interoperable, but resource-only purity can make business actions awkward; version proliferation can become expensive; pagination/filtering can still create expensive queries; cache can leak stale sensitive state; status codes alone cannot express the entire business failure model.

## H. Alternatives / comparisons — fit, not winner/loser

- task-oriented HTTP for explicit commands/resources;
- GraphQL for real nested/client-selected read composition;
- gRPC for measured typed/streaming RPC boundaries;
- live signaling for wakeup/UX;
- durable operation/status + Worker for long-running work;
- in-process calls within the modular monolith.

## I. Real implementation considerations

Executable endpoint metadata/inventory, authentication/audience, idempotency, expected-version, pagination bounds, safe error mapping, cache policy and retirement telemetry must be verified once endpoints exist.

### Implications for the Current Implementation

- **KEEP:** pragmatic REST/task HTTP for ordinary application API surfaces.
- **KEEP:** explicit domain actions such as approve/refund rather than generic CRUD where intent matters.
- **KEEP:** semantic idempotency and concurrency independent of HTTP method labels.
- **KEEP:** GraphQL/gRPC/live signaling remain complementary candidates.
- **IMPROVE NOW (implementation gate):** generated endpoint inventory and executable tests for method/status/idempotency/pagination/auth rules.
- **NEEDS MEASUREMENT:** exact API version transport and any GraphQL read surface.
- **AVOID:** strict REST purity as a product goal.

**What are we actually doing and why?** We use HTTP/task contracts because they make current business commands/resources explicit and easy to inventory, not because a cheatsheet ranks REST above other styles.

**What would falsify/change this?** A real surface whose read composition, streaming or live communication is materially worse under ordinary HTTP would justify another interface on that surface.

### Critical interrogation — answers intentionally withheld

**Foundation**
1. What are the six REST constraints?
2. What is the difference between HTTP idempotence and SquiFlow semantic retry safety?
3. Why are pagination and filtering security/resource concerns as well as usability features?

**Critical reasoning**
1. Which SquiFlow commands should not be forced into generic CRUD?
2. Which API responses must remain non-authoritative if cached?
3. Why does JWT structure not determine endpoint authorization?
4. What belongs to HTTP status versus SquiFlow failure code?

**Trade-off**
1. When is GraphQL a better read interface than several task HTTP queries?
2. When is gRPC a better Workstation transport?
3. What does REST/task HTTP keep simpler?

**Failure / edge**
1. `DELETE` request response is lost after effect.
2. Cursor references rows deleted between pages.
3. Filter creates an unbounded expensive DB query.

**Implementation**
1. What endpoint metadata must CI inventory?
2. How are max page/filter/sort rules enforced?
3. How is `If-Match` mapped to expected-version semantics?

**System design interview**
1. Design an HTTP contract for quote approval and refund.
2. Explain why one SquiFlow product can use HTTP, GraphQL, gRPC and live channels.

**Challenge**
A team proposes renaming every action route into CRUD nouns to become “more RESTful.” Identify which SquiFlow correctness/clarity properties could be lost and how you would evaluate the proposal.
