# Foundation Instructions

These rules apply below `foundation/` in addition to the repository-root `AGENTS.md`.

## Purpose

Foundation contains narrow product-wide primitives and infrastructure-neutral implementation that multiple capabilities/hosts genuinely share. It is not a generic `Common`/`Shared`/`Utils` dumping ground.

The current v0.0.20 baseline has **no Foundation project yet**. A folder/instruction file reserves ownership; it does not require a project to exist.

## Architecture rules

- Keep Foundation host-neutral unless a specific subproject is explicitly a host/provider adapter.
- If an application-kernel project is introduced, it may own composition/module/feature/permission/settings/context primitives that real consumers have proven shared; it must not become the owner of capability business meaning.
- If a product observability Foundation project is introduced, it owns SquiFlow instrumentation primitives needed by real consumers; it must not become an operational business/audit database or vendor-specific domain dependency.
- Add a product-wide primitive only when its semantics are truly cross-product and the ownership cannot reasonably remain in a capability.
- Prefer capability-owned IDs/permissions/settings/features over central string catalogs that erase ownership.

## Dependency constraints

Host-neutral Foundation code must not reference:

- Avalonia or Windows UI APIs;
- ASP.NET Core transport types;
- PostgreSQL/SQLite provider types;
- ZITADEL/OpenFGA/OpenBao provider SDKs;
- Worker/scheduler/actor/broker runtimes;
- New Relic/OpenSearch/Backtrace provider SDKs where an OTEL/Serilog-neutral boundary is intended.

## Code conventions

- Prefer small immutable identifiers/value objects/records where identity/revision has meaning.
- Validate identifiers/definitions at construction or registration time when invalid state has no useful runtime meaning.
- Fail deterministically on duplicate IDs, dependency cycles, impossible contributions, or invalid definition graphs when those concepts actually exist.
- Keep public surface intentionally small.

## DO NOT

- Do not create a Foundation project merely because the architecture contains a `foundation/` ownership area.
- Do not move a type into Foundation merely because two projects use similar-looking code.
- Do not add generic repository, generic unit-of-work, service-locator, mediator, provider, result, handler, or utility frameworks without a demonstrated cross-product responsibility.
- Do not add host/provider conditionals (`if Windows`, `if ASP.NET`, `if PostgreSQL`) to keep one project artificially universal.
- Do not make tenant/telemetry context itself authorization authority.
- Do not create a test/spec project merely so Foundation appears to have verification before Foundation exists.

## Validation

Verification is introduced with the real Foundation responsibility, not before it.

When a Foundation project/shared primitive is first introduced:

1. add the narrowest tests/specs that prove its current semantics and edge cases;
2. add architecture/dependency checks only for boundaries that now physically exist;
3. use the approved `tests/` structure when a separate test project is actually justified;
4. do not require a pre-named `SquiFlow.Architecture.Specs` project or recreate an old test project by memory.

If the first 0B code is capability-local and shared Foundation is not yet earned, test the capability first. Extract Foundation and its corresponding architecture checks only when current consumers prove that boundary is real.
