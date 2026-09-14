# SquiFlow Repository Instructions for Coding Agents

This file is the root instruction map for work in the SquiFlow repository.

More-specific `AGENTS.md` files under a directory add to or override these rules for files in their subtree. Prefer the closest applicable instructions when rules differ.

## 1. Read the architecture before changing code

SquiFlow is architecture-led. Do not infer the target design only from the current code because some accepted boundaries are intentionally not implemented yet.

Start with the smallest relevant set of owners:

- `README.IMPLEMENTATION.md` — current implemented Phase-0 boundary and exact local verification commands.
- `docs/implementation/phases/phase-0/DOCS_FIRST_REWRITE.md` — current rewrite/Phase-0 implementation contract.
- `docs/implementation/phases/phase-0/README.md` — current Phase-0 maturity package.
- `docs/architecture/REPOSITORY_STRUCTURE.md` — repository and executable structure.
- `docs/architecture/APPLICATION_KERNEL_AND_MODULES.md` — application-kernel/module ownership.
- `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md` — capability meaning versus host execution.
- `docs/decisions/CURRENT_DECISIONS.md` — accepted current decisions.
- `docs/decisions/OPEN_DECISIONS.md` — intentionally unresolved decisions.

Then read the focused owner for the area being changed (Workstation, Guard, persistence, API, security, observability, etc.).

### Decision precedence

When documentation appears inconsistent, use this order and stop to reconcile material conflicts rather than guessing:

1. current focused canonical owner for the exact responsibility;
2. current accepted decision record;
3. current implementation-phase owner;
4. historical review/source-study material.

An open decision is not permission to silently choose a technology in code.

## 2. Current implemented boundary

The documentation-first Phase-0 rewrite currently contains:

```text
foundation/
├── application-kernel/SquiFlow.ApplicationKernel
└── observability/SquiFlow.Observability

modules/
└── customers/
    ├── SquiFlow.Customers
    └── SquiFlow.Customers.Workstation

apps/
├── desktop/
│   ├── workstation/SquiFlow.Workstation
│   └── guard/SquiFlow.Guard
└── web/SquiFlow.Web

services/
└── core-api/SquiFlow.CoreApi

tests/
├── SquiFlow.Phase0.Specs
└── SquiFlow.Architecture.Specs
```

Do not pretend later architecture already exists. In particular, the current rewrite intentionally does not contain production PostgreSQL business persistence, SQLite/WAL business persistence/encryption, SyncApi, Worker runtime, Admin Web/Admin API, ZITADEL/OpenFGA/OpenBao integrations, or provider implementations merely as placeholders.

## 3. Global architecture invariants

Preserve these unless a canonical architecture decision is deliberately changed in the same work:

- C# / modern .NET; current baseline is .NET 10.
- Modular monolith first. A module boundary does not imply a network/service boundary.
- One capability has one authoritative business meaning. Web, Workstation, API, Sync, Worker, and Admin adapters must not independently redefine it.
- Host-neutral capability/kernel code must not depend on Avalonia, ASP.NET Core, Windows APIs, PostgreSQL/SQLite provider APIs, identity/authorization provider SDKs, OpenBao SDK details, scheduler/actor runtimes, brokers, or provider-specific telemetry SDKs.
- Workstation local state may be local/provisional; central/security-sensitive authority remains server-side when the architecture says so.
- Web is online-only for business operations in the current baseline.
- Guard is external supervision/recovery coordination, not business authority.
- PostgreSQL is the selected central transactional store when central persistence is introduced.
- SQLite/WAL is the selected Workstation local store when local persistence is introduced.
- Provider/framework adapters stay at the edge of the owning capability/host.
- Physical project/process/interface splits are earned by a real compile-time, replacement, deployment, security, fault, packaging, or resource boundary.
- Same-process modules communicate in process by default.
- New architecture mechanisms are selected from the workload, not from pattern popularity.

## 4. Code style and implementation conventions

These are design-review rules, not reasons to create ceremony.

### Naming

- Use intention-revealing business names.
- Prefer domain language already defined in SquiFlow docs over generic `Manager`, `Processor`, `Handler`, `Helper`, `Data`, `Info`, `Thing`, or `Util` names.
- Avoid unexplained abbreviations.
- Use stable domain IDs/value types when identity has meaning; do not use raw strings everywhere for convenience.

### Functions and control flow

- Keep methods small enough that one responsibility is obvious, but do not split code into forwarding-only helpers to satisfy an arbitrary line count.
- Prefer guard clauses for invalid/precondition paths when they reduce nesting.
- Keep one level of abstraction where practical; extract genuinely lower-level detail when it obscures the use case.
- Queries should not hide surprising durable mutations. Commands may return an explicit result/outcome; do not apply Command-Query Separation as a dogma that forbids useful command results.
- Fail fast on invalid startup configuration, violated internal invariants, impossible states, and unsupported versions. External/business failures should return stable explicit outcomes rather than crashing the process when recovery is expected.

### Data and mutation

- Prefer immutable records/value objects/snapshots for contracts, configuration revisions, messages, and facts where mutation is not part of the domain meaning.
- Do not mutate historical/issued truth in place when the architecture requires revision/correction.
- Keep authoritative, provisional, derived, cached, and presentation-only state conceptually distinct.
- Avoid hidden global mutable state/singletons as business authority.

### Constants and configuration

- No unexplained magic business/config values in logic.
- Extract a named constant/value object/config setting when the value has stable meaning.
- Do not turn every literal (`0`, `1`, empty string, small local timeout in a test) into a global constant when the meaning is already obvious and local.

### SOLID / patterns / abstractions

Use SOLID as pressure-testing guidance, not an abstraction quota.

- A type should have a coherent reason to change.
- Depend on abstractions only where a real inversion/replacement/test seam is architecturally justified.
- Prefer minimal public surface; `internal`/private by default when external use is not required.
- Subtypes/adapters must preserve the contract they claim to implement.
- Do not create an interface merely because a class exists or can be mocked.
- Do not create forwarding-only service/manager/helper layers.
- Generic `IRepository<T>`, universal `IUnitOfWork`, and one-interface-per-class conventions are not baseline.
- Do not add a design pattern unless it solves a named current pressure.
- DRY applies primarily to duplicated knowledge/business meaning, not to every similar-looking line. Small local duplication can be safer than a false shared abstraction.
- Prefer KISS/YAGNI, but do not use simplicity as an excuse to omit required durability, security, recovery, compatibility, or observability.

## 5. Boundary validation and defensive programming

- Treat user input, wire payloads, provider responses, persisted historical payloads, and file metadata/content as untrusted at their boundary.
- Validate structure, size, count, ranges, version, and business meaning where owned.
- Do not blindly “sanitize” every input and then assume it is safe for every output context; validate input and encode/sanitize at the actual output/use boundary.
- Use parameterized DB access and allow-listed dynamic identifiers/operators when persistence is introduced.
- Apply least privilege to users, processes, provider credentials, and future DB identities.
- Retries must have a named owner and bounded budget.
- Semantic idempotency is explicit for retryable mutations; HTTP method semantics alone are not proof of application idempotency.
- Circuit breakers, caches, brokers, retries, and fallback paths are optional mechanisms, not mandatory patterns. Use them only when their failure semantics are understood and the workload earns them.

## 6. Explicit DO NOT list

Unless a current focused owner/decision explicitly justifies it, DO NOT:

- create fake business persistence with an in-memory dictionary and present it as a real durable implementation;
- create empty future projects/executables just to match an architecture diagram;
- put business rules in controllers, Avalonia view models, Blazor components, Guard, transport serializers, or provider adapters;
- add HTTP/gRPC calls between modules running in the same process;
- create a second source of business truth for Web, Workstation, Sync, Worker, or Admin;
- add generic repository/unit-of-work/service-manager/helper/interface hierarchies without a real boundary;
- expose persistence/domain entities directly as API/UI contracts when a boundary-specific contract is required;
- rely on UI hiding, feature visibility, network location, or token claims alone as authoritative authorization;
- trust stale Workstation permission/config/limit snapshots as central authority;
- use fire-and-forget/in-memory queues for work that must survive restart;
- claim “exactly once”, “eventually consistent”, “stateless”, “zero downtime”, “high availability”, or “secure” without defining the exact scoped guarantee and evidence;
- add GraphQL, BFF, Redis, Kafka/broker, Kubernetes, service mesh, schema-registry service, event sourcing, CQRS infrastructure, or a new database merely because they are common architecture patterns;
- add provider SDK types to host-neutral contracts;
- write raw secrets/tokens/key material to source, config committed to git, logs, traces, diagnostics, or desktop artifacts;
- weaken correctness/security/recovery because a later phase is expected to “harden it”; once a real responsibility is introduced, implement the correctness obligations of that responsibility now;
- silently resolve a documented open decision inside implementation without updating the owning architecture/decision record.

## 7. Testing and validation

The current rewrite uses executable spec projects rather than a conventional `dotnet test` suite. Run the actual repository commands.

### Minimum repository verification

From repository root:

```bash
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release

dotnet run --project tests/SquiFlow.Phase0.Specs/SquiFlow.Phase0.Specs.csproj -c Release
dotnet run --project tests/SquiFlow.Architecture.Specs/SquiFlow.Architecture.Specs.csproj -c Release
```

Run both spec executables after code/structure changes unless the environment makes execution impossible. For documentation-only changes, still run the architecture/spec checks when practical because documentation may describe paths/project structure that those specs protect.

### Test design rules

- Tests must be deterministic, order-independent, and self-validating.
- A fast pure test is preferred for pure business invariants.
- Do not replace provider-specific verification with mocks when the behavior being claimed is provider-specific (SQLite locking/WAL, PostgreSQL isolation/RLS, ASP.NET middleware, etc.).
- Use the real layer required to prove the claim.
- Add negative/hostile tests for authority and tenant boundaries, not only happy paths.
- Preserve historical fixtures once compatibility/version behavior exists; do not regenerate every fixture from the latest model and accidentally erase old-version coverage.
- Failure/restart/retry/recovery state machines require tests of interruption and ambiguous outcomes, not only success.

## 8. Definition of done for a change

Before considering a change complete:

1. Identify the owner document and current implemented boundary.
2. Implement the smallest correct change; do not add speculative architecture.
3. Add/update tests for the responsibility changed.
4. Run the relevant verification commands above plus any closer scoped `AGENTS.md` checks.
5. Check that architecture dependencies still point inward correctly.
6. Check logs/errors do not disclose secrets or sensitive payloads.
7. Update focused architecture/decision/implementation docs if behavior or a material decision changed.
8. State any validation that could not be performed; do not claim it passed.

## 9. Documentation and comments

- Code should be readable through names and structure first.
- Comments explain why, invariants, non-obvious failure semantics, compatibility constraints, or provider quirks; do not narrate obvious syntax.
- XML/API documentation is useful for public contracts where consumers need semantics, especially failure/version/idempotency behavior.
- Do not duplicate entire canonical architecture decisions in code comments. Link/name the owner where a non-obvious constraint needs context.

## 10. Change discipline

- Preserve Chesterton's Fence: before removing an odd boundary/check, find why it exists in current docs/tests/history.
- Leave touched code cleaner when that cleanup is local and does not expand the task into an unrelated refactor.
- If a real requirement needs a future boundary earlier than the roadmap expected, pull the owning architecture responsibility forward deliberately; do not create a temporary shortcut intended to be replaced “later”.
