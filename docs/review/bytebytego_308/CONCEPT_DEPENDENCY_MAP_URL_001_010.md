# ByteByteGo Concept Dependency Map — URL Entries 001-010

**Coverage:** URL occurrences `001-010`, PDF pages `245-254`. This extends the archive concept maps into the independent URL-review section.

## 1. Container/runtime patterns -> problem first

```text
URL 001 container design patterns
    -> recurring process/container coordination problems
    -> same-machine vs cross-machine coordination

SquiFlow current
    -> ordinary business modules: in-process
    -> Core API / Admin API / Worker / Guard: separate only where runtime/security/supervision is real

candidate escalation
    -> separate process
    -> containerized process
    -> same-host helper composition
    -> multi-node orchestration

critical gate
    -> what measured coordination/deployment/failure problem exists?
    -> what resource/operations cost is introduced?
    -> same-host containers != physical HA
```

Kubernetes/sidecars/orchestration are not inferred from a pattern catalog or related visual.

## 2. Cross-cutting API concerns -> executable coverage

```text
URL 002
    -> authentication
    -> logging/correlation
    -> rate limiting/admission
    -> input validation
    -> uniform route coverage

SquiFlow pipeline
    request/correlation
        -> generic rate/admission
        -> authentication
        -> TenantContext
        -> coarse policy
        -> schema/input validation
        -> tenant-scoped resource lookup
        -> OpenFGA/resource authorization
        -> domain/workflow/concurrency/idempotency
        -> effect
        -> safe result/audit/trace
```

Critical rule:

```text
uniform enforcement
    != one giant middleware
```

Future implementation proof requires generated endpoint inventory and explicit reviewed exceptions.

## 3. Database performance -> benefit + hidden cost

```text
URL 003
    workload definition
        -> read/write mix
        -> tenant/data skew
        -> reconnect/import bursts
        -> consistency invariants
        -> data cardinality

measure
    -> query/transaction latency
    -> p95/p99
    -> pool wait
    -> locks
    -> disk/WAL/temp
    -> memory

identify bottleneck
    -> query/index/pool/model/cache/projection/partitioning candidate

remeasure
    -> intended benefit
    + hidden cost
    -> write amplification
    -> WAL/storage
    -> freshness
    -> recovery
    -> tenant fairness
```

Critical rule: no cache/shard/denormalization/index strategy is selected from a checklist.

## 4. API security -> controls prove different facts

```text
URL 004

TLS / edge
    -> channel/exposure protection

ZITADEL OIDC/session
    -> authenticated identity

SquiFlow membership
    -> authoritative TenantContext

OpenFGA
    -> relationship/resource permission

Domain/workflow/current state
    -> business validity

DTO/field allowlists + input/resource limits
    -> constrain untrusted input/output/effects
```

Critical rule:

```text
valid TLS/token/API key/gateway check
    != permission to access arbitrary tenant resource
```

## 5. Event Sourcing -> authority distinction

```text
URL 005

current SquiFlow
    normalized current-state DB
        -> authoritative business state
    audit/revisions
        -> history evidence where required
    transactional outbox
        -> durable after-commit delivery

Event Sourcing candidate
    append-only event sequence
        -> authoritative state representation
    projections
        -> derived current/read state
```

Critical rule:

```text
history/audit requirement
    != automatically Event Sourcing
```

Adoption requires a domain where authoritative temporal reconstruction/replay itself is valuable enough to own event evolution, replay, projections, snapshots, privacy/retention and side-effect isolation.

## 6. Stateless architecture -> process semantics

```text
URL 006

Core/Admin/Worker process memory
    -> transient/disposable execution state

central DB/object store/outbox/ZITADEL/OpenFGA/etc.
    -> durable/shared system state by responsibility

Blazor/session/circuit state
    -> allowed transient UX state
    -> explicit recovery/draft behavior where valuable
```

Critical rules:

```text
stateless process != no state
stateless process != automatic HA
single physical node remains one physical failure domain
```

## 7. Authentication -> principal/client/lifecycle fit

```text
URL 007

human Web / Workstation
    -> ZITADEL OIDC/OAuth

native Workstation
    -> Authorization Code + PKCE + system browser

Web app session
    -> hardened server-managed/cookie-backed direction where practical

future machine integration
    -> API key / client credentials / mTLS / signed request
       selected by exact principal, risk and lifecycle

all authenticated paths
    -> TenantContext
    -> OpenFGA
    -> domain/current-state authorization
```

Critical rule: no universal `session vs token vs OAuth vs API key` winner.

## 8. Clean code -> domain semantics before textual DRY

```text
URL 008
    meaningful names
    one responsibility
    no magic numbers
    descriptive booleans
    DRY where meaning is shared
    shallow control flow
    comments for why
    bounded arguments
    self-explanatory code

SquiFlow
    -> business-language commands
    -> explicit security/compatibility/recovery rationale
    -> false abstraction avoidance
```

Critical rule:

```text
similar code shape
    != same business invariant
```

Implementation quality remains unverified until application source exists.

## 9. Eventual consistency -> per invariant, not whole product

```text
URL 009

protected authoritative decisions
    payment / stock / credit / tenant isolation / current sensitive authorization
        -> transactional/current authority

safe derived state
    search/report/dashboard/projection candidate
        -> may be eventually consistent
        -> must define source
        -> sequence/effect identity
        -> duplicate/out-of-order handling
        -> freshness
        -> reconciliation/rebuild

Workstation local-first
    -> explicit LocalCommitted/PendingRemote/Conflict/etc.
    -> not vague eventual-consistency labeling
```

Critical rule:

```text
CQRS != eventual consistency
"eventual" without delivery/reconciliation/convergence != a consistency guarantee
```

## 10. API Gateway vs Service Mesh -> topology fit

```text
URL 010

current public edge need
    -> TLS termination/routing/custom domains
    -> coarse request limits/exposure/rate/WAF policy where selected
    -> direct routing to Core API or Admin API

application authority
    -> remains in backend

inside modular monolith
    -> in-process calls

future small number of real service boundaries
    -> direct service TLS/client policy may remain simplest

future meaningful east-west service network
    -> service mesh becomes candidate for
       service identity/mTLS/discovery/traffic policy/telemetry
```

Critical rules:

```text
gateway != business authorization
mesh != reason to create service boundaries
north-south vs east-west is a heuristic, not a complete product decision
infrastructure retries != semantic business retry safety
```

## 11. Integrated URL 001-010 decision path

```text
source exposes a technology/pattern
    -> identify exact problem class
    -> identify current SquiFlow boundary and mechanism
    -> state why current mechanism exists
    -> identify alternative surfaces where source mechanism could be better
    -> state authority owned / not owned
    -> state new failure/recovery/ops cost
    -> require measurement/POC/customer need
    -> record falsification trigger
    -> only then propose owner-architecture change
```

No technology in URL `001-010` is globally accepted/rejected from comparison alone.
