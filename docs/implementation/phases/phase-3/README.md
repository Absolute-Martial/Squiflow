# Phase 3 — Authoritative Server Persistence and Synchronization

## Purpose

Phase 3 connects local durable intent to real central authority and qualifies PostgreSQL using the actual workload rather than synthetic CRUD.

This does **not** mean server persistence, API contracts, or capability application code are forbidden before Phase 3. Real modules may begin earlier. Phase 3 is the point where the authoritative persistence + synchronization path is qualified as a dependable cross-device/server foundation.

## Subphases

```text
3A  PostgreSQL authoritative persistence and tenant isolation
3B  Authoritative admission, idempotency and concurrency
3C  Sync upload/download, cursor and backpressure
3D  Version overlap, migrations and compatibility
3E  Integrated Phase-3 gate
```

## Phase maturity added

After Phase 3 the same capability can have local/provisional Workstation execution and server-authoritative admission/commit without becoming two independent business implementations.

## Continuing development

Customers, Orders and any other real capability may gain authoritative queries/commands, local projections, Web/Workstation adapters, and compatibility fixtures as needed. The phase constrains correctness/authority; it does not restrict which business capability may be developed.