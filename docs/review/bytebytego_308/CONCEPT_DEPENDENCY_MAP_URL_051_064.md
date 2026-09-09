# Concept Dependency Map Extension — URL 051-064

This final URL extension preserves the standing rule: source comparisons, protocol charts, learning roadmaps, patterns and best-practice lists do not choose SquiFlow architecture. Every node is tied to a concrete boundary/problem, the property it provides, and the evidence that would justify changing it.

```text
Retry safety and effect identity (051, 058, 064)
  semantic IdempotencyKey
    -> same intended business operation across retry/response loss
  atomic receipt + business effect + outbox
    -> one authoritative transactional boundary where possible
  provider idempotency/reference
    -> external-provider duplicate suppression/evidence
  OutcomeUnknown + reconciliation
    -> ambiguous external effect
  RequestId / TraceId / MessageId
    -> diagnostics/transport identity, not business operation identity

Identity and token format (052)
  ZITADEL + OIDC/OAuth
    -> human authentication, session/MFA/SSO/federation lifecycle
  JWT/token format
    -> signed claims exchanged inside the identity protocol
  TenantContext
    -> current SquiFlow tenant scope
  OpenFGA + domain rules
    -> current application/resource/business authorization
  machine API key / client credentials / mTLS
    -> future principal-specific integration candidates
  valid token
    != current business permission

Non-functional requirements (053)
  HardInvariant
    -> must not be traded away for speed/availability
  OperationalTarget
    -> measurable quality target closed from evidence
  DegradedMode
    -> explicit behavior under dependency/resource failure
  actual rack + Workstation measurements
    -> numeric latency/capacity/recovery gates
  executable acceptance evidence
    -> converts architecture prose into implementation proof

Service/data consistency (054)
  current modular monolith + shared central authority
    -> local transactions for coupled business invariants
  in-process module calls
    -> no unnecessary network/failure boundary
  genuine extracted service
    -> explicit authoritative ownership + API/event contract
  saga/compensation/event propagation
    -> only if real distributed workflow exists
  DB-per-service
    -> ownership principle, not one physical DB server per service
  distributed monolith
    -> avoid shared private-table writes + synchronized deployment + long call chains

Communication fit (055, 061, 063)
  same host/module
    -> in-process
  explicit Web/external command/resource
    -> task-oriented HTTP
  flexible nested client read
    -> GraphQL candidate after measurement
  real high-frequency/streaming RPC
    -> gRPC candidate after POC
  live browser update
    -> polling / SSE / SignalR-WebSocket depending direction/frequency
  external callback
    -> webhook when consumer contract/retry/auth exists
  long-running / after-commit consequence
    -> durable outbox/job/Worker
  protocol change
    -> does not replace auth/idempotency/concurrency/compatibility

OO / SOLID / patterns (056, 060)
  cohesive domain/application code
    -> default
  IObjectStore / IBackupTarget
    -> justified provider replacement seams
  Adapter
    -> real external interface incompatibility/replacement
  Strategy / Factory / Builder / Decorator / Proxy
    -> only real variation/construction/cross-cutting boundary
  Observer
    -> in-process notification only unless durability added separately
  Command
    -> business intent shape, not mandatory command bus
  generic repository / interface-per-class / deep hierarchy
    -> avoid unless a concrete seam/problem earns it

Architecture and learning maps (057, 059, 061)
  language/runtime/framework/network/security/data/operations knowledge
    -> competency/question inventory
  named products/logos
    -> examples/candidates, not deployment requirements
  SquiFlow-specific gaps
    -> semantic idempotency
    -> offline authority/recovery
    -> tenant isolation/current authorization
    -> compatibility/version skew
    -> backpressure/resource bounds
    -> restore/privacy/small-team operations

API contract design (058, 064)
  resource-oriented naming
    -> useful default for stable resources
  explicit action/command endpoints
    -> approvals/refunds/publication/reconciliation and other domain transitions
  semantic retry safety
    -> operation identity + durable evidence, not HTTP verb alone
  bounded pagination
    -> stable order + tenant/auth-safe continuation
  versioning
    -> compatibility/migration/retirement, separate from product SemVer
  Problem Details/failure taxonomy
    -> stable machine/operator failure semantics

DDD (062)
  ubiquitous language
    -> shared business vocabulary
  bounded context
    -> model/semantic boundary, not automatic service
  aggregate
    -> invariant + transaction/concurrency boundary
  domain event
    -> domain fact inside model
  integration event
    -> cross-boundary contract when needed
  Event Sourcing
    -> separate choice where event sequence becomes authority
  repository
    -> domain collection abstraction when useful, not generic CRUD mandate
```

## Cross-links to earlier study

- URL `051`, `058`, and `064` close the loop with archive API/idempotency material and URL `031`: delivery semantics, HTTP verbs and protocol choice remain subordinate to semantic business-effect identity.
- URL `052` independently revisits archive `010` and the broader authentication cluster: identity token format remains separate from current authorization and tenancy.
- URL `053` connects the generic NFR source to SquiFlow's already accepted HardInvariant/OperationalTarget/DegradedMode model and actual-hardware measurement rule.
- URL `054` connects archive modular-monolith/microservice material with consistency and data-ownership studies: distribution is earned from a boundary, not from a maturity diagram.
- URL `055` and `063` consolidate the per-surface communication rule spanning REST, GraphQL, gRPC, live channels, webhooks, in-process calls and durable async.
- URL `056`, `060`, and `062` connect SOLID/pattern/DDD vocabulary to the anti-ceremony rule: abstractions follow invariants/replacement/fault boundaries rather than diagrams.
- URL `057`, `059`, and `061` confirm that knowledge maps and roadmaps generate competence/questions, not a product/component backlog.
