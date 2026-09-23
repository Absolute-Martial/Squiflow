# Server Service Instructions

These rules apply below `services/` in addition to the root instructions.

## Current state

The current service executables are `Application.CoreApi` for public bootstrap/liveness plus configured JWT validation, authenticated account resolution, active tenant-membership listing, and tenant workspace/order-draft create/read/browse/abandon protected by current membership plus pinned OpenFGA permission checks; `Application.DatabaseMigrator` applies ordered one-shot IdentityAccess, Tenancy and Orders PostgreSQL migrations. CoreApi uses Autofac as its root provider, Finbuckle only for untrusted route-candidate resolution, and ASP.NET Core resource authorization with the official OpenFGA SDK for narrow workspace and order checks. It composes the capability adapters over one explicitly bounded/resetting Npgsql data source, requires bounded non-permissive host/trust/cache/resource/authorization configuration, and owns the membership-derived `TenantContext` resolver. The narrow Orders operation is immutable priced draft create/read, bounded cursor browse, and one-way abandon. Create, view and abandon use distinct `order_creator`, `order_viewer`, and `order_abandoner` permissions. Abandon requires an expected revision, records the abandoning account and timestamp, increments revision and commits with caller-scoped idempotency; PostgreSQL permits only the required lifecycle-column updates under explicit tenant predicates and forced RLS. It preserves priced draft content and the original create receipt, and does not add editing, deletion, or a broader order lifecycle or text-search surface. CoreApi also contains bounded internal profile-runtime mechanics, which no production path acquires yet. It contains no external PostgreSQL pooler, durable profile authority, implementation variant, role/tuple administration, Worker or broader business lifecycle. DbMigrator requires an explicit deployment advisory-lock identity and timeout; it owns no seeding, per-tenant loop, identity placeholder, scheduler or application startup behavior.

Do not create `core-api/`, `web-api/`, `sync-api/`, `admin-api/` or `worker/` merely because those folders exist in the growth map.

## Role

A service executable is a transport/workload/security/deployment composition boundary. It does not automatically own business meaning merely because traffic enters there.

When a service is introduced, the same change should define:

- the exact workload/responsibility that earns the process boundary;
- authority and state ownership;
- failure/recovery behavior;
- authentication/authorization exposure;
- cancellation/backpressure/resource limits where applicable;
- health/readiness/shutdown semantics;
- verification at the real host boundary.

## API host rules

When an API host exists:

- endpoints/controllers stay thin: establish/validate transport context, map explicit contracts, call the owning capability application/query surface, map explicit result/error contracts;
- authentication is necessary but not sufficient for protected operations; current authorization/resource/tenant/domain checks remain below transport;
- arbitrary DB/provider access must not be performed directly from endpoints merely because the host can resolve the provider;
- request limits, cancellation/deadlines, correlation, stable error/Problem Details mapping, health/readiness and graceful shutdown belong at the host boundary.

## Authoritative application boundary

Below transport, authoritative capability application behavior owns as applicable:

- current resource/business authorization;
- current facts/rules/policies;
- business invariants/state transitions;
- idempotency/concurrency;
- transaction/outbox;
- capability-owned persistence interaction.

## Earned workload splits

A future interactive WebApi and Workstation SyncApi may be separate ingress hosts because their protocol/batching/fairness/latency/device-context workloads differ. They still converge on the same authoritative capability implementation.

Admin API, when implemented, is a separate private control-plane security boundary and must not be proxied through an ordinary tenant API as its normal path.

Worker, when implemented, executes durable work through the same owning capability application behavior; it is not another business implementation.

## DO NOT

- Do not create a service folder/project just for symmetry with the target diagram.
- Do not put business rules in controllers/endpoints/middleware.
- Do not add a network `CoreApi` hop solely to centralize in-process module calls.
- Do not expose direct PostgreSQL access to clients.
- Do not accept client-provided TenantId as authority.
- Do not trust gateway/private-network/Tailscale evidence as application authorization by itself.
- Do not blindly retry non-idempotent mutations or ambiguous provider effects.
- Do not add generic force-success/run-SQL/set-anything privileged endpoints.

## Validation

When functionality exists, test the real ASP.NET/host pipeline for the behavior actually claimed: authentication/session/authorization mapping, cross-tenant denial, malformed/oversized input, cancellation/timeouts, stable errors, dependency outage behavior and graceful shutdown. Provider/database-specific claims require integration tests against the actual provider rather than only mocked endpoints.
