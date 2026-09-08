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
→ /api or /sync
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

Tenant configuration changes are Web-only:

```text
apps/web / Settings
→ /tenant-admin/...
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

## Platform control plane

Platform/server-critical operations are only exposed from `apps/admin-web` through `/platform-admin/...`.

Examples:
- platform tenant/entitlement changes;
- Worker pause/drain/retry/quarantine/reconciliation controls;
- provider/global configuration;
- secret rotation;
- deployment/resource policy;
- database/storage maintenance/restore workflows;
- cross-tenant support/break-glass actions.

These commands use stronger audit, step-up authentication and review/approval based on risk.

### Authentication versus authorization

OpenID Connect can prove that the administrator authenticated recently/with an appropriate authentication context. It does **not** grant platform authority.

A high-risk flow is:

```text
Platform Admin Web
→ OIDC step-up if required (`max_age`/`auth_time`/supported `acr`)
→ /platform-admin/... endpoint policy
→ platform resource/action authorization
→ exact diff + current-version check
→ approval/cooldown where required
→ durable command/proposal
→ Worker/system execution if asynchronous
→ verification
→ audit
```

## Resource-based authorization

The API path is not the authorization boundary.

For an operation targeting a specific tenant/resource, use ASP.NET Core endpoint policy for coarse function access and `IAuthorizationService` resource/action authorization after the relevant resource/context is available.

Tenant-scoped reads should be constrained by authoritative tenant context before finer authorization where practical, reducing cross-tenant existence leakage.

## No direct infrastructure bypass

Neither tenant Web, Platform Admin Web nor Desktop talks directly to PostgreSQL/selected central DB, object-storage admin APIs, containers or SSH as part of normal application control.

Infrastructure/root operations remain a separate infrastructure plane when they truly require OS/container/database administrator access.

## API inventory and retirement

Because privileged endpoints are security-sensitive, the production API surface must be generated/inventoried from executable endpoint metadata/OpenAPI during CI/release.

Every `/tenant-admin` and `/platform-admin` endpoint declares its policy family and owner. Deprecated privileged versions have an explicit retirement plan rather than remaining available indefinitely.

This is a generated release/security verification artifact, not a manual CSV design source.

## Safety rule

If a command can alter multiple tenants, security policy, infrastructure behavior, secret material or operational truth, ask why it is not a platform-control-plane command.

If a command is ordinary business work, do not force users through the Platform Admin surface merely because a server eventually processes it.
