# Server Service Instructions

These rules apply below `services/` in addition to the root instructions.

## Role

A service executable is a transport/workload/security/deployment composition boundary. It does not automatically own business meaning merely because traffic enters there.

The current rewrite contains only `services/core-api/SquiFlow.CoreApi`. Future WebApi/SyncApi/AdminApi/Worker boundaries are introduced only when their first real responsibility is implemented.

## API host rules

- Endpoints/controllers stay thin: establish/validate transport context, map explicit contracts, call the owning capability application/query surface, map explicit result/error contracts.
- Current authentication is necessary but not sufficient for protected operations; current authorization/resource/tenant/domain checks remain below transport.
- Do not allow arbitrary DB/provider access directly from endpoints merely because the host can resolve the provider.
- Keep request limits, cancellation/deadlines, correlation, Problem Details/error mapping, health/readiness, and graceful shutdown at the host boundary.

## Authoritative application boundary

Below transport, authoritative capability application behavior owns as applicable:

- current resource/business authorization;
- current facts/rules/policies;
- business invariants/state transitions;
- idempotency/concurrency;
- transaction/outbox;
- module-owned persistence interaction.

## Future workload splits

A future interactive WebApi and Workstation SyncApi may be separate ingress hosts because their protocol/batching/fairness/latency/device-context workloads differ. They still converge on the same authoritative capability implementation.

Admin API, when implemented, is a separate private control-plane security boundary and must not be proxied through an ordinary tenant API as its normal path.

Worker, when implemented, executes durable work through the same owning capability application behavior; it is not a third business implementation.

## DO NOT

- Do not put business rules in controllers/endpoints/middleware.
- Do not add a network `CoreApi` hop solely to centralize in-process module calls.
- Do not expose direct PostgreSQL access to clients.
- Do not accept client-provided TenantId as authority.
- Do not trust gateway/private-network/Tailscale evidence as application authorization by itself.
- Do not blindly retry non-idempotent mutations or ambiguous provider effects.
- Do not add generic force-success/run-SQL/set-anything privileged endpoints.

## Validation

When functionality exists, test the real ASP.NET pipeline for authentication/session/authorization mapping, cross-tenant denial, malformed/oversized input, cancellation/timeouts, stable errors, provider/security dependency outage behavior, and graceful shutdown. Provider/database-specific claims require integration tests against the actual provider rather than only mocked endpoints.
