# Phase 10A — Rack Capacity, SPOF, and Resource Envelope

## Inventory reality

Record actual machine count, CPU/RAM, disks/filesystem, network/uplink, power/UPS, spare/replacement path and physical/operator access.

## Measure real workloads

On the target class measure at least as applicable:

- API latency/concurrency and DB pool waits;
- PostgreSQL CPU/RAM/WAL/checkpoint/autovacuum/temp/disk behavior;
- sync reconnect/backlog drain;
- Worker backlog/oldest age;
- object/backup transfer bandwidth;
- document/image peak memory;
- Workstation + Guard idle/active/recovery resource use;
- OpenBao/identity/auth dependency resource/latency;
- observability overhead/local spool;
- disk-full/low-space behavior.

Do not manufacture workloads for components not included in the production scope merely to satisfy the checklist.

## SPOF honesty

List every current single point of failure. `Stateless process` does not mean `high availability`.

## Next-move table

For each first-order bottleneck record current envelope, warning/critical evidence, safe degraded behavior, next architectural move and revisit trigger.

## Exit gate

Production claims match measured hardware and real workload; scaling techniques are selected from proven bottlenecks rather than architecture fashion.