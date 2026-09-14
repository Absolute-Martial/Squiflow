# SquiFlow Phase-0 documentation-first rewrite

The current implementation on `rewrite/phase-0-docs-first` was rebuilt after deleting the previous live source tree and repository CI/CD definition. The implementation is derived from the accepted architecture and Phase-0 owner documents rather than from the deleted code.

Read first:

- `docs/implementation/phases/phase-0/DOCS_FIRST_REWRITE.md`
- `docs/implementation/phases/phase-0/README.md`
- `docs/architecture/REPOSITORY_STRUCTURE.md`
- `docs/architecture/APPLICATION_KERNEL_AND_MODULES.md`
- `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`
- `docs/workstation/PRESENTATION_ARCHITECTURE.md`
- `docs/workstation/GUARD_AND_RECOVERY.md`

## Current implementation boundary

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

There is one Customers business meaning. `SquiFlow.Customers.Workstation` is only an Avalonia presentation adapter. CoreApi currently composes the capability but deliberately exposes no fake Customers CRUD/persistence endpoint.

## Platform independence

Host-neutral kernel/capability code targets plain `net10.0` and must remain free of Avalonia, ASP.NET Core, Windows APIs, PostgreSQL/SQLite provider APIs, ZITADEL/OpenFGA/OpenBao provider SDKs, Worker/scheduler runtimes, brokers and provider-specific telemetry dependencies.

Host/framework dependencies belong at host or provider adapter boundaries.

## Manual verification

Repository CI/CD is intentionally absent under the current rewrite directive. Run locally:

```bash
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release

dotnet run --project tests/SquiFlow.Phase0.Specs/SquiFlow.Phase0.Specs.csproj -c Release
dotnet run --project tests/SquiFlow.Architecture.Specs/SquiFlow.Architecture.Specs.csproj -c Release
```

Run current hosts:

```bash
dotnet run --project services/core-api/SquiFlow.CoreApi/SquiFlow.CoreApi.csproj
dotnet run --project apps/web/SquiFlow.Web/SquiFlow.Web.csproj
dotnet run --project apps/desktop/workstation/SquiFlow.Workstation/SquiFlow.Workstation.csproj
```

Guard requires the Workstation executable path either as the first argument or through `SQUIFLOW_WORKSTATION_PATH`:

```bash
dotnet run --project apps/desktop/guard/SquiFlow.Guard/SquiFlow.Guard.csproj -- path/to/SquiFlow.Workstation
```

## Deliberately not present

- `.gitlab-ci.yml` or another repository CI/CD pipeline definition;
- PostgreSQL business persistence;
- SQLite/WAL business persistence/encryption;
- SyncApi/Workstation synchronization;
- Worker/Proto.Actor/Quartz runtime;
- Admin Web/Admin API;
- ZITADEL/OpenFGA/OpenBao integrations;
- object-storage/backup provider implementations;
- fake business data or in-memory substitutes presented as durability.

These omissions are explicit. Later-phase responsibilities are not represented by empty projects or temporary production paths.
