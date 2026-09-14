# Phase 8C — Performance, Cache, Network, and Dependency Budgets

## Measure before optimizing

Use representative workloads and target hardware to measure API latency, DB pool waits, sync drain/reconnect, Worker backlog, object transfer, document peak memory and external dependency latency.

## Dependency budgets

For remote/provider composition define overall deadline, per-dependency timeout, retry owner, cancellation, bounded concurrency and required/degradable/optional behavior.

## Cache

Introduce/retain a cache only when measured workload benefit justifies it. Define tenant/permission-safe keys, freshness/invalidation, bounded capacity, stampede/cold-start behavior and outage bypass.

Never use stale cache as current authority for authorization/payment/stock/credit/hard limits.

## Network/edge

Edge/gateway owns TLS/routing/WAF/coarse admission/transport concerns. It never replaces backend auth, TenantContext, invariants, idempotency or durable limits.

WebSocket/SignalR/live signals remain non-authoritative; reconnect recovers from durable state.

## Strategy selection

Do not infer `high frequency = gRPC`, `many clients = GraphQL/BFF`, `large data = streaming`, or `performance problem = cache`. Each mechanism must be selected from the actual workload and measured trade-off.

## Exit gate

Every retained performance mechanism has measured benefit, bounded failure behavior and an explicit authority/freshness model.