# Phase 0 Documentation-First Rewrite

**Status:** Active rewrite; not yet a completed Phase-0 gate.

## Why this rewrite exists

The previous live implementation was created before the full repository architecture/reference context had been reconciled. It is therefore not treated as an implementation baseline.

On branch `rewrite/phase-0-docs-first`, commit `82cbbf24101abaeefdb1d4c9f81bd762f07bed88` removed the previous application/source projects, tests, solution/build files and `.gitlab-ci.yml`. The documentation corpus and decision history were preserved as the source basis for the rewrite.

## Source precedence used by this rewrite

```text
focused canonical owner documents
        ↓
CURRENT_DECISIONS / accepted focused decisions
        ↓
Phase-0 implementation package
        ↓
older project/reference discussions
```

Older references are used to recover intent and context, but they do not override a newer focused owner.

## Rebuilt Phase-0 structure

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

Only currently justified Phase-0 executables/projects are recreated. No empty future WebApi, SyncApi, Worker, Admin, Diagnostics, Maintenance, Sync or Document executable is scaffolded.

## What is intentionally implemented now

- .NET 10 / C# 14 repository baseline;
- narrow SquiFlow-owned application kernel;
- stable module/feature/permission/setting/tenant/experiment identifiers;
- deterministic module graph and host filtering;
- `DeviceLocal`, `LocalProvisional`, `ServerAuthoritative` execution vocabulary;
- release-channel/offline feature publication model;
- stable experiment bucketing primitive;
- typed capability-owned settings with platform ceiling support;
- one host-neutral Customers capability with the documented non-empty customer-name invariant;
- one Customers Workstation presentation contribution;
- Avalonia Workstation shell that composes explicit contributions;
- external Guard process with bounded restart behavior only;
- thin Blazor tenant Web shell;
- thin ASP.NET Core CoreApi with health/lifecycle composition only;
- bounded local structured logging plus optional OTLP log export;
- local executable Phase-0 and architecture specs.

## What is intentionally NOT implemented

The rewrite does not pretend later-phase responsibilities exist:

- PostgreSQL business persistence;
- SQLite/WAL business persistence or encryption;
- ZITADEL/OpenFGA integration;
- Workstation synchronization or OperationEnvelope persistence;
- WebApi/SyncApi split;
- Worker/scheduler;
- Admin Web/Admin API;
- OpenBao integration;
- durable outbox/jobs;
- document/diagnostic/maintenance helper processes;
- object-store/backup providers;
- financial/inventory/payment authority.

Those remain owned by their later phases unless a real requirement explicitly pulls the owning foundation forward.

## CI/CD status

The repository CI/CD pipeline definition is intentionally absent because the current user directive explicitly requested removal of live pipelines and CI/CD while the implementation is rewritten from the documentation baseline.

This creates a deliberate mismatch with Phase 0D/0F text that expects CI evidence. The rewrite does **not** silently resolve that contradiction. Therefore Phase 0 cannot be declared fully complete while that verification requirement remains accepted and repository CI/CD remains intentionally absent.

For now, verification is local/manual:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release
dotnet run --project tests/SquiFlow.Phase0.Specs/SquiFlow.Phase0.Specs.csproj -c Release
dotnet run --project tests/SquiFlow.Architecture.Specs/SquiFlow.Architecture.Specs.csproj -c Release
```

No passing result is claimed until these commands are actually executed in an environment with the selected .NET SDK.

## Current interpretation

The rewrite is a clean implementation candidate derived from the documentation. It is not a production-ready system and it does not make later-phase behavior appear complete through placeholders or in-memory substitutes.
