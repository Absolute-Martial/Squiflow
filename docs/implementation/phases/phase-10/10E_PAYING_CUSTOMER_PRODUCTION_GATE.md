# Phase 10E — Paying-Customer Production-Honesty Gate

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`

## Production intent

Passing Phase 10 means SquiFlow can accept the first paying customer's authoritative data for the **explicitly promised production scope and deployment profile** without knowingly depending on prototype-grade behavior, untested recovery, undocumented operator knowledge, or claims unsupported by the actual topology.

## Scope contract

Before sign-off, record the exact first-customer promise in plain language and classify every subsystem/responsibility required by that promise as:

```text
PRODUCTION_HONEST
NOT_INTRODUCED
BLOCKED
```

`BLOCKED` must be empty.

A feature not included in the first production promise does not need to be invented. Conversely, a promised feature cannot be waived as `later hardening` merely because the overall product remains incomplete.

## Production-honesty proof

Do not accept paying-customer authoritative data until the implemented and promised production scope proves, as applicable:

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

## Evidence requirement

Phase 10 evidence must be reproducible from repository/runbook state and the actual deployment profile. It includes real restore/recovery/release/capacity/security/compatibility drills where simulation cannot prove the property.

The sign-off record must state:

- what a paying customer can rely on;
- what is explicitly not part of the production promise;
- the actual supported Workstation/server/version window;
- realistic availability/maintenance/recovery claims;
- known finite resource/provider limits;
- operator owners and recovery paths;
- evidence names/locations for the promises above.

If the team would need to explain a known shortcut with “we will make this real after the first customer,” that promised responsibility is `BLOCKED` and the gate does not pass.

## Explicit non-claims

Do not claim HA, zero downtime, infinite scale, Kubernetes-grade orchestration, exactly-once delivery, universal eventual consistency or any other property not demonstrated by the current topology/tests.

## Completion meaning

Passing Phase 10 means the current **implemented product scope and deployment profile** are production-honest and qualified for the first paying customer. It does not mean SquiFlow development is finished. New capabilities/providers/topologies re-enter the same scope-and-production-honesty model as they are introduced.
