# Phase 2 — Local-First Workstation Durability and Guard Recovery

## Purpose

Phase 2 turns the Workstation from a presentation shell into a production-shaped local-capable runtime for the first real Customer/Order slice.

This is not a rule that only Customer/Order can exist. Other capabilities may continue or gain local state when their semantics fit the same qualified local foundation. Likewise, capability/domain/UI work may exist before Phase 2; this phase is where local durable execution itself becomes qualified.

## Subphases

```text
2A  First real local-capable business slice
2B  SQLite/WAL, encryption and atomic local durability
2C  Local outbox, provisional state and restart semantics
2D  Guard/update/migration/recovery coordination
2E  Integrated Phase-2 gate
```

## Phase maturity added

After Phase 2, an explicitly local-capable Workstation action can become durably local without a network round trip and survive supported process/restart failure, while still remaining distinct from server-authoritative acceptance.

## Continuing development

Customers, Orders, other modules, Web/API, identity/authorization, observability, deployment and testing may continue in parallel. Any capability that adopts local durability must respect the same encryption/migration/recovery/outbox foundations rather than create its own ad-hoc local store.