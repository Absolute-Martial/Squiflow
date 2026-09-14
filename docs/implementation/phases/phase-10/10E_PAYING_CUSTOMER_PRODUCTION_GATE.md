# Phase 10E — Paying-Customer Production Gate

Do not accept paying-customer authoritative data until the **implemented and promised production scope** proves, as applicable:

- business/domain correctness and historical truth;
- tenant isolation and current authorization;
- secure identity/device/admin boundaries;
- Workstation offline/local durability and encrypted local recovery;
- sync/idempotency/conflict/long-offline recovery;
- schema/contract compatibility across the supported window;
- Worker/scheduler/provider consequence recovery;
- object/file/document/printing failure isolation;
- payment/stock/credit protected authority where implemented;
- encrypted backup restore to replacement environment;
- OpenBao/key recovery and Admin/private recovery procedure;
- actual hardware capacity/resource envelope;
- immutable release + failed-release recovery drill;
- edge/DNS/TLS/time recovery;
- provider migration readiness/qualification;
- operator ownership and realistic RPO/RTO/support promise;
- observability/audit evidence without secrets/PII leakage.

A feature or subsystem that is not part of the first production promise does not need to be invented simply to make the gate longer. Conversely, any subsystem that *is* part of the promised scope must satisfy its owning gate rather than being waived as `later hardening`.

## Explicit non-claims

Do not claim HA, zero downtime, infinite scale, Kubernetes-grade orchestration, exactly-once delivery, universal eventual consistency or any other property not demonstrated by the current topology/tests.

## Completion meaning

Passing Phase 10 means the current **implemented product scope and deployment profile** are qualified for the first paying customer. It does not mean SquiFlow development is finished. New capabilities/providers/topologies re-enter the same maturity/gate model as they are introduced.