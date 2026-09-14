# Phase 3C — Sync Upload/Download, Cursor, and Backpressure

## Real Sync boundary

Create/qualify a dedicated Sync workload boundary when the real implementation now justifies it. A compact current CoreApi may evolve into SyncApi/WebApi deliberately; do not keep duplicate business logic across hosts.

## Upload

Use bounded batches carrying stable semantic operations. For each item preserve tenant/device identity, `OperationId`, contract version, expected/base version and relevant dependency/version evidence.

## Download

Provide an authorized change feed/projection contract with a durable cursor/checkpoint. Local application should apply downloaded state and advance its cursor atomically where correctness requires it.

## Backpressure/fairness

Bound batch size, in-flight requests, reconnect concurrency and retry ownership. Reconnect storms must not amplify an outage or let one tenant/workstation monopolize the service.

`System.Threading.Channels` may signal local work but is not durable queue authority.

## Transport

HTTP remains the simple baseline unless representative measurements justify gRPC. Transport choice must not change sync semantics.

## Exit gate

Upload/download survive retry, partial batch failure and restart without losing/duplicating semantic intent; reconnect behavior is bounded and fair.