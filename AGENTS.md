# SquiFlow Repository Instructions for Coding Agents

This is the repository-wide instruction map. A closer `AGENTS.md` in a subtree adds to or overrides these rules for that subtree.

## 1. Current implementation truth

SquiFlow is the internal development codename. It is not a public product/tenant brand and must not appear as a runtime branding fallback, user-facing message, public telemetry namespace, deployment credential/configuration name, or new wire/persistence contract. Existing source namespaces, project paths and historical records may retain the codename until a final product name is deliberately adopted.

The current product version is locked at **v0.1.0** until the complete production-capable product gate is explicitly qualified. API/schema/migration versions and Git revisions are separate compatibility or traceability identifiers and do not change the product version. Phase 0A is the qualified reset baseline. The former Phase 0B Parties implementation was purged on 2026-09-17 and is retained only as historical evidence.

The current tree contains seven production projects, six test projects, `SquiFlow.slnx`, CoreApi, DbMigrator, and a repository build/test contract. The narrow Branding, IdentityAccess, Tenancy, CoreApi, PostgreSQL migration and internal Autofac runtime claims described by `README.IMPLEMENTATION.md` are implemented; broader business, Web, Workstation, SyncApi, AdminApi and Worker responsibilities remain `NOT_INTRODUCED`. There is no current CI verification wrapper; `BLOCKED = none`.

The deleted `PartyKind` enum and its scaffolding must not be treated as a current baseline or recreated by memory. The accepted domain language remains available to a later useful capability journey, which may choose a different representation.

```text
folder exists
    != project exists
    != runtime exists
    != responsibility is implemented
```

Do not infer implementation from directory names, historical branches, old merge requests, diagrams, or phase labels.

Read first:

- `README.IMPLEMENTATION.md` — current implementation truth and active-slice rule.
- `docs/product/PRODUCT_IDENTITY_AND_VERSIONING.md` — codename, public identity and locked product-version rules.
- `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md` — purge decision and removed inventory.
- `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` — source-first routing for future implementation.
- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md` — canonical scope/quality gate.
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` — evidence permanence/requalification owner.
- `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` — qualified reset baseline and enduring 0A guarantees.
- `docs/implementation/phases/phase-0/0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md` and `0B_STATUS.md` — retired historical 0B record.
- `docs/domain/BUSINESS_TERMS.md` — accepted versus discovery-sensitive domain language.
- `docs/architecture/ENGINEERING_PRINCIPLES.md` — engineering philosophy.
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md` — boundary/abstraction rules.
- `docs/architecture/REPOSITORY_STRUCTURE.md` — ownership and executable growth map.
- `docs/architecture/REPOSITORY_FOLDER_STRUCTURE.md` — folder-only current/growth map.
- `docs/decisions/CURRENT_DECISIONS.md` — accepted current decisions.
- `docs/decisions/OPEN_DECISIONS.md` — intentionally unresolved choices.
- the focused owner for the responsibility being implemented.

Historical implementation/review/phase material remains evidence and context, not current authority when it conflicts with a current focused owner, accepted decision, or active gate.

### Decision precedence

When documents differ, prefer:

1. current focused canonical owner for the exact responsibility;
2. current accepted decision record;
3. current active implementation/gate record;
4. historical review/source-study/branch/MR material.

Do not silently resolve an open decision in code.

## 2. Production-honest gate model

A phase is not a checklist and a phase number does not authorize implementation.

For every material active responsibility, use exactly one state:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

- `NOT_INTRODUCED` means the responsibility is genuinely absent and no claimed path depends on it.
- `PRODUCTION_HONEST` means the declared scope is trustworthy for the guarantees it claims, with falsifiable evidence and a permanent/recurring regression guard.
- `BLOCKED` means the responsibility has been introduced but is not yet trustworthy for its claim. `BLOCKED` cannot be carried forward as “later hardening.” Finish it or un-introduce it.

Use the **smallest production-honest scope**. Breadth may be narrow; quality/depth of an introduced claim may not be prototype-grade.

Future governance detail is also earned. Do not prebuild future subphase structures, evidence matrices, cadences, providers, or failure inventories merely because a roadmap label exists.

## 3. Scoped instructions

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

## 4. Global architecture invariants

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

## 5. Code conventions

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
- KISS/YAGNI reduce accidental/speculative breadth; they do not justify omitting required correctness, durability, security, recovery, compatibility, resource bounds or observability for a responsibility already introduced.

## 6. Explicit DO NOT list

Unless a current focused owner and declared production-honest scope explicitly earn it, DO NOT:

- restore the old implementation tree by memory, symmetry or copy/paste from Git history;
- use a phase/subphase label as authority to create code or infrastructure;
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
- carry an introduced `BLOCKED` responsibility into a later phase as if it were `NOT_INTRODUCED`;
- treat architecture/review documentation as proof that runtime code already exists.

Do not recreate the purged Parties slice, solution, build/package files, tests, or CI simply to make the repository appear implemented. Any later introduction must be independently earned from a useful real responsibility.

## 7. Boundary validation

- Treat user/wire/provider/file/persisted historical input as untrusted at its boundary.
- Validate shape, size, count, ranges, version and owned business meaning.
- Encode/sanitize at the actual output/use context; do not assume generic input sanitization makes data universally safe.
- Use parameterized DB access and allow-listed dynamic identifiers/operators when persistence returns.
- Apply least privilege to users, processes, DB identities and provider credentials.
- Retries need an owner and a bounded budget.
- Semantic idempotency is explicit for retryable mutations; HTTP method names are not proof of application idempotency.
- Circuit breakers, caches, brokers and fallback paths are optional workload-earned mechanisms.

## 8. Testing and evidence

There is currently no test project, solution, repository-owned build/test command, or active CI verification contract. Do not claim restore, build, test, or CI success for the post-purge tree. The old 0B commands and GitHub/GitLab evidence are historical only.

When real executable code returns, introduce the smallest test/build/CI surface required to prove its declared claims. Select the test framework and commands from the actual runtime/toolchain needs rather than recreating the deleted setup automatically.

For every material active claim, identify:

```text
claim / owner
falsifiable evidence
permanent or recurring regression guard
requalification trigger
known non-claims
```

Do not mock away the property being tested and do not write tests around an implementation shortcut and then redefine the shortcut as the contract.

## 9. Definition of done

Before considering an active slice complete:

1. identify the focused owner and current implementation truth;
2. state the production intent and exact declared scope;
3. classify material responsibilities as `NOT_INTRODUCED`, `PRODUCTION_HONEST`, or `BLOCKED`;
4. implement the smallest production-honest change, not the smallest happy path;
5. add/update falsifiable evidence appropriate to every introduced material claim;
6. add or name the permanent/recurring regression guard and requalification trigger;
7. preserve dependency/authority direction;
8. satisfy applicable failure/recovery/security/concurrency/compatibility/resource/observability obligations created by the slice;
9. verify logs/errors do not disclose sensitive material;
10. update focused architecture/decision/phase docs when behavior or a material decision changes;
11. ensure `BLOCKED = none` before gate qualification;
12. state verification that could not be performed; never claim an unrun check passed.

## 10. Documentation and change discipline

- Code should explain itself through names and structure first; comments explain why, invariants, failure semantics, compatibility constraints or provider quirks.
- Do not duplicate large canonical decisions in code comments or agent files; point to the owner.
- Preserve Chesterton's Fence: understand why a boundary/check exists before removing it.
- Leave touched code cleaner when cleanup is local and does not become an unrelated refactor.
- If a future boundary becomes necessary earlier than the roadmap expected, pull the owning responsibility forward deliberately and qualify it now instead of creating a temporary unsafe shortcut.
- If historical work contains a useful idea, re-earn it from current facts; history is input, not authority.
