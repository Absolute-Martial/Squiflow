# Phase 10C — Release, Migration, Rollback, and Roll-Forward Drill

## Immutable release flow

```text
CI artifact/image
→ checksum/provenance
→ environment/config/dependency/capacity preflight
→ compatibility/migration preflight
→ controlled drain/maintenance where needed
→ deploy same verified bytes
→ health/readiness
→ authorized functional smoke journey
```

## Version overlap

Exercise supported old/new backend and Workstation combinations, pending pre-upgrade sync, durable jobs/messages and historical snapshots before destructive contraction.

Only combinations inside the documented support window must be supported; unsupported versions must fail explicitly rather than be silently interpreted.

## Failure drill

Actually fail a release/migration and execute the documented recovery path: binary rollback where safe, DB/app roll-forward, checkpoint restore, maintenance recovery, or another accepted mechanism.

Do not assume DB rollback is always possible after data transformation.

## Honest availability

On a single active rack node, planned maintenance downtime may be safer and more honest than fake zero-downtime claims. Canary/blue-green/rolling need spare capacity, compatible data, routing and observability before they are real.

## Exit gate

A failed deployment inside the supported production scope has been recovered without corrupting authoritative or pending local work.