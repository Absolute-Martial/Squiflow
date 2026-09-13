# Control Plane and Business Data Plane

**Version:** v0.0.19

## Why this distinction matters

SquiFlow has materially different classes of command:

1. normal tenant business operations;
2. tenant configuration/administration;
3. SquiFlow platform/server control-plane operations;
4. private infrastructure/bootstrap/recovery operations.

Mixing them would make authorization difficult and could accidentally expose privileged operations through the Workstation, ordinary business API, or public edge.

## 1. Business data plane

Normal tenant work:

```text
Web / Workstation
→ WebApi / SyncApi / current compact CoreApi host
→ transport/session/device admission
→ authoritative tenant scope
→ owning capability application/use case
→ resource/action authorization where required
→ current facts/rules/invariants
→ transaction
→ outbox/worker if needed
→ result
```

Examples:

- create/edit customer;
- create order;
- quotation/tender work;
- permitted inventory operation;
- payment/credit operation according to authority policy;
- purchasing/supplier transaction;
- document/print request.

The Workstation can perform specifically approved operations locally/offline and later sync them, but server authority rechecks current permissions/invariants during synchronization. Local execution is provisional where server authority matters.

WebApi and SyncApi are workload-specific hosts into the same authoritative capability modules, not separate business implementations.

## 2. Tenant control plane

Tenant administration remains server-authoritative tenant-scoped application behavior.

The **Web is the primary/broader tenant administration surface**, but selected tenant-scoped operations may also be surfaced on an **online authorized Workstation** when the owning capability explicitly supports that host.

Examples that may be exposed on Web and an authorized online Workstation include:

- invite/create/suspend Staff;
- assign role/permission IDs within delegation ceilings;
- device/workstation visibility and revocation;
- branch/program setup where approved.

Broader configuration/rule/workflow/form authoring can remain Web-only when no Workstation product requirement exists.

Accepted path:

```text
Tenant Web OR approved online Workstation surface
→ authoritative tenant-admin endpoint/use case
→ ZITADEL-authenticated user/device context
→ tenant scope + current resource/delegation authorization
→ validate current version/permission ceiling
→ durable authoritative config/role/device change
→ OpenFGA/config revision where applicable
→ audit + outbox/invalidation
→ authoritative outcome
```

An online Workstation surface is only a host adapter. It does not grant/revoke authority locally, does not make tenant-control changes offline-authoritative, and never receives OpenFGA administrative credentials.

## 3. Platform control plane is a separate backend

Platform/server-critical operations use a separate application/control-plane stack:

```text
apps/admin-web
→ services/admin-api
→ platform-owned application/data/provider/control-plane dependencies
```

`services/admin-api` is a separate ASP.NET Core executable/deployment/security boundary from ordinary tenant Web/Sync hosts.

The platform-admin plane must **not** depend on an ordinary tenant API process being available for normal platform administration. Admin API does not call tenant WebApi/SyncApi/CoreApi as the ordinary execution path for platform commands.

Platform Admin and tenant hosts may share reviewed modules/contracts/infrastructure where appropriate, but ordinary runtime topology must not be:

```text
Admin Web → Admin API → tenant business API
```

This protects availability and limits attack surface.

Examples of Admin API responsibilities:

- platform tenant/entitlement/capability ceilings;
- Worker pause/drain/retry/quarantine/reconciliation controls;
- provider/global configuration;
- encryption policy and key-lifecycle requests;
- device-key recovery workflows;
- deployment/resource policy represented at application level;
- database/storage maintenance or restore orchestration where appropriate;
- controlled cross-tenant support/break-glass application actions;
- platform authorization/operator configuration.

## 4. Platform Admin is a private zero-trust plane

Network reachability is a prerequisite, not authority.

Current deployment direction:

```text
approved Admin device
      │
      │ Tailscale / approved private network
      ▼
deny-by-default private ingress
      │
      ▼
Platform Admin Web/API
      │
      ▼
independent application authentication + authorization
```

A protected Platform Admin operation evaluates as applicable:

```text
private/trusted ingress evidence
+ ZITADEL authenticated administrator
+ registered non-revoked Admin device
+ PlatformAdmin.Access
+ operation-specific platform permission
+ current target/resource state
+ step-up/recent authentication
+ physical security/recovery factor for protected cryptographic actions
+ JIT/time-bounded elevation where appropriate
+ independent approval where required
+ authoritative audit
```

Do not authorize Platform Admin because `RemoteIpAddress` is private, the source appears to be a Tailscale address, or the user merely belongs to the tailnet.

Detailed owners:

- `docs/admin/ADMIN_SURFACES.md`;
- `docs/operations/PRIVATE_ADMIN_NETWORK_AND_PODMAN.md`;
- `docs/security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md`.

## 5. Authentication versus authorization

ZITADEL can prove that the administrator authenticated and can provide recent/step-up authentication context. It does **not** itself grant SquiFlow platform authority.

A high-risk flow is:

```text
Platform Admin Web
→ approved private ingress
→ ZITADEL step-up if required
→ Admin API
→ registered Admin-device verification
→ platform OpenFGA/resource/action authorization
→ exact diff + current-version check
→ JIT/time-bounded elevation if required
→ physical factor/recovery proof for protected crypto operation
→ independent approval/cooldown where required
→ durable command/proposal
→ Worker/provider/system execution if asynchronous
→ verification
→ authoritative audit
```

Tenant OpenFGA roles/permissions cannot imply platform authority.

## 6. Encryption/key-management control plane

SquiFlow does not build a custom cryptographic vault.

Normal administration path:

```text
Platform Admin Web/API
        │
        │ policy/lifecycle request
        ▼
SquiFlow key-management adapter
        │
        ▼
OpenBao / future compatible provider
```

OpenBao/Vault owns cryptographic root/key storage and cryptographic operations. SquiFlow owns encryption policy, authorization, resource/key mapping, recovery workflow and authoritative audit semantics.

Raw KEKs/DEKs/root/seal material/recovery shares are not ordinary Admin UI/API data.

Encryption administration does not imply tenant customer-data browsing. Support/data access requires its own explicit authorization/workflow.

## 7. Admin API independence boundaries

Admin API owns its own:

- request pipeline;
- platform authentication/session validation;
- registered Admin-device validation;
- platform authorization policies;
- private-ingress/trusted-proxy handling;
- rate/admission limits;
- audit/correlation;
- health/readiness;
- endpoint inventory;
- service credentials/scopes;
- deployment and restart lifecycle.

It may share underlying infrastructure such as PostgreSQL, OpenFGA, ZITADEL, Worker, object storage, OpenBao or observability where intentional. Sharing a dependency does not make the ordinary tenant API the control-plane gateway.

If a shared database/provider/key service itself is unavailable, the operations that genuinely depend on it may also be degraded. The requirement is **process/API independence from tenant ingress**, not impossible independence from all shared infrastructure.

Admin API outage alone must not stop ordinary already-provisioned tenant business operation.

## 8. Private infrastructure/bootstrap plane

If Platform Admin or OpenBao itself is unavailable, recovery cannot depend solely on Platform Admin.

A separate private infrastructure runbook/ceremony owns operations such as:

- OpenBao initialization/unseal/recovery;
- service/container restart/redeploy;
- node replacement;
- DB recovery needed for application startup;
- private-network/edge repair;
- ZITADEL/OpenFGA/storage connectivity/config recovery needed to restore service.

Conceptual OpenBao bootstrap:

```text
approved operator/recovery machine
+ private infrastructure access
+ required physical factor or recovery quorum
        │
        ▼
OpenBao bootstrap/unseal procedure
        │
        ▼
OpenBao usable
        │
        ▼
normal Admin API key-management identity becomes usable
```

This avoids the circular dependency `Admin API needs OpenBao` and `OpenBao needs Admin API to unseal`.

The private infrastructure plane is not a hidden tenant/business API and is never exposed as an ordinary public route.

## 9. Edge gateway/reverse-proxy boundary

An edge reverse proxy/API-gateway capability may route north-south traffic and may own generic concerns such as:

- TLS termination;
- hostname/custom-domain routing;
- public/private exposure policy;
- request-size limits;
- WAF/DDoS controls where provided;
- coarse rate limiting;
- transport/protocol negotiation and edge observability where supported.

A shared edge does **not** collapse application planes.

Ordinary/public topology may be:

```text
public edge
├── tenant Web/WebApi
└── approved Sync endpoints
```

Platform Admin uses a private ingress path rather than a hidden public `/platform-admin` convenience route.

Invalid assumptions include:

```text
edge authorization
→ therefore backend skips authorization
```

or:

```text
private IP / Tailscale membership
→ therefore Platform Admin allowed
```

Each backend still independently authenticates/authorizes, validates resource scope/state, enforces operation-specific admission, and emits its own audit/health evidence.

A service mesh remains non-baseline until real east-west service topology earns it.

## 10. Service/data-sharing implication

WebApi, SyncApi, Admin API and Worker are runtime hosts around the same modular-monolith capability architecture. Separate processes do not automatically make them independent microservices with private databases or separate business meanings.

They may intentionally share the authoritative central DB, but they must preserve explicit module/data ownership and the same domain/transaction invariants. A host must not bypass a module's rules through ad-hoc direct SQL merely because a table is physically reachable.

If a capability is later extracted into a truly independent service, its data ownership and communication contract must be designed explicitly then.

## 11. Resource-based authorization

The API path is not the authorization boundary.

Tenant operations use endpoint policy plus resource authorization after tenant-scoped resolution and owning-capability/domain checks.

Platform operations apply the same principle using platform-scoped resources and platform authorization. They must not reuse tenant request context as platform-admin authority.

Platform Admin entry permission and individual operation permission are separate checks.

## 12. No direct infrastructure bypass from UI

Tenant Web, Platform Admin Web and Workstation do not talk directly to PostgreSQL, object-storage admin APIs, Podman/container control, SSH, or OpenBao as normal UI behavior.

Platform Admin Web talks to Admin API. Admin API may invoke narrowly authorized provider/control-plane integrations as part of an audited application operation.

OpenBao is normally reached through the narrow SquiFlow key-management adapter. Direct operator OpenBao access exists only for documented private bootstrap/unseal/recovery.

Infrastructure/root operations remain a separate private plane when they truly require OS/container/database/key-service administrator access or when Admin API itself is unavailable.

## 13. Break-glass and support

Break-glass is a narrow emergency workflow, strongly verified and high-severity audited/evidenced, followed by post-use credential/factor review/rotation. It is not a standing universal administrator account or public endpoint.

Cross-tenant support access is separately authorized, tenant/resource scoped, reason/ticket based, time-bounded/JIT where appropriate, optionally approval/tenant-acknowledgement gated, automatically expired/revoked, and audited. Encryption/key administration alone never grants support-data access.

## 14. API inventory and retirement

Because privileged endpoints are security-sensitive, production surfaces are generated/inventoried from executable endpoint metadata/OpenAPI during CI/release.

Every tenant-admin and Platform Admin endpoint declares its policy family and owner. Deprecated privileged versions have an explicit retirement plan rather than remaining available indefinitely.

Do not keep `/platform-admin/...` routes on the ordinary public tenant API as a hidden compatibility surface after Admin API exists.

## 15. Safety rules

If a command can alter multiple tenants, platform security/encryption policy, key lifecycle, provider/global configuration, Worker/server control behavior, secret material, or operational truth, ask why it is not an **Admin API** command.

If a command is ordinary tenant business work, do not force users through Platform Admin merely because a server eventually processes it.

If an operation is tenant administration, keep it server-authoritative; Web remains primary, while an online Workstation may expose only explicitly approved tenant-scoped actions.

If an operation is infrastructure bootstrap/recovery required to restore Admin/OpenBao itself, keep it in the private infrastructure runbook rather than inventing a hidden business endpoint.
