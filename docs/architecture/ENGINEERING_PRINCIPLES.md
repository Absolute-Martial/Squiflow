# Engineering Principles and Complete Simplicity

**Status:** Canonical development rule  
**Applies to:** production code, tests, migrations, scripts, adapters, hosts, deployment definitions, and refactoring  
**Baseline:** v0.0.20

## 1. Purpose

SquiFlow uses engineering principles as constraints on design and implementation, not as slogans and not as a reason to maximize classes, interfaces, projects, patterns, or technologies.

The governing rule is:

> Choose the simplest design that completely covers the current responsibility, including material edge cases, failure, recovery, security, compatibility, concurrency, resource limits, observability, and operability.

A shorter implementation is not simpler if it hides required behavior. A larger implementation is not better merely because it demonstrates more patterns.

This document works together with:

- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`;
- `docs/architecture/REPOSITORY_STRUCTURE.md`;
- focused capability/security/data/sync/workstation/server owners;
- `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md`;
- `docs/testing/VERIFICATION_STRATEGY.md`.

Repository/file-structure examples in those documents are design guidance. They preserve accepted ownership locations and dependency direction, but they do not require empty projects or speculative folders before a real boundary exists.

## 2. KISS means complete simplicity, not happy-path minimalism

For SquiFlow, KISS means **minimum accidental complexity with complete current behavior**.

Before calling a design simple, ask whether the current responsibility has defined behavior for the relevant cases:

- valid success;
- invalid input and malformed external data;
- missing/null/empty state where applicable;
- duplicate/retried requests;
- cancellation and timeout;
- concurrency/races/conflicts where state is shared;
- dependency outage, slow response, partial response, and outcome-unknown cases;
- process restart/crash and durable recovery when durability is promised;
- offline/reconnect behavior for Workstation responsibilities;
- authorization, tenant isolation, least privilege, and sensitive-data handling;
- compatibility/version skew when contracts or durable state cross versions;
- bounded memory/disk/network/provider usage;
- useful structured diagnostics without leaking secrets/PII;
- operator/user-visible failure semantics and recovery path.

Not every function needs every item. The owner of a responsibility must identify which cases are material and either implement them or explicitly document why they are not applicable yet.

Bad KISS:

```text
"ignore retries for now because one call is simpler"
"use one giant service because fewer files is simpler"
"skip restart recovery because the happy path works"
"store duplicated authority because reading directly is easier"
"put business code in the UI because it saves an abstraction"
```

Good KISS:

```text
one clear owner
small explicit contract
few moving parts
no speculative framework
complete material failure behavior
bounded resources
observable outcomes
straightforward recovery
```

## 3. Principle classification

Principles are classified so they do not become absolute cargo-cult rules.

### Always-on constraints

These apply unless a focused owner explicitly records a justified exception:

- explicit ownership and dependency direction;
- intention-revealing naming;
- least astonishment;
- validation of untrusted external input;
- least privilege and defense in depth;
- one authoritative owner/source for business truth;
- no hidden provider/platform leakage across protected boundaries;
- bounded failure/retry/resource behavior;
- meaningful verification for introduced responsibilities;
- public surface kept no larger than needed;
- secrets never committed or logged.

### Strong defaults

These are preferred but may yield to clearer semantics:

- guard clauses over deep nesting;
- immutability/value semantics where mutation is unnecessary;
- command/query separation where separating mutation and observation preserves clear semantics;
- composition over inheritance;
- narrow interfaces for real consumers;
- normalized authoritative relational data;
- in-process calls inside the modular monolith;
- structured logging and explicit failure codes for material operational events;
- deterministic/repeatable tests where possible;
- concrete classes until a real inversion/replacement seam exists.

### Context-triggered techniques

These are tools, not universal architecture:

- circuit breakers;
- durable idempotency receipts;
- event-driven/asynchronous messaging;
- distributed tracing across actual process/network boundaries;
- chaos/failure experiments;
- caching;
- denormalized projections;
- special isolation/locking;
- extra process/service boundaries;
- polyglot persistence;
- Redis/message brokers/stream platforms;
- data-locality or low-level mechanical-sympathy optimizations.

They are introduced only when the named problem exists and the chosen technique has a clear owner, failure contract, and verification evidence.

## 4. Clean code and self-documentation

### Intention-revealing naming

Names state the business/technical meaning and reason for existence. Avoid `Manager`, `Helper`, `Thing`, `Data`, `flag`, `status`, or abbreviations when a more precise name exists.

Names should communicate units and semantics when ambiguity matters, for example `retryDelay`, `maxRestartAttempts`, `tenantId`, or `expectedRevision`.

### Single level of abstraction

A method should read at one conceptual level. High-level orchestration should not be mixed with low-level parsing, serialization, SQL construction, filesystem primitives, or provider-specific protocol details.

Do not extract tiny helpers solely to satisfy a style rule. Extraction must make the responsibility/control flow clearer.

### Command-query separation

Prefer operations that either observe or mutate, especially across public/application boundaries. Do not hide mutation in a method named like a query.

This is not an absolute ban on returning the result of a command. A command may return the authoritative outcome, generated identifier, new version, or receipt needed by the caller. The prohibition is **surprising hidden mutation**, not useful command results.

### Magic values

Business/protocol/resource constants with semantic meaning receive names and ownership. Do not create constants for obvious literals such as `0` or `1` when naming them adds no meaning.

## 5. Control flow and failure

### Guard clauses

Reject invalid/precondition-failing cases early when doing so makes the main path linear and readable.

### Fail fast at broken invariants; fail safely at external boundaries

Programming/configuration/invariant errors should surface immediately rather than being silently ignored.

Expected operational failures—bad user input, dependency outage, conflict, cancellation, offline state—must be translated into explicit safe outcomes. “Fail fast” does not mean crashing the process for every recoverable error.

### Principle of least astonishment

Names, return values, side effects, retries, cancellation, persistence, and failure semantics should behave as a reasonable caller expects. Avoid hidden global hooks and clever control flow.

## 6. SOLID, applied pragmatically

Detailed owner: `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`.

- **SRP:** one cohesive reason to change; not one method/class.
- **OCP:** expected extension should not require editing unrelated central topology switches; not a mandate for speculative plugin systems.
- **LSP:** substitutability includes semantic behavior, failure, cancellation, security, durability, and performance constraints material to the contract—not just matching method signatures.
- **ISP:** consumers depend on the smallest coherent capability they need; do not create giant manager/provider interfaces.
- **DIP:** high-level business policy should not depend on volatile provider/platform details where a real inversion seam exists. DIP does **not** require `IThing` for every `Thing`, generic repositories, abstract factories everywhere, or interfaces around stable value/domain objects.

A proposed abstraction must answer:

> What architectural guarantee, replacement seam, test seam, security boundary, or ownership rule would be lost if this abstraction were removed?

If there is no meaningful answer, prefer the simpler concrete design.

## 7. Module and architectural boundaries

### Separation of concerns

Business meaning, application orchestration, presentation, transport, persistence providers, identity/authorization providers, process supervision, and observability have distinct ownership. Combine responsibilities only when they truly have the same reason to change and the combined boundary remains clear.

### Law of Demeter / least knowledge

Do not reach through another module/object's internals to make decisions based on its private representation. Expose an operation or query that matches the owning abstraction.

Do not create forwarding wrappers solely to obey Demeter; use the owning public/application surface directly.

### Stable dependency direction

Dependencies point toward stable business/foundation abstractions, not outward toward UI/provider/executable topology. Compile-time project boundaries are used when they materially enforce this rule.

## 8. Data and state

### Immutability first

Prefer immutable value objects, records, snapshots, messages, configuration revisions, and decision inputs/results where they model reality well. Stateful domain objects are allowed when controlled mutation protects a real lifecycle/invariant.

### Single authoritative owner

Every piece of business truth has one authoritative owner. Derived caches/projections/copies state their source, freshness, rebuild/reconciliation behavior, and may not silently become authority.

Workstation provisional/local state is not PostgreSQL server authority. Telemetry is not business/security audit authority. UI state is not authorization authority.

### Tell, don't ask

Prefer behavior near the invariant owner instead of extracting internal state and reproducing decisions elsewhere. Query/read models are still valid; this principle must not force rich domain objects into read-only projection paths.

## 9. Pragmatic simplicity: KISS, YAGNI, DRY, Boy Scout

### KISS

Use the complete-simplicity definition in section 2.

### YAGNI

Do not implement future services, providers, plugins, database technologies, process splits, interfaces, or configuration knobs without a current accepted requirement or a foundation needed by current code.

Preserving a documented future ownership location is not the same as implementing it.

### DRY

Do not duplicate **business knowledge, invariants, authority rules, protocol semantics, or operational policy**.

Do not aggressively deduplicate coincidentally similar code if doing so couples unrelated concepts. A small amount of repeated mechanical code is often safer than a false shared abstraction.

### Boy Scout Rule

Improve nearby code when the change is understood, bounded, and verified. Do not turn every feature change into an unrelated repository-wide rewrite.

### Chesterton's Fence

Before deleting/refactoring an existing architectural boundary, understand why it exists. Preserve decision history even when replacing implementation. Git history and focused decision documents are evidence, not clutter to erase casually.

## 10. Defensive programming and resilience

External inputs—including user input, HTTP/sync payloads, files, provider responses, environment/configuration, durable records from older versions, and IPC messages—are untrusted until validated at the appropriate boundary.

Do not duplicate validation blindly at every layer. Validate syntactic/trust-boundary concerns at ingress and protect business invariants at the owning capability.

### Circuit breaker

Use only for a remote/external dependency where repeated calls during a known failure would amplify harm. Define trip/recovery semantics, timeout/retry interaction, observability, and fallback behavior. Do not wrap local pure code or database transactions in a circuit breaker without a real reason.

### Idempotency

Apply semantic idempotency where retries/duplicates can repeat an effect. It does not mean every method called ten times must always produce an identical result. Read operations, time-based behavior, sequence allocation, and intentionally additive operations have different semantics.

Retryable business mutations/provider effects must define duplicate-key/changed-intent/outcome-unknown behavior where relevant.

## 11. Testing and verification

Tests should be fast, isolated, repeatable, self-validating, and written alongside the responsibility they protect where practical.

“Timely” does not mandate test-first syntax for every line. What matters is that production responsibility and its verification are developed together rather than tests being postponed to a distant cleanup phase.

Use the appropriate evidence level:

- unit/property tests for deterministic logic;
- contract tests for provider/substitution seams;
- architecture tests for dependency rules;
- integration tests for persistence/protocol/identity/provider behavior;
- process/restart/failure tests for lifecycle boundaries;
- actual restore/migration/hardware tests where simulation cannot prove the property.

A test suite being green is not proof of a property the suite never exercises.

## 12. Distributed-systems principles

Design for failure only where distribution/durable asynchronous behavior exists. Do not introduce distribution to demonstrate resilience patterns.

Network/process boundaries explicitly define timeout, cancellation, retry/idempotency, backpressure, compatibility, authentication/authorization, observability, and recovery behavior.

### CAP

CAP is reasoning about distributed data systems under network partition; it is not a rule to label the entire application “CP” or “AP.” SquiFlow chooses consistency/availability behavior per authoritative/derived responsibility.

### Event-driven architecture

Events/queues are used when asynchronous durable decoupling, after-commit consequences, fan-out, or independent workload handling genuinely require them. Same-process module calls remain in-process by default.

## 13. Performance and resource efficiency

Correctness and clear boundaries come before speculative micro-optimization.

Profile/measure before changing architecture for speed. At the same time, obvious unbounded behavior is a correctness problem and must not wait for profiling.

Mechanical sympathy and data locality may matter in proven hot paths, serialization, large buffers, image/document work, or high-volume processing. They do not justify low-level complexity in ordinary business code without evidence.

CPU, memory, disk, DB connections, queues, batches, payloads, logs, retries, parallelism, and provider usage are bounded where exhaustion is possible.

## 14. Security

Apply least privilege and defense in depth across user, process, service, database, provider, filesystem, network, and key-management boundaries.

Authentication, authorization, tenant isolation, business invariants, input safety, encryption, and auditing are separate controls. One does not replace another.

Security-sensitive failures fail closed where continuing would create unauthorized/unsafe state, while availability/degraded behavior remains explicit for non-sensitive optional functions.

## 15. Database and persistence

Relational authoritative data is normalized around real identities/relationships/invariants by default. Denormalized/materialized structures are derived optimizations with explicit source/freshness/rebuild behavior.

ACID/transaction semantics are selected around real invariants; transaction scope is explicit and bounded.

“Polyglot persistence” is not a requirement. Use a different storage technology only when a named workload cannot be served appropriately by the current stores and the new store's authority, consistency, backup, security, migration, and operational costs are justified.

Current accepted directions remain PostgreSQL for authoritative central transactional state, SQLite/WAL for Workstation local/provisional state, and object storage for large objects—implemented only when their owning slices are developed.

## 16. API and contract ergonomics

Design the pit of success: the easiest normal call path should preserve tenant context, authorization, validation, idempotency/concurrency requirements, cancellation, and safe errors rather than requiring every caller to remember hidden steps.

Keep public/internal surfaces minimal. Default to `private`/`internal` unless another assembly/module actually needs the contract.

Use semantic compatibility/versioning intentionally. Repository architecture/documentation version markers are not automatically public package SemVer; external APIs/packages independently define their compatibility/version policy.

## 17. Release, configuration, and CI/CD

Configuration and secrets are separated from code. Secrets never live in source, images, logs, or ordinary committed configuration.

Production artifacts should be reproducible and replaced through controlled deployment rather than hand-patching live binaries when the deployment topology supports that model.

CI/CD logic belongs to the repository and must be runnable locally. GitHub/GitLab orchestration should call the same repository-owned verification steps. Self-hosted/self-managed runners are the preferred primary compute path under current hosted-minute constraints.

A CI badge does not replace provider, recovery, migration, security, hardware, or failure testing.

## 18. Observability

Use structured events with stable names/codes for material operations. Logs, metrics, and traces are chosen according to the question being answered; not every method needs all three.

Distributed tracing is required across actual network/process flows where end-to-end causality matters, not inside every in-process call.

`TraceId`, application `CorrelationId`, and `CausationId` remain distinct where used. Telemetry must not leak secrets or become the only copy of authoritative business/security state.

Observability failure must not silently break business correctness.

## 19. Cloud-native techniques are conditional

SquiFlow is not designed by assuming every workload is cloud-elastic.

Server components should avoid unnecessary local authoritative state when horizontal replacement/scaling is required, but Workstation is intentionally stateful/local-first and some server processes may use bounded local ephemeral state.

Redis is not required merely to call a server “stateless.”

Graceful degradation is desirable where an optional capability can be shed without corrupting authority/security, but critical invariants must fail safely rather than degrade into unsafe behavior.

Loose coupling does not mean every interaction becomes a message queue. In-process calls remain preferred inside one modular-monolith process unless asynchronous durability/fault isolation is required.

## 20. Review checklist for every introduced responsibility

Before accepting a component, class, module, adapter, process, persistence path, or external integration, reviewers should be able to answer:

1. What responsibility does it own and why does it exist?
2. What are its explicit inputs/outputs and public surface?
3. What state/authority does it own, if any?
4. What may it depend on, and what must never depend on it?
5. Which material edge/failure/recovery/security/concurrency/compatibility cases apply?
6. What is bounded (time, retry, memory, disk, batch, concurrency, payload, provider usage)?
7. What diagnostics/evidence exist without leaking sensitive data?
8. What tests/evidence prove the important properties?
9. Which abstraction/project/process is genuinely earned now?
10. Can any layer/interface/helper/configuration be removed while preserving all required behavior and explicit boundaries?

If the answer to 10 is yes, simplify it. If simplification drops a required edge case or makes ownership/authority/failure implicit, it is not KISS—it is incompleteness.
