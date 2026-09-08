# Open Decisions — v0.0.15

These are decisions that can materially affect the current implementation baseline. Deferred ideas are not kept here merely because they may be useful someday.

- Final central transactional database after workload/benchmark proof.
- Final Workstation embedded database after SQLite versus libSQL proof.
- Exact OpenID Connect provider/implementation and any future federation strategy.
- Exact native Workstation callback mechanism after Windows packaging/security POC: app-claimed HTTPS if reliable, otherwise standards-compliant loopback IP callback.
- Final browser cookie/BFF/server-session implementation per Web host.
- Exact session-revocation persistence/rotation strategy after identity-provider and multi-node session design is selected.
- Whether the chosen identity provider supports/should enable Back-Channel Logout for SquiFlow Web sessions.
- Exact authentication-context (`acr`) vocabulary/provider mapping for high-risk step-up operations.
- Final set of built-in Staff permission defaults and role-template defaults.
- Whether explicit `Deny` permission semantics are ever necessary; baseline starts allow-oriented.
- Exact persistence representation/type of `TenantAuthorizationRevision`; semantics are accepted, storage detail remains implementation-level.
- Whether any future business requirement actually justifies explicit per-resource relationship sharing beyond tenant/role/scope authorization. No Zanzibar-style relationship service is planned without such evidence.
- Final Web/UI framework commitments where implementation evidence is still required.
- Exact messaging/scheduling libraries after POC.
- Central-store HA topology and RPO/RTO.
- Final object-storage provider/tiering after current pricing/region/compliance verification.
- Tailscale vs Twingate/private-access split and Flux deployment/compliance suitability.
- Jurisdiction-specific money/tax/invoice numbering/privacy requirements.

## Deferred, not an active v0.0.15 decision

- Browser partial-offline/offline business execution is deliberately deferred. Do not spend implementation time selecting IndexedDB synchronization, service-worker business queues, browser conflict resolution or PWA offline mutation architecture during the current baseline.
- Dynamic OpenID Connect Client Registration is not needed for the controlled SquiFlow client set now.
- OpenID Connect Native SSO for Mobile Apps is not the Windows Workstation login architecture.
- A dedicated Zanzibar-style authorization microservice/tuple store/specialized set index is not justified by the current Owner/Staff + scoped-role product model.
