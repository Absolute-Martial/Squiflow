# SquiFlow Phase-0 implementation foundation

This implementation batch starts the accepted Phase-0 architecture on .NET 10 LTS.

## Boundary rule

There is **one shared business module**, not a separate copy for Workstation and server.

```text
SquiFlow.Customers                       host-neutral semantics
├── domain/value meaning
├── feature/permission/setting definitions
└── stable contracts

SquiFlow.Customers.Workstation           Avalonia presentation adapter
└── desktop workspace contribution

SquiFlow.Customers.Api                   ASP.NET Core transport adapter
└── explicitly reviewed endpoints
```

The Workstation and Core API therefore reuse business meaning without pretending that their application flows have the same authority. A future offline `CaptureOrder` flow and server `ApplySyncedOrder` flow can be separate orchestrators while sharing domain rules and semantic contracts.

## Platform independence

All shared kernel and module projects target plain `net10.0`. They contain no Avalonia, ASP.NET Core, Windows, SQLite, PostgreSQL, ZITADEL, OpenFGA, Quartz, Proto.Actor, or provider SDK references.

Platform/host packages appear only in host-specific adapters.

## Run

```bash
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release

dotnet run --project tests/SquiFlow.ApplicationKernel.Specs
dotnet run --project tests/SquiFlow.Architecture.Specs

dotnet run --project services/core-api/SquiFlow.CoreApi
dotnet run --project apps/web/SquiFlow.Web
dotnet run --project apps/desktop/workstation/SquiFlow.Workstation
```

Guard expects the supervised executable as its first argument:

```bash
dotnet run --project apps/desktop/guard/SquiFlow.Guard -- path/to/SquiFlow.Workstation [args...]
```

## Deliberately not present

- Worker/Proto.Actor/Quartz: accepted for Phase 6, not created before the first durable workload.
- SQLite/PostgreSQL implementation: selected technologies, qualified in their owning phases.
- ZITADEL/OpenFGA SDKs: Phase 1 adapters, not allowed in shared modules.
- fake dashboards or fake business data.
