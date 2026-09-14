# SquiFlow Repository Instructions for Coding Agents

This is the repository-wide instruction map. A closer `AGENTS.md` in a subtree adds to or overrides these rules for that subtree.

## 1. Current implementation truth

SquiFlow is currently at the **v0.0.20 principles-first reset baseline**.

There are currently no production/test `*.csproj` projects in the implementation baseline. `SquiFlow.sln` is an empty rebuild container. Source-area folders may exist because architecture documentation or scoped `AGENTS.md` guidance reserves ownership, but:

```text
folder exists
    != project exists
    != runtime exists
    != responsibility is implemented
```

Do not infer implementation from directory names.

Read first:

- `README.IMPLEMENTATION.md` — current reset truth and rebuild rule.
- `docs/architecture/ENGINEERING_PRINCIPLES.md` — engineering philosophy.
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md` — boundary/abstraction rules.
- `docs/architecture/REPOSITORY_STRUCTURE.md` — ownership and executable growth map.
- `docs/architecture/REPOSITORY_FOLDER_STRUCTURE.md` — folder-only current/growth map.
- `docs/decisions/CURRENT_DECISIONS.md` — accepted current decisions.
- `docs/decisions/OPEN_DECISIONS.md` — intentionally unresolved choices.
- the focused owner and relevant phase package for the responsibility being implemented.

Historical Phase-0 rewrite documents remain evidence/history, not current implementation truth when they conflict with the principles-first reset.

### Decision precedence

When documents differ, prefer:

1. current focused canonical owner for the exact responsibility;
2. current accepted decision record;
3. current implementation/phase owner;
4. historical review/source-study material.

Do not silently resolve an open decision in code.

## 2. Scoped instructions

Read the closest applicable file when working below these paths:

```text
foundation/
modules/
apps/web/
apps/desktop/workstation/
apps/desktop/guard/
services/
tests/
docs/
```

Do not add more nested `AGENTS.md` files unless a subtree genuinely needs different rules from its parent.

## 3. Global architecture invariants

Preserve these unless a deliberate architecture decision changes them in the same work:

- C# / modern .NET; current toolchain baseline is .NET 10.
- Modular monolith first. A module/bounded context is not automatically a network service.
- One capability owns one business meaning. Web, Workstation, Sync, Worker, Admin and API hosts must not independently redefine it.
- Host-neutral capability/foundation code must remain free of UI, transport, OS, DB-provider, identity-provider, authorization-provider, key-management-provider, scheduler/actor/broker and vendor-telemetry dependencies unless the owning boundary explicitly permits them.
- Workstation local state may be provisional/local; central/security-sensitive authority remains server-side where defined.
- Web remains online-only for business operations unless that architecture is deliberately changed.
- Guard is external supervision/recovery coordination, not business authority.
- PostgreSQL is the selected central transactional store when central persistence is reintroduced.
- SQLite/WAL is the selected Workstation local store when local persistence is reintroduced.
- Same-process capability interaction is in-process by default.
- Physical projects, processes, interfaces, databases and protocols are earned by a real compile-time/provider/platform/security/fault/lifecycle/resource/workload boundary.
- New mechanisms are selected from workload/invariants/evidence, not pattern popularity.

## 4. Code conventions

Use these as review guidance, not ceremony quotas.

- Use intention-revealing business names and existing SquiFlow terminology.
- Avoid vague `Manager`, `Helper`, `Util`, `Data`, `Info`, `Thing` names unless the term genuinely describes the responsibility.
- Prefer guard clauses where they reduce nesting.
- Keep a method at one useful level of abstraction; do not split code into forwarding-only helpers to satisfy arbitrary size rules.
- Queries must not hide surprising durable mutations. Commands may return explicit outcomes/results.
- Fail fast for invalid startup configuration, impossible internal states and unsupported versions; model expected business/external failures explicitly.
- Prefer immutable records/value objects/snapshots for contracts, revisions and facts where mutation is not domain meaning.
- Keep authoritative, provisional, derived, cached and presentation-only state distinct.
- Avoid unexplained magic business/config values.
- Keep public surface minimal; `internal`/private by default when external use is not required.
- SOLID is pressure-testing guidance, not one-interface-per-class.
- DRY targets duplicated knowledge/business meaning, not every similar-looking line.
- KISS/YAGNI do not justify omitting required durability, security, recovery, compatibility or observability for a responsibility that already exists.

## 5. Explicit DO NOT list

Unless a current focused owner explicitly earns it, DO NOT:

- restore the old implementation tree by memory, symmetry or copy/paste from Git history;
- create empty future projects/executables/folders merely to match a diagram;
- create fake in-memory persistence and present it as durable business storage;
- put business rules in controllers, view models, Blazor components, Guard, serializers or provider adapters;
- add HTTP/gRPC between modules in the same process;
- create separate Web/Workstation/Sync/Worker/Admin business meanings;
- add generic `IRepository<T>`, universal `IUnitOfWork`, service-manager/helper/interface hierarchies without a real boundary;
- expose DB/domain entities directly as public API/UI contracts merely for convenience;
- rely on UI hiding, network location, feature visibility or token claims alone as authoritative authorization;
- trust stale Workstation snapshots as current central authority;
- use fire-and-forget/in-memory queues for work that must survive restart;
- claim exactly-once, HA, zero-downtime, statelessness or security without a scoped guarantee and evidence;
- add GraphQL, BFF, Redis, Kafka/broker, Kubernetes, service mesh, schema-registry service, CQRS/event-sourcing infrastructure or another database because it is fashionable/common;
- leak provider SDK types into host-neutral contracts;
- write secrets/tokens/key material to source, committed config, logs, traces or diagnostics;
- introduce a real boundary while deferring that boundary's correctness/security/recovery/compatibility obligations to unspecified later hardening;
- treat architecture/review documentation as proof that runtime code already exists.

## 6. Boundary validation

- Treat user/wire/provider/file/persisted historical input as untrusted at its boundary.
- Validate shape, size, count, ranges, version and owned business meaning.
- Encode/sanitize at the actual output/use context; do not assume generic input sanitization makes data universally safe.
- Use parameterized DB access and allow-listed dynamic identifiers/operators when persistence returns.
- Apply least privilege to users, processes, DB identities and provider credentials.
- Retries need an owner and a bounded budget.
- Semantic idempotency is explicit for retryable mutations; HTTP method names are not proof of application idempotency.
- Circuit breakers, caches, brokers and fallback paths are optional workload-earned mechanisms.

## 7. Testing and validation

The current reset has **no executable test/spec projects yet**. Do not run or document old Phase-0 project commands as if they are current.

For documentation-only work:

- verify referenced paths/owners against the current repository;
- check that accepted/current/open/implemented status is represented accurately;
- ensure folder/project/runtime examples are explicitly marked as current versus growth/illustrative.

When the first implementation project is reintroduced, that change must also establish the narrowest useful local verification path for the responsibility being added. Tests should be deterministic, independent and self-validating, while provider/framework/process claims must be tested at the real layer that can prove them.

Do not mock away the property being tested.

## 8. Definition of done

Before considering a change complete:

1. identify the focused owner and current implementation truth;
2. implement the smallest **complete** change, not the smallest happy path;
3. add/update verification appropriate to the responsibility that now exists;
4. preserve dependency/authority direction;
5. check failure/recovery/security/compatibility obligations made applicable by the change;
6. verify logs/errors do not disclose sensitive material;
7. update focused architecture/decision/phase docs when behavior or a material decision changes;
8. state validation that could not be performed; never claim an unrun check passed.

## 9. Documentation and change discipline

- Code should explain itself through names and structure first; comments explain why, invariants, failure semantics, compatibility constraints or provider quirks.
- Do not duplicate large canonical decisions in code comments or agent files; point to the owner.
- Preserve Chesterton's Fence: understand why a boundary/check exists before removing it.
- Leave touched code cleaner when cleanup is local and does not become an unrelated refactor.
- If a future boundary becomes necessary earlier than the roadmap expected, pull the owning responsibility forward deliberately instead of creating a temporary unsafe shortcut.
