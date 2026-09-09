# Architecture Question Ledger — v0.0.15

**Status:** Review/audit aid, not an independent architecture authority. Accepted answers belong in the focused owner documents; unresolved applicable questions belong in `docs/decisions/OPEN_DECISIONS.md` or the owning phase gate.

## Why this exists

Architecture articles often contain better value in the **questions they force us to ask** than in the named pattern/technology they describe.

SquiFlow therefore does not review a source only as:

```text
article says pattern X
→ adopt/defer pattern X
```

The required review is:

```text
source question / trade-off
→ ask it against SquiFlow
→ inspect the real user journey/runtime/data flow
→ answer from current requirements/evidence
→ if unanswered but applicable, assign owner + phase + failure/revisit trigger
```

A question is allowed to end in **NO / NOT NEEDED NOW**. The purpose is to discover missing requirements and failure modes, not manufacture features.

## Required question record

For every material source question or trade-off, record:

1. **Question** — source-explicit where available; otherwise clearly marked `SquiFlow-derived`.
2. **Applies to** — concrete SquiFlow journey/component.
3. **Current answer** — what v0.0.15 currently believes.
4. **Evidence/reason** — requirement, failure mode, measured constraint, or accepted architecture.
5. **If wrong / unanswered** — concrete failure or uncertainty.
6. **Status** — `ANSWERED`, `OPEN`, `DEFERRED`, or `NOT APPLICABLE NOW`.
7. **Owner / phase** — where the decision is actually closed.
8. **Revisit trigger** — what future evidence makes the answer change.

Do not copy interview-style questions into documentation without applying them to SquiFlow.

---

# Current question pass — API gateway, service communication, data sharing, API design, REST, protocols

## 1. API gateway questions

The publicly visible API-gateway article describes the gateway as a client-facing entry point that hides internal service details and may centralize routing, security, versions, transformation, and analytics. The public excerpt does not expose a specific closing question, so the questions below are **SquiFlow-derived from that problem framing**.

### Q1 — Do SquiFlow clients actually need a gateway to hide many internal service locations?

**Current answer:** Partly. Tenant and platform clients should not know deployment internals, but SquiFlow currently has only a few justified backends. A normal reverse proxy/edge can provide the required stable public/private entry points; a heavyweight API-management platform is not yet justified.

**Status:** ANSWERED for baseline; exact edge product/config remains OPEN.

**Owner/phase:** `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`, deployment work in Phase 8/10.

**Revisit trigger:** many external APIs, partner/developer API program, complex protocol transformation/version routing, or edge-policy needs that exceed the simple reverse proxy.

### Q2 — Which responsibilities belong at the edge and which must remain in the backend?

**Current answer:** Edge may own TLS termination, hostname routing, request-size/WAF/private-access policy, coarse rate limits and transport negotiation. Core/Admin API still own authentication/session validation as applicable, OpenFGA authorization, TenantContext/platform context, resource/domain validation, idempotency/concurrency, operation-specific admission, and audit.

**If wrong:** a gateway misconfiguration could become an authorization bypass or collapse the Admin/Core security planes.

**Status:** ANSWERED.

### Q3 — What happens if the gateway/edge itself is unavailable?

**Current answer:** This is a real deployment failure mode. Tenant Web/API access can be unavailable even when Core API is healthy. Platform emergency recovery must not assume the same public edge is the only path to infrastructure recovery.

**Status:** OPEN for concrete topology/runbook.

**Owner/phase:** Phase 10 physical deployment/recovery; `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

**Need discovered by asking the question:** explicit edge failure/SPOF and private recovery-path test.

### Q4 — Does the gateway route platform administration through Core API?

**Current answer:** No. Shared edge is allowed, but routing is direct:

```text
edge
├── tenant/business → Core API
└── platform/admin  → Admin API
```

**Status:** ANSWERED; Phase 6/8 hostile test.

---

## 2. Service-to-service communication questions

The source frames synchronous/blocking versus asynchronous/non-blocking communication as a decision affecting latency, scalability, failure recovery and consistency. The following are the questions SquiFlow must ask **before creating any network call between internal components**.

### Q1 — Is this actually service-to-service communication, or are both capabilities in the same modular-monolith host?

**Current answer:** Modules in the same host communicate in-process. Do not introduce HTTP/gRPC merely to imitate microservices.

**Status:** ANSWERED.

### Q2 — Does the caller require the authoritative result before it can respond to the user?

**Current answer:** If yes and the operation fits the bounded interactive budget, use a synchronous in-process or HTTP dependency call as appropriate. If the consequence can happen after the authoritative commit, prefer durable outbox/Worker execution.

**Status:** ANSWERED as selection rule; per-interaction answer required during implementation.

### Q3 — What happens when the callee is slow, unavailable, or returns after the caller times out?

**Current answer:** Every real remote dependency needs timeout classification, retry ownership, idempotency/reconciliation where effects may have happened, and an explicit user/operation state such as `DependencyTransient` or `OutcomeUnknown` where appropriate.

**Status:** ANSWERED as contract; provider-specific behavior remains per integration.

### Q4 — How many synchronous network hops are on the critical user path?

**Current answer:** Keep this deliberately small. Core API does not call Admin API for tenant work; Admin API does not call Core API for platform work. Avoid synchronous chains created only by decomposition.

**Status:** ANSWERED baseline; Phase 6/8 should measure/inspect actual call chains.

### Q5 — Does the interaction require ordering, fan-out, replay, or just one durable task?

**Current answer:** Select queue/job, pub/sub, event stream, or direct call from the semantic need. No generic broker/event-stream platform is baseline.

**Status:** ANSWERED selection rule.

---

## 3. Data-sharing questions

The article explicitly asks whether services should connect to the same database or communicate through APIs/messages, and how to preserve consistency, performance, fault tolerance, and loose coupling while sharing data.

### Q1 — Should multiple SquiFlow runtime hosts connect to the same authoritative database?

**Current answer:** Yes for the current modular-monolith deployment when Core API, Admin API and Worker legitimately operate on shared business/platform state. Separate executable is not automatically separate data ownership.

**Critical qualification:** shared DB access does not grant arbitrary cross-module writes.

**Status:** ANSWERED baseline.

### Q2 — Who owns the invariant when several runtime hosts can reach the same tables?

**Current answer:** The business module/domain/application contract owns the invariant, not the host process. Admin API, Core API and Worker must use the same reviewed state-transition/transaction rules for the same business concept.

**If wrong:** an Admin API maintenance handler can bypass payment/order/inventory invariants with direct SQL even though the service topology looks clean.

**Status:** ANSWERED principle; Phase 6 tests shared-invariant paths.

### Q3 — When should data be shared through an API or event instead of shared-table access?

**Current answer:** When a capability becomes a genuinely independently owned/extracted service with its own deployment/data authority, consumers should use its stable API/events/read model rather than its private schema. Do not prebuild that boundary before extraction is real.

**Status:** DEFERRED until service extraction evidence exists.

### Q4 — What consistency is actually required for the shared data?

**Current answer:** Decide per invariant. Payments, stock, credit, tenant isolation, sensitive authorization and expected-version transitions require current authority. Reports/search/telemetry and other reconstructable projections may be eventually updated with explicit freshness/rebuild behavior.

**Status:** ANSWERED policy; each new derived view must still classify itself.

### Q5 — What failure occurs if the shared DB is healthy but one runtime host is not?

**Current answer:** Core API and Admin API should remain process-independent. Worker downtime delays asynchronous work but cannot erase already committed truth. Shared-DB outage can affect all hosts that depend on it.

**Status:** ANSWERED conceptually; Phase 6/10 verifies real topology.

---

## 4. Good-API questions

The API-design newsletter explicitly asks readers to identify common API design mistakes and how they would fix them. Applied to SquiFlow, the highest-risk mistake is **an endpoint that exposes generic CRUD or transport mechanics while hiding the business intent, authority, idempotency and concurrency contract**.

### Q1 — What business intent does this endpoint represent?

**Current answer:** Material mutations use task-oriented commands such as `ApproveQuote`, `RefundPayment`, `AdjustInventory`, or explicit resource creation. Avoid generic `UpdateEverything` endpoints.

### Q2 — Can it be retried, and what proves a retry does not duplicate the effect?

**Current answer:** Semantic idempotency key + receipt/effect reconciliation where applicable.

### Q3 — What API/protocol versions can call it, especially a Workstation that skipped releases?

**Current answer:** Compatibility must be explicit; unsupported clients receive an explicit upgrade/protocol result. The exact versioning mechanism remains OPEN for Phase 3.

**Need discovered by asking the question:** version-compatibility policy is not optional merely because there is initially one server release.

### Q4 — Can the response be unbounded?

**Current answer:** No. Collections have server-enforced pagination/maxima and stable ordering/filter allowlists.

### Q5 — Does resource naming hide a real domain transition?

**Current answer:** Normal resources use noun-oriented URLs, but semantic transition subresources are allowed. REST aesthetics do not force `UpdateQuote(status=Approved)` when `approval` is the real domain operation.

**Owner:** `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`.

---

## 5. REST questions

The REST newsletter explicitly asks which REST constraint is most often overlooked. For SquiFlow, the useful response is to ask **all six constraints against each relevant API surface** rather than declare the whole product RESTful.

### Client/server

**Question:** Can client and server evolve without the client depending on internal DB/provider details?

**Current answer:** Yes by explicit HTTP/sync contracts and provider containment.

### Statelessness

**Question:** What state is required between requests, and where does it actually live?

**Current answer:** Core/Admin API process RAM is not authoritative business state. Sessions/Blazor circuits may exist, so SquiFlow does not claim strict system-wide statelessness. Valuable drafts/business state must be durable outside transient circuit memory.

### Uniform interface

**Question:** Are resource/error/version/idempotency semantics predictable across endpoints?

**Current answer:** This is a design requirement, but semantic business command subresources remain allowed.

### Cacheability

**Question:** Which responses are safe to cache, for how long, and under which tenant/authorization scope?

**Current answer:** Public/static/reconstructable responses may be cacheable. Payment, authorization, stock, credit and other current-authority responses must not become stale authority. Exact cache headers are Phase-8 surface policy.

**Need discovered by asking the question:** endpoint-level cacheability classification/test, not merely “we use CDN”.

### Layered system

**Question:** Can an edge/proxy be inserted without changing business authority semantics?

**Current answer:** Yes; backend repeats its own security/resource checks. Admin/Core routing remains separate.

### Code on demand

**Question:** Does SquiFlow need server-delivered executable code as an API capability?

**Current answer:** No special REST code-on-demand architecture is required. Normal Web static/application code delivery is not a reason to introduce executable tenant code or arbitrary scripts.

---

## 6. Network/protocol failure questions

The network-protocol newsletter explicitly asks which protocol failure would be most disruptive. For SquiFlow, the more useful architecture exercise is a **dependency-failure matrix**: what user capability fails when each required protocol/dependency is unavailable, and what must degrade safely?

| Dependency/protocol | SquiFlow question | Current answer/status |
|---|---|---|
| DNS | What happens if names cannot resolve? | Online Web/API/provider calls fail; local-capable Workstation work can continue. Recovery/private access must have an operator plan. Concrete deployment test OPEN Phase 10. |
| TLS/HTTPS | What happens when certificate validation/handshake fails? | Fail closed; no insecure fallback. Certificate renewal/expiry monitoring belongs to operations. |
| OIDC/OAuth / ZITADEL | What happens if identity provider is unavailable? | New login/step-up can fail. Exact existing-session/degraded behavior is Phase-1 OPEN and must not become accidental permanent access. |
| OpenFGA over HTTPS | What happens if authorization service is unavailable? | Never convert outage into allow. Exact fail-closed/degraded/cached-read matrix by operation risk remains Phase-1 OPEN. |
| NTP/time sync | What happens under clock skew? | Monitor and bound skew because TLS/OIDC/leases/schedules depend on reasonable time. Wall-clock time does not replace versions/fencing/idempotency. Concrete tolerance/test OPEN Phase 10. |
| WebSocket/SignalR | What happens if the live channel drops? | No business truth is lost. Reconnect/reconcile/poll as appropriate; durable DB/outbox/change state remains truth. |
| SSH/private-access network | What happens if operator remote access fails? | Tenant business path should not depend on SSH. Recovery becomes operationally harder; local/private break-glass runbook required. |
| HTTP version/QUIC | Does HTTP/3 vs HTTP/2 change business semantics? | No. Transport negotiation is deployment/runtime detail unless measurements prove a need. |
| SMTP/SMS/provider protocols | Is external delivery part of transaction truth? | Normally no; delivery is durable consequence with retry/reconciliation and explicit status. |

**Need discovered by asking these questions:** Phase 1 and Phase 10 require explicit identity/authorization outage and clock/DNS/TLS failure behavior rather than only happy-path protocol configuration.

---

# Sequential archive question pass — articles 001–015

Full source application is recorded in `docs/review/BYTEBYTEGO_ARCHIVE_SEQUENTIAL_REVIEW_001_015.md`. The material questions that change or strengthen SquiFlow are kept here.

## 7. gRPC question — what limitations justify not using it now?

**Source-explicit question:** What are gRPC's limitations in a real project?

**Applied to:** Workstation sync and future extracted services.

**Current answer:** SquiFlow has no measured requirement that makes gRPC worth an additional transport/runtime contract today. The modular monolith remains in-process, and Workstation sync can start with explicit HTTP. gRPC is reconsidered only when a real streaming/binary/generated-contract need materially improves the implemented workload.

**If wrong:** HTTP sync could become unnecessarily chatty/large, or a future streaming requirement could be awkward. The fix is then a measured protocol POC, not prebuilding gRPC now.

**Status:** DEFERRED / NOT NEEDED NOW.

**Owner/phase:** API/sync owner; revisit after Phase-3 measurements or a future service extraction.

## 8. Kubernetes question — what challenge would make us switch?

**Source-explicit question:** What challenges prompted switching from Docker to Kubernetes?

**Applied to:** owned-rack deployment.

**Current answer:** No current challenge requires Kubernetes. Revisit only when repeated multi-node placement, replica reconciliation, rollout/rollback, discovery, failover/replacement or scaling operations exceed what the simpler deployment automation can own reliably.

**If wrong:** introducing Kubernetes too early wastes RAM/CPU and adds another control-plane failure surface; refusing it after orchestration pain becomes real would create manual operational risk.

**Status:** ANSWERED baseline; trigger-driven revisit.

**Owner/phase:** operations, Phase 10 and later topology evolution.

## 9. IaC question — can the production deployment be rebuilt from versioned definitions?

**Source-explicit question:** Have you used Infrastructure as Code for the project?

**Applied to:** SquiFlow single-node paying-customer deployment and later topology.

**Current answer:** It must be possible to reproduce the deployment from version-controlled infrastructure/deployment definitions and runbooks. This is distinct from tenant/platform application configuration, which remains Web/Admin-API driven where applicable.

**If unanswered:** a disk/node replacement could require reconstructing production from undocumented manual commands.

**Status:** OPEN for exact tooling, ANSWERED for the requirement.

**Owner/phase:** `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`; production mechanism closed before Phase 10 gate.

**Revisit trigger:** multi-node/provider deployment may require a stronger IaC/GitOps tool, but Kubernetes/Flux/Terraform are not automatically selected.

## 10. Scalability question — what is our next move when capacity is reached?

**Source-explicit question:** How do you improve a system's scalability?

**Applied to:** Core API/Admin API/Worker/DB/storage/network on the owned rack.

**Current answer:** First measure the bottleneck. Each production profile needs a capacity envelope and a known next move for the first-order bottlenecks. A central DB is intentionally accepted until it becomes the measured constraint. Caching, replicas, extra nodes, sharding, or async decomposition are selected only for the actual bottleneck.

**If unanswered:** “scalable” becomes an unverifiable claim and the team may add the wrong infrastructure under pressure.

**Status:** OPEN for measured thresholds/next-move table; principle ANSWERED.

**Owner/phase:** operations/Phase 10 qualification.

## 11. Authentication question — session, token, JWT or PASETO?

**Source-explicit question:** Which authentication approach should be used?

**Applied to:** tenant Web, Admin Web, Workstation.

**Current answer:** ZITADEL is the identity platform. Workstation uses OIDC Authorization Code + PKCE. Exact Web session/cookie topology remains Phase-1 OPEN. SquiFlow does not create a second JWT/PASETO authentication system, and token/session form never replaces OpenFGA authorization or TenantContext isolation.

**Status:** PARTLY OPEN only for Web session topology.

## 12. Production Web question — what components are missing from the generic architecture?

**Source-explicit question:** What other components belong in a production Web architecture?

**Applied to:** paying-customer deployment.

**Current answer:** In addition to Web/API/DB/Worker/edge/observability, SquiFlow needs backup/restore, identity, authorization, tenant isolation, object storage, separate Admin API, secrets/configuration, rate/admission, certificate/time health and private recovery. Generic cache/search services remain optional until needed.

**Status:** ANSWERED baseline; exact deployment inventory remains Phase 10 evidence.

## 13. Database-performance question — what workload are we actually tuning?

**Source-explicit question:** Which other database performance strategy should be added?

**Applied to:** Phase-3 central DB selection.

**Current answer:** Before choosing another performance technique, characterize the workload: read/write/delete mix, row sizes, tenant skew, normal/reconnect-burst concurrency, consistency needs, hot queries, sync/import bursts and initial HA/geographic assumptions.

**If unanswered:** a database can pass toy reads yet fail under offline-reconnect write bursts or larger tenant cardinalities.

**Status:** ANSWERED requirement; measurements pending Phase 3.

## 14. PostgreSQL question — what else must we understand if it wins?

**Source-explicit question:** What else is needed to understand PostgreSQL architecture?

**Applied to:** strongest current central DB candidate on low-resource rack hardware.

**Current answer:** In addition to transactions/RLS/query plans, measure connection/backend process cost, WAL growth, checkpoint spikes, autovacuum, temp spill, archive/log growth, restart/crash recovery and disk-full behavior under the SquiFlow burst workload.

**If unanswered:** PostgreSQL can be logically correct but operationally unstable on the actual hardware envelope.

**Status:** Phase-3 POC requirement; PostgreSQL itself remains unselected until that gate.

## 15. Algorithm question — do we need to implement the standard distributed-system algorithms ourselves?

**Source-explicit question:** Which algorithms belong in the system-design toolkit?

**Applied to:** SquiFlow architecture selection.

**Current answer:** No algorithm is adopted from the list without its problem. SquiFlow does not build its own Raft/consistent-hashing/Merkle repair layer, does not use Operational Transformation without collaborative-editing requirements, and does not confuse rsync with semantic Workstation sync. Framework-provided bucket limiters or future Bloom filters are considered only from measured need.

**Status:** ANSWERED / NOT NEEDED NOW.

## 16. Architecture-source question — what evidence quality closes a technical decision?

**Source-explicit prompt:** What additional architecture resources should engineers use?

**Applied to:** SquiFlow source review itself.

**Current answer:** Secondary diagrams/newsletters are good at exposing questions and trade-offs. Exact high-impact security/database/protocol/provider claims should be checked against primary specifications, official provider docs or foundational papers when available before being locked as architecture.

**Status:** ANSWERED process rule.

## 17. Messaging question — queue, pub/sub, event bus or stream?

**Source-explicit question:** Which of SQS/SNS/EventBridge/Kinesis-style workloads is actually present?

**Applied to:** Worker/outbox/external integration.

**Current answer:** one task → queue/job; several independent consequences → multiple durable deliveries/pub-sub only if present; complex routed events → event-bus semantics only if topology appears; replayable high-volume streams → stream infrastructure only from evidence. Provider products do not determine the semantic choice.

**Status:** ANSWERED selection rule; Phase 6 chooses the simplest mechanism for the first real workload.

---

# Remaining archive question pass - articles 016-123 and Web entries 023-064

Complete per-entry dispositions are recorded in `BYTEBYTEGO_ARCHIVE_SEQUENTIAL_REVIEW_016_123.md` and `BYTEBYTEGO_WEB_CONTENT_REVIEW_023_064.md`. Only the questions that materially strengthen SquiFlow are promoted here.

## 18. Release question - what can this deployment actually roll back?

**Applied to:** first paying-customer rack deployment and database/schema changes.

**Current answer:** Promote one immutable verified artifact; preflight configuration/capacity/dependencies/migrations; preserve supported cross-version contracts; evaluate health plus a small authorized smoke journey; and state whether failure uses binary rollback, schema/data roll-forward, maintenance restore, or another tested path. One active node may require an honest maintenance window.

**If unanswered:** a supposedly safe deployment can leave new code with old schema, old code with incompatible schema, or a database change that cannot be reversed even though the binary can.

**Status:** Requirement ANSWERED; exact first-production strategy/tooling OPEN.

**Owner/phase:** operations and persistence; Phase 10 qualification.

## 19. Schema-evolution question - who still reads or writes the old shape?

**Applied to:** central/local DB, API/sync, durable work, idempotency results, rules/workflow/forms.

**Current answer:** Prefer expand-migrate-switch-contract. Before contraction, inventory supported old/new backends, skipped Workstations, pending sync, jobs/messages, stored results, and versioned snapshots; drain, migrate, reject explicitly, or keep compatibility according to the supported contract.

**If unanswered:** an apparently successful migration can break an offline Workstation or delayed durable job later.

**Status:** ANSWERED policy; exact first compatibility mechanism OPEN in Phase 3.

## 20. Database-concurrency question - what protects this exact invariant?

**Applied to:** orders/quotations, issued numbers, stock, credit, payments/refunds, and authorization/configuration changes.

**Current answer:** Default to expected-version/conditional updates, use database constraints for uniqueness, and choose stronger isolation/locks only when needed. Transactions remain short, multi-lock order is deliberate, and only classified deadlock/serialization failures can retry the entire transaction with a bounded budget and existing idempotency.

**If unanswered:** two individually valid operations can create a lost update, oversell, duplicate number, or deadlock/retry storm.

**Status:** ANSWERED selection rule; each implemented invariant must close its mechanism.

## 21. Cache question - what happens when it is empty, wrong, or down?

**Applied to:** any future in-process/distributed/browser/edge cache.

**Current answer:** Cache is disposable and non-authoritative. Define tenant/permission/configuration-safe keys, source/freshness/invalidation, bounds, stampede/miss amplification, outage bypass, cold recovery, and privacy. Sensitive stale data never becomes authority.

**If unanswered:** a cache can leak across tenants, preserve revoked authority, or amplify load during expiry/outage.

**Status:** NOT NEEDED NOW for a product; policy ANSWERED if a cache is introduced.

## 22. Application-security question - which trust boundary handles this input and output?

**Applied to:** Web/API fields, SQL/query composition, URLs/webhooks, files/artwork, templates/documents, provider responses, logs, build artifacts, and optional containers.

**Current answer:** Use layered encoded/sanitized Web output, CSRF protection for cookie mutations, explicit DTO/field allow-lists, parameterized SQL plus allow-listed identifiers/operators, bounded SSRF-safe outbound access, tenant-scoped files, TLS/secrets, dependency/build controls, and conditional container hardening. ZITADEL/OpenFGA remain identity/authorization owners.

**If unanswered:** a valid authenticated request can still exploit SQLi/XSS/CSRF/SSRF, cross tenant boundaries, expose secrets, or exhaust parsers/resources.

**Status:** ANSWERED baseline; exact surface-specific mechanisms close in Phase 1/3/7/8.

**Owner:** `docs/security/APPLICATION_SECURITY_BASELINE.md`.

## 23. Business-model question - is a generic architecture taxonomy changing the product?

**Applied to:** product/service categories, quotations, pricing, outsourced work, suppliers, and inventory.

**Current answer:** No. System-design/DDD/database patterns implement SquiFlow's actual domain: no forced ready-made/custom-design or social category; Owner-authorized pricing; explainable quotation revisions; outsourced print-only costs; informal supplier ordering and partial payables; damaged-stock adjustments; no universal reservation/MRP/banner-wastage engine.

**Status:** ANSWERED and guarded by `docs/domain/BUSINESS_MODEL.md`.

---

# Cross-source question families to ask on every future architecture review

Regardless of article topic, ask these against the affected SquiFlow journey:

```text
WHY is this boundary/pattern needed here?
WHO owns the authoritative decision/state?
WHERE is durable truth?
WHAT if this dependency is unavailable/slow/returns late?
WHAT if the request/effect happens twice?
WHAT if messages/results arrive out of order?
WHAT if the client is offline or an old version?
WHAT if the user's permission/tenant context changes mid-flow?
WHAT if one tenant consumes most shared capacity?
WHAT is allowed to be stale, and how can the user know?
WHAT is the resource/capacity ceiling?
WHAT state must survive process/node/provider failure?
HOW is the operation recovered/reconciled rather than guessed?
HOW do old/new readers, writers, messages, and offline clients coexist during change?
HOW does release failure recover if the database cannot simply roll back?
HOW does a future provider/runtime replacement happen?
HOW is the behavior verified on the actual deployment class?
CAN an untrusted field, URL, file, template, provider response, or cache cross a trust/tenant boundary?
IS a generic architecture taxonomy accidentally changing the actual business model?
DO we really need another interface/process/network hop/database/protocol?
```

This checklist is not a demand to implement every possible subsystem. Its purpose is to reveal the requirements that already follow from the journey we are implementing.

# Integration rule

Future source reviews must include a `Questions applied to SquiFlow` section. Applicable unanswered questions are not allowed to disappear inside a review document: they must become either:

- an OPEN decision with owner/phase;
- a phase implementation/test gate;
- a current accepted decision in the focused owner document;
- or an explicit `NOT NEEDED NOW` answer with a revisit trigger.
