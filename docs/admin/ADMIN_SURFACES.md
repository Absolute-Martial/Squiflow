# Tenant Administration and Platform Administration

**Version:** v0.0.15

## 1. Tenant administration

Tenant Owner/delegated settings live in the ordinary authenticated Web application under privileged `Settings / Administration` routes.

For a two-person customer this should feel like ordinary Settings, not a separate enterprise console.

Includes:
- Team and invitations
- Roles/permission assignments
- Branch/program setup
- Rules/workflow/stage publication
- Custom forms/fields
- Custom domains/branding
- Feature settings
- Devices/workstations
- Tenant-level audit/configuration history

Backend contract uses explicit `/tenant-admin/...` APIs and authoritative server authorization.

### Web-only rule

Tenant role/permission assignment, workflow/rule publication and other tenant control-plane changes are initiated only from Web administration. The Desktop can consume effective configuration/permissions but cannot modify them.

## 2. Platform administration

`apps/admin-web` is the separate SquiFlow operator surface for:
- tenants/subscriptions/entitlements;
- platform feature/configuration changes;
- runtime health/incidents;
- provider configuration;
- platform security;
- support/break-glass operations;
- privileged Worker/server control-plane actions.

Backend contract uses `/platform-admin/...` policies.

## 3. Critical server tasks are Platform-Admin-Web only

Examples:
- pause/drain/resume Worker classes;
- retry/quarantine/reconcile privileged failed jobs;
- rotate platform/provider secrets;
- modify resource/deployment/runtime policies;
- database/storage maintenance or restore workflows;
- global provider/domain configuration;
- cross-tenant support actions.

These are not exposed through Desktop, `/sync/...`, ordinary `/api/...`, or the tenant Owner settings surface unless a very specific tenant-scoped operation is intentionally designed there.

## 4. Safety workflow

The browser is never the authorization authority.

High-risk operations use:

```text
Draft/Proposal
→ validate
→ exact diff
→ risk classification
→ step-up MFA / approval where required
→ execute or enqueue durable command
→ verify outcome
→ audit
→ rollback/correction where possible
```

A user must be able to distinguish a proposed change from an active one.

Do not provide generic `run SQL`, `set any config`, `force success`, or `mark payment/job complete` controls.
