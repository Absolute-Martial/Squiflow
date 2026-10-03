# AdminApi slice sequence

**Status:** active sequence, 2026-10-03  
**Product version:** remains v0.1.0

## Delivery rule

AdminApi work advances one production-honest slice at a time. A slice is complete
only when its domain owner, provider boundary, PostgreSQL authority, HTTP contract,
authorization checks, idempotency/concurrency behavior, retained audit, least-
privilege deployment grants and real-boundary regression evidence agree.

Do not add placeholder endpoints, permissive fallback behavior, provider secrets
in persisted/admin responses, speculative configuration fields, or future-slice
tables and tuples merely to make the surface look complete. Shared mechanics
should be extracted only after a second real use proves the abstraction.

## Ordered slices

### 1. Account and identity onboarding — current qualified slice

Exact implemented scope:

- import an already-existing ZITADEL human identity into one stable local account;
- link another verified identity from the configured issuer to an existing active
  local account;
- preserve unique `(issuer, subject)` ownership, caller-scoped semantic
  idempotency, concurrency safety and atomic receipts;
- require the existing AdminApi OIDC, registered-device and pinned OpenFGA
  boundary;
- fail closed and return bounded errors when ZITADEL is missing, malformed,
  oversized, mismatched, slow or unavailable.

Provider-side user creation, tenant/provider organization mapping, bulk import,
drift reconciliation, memberships and roles are not part of this slice. The
authoritative contract and evidence are in
`docs/implementation/ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md`.

### 2. Membership lifecycle — qualified narrow slice

Introduce invitation/addition of an existing onboarded account, activation,
suspension and removal; first-Owner onboarding; tenant suspension/reactivation;
revision checks; caller-scoped idempotency; concurrency arbitration; retained
audit; and real AdminApi/PostgreSQL/OpenFGA evidence.

The initial Owner is an immutable bootstrap designation and remains protected
until the next role-administration slice provides an explicit handoff. Tenant
suspension blocks all tenant access by making membership-derived `TenantContext`
unavailable while preserving membership states. Exact behavior and evidence are
owned by `docs/implementation/ADMIN_API_MEMBERSHIP_LIFECYCLE.md`.

The 2026-10-03 normal repository gate qualified this declared scope with real
PostgreSQL/OpenFGA evidence. Later slices remain deferred; qualification is not
authorization to begin another responsibility.

### 3. Baseline permission administration — deferred

Introduce the accepted Owner/Staff meanings, assignment/revocation, delegation
ceilings, authorization revision, OpenFGA tuple reconciliation and replay/recovery
semantics. Membership state and permission state remain separate authorities.

### 4. Custom roles and grants

Introduce tenant-owned roles, bounded permission selection, grant/revoke,
revision/conflict behavior, OpenFGA reconciliation, and safe role change/deletion.
Custom roles cannot exceed the assigning actor's accepted delegation ceiling.

### 5. Platform operator lifecycle

Introduce add/revoke and active/unavailable platform operators with explicit,
separate platform permissions. Never restore first-login SuperAdmin behavior.

### 6. Admin-device lifecycle

Introduce additional-device registration, revocation, certificate
rotation/reprovisioning, replacement/recovery semantics, principal/device binding
and authoritative audit. Private keys remain device-held.

### 7. Tenant capability and configuration controls

Introduce only controls whose authoritative capability owners already exist.
Publication, activation and rollback are separate revision-checked commands.

### 8. Platform configuration controls

Introduce provider/runtime configuration that genuinely belongs to AdminApi.
Secrets remain outside ordinary persistence and responses; readiness and provider
failure behavior must be explicit.

### 9. Operational control-plane operations

Introduce incident controls only for real durable workloads. Worker pause, drain,
retry and quarantine wait until Worker owns durable work with defined recovery.

### 10. Privileged support access

Introduce scoped cross-tenant/JIT access with reason or ticket, expiry,
step-up where required, optional four-eyes approval, constrained permissions and
authoritative audit.

### 11. Security and key lifecycle operations

Introduce OpenBao-backed rotation, retirement, recovery requests, status,
idempotent replay and provider-failure semantics only after OpenBao is the
qualified key authority.

### 12. Recovery and orchestration operations

Introduce device-key recovery and backup/restore administration only after the
underlying recovery mechanisms are production-qualified and independently
testable.

## Gate between slices

Before starting the next slice:

1. update the current implementation truth and explicit non-claims;
2. run locked restore, format verification, Release build and the complete test
   suite against required real dependencies;
3. retain a focused implementation owner document naming evidence and
   requalification triggers;
4. verify runtime database grants and provider credentials remain least
   privilege;
5. leave no introduced responsibility classified as `BLOCKED`.
