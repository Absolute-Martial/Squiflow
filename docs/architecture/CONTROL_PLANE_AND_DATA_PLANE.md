# Control Plane and Business Data Plane

**Version:** v0.0.15

## Why this distinction matters

SquiFlow has two very different kinds of commands:

1. normal business operations performed by tenant users;
2. privileged control-plane operations that change how SquiFlow itself behaves or operates.

Mixing them would make authorization difficult and could accidentally expose server-critical operations to the Workstation.

## Business data plane

Normal tenant work:

```text
Web / Workstation
→ /api or /sync
→ authentication + tenant scope
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

The Workstation can perform specifically approved operations locally/offline and later sync them.

## Tenant control plane

Tenant configuration changes are Web-only:

```text
apps/web / Settings
→ /tenant-admin/...
→ Owner/delegated permission
→ validation/version check
→ durable config/rule/workflow/role change
→ audit
```

Examples:
- invite/suspend Staff;
- assign role/permission IDs;
- create custom role;
- publish tenant rule/workflow/stage;
- custom domain/branding;
- device/workstation policy.

Desktop may consume the result, but it cannot originate these control-plane changes.

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

## No direct infrastructure bypass

Neither tenant Web, Platform Admin Web nor Desktop talks directly to PostgreSQL/selected central DB, object-storage admin APIs, containers or SSH as part of normal application control.

Infrastructure/root operations remain a separate infrastructure plane when they truly require OS/container/database administrator access.

## Safety rule

If a command can alter multiple tenants, security policy, infrastructure behavior, secret material or operational truth, ask why it is not a platform-control-plane command.

If a command is ordinary business work, do not force users through the Platform Admin surface merely because a server eventually processes it.
