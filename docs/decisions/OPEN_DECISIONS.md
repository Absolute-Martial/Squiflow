# Open Decisions — v0.0.15

These are decisions that can materially affect the current implementation baseline. Deferred ideas are not kept here merely because they may be useful someday.

- Final central transactional database after workload/benchmark proof.
- Final Workstation embedded database after SQLite versus libSQL proof.
- Exact identity provider/implementation and federation strategy.
- Exact native Workstation callback mechanism after Windows packaging/security POC.
- Final browser cookie/BFF/session implementation per Web host.
- Final set of built-in Staff permission defaults and role-template defaults.
- Whether explicit `Deny` permission semantics are ever necessary; baseline starts allow-oriented.
- Final Web/UI framework commitments where implementation evidence is still required.
- Exact messaging/scheduling libraries after POC.
- Central-store HA topology and RPO/RTO.
- Final object-storage provider/tiering after current pricing/region/compliance verification.
- Tailscale vs Twingate/private-access split and Flux deployment/compliance suitability.
- Jurisdiction-specific money/tax/invoice numbering/privacy requirements.

## Deferred, not an active v0.0.15 decision

Browser partial-offline/offline business execution is deliberately deferred. Do not spend implementation time selecting IndexedDB synchronization, service-worker business queues, browser conflict resolution or PWA offline mutation architecture during the current baseline.
