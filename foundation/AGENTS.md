# Foundation Instructions

These rules apply below `foundation/` in addition to the repository-root `AGENTS.md`.

## Purpose

Foundation contains narrow product-wide primitives and infrastructure-neutral implementation that multiple capabilities/hosts genuinely share. It is not a generic `Common`/`Shared`/`Utils` dumping ground.

## Architecture rules

- Keep Foundation host-neutral unless a specific subproject is explicitly a host/provider adapter.
- `SquiFlow.ApplicationKernel` defines composition/module/feature/permission/settings/context primitives; it must not become the owner of capability business meaning.
- `SquiFlow.Observability` owns SquiFlow instrumentation primitives; it must not become an operational business/audit database or vendor-specific domain dependency.
- Add a product-wide primitive only when its semantics are truly cross-product and the ownership cannot reasonably remain in a capability.
- Prefer capability-owned IDs/permissions/settings/features over central string catalogs that erase ownership.

## Dependency constraints

Host-neutral foundation code must not reference:

- Avalonia or Windows UI APIs;
- ASP.NET Core transport types;
- PostgreSQL/SQLite provider types;
- ZITADEL/OpenFGA/OpenBao provider SDKs;
- Worker/scheduler/actor/broker runtimes;
- New Relic/OpenSearch/Backtrace provider SDKs where an OTEL/Serilog-neutral boundary is intended.

## Code conventions

- Prefer small immutable identifiers/value objects/records where identity/revision has meaning.
- Validate identifiers/definitions at construction or registration time when invalid state has no useful runtime meaning.
- Fail deterministically on duplicate IDs, dependency cycles, impossible host contributions, or invalid definition graphs.
- Keep public surface intentionally small.

## DO NOT

- Do not move a type into Foundation merely because two projects use similar code.
- Do not add generic repository, generic unit-of-work, service-locator, mediator, provider, result, handler, or utility frameworks without a demonstrated cross-product responsibility.
- Do not add host/provider conditionals (`if Windows`, `if ASP.NET`, `if PostgreSQL`) to keep one project artificially universal.
- Do not make `TenantContext`/telemetry context itself authorization authority.

## Validation

For Foundation changes run the root verification commands and ensure `SquiFlow.Architecture.Specs` covers any new forbidden dependency/invariant introduced by the change.
