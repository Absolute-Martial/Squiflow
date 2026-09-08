# Control Plane and Business Data Plane

**Version:** v0.0.15

## Why this distinction matters

SquiFlow has three materially different classes of command:

1. normal tenant business operations;
2. tenant configuration/administration;
3. SquiFlow platform/server control-plane operations.

Mixing them would make authorization difficult and could accidentally expose privileged operations through the Workstation or ordinary business API.

## Business data plane

Normal tenant work:

```text
Web / Workstation
→ Core API /api or /sync
→ authentication
→ authoritative tenant scope
→ endpoint/function policy
→ resource/action authorization where required
→ application/domain validation
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

The Workstation can perform specifically approved operations locally/offline and later sync them, but server authority rechecks shared permissions/invariants during synchronization.

## Tenant control plane

Tenant configuration changes are Web-only and remain part of the tenant/business backend because they are tenant-scoped application behavior:

```text
apps/web / Settings
→ Core API /tenant-admin/...
→ authentication + tenant scope
→ endpoint policy
→ resource/delegation authorization
→ validation/version check
→ durable config/rule/workflow/role change
→ authorization/config revision where applicable
→ audit + outbox/invalidation
```

Examples:
- invite/suspend Staff;
- assign role/permission IDs;
- create/edit custom role;
- publish tenant rule/workflow/stage;
- custom domain/branding;
- device/workstation policy.

Desktop may consume the resulting snapshots/configuration, but it cannot originate these control-plane changes.

## Platform control plane is a separate backend

Platform/server-critical operations use a separate application/control-plane stack:

```text
apps/admin-web
→ services/admin-api
→ platform-owned application/data/provider/control-plane dependencies
```

`services/admin-api` is a separate ASP.NET Core executable and deployment boundary from `services/core-api`.

The platform/super-admin plane must **not** depend on Core API being available for normal platform administration. Admin API does not call Core API as the ordinary execution path for platform commands.

Platform Admin and Core API may share reviewed libraries/modules/contracts, but ordinary runtime topology must not be:

```text
Admin Web → Admin API → Core API
```

This protects availability and limits attack surface: a tenant business API failure/restart/overload should not automatically remove the operator control surface, and Admin API deployment should not require a Core API restart.

Examples of Admin API responsibilities:
- platform tenant/entitlement changes;
- Worker pause/drain/retry/quarantine/reconciliation controls;
- provider/global configuration;
- secret rotation;
- deployment/resource policy represented at application level;
- database/storage maintenance or restore orchestration where appropriate;
- cross-tenant support/break-glass application actions;
- platform authorization/operator configuration.

These commands use stronger audit, step-up authentication and review/approval based on risk.

### Authentication versus authorization

ZITADEL can prove that the administrator authenticated recently/with an appropriate authentication context. It does **not** grant platform authority.

A high-risk flow is:

```text
Platform Admin Web
→ ZITADEL step-up if required
→ Admin API
→ platform OpenFGA/resource/action authorization
→ exact diff + current-version check
→ approval/cooldown where required
→ durable command/proposal
→ Worker/system execution if asynchronous
→ verification
→ audit
```

Tenant OpenFGA roles/permissions cannot imply platform authority.

## Admin API independence boundaries

Admin API owns its own:
- request pipeline;
- platform authentication/session validation;
- platform authorization policies;
- rate/admission limits;
- audit/correlation;
- health/readiness;
- endpoint inventory;
- service credentials/scopes;
- deployment and restart lifecycle.

It may share underlying infrastructure such as the central database, OpenFGA, ZITADEL, Worker, object storage, or observability where that is intentional. Sharing a dependency does not make Core API the control-plane gateway.

If a shared database/provider itself is unavailable, Admin API may also be degraded. The requirement is **process/API independence from Core API**, not impossible independence from all shared infrastructure.

## Edge gateway/reverse-proxy boundary

An edge reverse proxy or API-gateway capability may route north-south traffic to the appropriate backend and may own generic edge concerns such as:
- TLS termination;
- hostname/custom-domain routing;
- public/private exposure policy;
- request-size limits;
- WAF/DDoS controls where provided;
- coarse rate limiting.

A shared edge does **not** collapse the application planes.

Valid topology:

```text
edge
├── tenant/business routes → Core API
└── private/platform routes → Admin API
```

Invalid normal topology:

```text
edge
→ Core API
→ Admin API
```

or:

```text
edge authorization
→ therefore backend skips authorization
```

Core API/Admin API still independently authenticate/authorize, validate resource scope/state, enforce operation-specific admission, and emit their own audit/health evidence.

A service mesh is not baseline. SquiFlow does not currently have enough independently deployed east-west services to justify the memory/network/failure/operations cost. Revisit only when real service-to-service topology makes mTLS, discovery, traffic policy, and distributed observability materially difficult without one.

## Resource-based authorization

The API path is not the authorization boundary.

For tenant operations, Core API uses ASP.NET Core endpoint policy plus resource authorization after tenant-scoped resource resolution.

For platform operations, Admin API applies the same principle using platform-scoped resources and platform authorization. It must not reuse tenant request context as super-admin authority.

## No direct infrastructure bypass from UI

Tenant Web, Platform Admin Web and Desktop do not talk directly to PostgreSQL/selected central DB, object-storage admin APIs, containers or SSH.

Platform Admin Web talks to Admin API. Admin API may invoke narrowly authorized provider/control-plane integrations as part of an audited application operation.

Infrastructure/root operations remain a separate private infrastructure plane when they truly require OS/container/database administrator access or when Admin API itself is unavailable.

## API inventory and retirement

Because privileged endpoints are security-sensitive, production surfaces are generated/inventoried from executable endpoint metadata/OpenAPI during CI/release.

Every tenant-admin Core API endpoint and every Admin API endpoint declares its policy family and owner. Deprecated privileged versions have an explicit retirement plan rather than remaining available indefinitely.

Do not keep `/platform-admin/...` routes on Core API as a hidden compatibility surface after Admin API is introduced.

## Safety rule

If a command can alter multiple tenants, platform security policy, provider/global configuration, Worker/server control behavior, secret material, or operational truth, ask why it is not an **Admin API** command.

If a command is ordinary tenant business work, do not force users through the Platform Admin stack merely because a server eventually processes it.
