# ByteByteGo Exhaustive Sequential Study — URL Entry 020

# URL 020 — How to Design Good APIs

## A. Identification

- **URL entry:** `020`
- **PDF page:** `264`
- **Source URL:** `https://blog.bytebytego.com/p/ep189-how-to-design-good-apis`
- **Public source access:** public newsletter section accessible; reviewed directly.
- **Related visual:** archive page `380`; exact archive overlap with `101`.
- **Exact archive-title overlap:** archive `101`; independently reviewed.
- **Visual inspection:** PDF page `264` rendered and inspected in full.

## B. Core concept

### SOURCE

The accessible source recommends idempotency/idempotency keys, API versioning, noun-based resource names, endpoint security with authentication/HTTPS and pagination for large datasets. It gives a simplified HTTP-method idempotency description and suggests storing idempotency keys in Redis or a database.

### INFERENCE

Good SquiFlow API design is a business-contract problem, not REST purity. Resource nouns are a useful default, while explicit semantic action endpoints remain appropriate where they make business intent, authorization, idempotency and audit clearer.

### EXTERNAL KNOWLEDGE / CAVEAT

HTTP method idempotence does not by itself prove application retry safety. A DELETE can trigger unsafe duplicate downstream effects if implementation is wrong, while a POST can be safely retryable under a semantic idempotency key. “Same result” should mean same semantic effect, not necessarily byte-identical response. Offset pagination can drift under concurrent changes; cursor/keyset approaches need stable ordering. Authentication/HTTPS do not replace resource authorization/TenantContext/domain rules. Redis is one possible idempotency store, not a requirement; where mutation + receipt + outbox share a DB, transactional atomicity may be preferable.

## C. Important concepts

- explicit business intent;
- resource nouns and semantic actions;
- semantic idempotency key;
- same-key/same-intent vs changed-intent;
- version/compatibility;
- Problem Details/failure codes;
- pagination/cursors;
- authentication vs authorization;
- concurrency/expected version;
- bounded requests/results;
- deprecation/old Workstation compatibility.

## D. Diagram / visual explanation

The visual summarizes good API practices. SquiFlow should treat the method/idempotence table and REST naming examples as heuristics, then layer real domain semantics on top.

## E. How it works — step by step

1. Name the operation/resource in domain language.
2. Classify audience: tenant Web, Workstation sync, external partner, tenant admin, platform admin.
3. Authenticate and derive authoritative context.
4. Validate request/schema/field allowlists.
5. Authorize function/resource/current domain state.
6. For retryable mutation, require semantic idempotency key and bind it to intent.
7. Apply expected-version/concurrency and transaction constraints.
8. Persist mutation + idempotency receipt + outbox atomically where one store owns them.
9. Return stable Problem Details/status/failure codes.
10. Bound/paginate collection responses with stable ordering.
11. Version/deprecate around supported old/new clients/jobs/sync, not just URL version numbers.

## F. Why it matters

The article overlaps prior archive material but is independently reviewed. Its value is in validating several current choices while also exposing simplifications that SquiFlow must not turn into retry or authorization guarantees.

## G. Trade-offs / limitations

Strict resource-only naming can hide domain intent. Idempotency storage adds retention/storage/race concerns. Versioning creates compatibility burden. Pagination changes UX and consistency. Security checks add latency but are mandatory. Over-generalized generic CRUD endpoints can create mass-assignment/business-rule ambiguity.

## H. Alternatives / comparisons — fit, not winner/loser

```text
resource-style endpoint
    -> ordinary create/read/list/status

semantic command/action
    -> approve/refund/publish/reconcile when domain intent matters

GraphQL read surface
    -> candidate for flexible composition

gRPC sync
    -> candidate for transport-specific Workstation/service workload
```
API styles can coexist by responsibility.

## I. Real implementation considerations / SquiFlow implications

- **KEEP:** task-oriented HTTP with resource nouns as default, not strict REST purity.
- **KEEP:** semantic idempotency keys for retryable mutations and mismatch rejection.
- **KEEP:** authentication + TenantContext + OpenFGA + domain/concurrency chain.
- **KEEP:** bounded pagination and explicit compatibility/deprecation.
- **AVOID:** treating HTTP method semantics as proof that backend effects are retry-safe.
- **AVOID:** requiring Redis solely for idempotency; store choice follows atomicity/workload.
- **KEEP:** semantic actions where they expose real business intent.
- **Duplicate traceability:** URL `020` independently complete despite archive `101`.

**What are we doing and why?** We use task-oriented HTTP because explicit resources and business actions make SquiFlow’s authorization, idempotency, concurrency and audit contracts understandable. We use semantic idempotency for retryable mutations because response loss can make outcome uncertain. We would introduce other API styles on surfaces where their specific properties solve a real problem, not because REST conventions “win.”

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. Which API practices does the source list?
2. What does it recommend for idempotency keys?
3. What naming/versioning/pagination guidance does it give?

**Critical reasoning questions**
1. Which SquiFlow commands should be semantic actions rather than generic UPDATE?
2. Why can POST be retry-safe while DELETE can still be implemented unsafely?
3. How does idempotency key identity differ from hashing request parameters?
4. What compatibility dimensions matter beyond `/v1`?
5. What does authentication still fail to prove about a tenant resource?

**Trade-off questions**
1. When is cursor pagination better than offset?
2. When is a domain action endpoint clearer than pure resource CRUD?
3. When should idempotency receipts live in the central DB versus another store?

**Failure / edge-case questions**
1. Server commits refund then response is lost. What does retry return?
2. Same idempotency key arrives with a different amount. What happens?
3. Old Workstation calls a contract after schema expansion. How is compatibility preserved?
4. List pagination runs while records change. What ordering/cursor semantics prevent duplicates/skips where required?

**Implementation questions**
1. How is endpoint audience/auth/version ownership generated from executable metadata?
2. How are idempotency receipts retained/expired/reconciled?
3. What concurrency token/expected-version applies to commands?
4. What pagination bounds exist for report/list/export endpoints?
5. What hostile tests prove object/property authorization?

**System design interview questions**
1. Design a refund API with semantic idempotency and response-loss recovery.
2. Explain why good API design is broader than REST naming conventions.

**Challenge**
A reviewer demands every action become pure CRUD and every POST be treated as unsafe to retry. Redesign the API around business intent and semantic idempotency without relying on method labels alone.

---
